using System.IO.Compression;
using System.Text.Json;
using DesktopBubbles.Core;

internal static class ManagedShortcutTests
{
    public static void Run(string root, Action<string, Action> test, Action<bool> assert, Action<Action> reject)
    {
        Fixture New() => new(Path.Combine(root, Guid.NewGuid().ToString("N")));
        void Fail(Action action) { try { action(); } catch (IOException) { return; } throw new Exception("Expected I/O failure"); }
        test("v1 migriert ohne Übernahme bestehender Dateien", () =>
        {
            var f = New(); var source = f.Link(); var state = new BubbleState { Version = 1, Groups = [new BubbleGroup { Items = [Targets.Create(source)] }] };
            f.Store.Save(state); var loaded = f.Store.Load(); assert(loaded.Version == 6 && loaded.Groups[0].ShowName && loaded.Groups[0].Items[0].Target == source && File.Exists(source));
        });
        test("Desktop-Übernahme: erst Daten speichern, dann Original entfernen", () =>
        {
            var f = New(); var source = f.Link(); bool committed = false;
            var manager = new ShortcutManager(f.Vault, state => { assert(File.Exists(source)); f.Store.Save(state); committed = true; });
            manager.Add(f.State, f.Group, source); var target = f.Group.Items.Single().Target;
            assert(committed && !File.Exists(source) && ManagedTargets.IsManaged(target) && File.ReadAllText(manager.Resolve(target)).Contains("https://example.com"));
            assert(f.Store.Load().Groups[0].Items[0].Target == target);
        });
        test("Fehler beim Speichern lässt Desktop und Gruppen unverändert", () =>
        {
            var f = New(); var source = f.Link(); var manager = new ShortcutManager(f.Vault, _ => throw new IOException("disk full"));
            Fail(() => manager.Add(f.State, f.Group, source)); assert(File.Exists(source) && f.Group.Items.Count == 0);
        });
        test("Gesperrtes Desktop-Original bleibt neben nutzbarer Kopie erhalten", () =>
        {
            var f = New(); f.Platform.DenyDelete = true; var source = f.Link();
            var warning = f.Manager.Add(f.State, f.Group, source); assert(warning is not null && File.Exists(source) && File.Exists(f.Manager.Resolve(f.Group.Items[0].Target)));
        });
        test("Verändertes Original wird zwischen Kopieren und Commit nicht gelöscht", () =>
        {
            var f = New(); var source = f.Link(); var manager = new ShortcutManager(f.Vault, state => { f.Store.Save(state); File.WriteAllText(source, "changed by user"); });
            assert(manager.Add(f.State, f.Group, source) is not null && File.ReadAllText(source) == "changed by user");
        });
        test("Kopiermodus behält Desktop und startet aus eigenem Speicher", () =>
        {
            var f = New(); f.State.Settings.MoveDesktopShortcuts = false; var source = f.Link(); f.Manager.Add(f.State, f.Group, source);
            assert(File.Exists(source) && f.Manager.Resolve(f.Group.Items[0].Target) != source);
        });
        test("Programme bleiben Referenzen; fremde Links erhalten eine eigene Kopie", () =>
        {
            var f = New(); var shared = Path.Combine(f.Root, "Shared.url"); File.WriteAllText(shared, "shared");
            f.Manager.Add(f.State, f.Group, shared); f.Manager.Add(f.State, f.Group, Path.Combine(f.Platform.PersonalDesktop, "app.exe"));
            assert(ManagedTargets.IsManaged(f.Group.Items[0].Target) && !ManagedTargets.IsManaged(f.Group.Items[1].Target) && File.Exists(shared));
            assert(!f.Manager.CanRestore(f.Group.Items[0].Target));
        });
        test("Nicht sicher verlagerbare Links bleiben am Ursprung", () =>
        {
            var f = New(); f.Platform.Valid = false; var source = f.Link(); Fail(() => f.Manager.Add(f.State, f.Group, source)); assert(File.Exists(source) && f.Group.Items.Count == 0);
        });
        test("Kopierter Link bleibt nach manuellem Entfernen einer fremden Quelle nutzbar", () =>
        {
            var f = New(); var source = Path.Combine(f.Root, "Extern.url"); File.WriteAllText(source, "[InternetShortcut]\nURL=https://example.com");
            f.Manager.Add(f.State, f.Group, source); File.Delete(source);
            assert(File.ReadAllText(f.Manager.Resolve(f.Group.Items[0].Target)).Contains("https://example.com"));
        });
        test("Fehlende Kopie blockiert nicht die gesamte Recovery", () =>
        {
            var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); File.Delete(f.Manager.Resolve(f.Group.Items[0].Target));
            assert(f.Manager.Recover(f.State).Count == 1 && f.State.Groups.Count == 1);
        });
        test("Beschädigtes Journal mit Nullfeldern wird gemeldet statt abzustürzen", () =>
        {
            var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); var target = f.Group.Items[0].Target;
            File.WriteAllText(f.Manager.Resolve(target) + ".json", "{\"FileName\":\"" + Path.GetFileName(f.Manager.Resolve(target)) + "\",\"Sha256\":null,\"Source\":null}");
            assert(f.Manager.Recover(f.State).Count == 1 && !f.Manager.CanRestore(target) && f.Vault.OriginalSource(target) is null);
        });
        test("Übernahme aktualisiert Referenzen in anderen Gruppen", () =>
        {
            var f = New(); var source = f.Link(); var second = new BubbleGroup { Items = [Targets.Create(source)] }; f.State.Groups.Add(second);
            f.Manager.Add(f.State, f.Group, source); assert(!File.Exists(source) && second.Items[0].Target == f.Group.Items[0].Target);
        });
        test("Alte Einträge werden erst durch ausdrückliche Übernahme verschoben", () =>
        {
            var f = New(); var source = f.Link(); f.Group.Items.Add(Targets.Create(source));
            f.Manager.Add(f.State, f.Group, source); assert(File.Exists(source));
            f.Manager.Add(f.State, f.Group, source, true); assert(!File.Exists(source) && f.Group.Items.Count == 1 && ManagedTargets.IsManaged(f.Group.Items[0].Target));
        });
        test("Doppelter Drop erzeugt keinen zweiten Eintrag", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); f.Manager.Add(f.State, f.Group, source); assert(f.Group.Items.Count == 1);
        });
        test("Aus Gruppe löschen entfernt fehlende externe Ziele dauerhaft ohne Desktop-Kopie", () =>
        {
            var f = New(); var entry = Targets.Create(Path.Combine(f.Root, "missing.lnk")); f.Group.Items.Add(entry); f.Store.Save(f.State);
            f.Manager.RemoveFromGroup(f.State, f.Group, entry);
            assert(f.Group.Items.Count == 0 && f.Store.Load().Groups[0].Items.Count == 0 && !Directory.EnumerateFiles(f.Platform.PersonalDesktop).Any());
        });
        test("Aus Gruppe löschen funktioniert auch bei fehlender Kopie oder kaputtem Journal", () =>
        {
            foreach (bool missingCopy in new[] { true, false })
            {
                var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); var entry = f.Group.Items.Single(); string copy = f.Manager.Resolve(entry.Target);
                if (missingCopy) File.Delete(copy); else File.WriteAllText(copy + ".json", "broken");
                f.Manager.RemoveFromGroup(f.State, f.Group, entry);
                assert(f.Store.Load().Groups[0].Items.Count == 0 && File.Exists(copy) == !missingCopy && !Directory.EnumerateFiles(f.Platform.PersonalDesktop).Any());
            }
        });
        test("Nur Gruppenreferenz löschen erhält Sicherung und andere Gruppen", () =>
        {
            var f = New(); string source = f.Link(); f.Manager.Add(f.State, f.Group, source); var entry = f.Group.Items.Single(); var second = new BubbleGroup { Items = [Targets.Create(entry.Target)] }; f.State.Groups.Add(second);
            string copy = f.Manager.Resolve(entry.Target); var bytes = File.ReadAllBytes(copy); var journal = File.ReadAllBytes(copy + ".json");
            f.Manager.RemoveFromGroup(f.State, f.Group, entry);
            assert(f.Group.Items.Count == 0 && second.Items.Count == 1 && !File.Exists(source) && bytes.SequenceEqual(File.ReadAllBytes(copy)) && journal.SequenceEqual(File.ReadAllBytes(copy + ".json")) && f.Manager.CanRestore(entry.Target));
            f.Manager.Recover(f.Store.Load()); assert(!File.Exists(source));
        });
        test("Speicherfehler beim Löschen stellt Eintrag an ursprünglicher Position wieder her", () =>
        {
            var f = New(); var entries = Enumerable.Range(0, 3).Select(i => Targets.Create(Path.Combine(f.Root, "missing-" + i + ".lnk"))).ToList(); f.Group.Items.AddRange(entries); f.Store.Save(f.State);
            var manager = new ShortcutManager(f.Vault, _ => throw new IOException("disk full")); Fail(() => manager.RemoveFromGroup(f.State, f.Group, entries[1]));
            assert(f.Group.Items.SequenceEqual(entries) && f.Store.Load().Groups[0].Items.Select(e => e.Id).SequenceEqual(entries.Select(e => e.Id)));
        });
        test("Letzte Referenz entfernen stellt Desktop-Datei wieder her", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); var target = f.Group.Items[0].Target;
            f.Manager.Remove(f.State, f.Group, f.Group.Items[0]); assert(File.Exists(source) && f.Group.Items.Count == 0 && File.Exists(f.Manager.Resolve(target)));
        });
        test("Weitere Gruppe hält Verknüpfung im Speicher", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); var second = new BubbleGroup(); f.State.Groups.Add(second);
            f.Manager.Add(f.State, second, source); f.Manager.RemoveGroup(f.State, f.Group); assert(!File.Exists(source));
            f.Manager.RemoveGroup(f.State, second); assert(File.Exists(source));
        });
        test("Verschieben zwischen Bubbles stellt kein Desktop-Original wieder her", () =>
        {
            var f = New(); string source = f.Link(); f.Manager.Add(f.State, f.Group, source); var entry = f.Group.Items[0]; var other = new BubbleGroup(); f.State.Groups.Add(other);
            f.Manager.Transfer(f.State, f.Group, other, entry);
            assert(f.Group.Items.Count == 0 && other.Items.Single() == entry && !File.Exists(source) && f.Manager.CanRestore(entry.Target));
        });
        test("Fehler beim Bubble-Transfer rollt beide Gruppen zurück", () =>
        {
            var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); var entry = f.Group.Items[0]; var other = new BubbleGroup(); f.State.Groups.Add(other);
            var manager = new ShortcutManager(f.Vault, _ => throw new IOException("disk full")); Fail(() => manager.Transfer(f.State, f.Group, other, entry));
            assert(f.Group.Items.Single() == entry && other.Items.Count == 0);
        });
        test("Abgebrochener oder unbestätigter Export behält Bubble-Eintrag", () =>
        {
            var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); var entry = f.Group.Items[0];
            assert(!f.Manager.CompleteExport(f.State, f.Group, entry, false) && f.Group.Items.Count == 1 && f.Manager.CanRestore(entry.Target));
        });
        test("Bestätigter Export entfernt nur Eintrag und behält interne Nutzdaten", () =>
        {
            var f = New(); string source = f.Link(); f.Manager.Add(f.State, f.Group, source); var entry = f.Group.Items[0];
            assert(f.Manager.CompleteExport(f.State, f.Group, entry, true) && f.Group.Items.Count == 0 && File.Exists(f.Manager.Resolve(entry.Target)) && !File.Exists(source) && !f.Manager.CanRestore(entry.Target));
        });
        test("Export-Commitfehler behält Eintrag und Rückgabeanspruch", () =>
        {
            var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); var entry = f.Group.Items[0];
            var manager = new ShortcutManager(f.Vault, _ => throw new IOException("disk full")); Fail(() => manager.CompleteExport(f.State, f.Group, entry, true));
            assert(f.Group.Items.Single() == entry && f.Manager.CanRestore(entry.Target));
        });
        test("Restore überschreibt keine gleichnamige Desktop-Datei", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); File.WriteAllText(source, "new user file");
            string? restored = f.Manager.Restore(f.Group.Items[0].Target);
            assert(restored != source && File.ReadAllText(source) == "new user file" && File.ReadAllText(restored!).Contains("https://example.com"));
            assert(f.Manager.Restore(f.Group.Items[0].Target) == restored);
        });
        test("Restore scheitert: Eintrag und Sicherung bleiben erhalten", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); var target = f.Group.Items[0].Target;
            File.Delete(f.Manager.Resolve(target)); Fail(() => f.Manager.Remove(f.State, f.Group, f.Group.Items[0])); assert(f.Group.Items.Count == 1);
        });
        test("Restore mit langem kollidierendem Dateinamen bleibt erreichbar", () =>
        {
            var f = New(); string source = Path.Combine(f.Platform.PersonalDesktop, new string('a', 240) + ".url"); File.WriteAllText(source, "[InternetShortcut]\nURL=https://example.com");
            f.Manager.Add(f.State, f.Group, source); File.WriteAllText(source, "other"); var restored = f.Manager.Restore(f.Group.Items[0].Target);
            assert(restored is not null && Path.GetFileName(restored).Length <= 255 && File.ReadAllText(source) == "other");
        });
        test("Speichern nach Restore scheitert: Eintrag bleibt nutzbar", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); var target = f.Group.Items[0].Target;
            var manager = new ShortcutManager(f.Vault, _ => throw new IOException("disk full")); Fail(() => manager.Remove(f.State, f.Group, f.Group.Items[0]));
            assert(f.Group.Items.Count == 1 && File.Exists(source) && File.Exists(manager.Resolve(target)));
        });
        test("Crash vor Desktop-Löschung: Recovery löscht kein Original", () =>
        {
            var f = New(); var source = f.Link(); var prepared = f.Vault.Prepare(source, true); f.Group.Items.Add(Targets.Create(prepared.Target)); f.Store.Save(f.State);
            var restarted = new ShortcutVault(f.Root, f.Platform); var manager = new ShortcutManager(restarted, f.Store.Save);
            assert(manager.Recover(f.Store.Load()).Count == 1 && File.Exists(source));
        });
        test("Crash nach Desktop-Löschung: Sicherung bleibt wiederherstellbar", () =>
        {
            var f = New(); var source = f.Link(); var prepared = f.Vault.Prepare(source, true); f.Group.Items.Add(Targets.Create(prepared.Target)); f.Store.Save(f.State); File.Delete(source);
            var manager = new ShortcutManager(new ShortcutVault(f.Root, f.Platform), f.Store.Save); assert(manager.Recover(f.Store.Load()).Count == 1);
            assert(manager.Restore(prepared.Target) == source && File.Exists(source));
        });
        test("Sicherung übersteht absoluten Umzug des Datenordners", () =>
        {
            var f = New(); var source = f.Link(); f.Manager.Add(f.State, f.Group, source); var archive = Path.Combine(f.Root, "backup.bubbles"); ConfigurationBundle.Export(archive, f.State, f.Vault);
            var imported = ConfigurationBundle.Read(archive); var other = New(); foreach (var payload in imported.Payloads) other.Vault.ImportPayload(payload.Key, payload.Value);
            var target = imported.State.Groups[0].Items[0].Target;
            assert(File.Exists(other.Manager.Resolve(target)) && other.Manager.Resolve(target) != f.Manager.Resolve(target) && !other.Manager.CanRestore(target));
        });
        test("Ein fremdes Archiv bekommt keine lokalen Desktop-Löschrechte", () =>
        {
            var f = New(); var target = ManagedTargets.Prefix + Guid.NewGuid().ToString("N") + ".url"; f.Vault.ImportPayload(target, "[InternetShortcut]\nURL=https://example.com"u8.ToArray());
            assert(!f.Manager.CanRestore(target) && f.Manager.Restore(target) is null);
        });
        test("Bundle blockiert Pfad-Traversal ohne zu extrahieren", () =>
        {
            var f = New(); var path = Path.Combine(f.Root, "bad.bubbles"); using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) zip.CreateEntry("../escape.url");
            reject(() => ConfigurationBundle.Read(path)); assert(!File.Exists(Path.Combine(f.Root, "escape.url")));
        });
        test("Bundle blockiert doppelte Manifest-Einträge", () =>
        {
            var f = New(); var path = Path.Combine(f.Root, "duplicates.bubbles"); using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) { zip.CreateEntry("groups.json"); zip.CreateEntry("groups.json"); }
            reject(() => ConfigurationBundle.Read(path));
        });
        test("Bundle erkennt beschädigte Verknüpfungen", () =>
        {
            var f = New(); f.Manager.Add(f.State, f.Group, f.Link()); var path = Path.Combine(f.Root, "tampered.bubbles"); ConfigurationBundle.Export(path, f.State, f.Vault);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) { var entry = zip.Entries.First(e => e.FullName.StartsWith("shortcuts/")); string name = entry.FullName; entry.Delete(); using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("corrupt"); }
            reject(() => ConfigurationBundle.Read(path));
        });
        test("Managed-IDs erlauben keine beliebigen Dateipfade", () =>
        {
            foreach (var target in new[] { "bubble-link:../x.url", "bubble-link:C:\\x.lnk", "bubble-link:" + Guid.NewGuid().ToString("N") + ".exe" })
            { assert(!ManagedTargets.IsManaged(target)); reject(() => New().Vault.Resolve(target)); }
        });
        test("Versteckte Namen und Kopiermodus bleiben nach Neustart erhalten", () =>
        {
            var f = New(); f.Group.ShowName = false; f.State.Settings.MoveDesktopShortcuts = false; f.Store.Save(f.State); var state = f.Store.Load(); assert(!state.Groups[0].ShowName && !state.Settings.MoveDesktopShortcuts);
        });
        test("Popup-Zentrum und vier Bildschirmkanten", () =>
        {
            assert(Placement.Flyout(new(800, 500, 100, 100), 400, 400, new(0, 0, 1920, 1080)) == (650, 350));
            foreach (var point in new[] { (0, 0), (1800, 0), (0, 950), (1800, 950) })
            { var p = Placement.Flyout(new(point.Item1, point.Item2, 104, 116), 450, 400, new(0, 0, 1920, 1080)); assert(p.X >= 0 && p.X + 450 <= 1920 && p.Y >= 0 && p.Y + 400 <= 1080); }
        });
    }
    private sealed class Fixture
    {
        public string Root { get; }
        public FakePlatform Platform { get; }
        public ShortcutVault Vault { get; }
        public ShortcutManager Manager { get; }
        public StateStore Store { get; }
        public BubbleState State { get; } = new();
        public BubbleGroup Group { get; } = new();
        public Fixture(string root)
        {
            Root = root; Directory.CreateDirectory(root); Platform = new FakePlatform(Path.Combine(root, "Desktop")); Directory.CreateDirectory(Platform.PersonalDesktop);
            Vault = new ShortcutVault(root, Platform); Store = new StateStore(Path.Combine(root, "groups.json")); Manager = new ShortcutManager(Vault, Store.Save); State.Groups.Add(Group);
        }
        public string Link() { var path = Path.Combine(Platform.PersonalDesktop, "Launcher.url"); File.WriteAllText(path, "[InternetShortcut]\nURL=https://example.com"); return path; }
    }
    private sealed class FakePlatform(string desktop) : IShortcutPlatform
    {
        public bool DenyDelete, Valid = true;
        public string PersonalDesktop => desktop;
        public bool IsPersonalDesktopShortcut(string path) => Path.IsPathFullyQualified(path) && Path.GetDirectoryName(path) == desktop && Path.GetExtension(path) is ".lnk" or ".url";
        public bool ValidateRelocatedShortcut(string source, string copy) => Valid;
        public void DeleteOriginalIfUnchanged(string path, string hash)
        { if (DenyDelete || ShortcutVault.Hash(path) != hash) throw new IOException("Locked or changed"); File.Delete(path); }
    }
}

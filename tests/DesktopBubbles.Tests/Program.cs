using System.Text.Json;
using DesktopBubbles.Core;

var root = Path.Combine(args.FirstOrDefault() ?? Path.GetTempPath(), "DesktopBubbles-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
int passed = 0, failed = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex); failed++; }
}
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Reject(Action action) { try { action(); } catch (Exception ex) when (ex is InvalidDataException or ArgumentException or JsonException) { return; } throw new Exception("Expected rejection"); }
BubbleState Sample() => new() { Groups = [new BubbleGroup { Name = "Gaming 🎮", X = -1200, Y = 86, Items = [Targets.Create(Path.Combine(root, "Steam.lnk"), "Steam")] }] };
try
{
    Test("Leere Konfiguration", () => Assert(new StateStore(Path.Combine(root, "none.json")).Load().Groups.Count == 0));
    Test("JSON-Roundtrip mit Unicode, Reihenfolge und negativen Koordinaten", () =>
    {
        var store = new StateStore(Path.Combine(root, "roundtrip.json")); var state = Sample();
        state.Groups[0].CustomIcon = Path.Combine(root, "custom.ico"); Targets.Add(state.Groups[0], Targets.Create("https://example.com"));
        store.Save(state); var loaded = store.Load(); Assert(loaded.Groups[0].Name == "Gaming 🎮" && loaded.Groups[0].X == -1200);
        Assert(loaded.Groups[0].Items.Select(x => x.Target).SequenceEqual(state.Groups[0].Items.Select(x => x.Target)) && loaded.Groups[0].CustomIcon == state.Groups[0].CustomIcon);
    });
    Test("Atomisches Ersetzen und Backup", () =>
    {
        var store = new StateStore(Path.Combine(root, "backup.json")); var state = Sample(); store.Save(state);
        state.Groups[0].Name = "Neu"; store.Save(state); Assert(store.Load().Groups[0].Name == "Neu");
        Assert(StateStore.Read(store.Path + ".bak").Groups[0].Name == "Gaming 🎮" && !File.Exists(store.Path + ".tmp"));
    });
    Test("Defekte Konfiguration aus Backup wiederherstellen", () =>
    {
        var store = new StateStore(Path.Combine(root, "recovery.json")); store.Save(Sample()); store.Save(Sample());
        File.WriteAllText(store.Path, "{BROKEN"); Assert(store.Load().Groups.Count == 1 && store.RecoveryNotice is not null);
        Assert(Directory.GetFiles(root, "recovery.json.damaged-*").Length == 1);
    });
    Test("Defekte Konfiguration ohne Backup erhalten", () =>
    {
        var store = new StateStore(Path.Combine(root, "damaged.json")); File.WriteAllText(store.Path, "null");
        Assert(store.Load().Groups.Count == 0 && Directory.GetFiles(root, "damaged.json.damaged-*").Length == 1);
    });
    Test("Gültiges Backup nach Wiederherstellung behalten", () =>
    {
        var store = new StateStore(Path.Combine(root, "keep-backup.json")); store.Save(Sample()); store.Save(Sample());
        File.WriteAllText(store.Path, "BROKEN"); var recovered = store.Load(); recovered.Groups[0].Name = "Repariert"; store.Save(recovered);
        Assert(StateStore.Read(store.Path).Groups[0].Name == "Repariert");
        Assert(StateStore.Read(store.Path + ".bak").Groups[0].Name == "Gaming 🎮");
    });
    Test("Unbekannte Importversion ablehnen", () => Reject(() => StateStore.Validate(new BubbleState { Version = 999 })));
    Test("Doppelte Gruppen-ID ablehnen", () => { var s = Sample(); s.Groups.Add(s.Groups[0]); Reject(() => StateStore.Validate(s)); });
    Test("Doppelte Eintrags-ID ablehnen", () => { var s = Sample(); s.Groups[0].Items.Add(s.Groups[0].Items[0]); Reject(() => StateStore.Validate(s)); });
    Test("Null-Listen ablehnen", () => Reject(() => StateStore.Validate(new BubbleState { Groups = null! })));
    Test("Null-Einträge ablehnen", () => { var s = Sample(); s.Groups[0].Items.Add(null!); Reject(() => StateStore.Validate(s)); });
    Test("NaN-Position ablehnen", () => { var s = Sample(); s.Groups[0].X = double.NaN; Reject(() => StateStore.Validate(s)); });
    Test("Ungültige Einstellungen begrenzen", () =>
    {
        var s = Sample(); s.Settings.Columns = 100; s.Settings.Opacity = -2; s.Settings.AnimationMilliseconds = 900;
        s.Settings.TileSize = double.NaN; s.Settings.Theme = "Other"; StateStore.Validate(s);
        Assert(s.Settings.Columns == 8 && s.Settings.Opacity == 0 && s.Settings.AnimationMilliseconds == 450 && s.Settings.TileSize == 116 && s.Settings.Theme == "System");
    });
    Test("Mehr als 100 Gruppen ablehnen", () => Reject(() => StateStore.Validate(new BubbleState { Groups = Enumerable.Range(0, 101).Select(_ => new BubbleGroup()).ToList() })));
    Test("Relative Pfade ablehnen", () => Reject(() => Targets.Create("../oops.exe")));
    Test("Befehlszeile statt Datei ablehnen", () => Reject(() => Targets.Create("cmd /c test")));
    Test("Nicht unterstützte URL-Schemata ablehnen", () => { Reject(() => Targets.Create("javascript:alert(1)")); Reject(() => Targets.Create("file:///C:/x.exe")); });
    Test("HTTP und HTTPS akzeptieren", () => Assert(Targets.IsWeb("https://example.com/a?q=1") && Targets.IsWeb("http://example.com")));
    Test("Store-AUMID behalten", () => { var e = Targets.Create(Targets.AppsPrefix + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "Rechner"); Assert(Targets.IsStore(e.Target) && Targets.Exists(e.Target)); });
    Test("Leere Store-ID ablehnen", () => Reject(() => Targets.Create(Targets.AppsPrefix)));
    Test("Anführungszeichen im Store-Ziel ablehnen", () => Reject(() => Targets.Create(Targets.AppsPrefix + "bad\"app")));
    Test("Gelöschtes Ziel erkennen", () => Assert(!Targets.Exists(Path.Combine(root, "missing.exe"))));
    Test("Datei und Ordner erkennen", () => { var p = Path.Combine(root, "real.url"); File.WriteAllText(p, "[InternetShortcut]\nURL=https://example.com"); Assert(Targets.Exists(p) && Targets.Exists(root)); });
    Test(".lnk .exe .url unverändert speichern", () =>
    {
        foreach (var ext in new[] { ".lnk", ".exe", ".url", ".txt" }) { var p = Path.Combine(root, "app" + ext); Assert(Targets.Create(p).Target == p); }
    });
    Test("Hinzufügen verändert Originaldatei nicht", () =>
    {
        var p = Path.Combine(root, "original.lnk"); File.WriteAllText(p, "Original"); var g = new BubbleGroup(); Targets.Add(g, Targets.Create(p));
        Assert(File.Exists(p) && File.ReadAllText(p) == "Original");
    });
    Test("Doppelte Ziele ignorieren", () => { var g = new BubbleGroup(); var e = Targets.Create(Path.Combine(root, "app.exe")); Assert(Targets.Add(g, e) && !Targets.Add(g, Targets.Create(e.Target.ToUpperInvariant())) && g.Items.Count == 1); });
    Test("Sortieren nach hinten", () => { var g = Sample().Groups[0]; var first = g.Items[0]; Targets.Add(g, Targets.Create("https://example.com")); Targets.Move(g, first.Id, 1); Assert(g.Items[1] == first); });
    Test("Sortieren nach vorne / Index begrenzen", () => { var g = Sample().Groups[0]; Targets.Add(g, Targets.Create("https://example.com")); var last = g.Items[1]; Targets.Move(g, last.Id, -99); Assert(g.Items[0] == last); Targets.Move(g, last.Id, 99); Assert(g.Items[1] == last); });
    Test("Unbekannte Sortier-ID ohne Änderung", () => { var g = Sample().Groups[0]; var id = g.Items[0].Id; Targets.Move(g, Guid.NewGuid(), 0); Assert(g.Items[0].Id == id); });
    Test("Entfernen löscht Original nicht", () => { var p = Path.Combine(root, "keep.txt"); File.WriteAllText(p, "keep"); var g = new BubbleGroup(); Targets.Add(g, Targets.Create(p)); g.Items.Clear(); Assert(File.Exists(p)); });
    Test("Position auf 1080p begrenzen", () => Assert(Placement.Clamp(3000, -100, 104, 116, new(0, 0, 1920, 1080)) == (1816, 0)));
    Test("Monitor links mit negativen Koordinaten", () => Assert(Placement.Clamp(-1800, 100, 104, 116, new(-1920, 0, 1920, 1080)) == (-1800, 100)));
    Test("Entfernter Monitor: nächster verfügbarer Monitor", () => Assert(Placement.Nearest(-1800, 100, [new(0, 0, 1920, 1080)]).X == 0));
    Test("Monitorwahl bei mehreren Auflösungen", () => Assert(Placement.Nearest(2300, -500, [new(0, 0, 1920, 1080), new(1920, -800, 2560, 1440)]).X == 1920));
    Test("Übergroße Fläche bleibt erreichbar", () => Assert(Placement.Clamp(700, 700, 900, 900, new(0, 0, 640, 480)) == (0, 0)));
    Test("Flyout zentriert über Bubble und begrenzt unten", () => { var p = Placement.Flyout(new(900, 900, 100, 100), 400, 400, new(0, 0, 1920, 1080)); Assert(p == (750, 680)); });
    Test("Flyout an linker Kante", () => Assert(Placement.Flyout(new(0, 100, 104, 116), 450, 400, new(0, 0, 1920, 1080)).X == 0));
    Test("4K-Skalierung in physischen Pixeln", () => Assert(Placement.Clamp(3800, 2100, 208, 232, new(0, 0, 3840, 2160)) == (3632, 1928)));
    Test("Namen bereinigen und begrenzen", () => Assert(StateStore.CleanName("\n") == "Unbenannt" && StateStore.CleanName(new string('a', 100)).Length == 80));
    Test("Leere Unicode-Linknamen benötigen App-Namensauflösung", () => Assert(Targets.NeedsFriendlyName(Targets.Create(Path.Combine(root, "\u00A0\u00A0.lnk")))));
    Test("Gespeicherten Platzhalter eines leeren Links erkennen", () => Assert(Targets.NeedsFriendlyName(new AppEntry { Target = Path.Combine(root, "\u00A0 \u00A0.lnk"), Name = "Unbenannt" })));
    Test("Eigene Anzeigenamen beibehalten", () => Assert(!Targets.NeedsFriendlyName(new AppEntry { Target = Path.Combine(root, "\u00A0.lnk"), Name = "Mein Launcher" }) && !Targets.NeedsFriendlyName(new AppEntry { Target = Path.Combine(root, "named.lnk"), Name = "Unbenannt" })));
    Test("Export-Import übernimmt Einstellungen", () => { var s = Sample(); s.Settings.Animations = false; s.Settings.Theme = "Dark"; var p = Path.Combine(root, "export.json"); new StateStore(p).Save(s); Assert(!StateStore.Read(p).Settings.Animations && StateStore.Read(p).Settings.Theme == "Dark"); });
    Test("Import größer 8 MiB ablehnen", () => { var p = Path.Combine(root, "large.json"); using (var f = File.Create(p)) f.SetLength(8 * 1024 * 1024 + 1); Reject(() => StateStore.Read(p)); });
    Test("100 Speichervorgänge ohne temporäre Restdateien", () => { var store = new StateStore(Path.Combine(root, "cycles.json")); for (int i = 0; i < 100; i++) store.Save(Sample()); Assert(!File.Exists(store.Path + ".tmp") && store.Load().Groups.Count == 1); });
    ManagedShortcutTests.Run(root, Test, Assert, Reject);
    AppearanceTests.Run(Test, Assert, root);
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"{passed} bestanden, {failed} fehlgeschlagen.");
return failed == 0 ? 0 : 1;

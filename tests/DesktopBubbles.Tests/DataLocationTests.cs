using DesktopBubbles.Core;
using DesktopBubbles.Infrastructure;

internal static class DataLocationTests
{
    public static void Run(string root, Action<string, Action> test, Action<bool> assert)
    {
        (string Source, string User, BubbleState State) Fixture()
        {
            string fixture = Path.Combine(root, "storage-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(fixture, "portable"), user = Path.Combine(fixture, "user");
            var state = new BubbleState { Groups = [new BubbleGroup { Name = "Behalten", Items = [Targets.Create(Path.Combine(fixture, "missing.exe"))] }] };
            new StateStore(Path.Combine(source, "groups.json")).Save(state);
            Directory.CreateDirectory(Path.Combine(source, "shortcuts"));
            File.WriteAllText(Path.Combine(source, "shortcuts", "copy.url"), "[InternetShortcut]\nURL=https://example.com");
            File.WriteAllText(Path.Combine(source, "shortcuts", "copy.url.json"), "history");
            return (source, user, state);
        }
        test("Beschreibbare portable Daten bleiben am bisherigen Ort", () =>
        {
            var f = Fixture(); var location = DataLocation.Select(f.Source, f.User); location.Prepare();
            assert(!location.Redirected && location.DirectoryPath == f.Source && !Directory.Exists(f.User));
        });
        test("Geschützter Speicher wird vollständig und ohne Quelländerung übernommen", () =>
        {
            var f = Fixture(); string original = File.ReadAllText(Path.Combine(f.Source, "groups.json"));
            var location = DataLocation.Select(f.Source, f.User, path => path != f.Source && DataLocation.IsWritable(path)); location.Prepare();
            var loaded = StateStore.Read(Path.Combine(location.DirectoryPath, "groups.json"));
            assert(location.Redirected && loaded.Groups[0].Id == f.State.Groups[0].Id && loaded.Groups[0].Items.Count == 1 && File.ReadAllText(Path.Combine(f.Source, "groups.json")) == original);
            assert(File.ReadAllText(Path.Combine(location.DirectoryPath, "shortcuts", "copy.url.json")) == "history");
            loaded.Groups[0].Items.Clear(); new StateStore(Path.Combine(location.DirectoryPath, "groups.json")).Save(loaded);
            assert(StateStore.Read(Path.Combine(location.DirectoryPath, "groups.json")).Groups[0].Items.Count == 0 && StateStore.Read(Path.Combine(f.Source, "groups.json")).Groups[0].Items.Count == 1);
        });
        test("Neustart verwendet neuere Benutzerdaten statt erneuter Kopie", () =>
        {
            var f = Fixture(); var first = DataLocation.Select(f.Source, f.User, path => path != f.Source && DataLocation.IsWritable(path)); first.Prepare();
            var state = StateStore.Read(Path.Combine(first.DirectoryPath, "groups.json")); state.Groups.Clear(); new StateStore(Path.Combine(first.DirectoryPath, "groups.json")).Save(state);
            var second = DataLocation.Select(f.Source, f.User); second.Prepare();
            assert(second.Redirected && second.DirectoryPath == first.DirectoryPath && StateStore.Read(Path.Combine(second.DirectoryPath, "groups.json")).Groups.Count == 0);
        });
        test("Verschiedene portable Datensammlungen überschreiben einander nicht", () =>
        {
            var a = Fixture(); var b = Fixture(); var one = DataLocation.Select(a.Source, a.User, p => p != a.Source && DataLocation.IsWritable(p)); var two = DataLocation.Select(b.Source, a.User, p => p != b.Source && DataLocation.IsWritable(p));
            one.Prepare(); two.Prepare(); assert(one.DirectoryPath != two.DirectoryPath && StateStore.Read(Path.Combine(one.DirectoryPath, "groups.json")).Groups[0].Id == a.State.Groups[0].Id && StateStore.Read(Path.Combine(two.DirectoryPath, "groups.json")).Groups[0].Id == b.State.Groups[0].Id);
        });
        test("Fehlerhafte Quelle wird nicht als leere erfolgreiche Migration veröffentlicht", () =>
        {
            var f = Fixture(); File.WriteAllText(Path.Combine(f.Source, "groups.json"), "broken");
            var location = DataLocation.Select(f.Source, f.User, p => p != f.Source && DataLocation.IsWritable(p));
            bool rejected = false; try { location.Prepare(); } catch (System.Text.Json.JsonException) { rejected = true; }
            assert(rejected && !Directory.Exists(location.DirectoryPath) && !Directory.EnumerateDirectories(Path.GetDirectoryName(location.DirectoryPath)!, ".migration-*").Any());
        });
        test("Gültiges Backup bleibt bei beschädigter Hauptdatei in der Migration erhalten", () =>
        {
            var f = Fixture(); string path = Path.Combine(f.Source, "groups.json"); File.Copy(path, path + ".bak"); File.WriteAllText(path, "broken");
            var location = DataLocation.Select(f.Source, f.User, p => p != f.Source && DataLocation.IsWritable(p)); location.Prepare();
            assert(new StateStore(Path.Combine(location.DirectoryPath, "groups.json")).Load().Groups[0].Id == f.State.Groups[0].Id && File.ReadAllText(path) == "broken");
        });
        test("Read-only Konfiguration löst unter Windows echte Umleitung aus", () =>
        {
            if (!OperatingSystem.IsWindows()) return;
            var f = Fixture(); string path = Path.Combine(f.Source, "groups.json"); File.SetAttributes(path, FileAttributes.ReadOnly);
            try
            {
                var location = DataLocation.Select(f.Source, f.User); location.Prepare();
                assert(location.Redirected && DataLocation.IsWritable(location.DirectoryPath) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0);
            }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        });
        test("Alte schreibgeschützte groups.json.tmp blockiert neue atomare Speicherung nicht", () =>
        {
            var f = Fixture(); string path = Path.Combine(f.Source, "groups.json"), stale = path + ".tmp"; File.WriteAllText(stale, "stale"); File.SetAttributes(stale, FileAttributes.ReadOnly);
            try { f.State.Groups.Clear(); new StateStore(path).Save(f.State); assert(StateStore.Read(path).Groups.Count == 0 && File.ReadAllText(stale) == "stale"); }
            finally { File.SetAttributes(stale, FileAttributes.Normal); }
        });
    }
}

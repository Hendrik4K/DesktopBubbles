using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DesktopBubbles.Infrastructure;

public sealed record DataLocation(string RequestedDirectory, string DirectoryPath, bool Redirected)
{
    private const string Marker = ".storage-origin.json";

    public static DataLocation Select(string requestedDirectory, string userRoot, Func<string, bool>? canWrite = null)
    {
        string requested = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requestedDirectory));
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requested.ToUpperInvariant())))[..32];
        string fallback = Path.Combine(Path.GetFullPath(userRoot), "profiles", key);
        canWrite ??= IsWritable;
        // Once redirected, keep the newer user data even if the original later becomes writable.
        if (Directory.Exists(fallback))
        {
            VerifyOrigin(fallback, requested);
            if (!canWrite(fallback)) throw new IOException("Der Benutzerdatenordner ist nicht beschreibbar: " + fallback);
            return new(requested, fallback, true);
        }
        if (canWrite(requested)) return new(requested, requested, false);
        string parent = Path.GetDirectoryName(fallback)!;
        if (!canWrite(parent)) throw new IOException("Kein beschreibbarer Datenspeicher verfügbar. Bitte einen eigenen Datenordner mit Schreibrechten wählen.");
        return new(requested, fallback, true);
    }

    public static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            // Probe actual permissions, including delete rights used by atomic replacement.
            using (var probe = new FileStream(Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N")), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose)) { probe.WriteByte(0); probe.Flush(true); }
            foreach (string name in new[] { "groups.json", "groups.json.bak" })
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path)) { using var check = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); }
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { return false; }
    }

    // Call after acquiring the mutex for DirectoryPath. Never change the protected source.
    public void Prepare()
    {
        if (!Redirected) { Directory.CreateDirectory(DirectoryPath); return; }
        if (Directory.Exists(DirectoryPath)) { VerifyOrigin(DirectoryPath, RequestedDirectory); return; }
        string parent = Path.GetDirectoryName(DirectoryPath)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var files = SourceFiles().ToArray();
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string source in files)
            {
                string relative = Path.GetRelativePath(RequestedDirectory, source);
                string destination = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string before = Hash(source);
                // Copy bytes, not read-only attributes or ACLs from Program Files.
                using (var input = File.OpenRead(source))
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
                if (Hash(destination) != before) throw new IOException("Datenkopie konnte nicht geprüft werden.");
                hashes[source] = before;
            }
            if (!SourceFiles().Order().SequenceEqual(files.Order()) || hashes.Any(pair => Hash(pair.Key) != pair.Value))
                throw new IOException("Die bisherigen Daten wurden während der Übernahme geändert. Bitte die alte App beenden und erneut starten.");
            string state = Path.Combine(staging, "groups.json");
            if (File.Exists(state))
            {
                try { StateStore.Read(state); }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                { if (!File.Exists(state + ".bak")) throw; StateStore.Read(state + ".bak"); }
            }
            File.WriteAllText(Path.Combine(staging, Marker), JsonSerializer.Serialize(new Origin(RequestedDirectory)));
            // Windows can briefly hold a newly scanned file without delete sharing.
            // Retry only the atomic rename, never overwrite or recopy newer user data.
            for (int attempt = 0; ; attempt++)
            {
                try { Directory.Move(staging, DirectoryPath); break; }
                catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException)
                { Thread.Sleep((attempt + 1) * 100); }
            }
        }
        finally
        {
            if (Directory.Exists(staging) && Path.GetFullPath(staging).StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(staging, true);
        }
    }

    private IEnumerable<string> SourceFiles()
    {
        if (!Directory.Exists(RequestedDirectory)) yield break;
        RejectReparse(RequestedDirectory);
        foreach (string file in Directory.EnumerateFiles(RequestedDirectory))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("groups.json", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("before-import-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".bubbles", StringComparison.OrdinalIgnoreCase)) { RejectReparse(file); yield return file; }
        }
        string shortcuts = Path.Combine(RequestedDirectory, "shortcuts");
        if (Directory.Exists(shortcuts)) foreach (string file in Tree(shortcuts)) yield return file;
    }
    private static IEnumerable<string> Tree(string directory)
    {
        RejectReparse(directory);
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectReparse(path);
            if (Directory.Exists(path)) { foreach (string file in Tree(path)) yield return file; }
            else if (!path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) yield return path;
        }
    }
    private static void RejectReparse(string path)
    { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Der Datenspeicher enthält einen Dateisystem-Link und kann nicht automatisch kopiert werden."); }
    private static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
    private static void VerifyOrigin(string directory, string requested)
    {
        var origin = JsonSerializer.Deserialize<Origin>(File.ReadAllText(Path.Combine(directory, Marker)));
        if (origin is null || !string.Equals(origin.SourceDirectory, requested, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Vorhandene Benutzerdaten gehören zu einem anderen Speicherort; sie werden nicht überschrieben.");
    }
    private sealed record Origin(string SourceDirectory);
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DesktopBubbles.Core;

namespace DesktopBubbles.Infrastructure;

/// <summary>A bounded archive reader; never extracts paths supplied by a ZIP entry.</summary>
public static class ConfigurationBundle
{
    private const long MaxArchiveBytes = 128L * 1024 * 1024;
    public static void Export(string path, BubbleState state, ShortcutVault vault)
    {
        StateStore.Validate(state);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(state, StateStore.JsonOptions);
        if (manifestBytes.Length > 8 * 1024 * 1024) throw new IOException("Die Konfiguration überschreitet 8 MB.");
        string temp = path + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    using (var manifest = zip.CreateEntry("groups.json").Open()) manifest.Write(manifestBytes);
                    var hashes = new Dictionary<string, string>();
                    long total = manifestBytes.Length;
                    foreach (var target in state.Groups.SelectMany(g => g.Items).Select(e => e.Target).Where(ManagedTargets.IsManaged).Distinct())
                    {
                        ManagedTargets.TryFileName(target, out var name);
                        using var memory = new MemoryStream(); vault.ExportPayload(target, memory);
                        total += memory.Length; if (total > MaxArchiveBytes) throw new IOException("Die Sicherung überschreitet 128 MB.");
                        var bytes = memory.ToArray(); hashes[name] = Convert.ToHexString(SHA256.HashData(bytes));
                        using var entry = zip.CreateEntry("shortcuts/" + name).Open(); entry.Write(bytes);
                    }
                    var digestBytes = JsonSerializer.SerializeToUtf8Bytes(hashes);
                    if (total + digestBytes.Length > MaxArchiveBytes) throw new IOException("Die Sicherung überschreitet 128 MB.");
                    using var digest = zip.CreateEntry("sha256.json").Open(); digest.Write(digestBytes);
                }
                if (output.Length > MaxArchiveBytes) throw new IOException("Die Sicherung überschreitet 128 MB.");
                output.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public sealed record ImportData(BubbleState State, IReadOnlyDictionary<string, byte[]> Payloads);
    public static ImportData Read(string path)
    {
        if (new FileInfo(path).Length > MaxArchiveBytes) throw new InvalidDataException("Das Archiv überschreitet 128 MB.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 30002) throw new InvalidDataException("Zu viele Archiveinträge.");
        long total = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            total += entry.Length;
            bool allowed = entry.FullName is "groups.json" or "sha256.json" || (entry.FullName.StartsWith("shortcuts/", StringComparison.Ordinal)
                && ManagedTargets.IsManaged(ManagedTargets.Prefix + entry.FullName[10..]));
            if (!allowed || !names.Add(entry.FullName) || entry.Length < 0 || entry.Length > 8 * 1024 * 1024 || total > MaxArchiveBytes)
                throw new InvalidDataException("Ungültiger oder zu großer Archiveintrag.");
        }
        long actualTotal = 0;
        byte[] Bytes(string name, int limit)
        {
            var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Archiveintrag fehlt: " + name);
            if (entry.Length > limit) throw new InvalidDataException("Archiveintrag zu groß.");
            using var stream = entry.Open(); using var memory = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = stream.Read(buffer)) > 0)
            {
                actualTotal += count;
                if (memory.Length + count > limit || actualTotal > MaxArchiveBytes) throw new InvalidDataException("Archiveintrag zu groß.");
                memory.Write(buffer, 0, count);
            }
            return memory.ToArray();
        }
        var state = JsonSerializer.Deserialize<BubbleState>(Bytes("groups.json", 8 * 1024 * 1024), StateStore.JsonOptions) ?? throw new InvalidDataException("Konfiguration fehlt.");
        StateStore.Validate(state);
        var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(Bytes("sha256.json", 4 * 1024 * 1024)) ?? throw new InvalidDataException("Prüfsummen fehlen.");
        var payloads = new Dictionary<string, byte[]>();
        foreach (var target in state.Groups.SelectMany(g => g.Items).Select(e => e.Target).Where(ManagedTargets.IsManaged).Distinct())
        {
            ManagedTargets.TryFileName(target, out var name);
            var bytes = Bytes("shortcuts/" + name, ShortcutVault.MaxLinkBytes);
            if (!hashes.TryGetValue(name, out var hash) || hash != Convert.ToHexString(SHA256.HashData(bytes))) throw new InvalidDataException("Prüfsumme stimmt nicht: " + name);
            payloads.Add(target, bytes);
        }
        return new(state, payloads);
    }
}

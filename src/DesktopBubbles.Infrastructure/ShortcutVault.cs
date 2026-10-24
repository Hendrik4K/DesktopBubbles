using System.Security.Cryptography;
using System.Text.Json;
using DesktopBubbles.Application;
using DesktopBubbles.Core;

namespace DesktopBubbles.Infrastructure;

public sealed class ShortcutRecord
{
    public string FileName { get; set; } = "";
    public string Source { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Phase { get; set; } = "Prepared";
    public bool MoveRequested { get; set; }
    public string? RestoredPath { get; set; }
}

/// <summary>Append-only recovery payloads. Config backups remain usable after removal/import.</summary>
public sealed class ShortcutVault : IShortcutVault
{
    public const int MaxLinkBytes = 1024 * 1024;
    private readonly string root;
    private readonly IShortcutPlatform platform;
    public ShortcutVault(string dataDirectory, IShortcutPlatform platform)
    {
        root = Path.Combine(Path.GetFullPath(dataDirectory), "shortcuts"); this.platform = platform;
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Der Verknüpfungsspeicher darf kein Dateisystem-Link sein.");
    }
    public string Resolve(string target)
    {
        if (!target.StartsWith(ManagedTargets.Prefix, StringComparison.Ordinal)) return target;
        if (!ManagedTargets.TryFileName(target, out var name)) throw new InvalidDataException("Ungültige gespeicherte Verknüpfung.");
        var path = Path.Combine(root, name);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfungsspeicher enthält einen Dateisystem-Link.");
        return path;
    }
    public static string Hash(string path)
    {
        using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file));
    }
    private ShortcutRecord? ReadRecord(string target)
    {
        if (!ManagedTargets.IsManaged(target)) return null;
        var path = Resolve(target) + ".json";
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 65536 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Ungültiger Wiederherstellungseintrag.");
        var record = JsonSerializer.Deserialize<ShortcutRecord>(File.ReadAllText(path), StateStore.JsonOptions) ?? throw new InvalidDataException("Leerer Wiederherstellungseintrag.");
        if (ManagedTargets.Prefix + record.FileName != target || record.Sha256 is not { Length: 64 } || record.Source is null)
            throw new InvalidDataException("Ungültiger Wiederherstellungseintrag.");
        return record;
    }
    private static bool Matches(string path, string hash)
    {
        try { return File.Exists(path) && new FileInfo(path).Length <= MaxLinkBytes && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && Hash(path) == hash; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    private void WriteRecord(ShortcutRecord record)
    {
        var path = Resolve(ManagedTargets.Prefix + record.FileName) + ".json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, StateStore.JsonOptions);
        using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
        File.Move(path + ".tmp", path, true);
    }
    public string? FindBySource(string source)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            var target = ManagedTargets.Prefix + Path.GetFileNameWithoutExtension(file);
            if (!ManagedTargets.IsManaged(target)) continue;
            var record = ReadRecord(target);
            if (record is not null && string.Equals(record.Source, source, StringComparison.OrdinalIgnoreCase)
                && Matches(Resolve(target), record.Sha256)
                && (!File.Exists(source) || Matches(source, record.Sha256))) return target;
        }
        return null;
    }
    public string? OriginalSource(string target)
    {
        try { return ReadRecord(target)?.Source; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { return null; }
    }
    public void ConfirmExport(string target)
    {
        var record = ReadRecord(target); if (record is null) return;
        record.MoveRequested = false; record.Phase = "Exported"; WriteRecord(record);
    }
    public IReadOnlyList<string> RestoreAll()
    {
        var paths = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            var target = ManagedTargets.Prefix + Path.GetFileNameWithoutExtension(file);
            if (ManagedTargets.IsManaged(target) && Restore(target) is { } path) paths.Add(path);
        }
        return paths;
    }
    public PreparedShortcut Prepare(string source, bool move)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (!Path.IsPathFullyQualified(source) || extension is not (".lnk" or ".url")) return new(source, false);
        // Shared desktop and other locations may be copied; removal is personal-desktop only.
        move &= platform.IsPersonalDesktopShortcut(source);
        var existing = FindBySource(source);
        if (existing is not null)
        {
            var previous = ReadRecord(existing)!;
            if (move) { previous.MoveRequested = true; previous.Phase = "Prepared"; WriteRecord(previous); }
            return new(existing, move);
        }
        if (!File.Exists(source)) throw new FileNotFoundException("Desktop-Verknüpfung wurde nicht gefunden.", source);
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("Diese Datei ist ein Dateisystem-Link und kann nicht sicher übernommen werden. Das Original bleibt erhalten.");
        if (new FileInfo(source).Length > MaxLinkBytes) throw new IOException("Diese Verknüpfung überschreitet 1 MB und wird nicht übernommen.");
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(source).ToLowerInvariant();
        var target = ManagedTargets.Prefix + name; var destination = Resolve(target);
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
        if (Hash(source) != Hash(destination) || !platform.ValidateRelocatedShortcut(source, destination))
        {
            File.Delete(destination);
            throw new IOException("Diese Verknüpfung lässt sich nicht sicher umziehen. Das Original bleibt erhalten. Bitte die Anwendung direkt über „Installierte App“ oder ihre Programmdatei hinzufügen.");
        }
        WriteRecord(new ShortcutRecord { FileName = name, Source = source, Sha256 = Hash(destination), MoveRequested = move });
        return new(target, move);
    }
    public void Complete(PreparedShortcut prepared)
    {
        var record = ReadRecord(prepared.Target); if (record is null) return;
        if (prepared.RemoveOriginal && File.Exists(record.Source))
        {
            if (!platform.IsPersonalDesktopShortcut(record.Source)) throw new IOException("Nur eigene Desktop-Verknüpfungen können übernommen werden.");
            Verify(prepared.Target, record);
            // The Windows adapter locks the source against writers and marks this exact handle for deletion.
            platform.DeleteOriginalIfUnchanged(record.Source, record.Sha256);
        }
        record.Phase = prepared.RemoveOriginal ? "Owned" : "Copied"; WriteRecord(record);
    }
    private void Verify(string target, ShortcutRecord record)
    {
        if (!Matches(Resolve(target), record.Sha256)) throw new IOException("Die gesicherte Verknüpfung fehlt oder wurde verändert. Das Desktop-Original bleibt erhalten.");
    }
    public bool OwnsDesktopOriginal(string target)
    {
        try
        {
            var record = ReadRecord(target);
            return record is { MoveRequested: true } && platform.IsPersonalDesktopShortcut(record.Source);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { return false; }
    }
    public string? Restore(string target)
    {
        var record = ReadRecord(target);
        if (record is null || !record.MoveRequested || !platform.IsPersonalDesktopShortcut(record.Source)) return null;
        Verify(target, record);
        Directory.CreateDirectory(platform.PersonalDesktop);
        var destination = Path.Combine(platform.PersonalDesktop, Path.GetFileName(record.Source));
        if (record.RestoredPath is { } restored && platform.IsPersonalDesktopShortcut(restored) && Matches(restored, record.Sha256)) return restored;
        // Never overwrite a desktop file, including a different shortcut with the same name.
        for (int i = 0; ; i++)
        {
            string stem = Path.GetFileNameWithoutExtension(destination);
            string candidate = i == 0 ? destination : Path.Combine(platform.PersonalDesktop, stem[..Math.Min(stem.Length, 200)] + $" (Bubble {i})" + Path.GetExtension(destination));
            if (Matches(candidate, record.Sha256)) { destination = candidate; break; }
            if (File.Exists(candidate) || Directory.Exists(candidate)) continue;
            string temp = Path.Combine(platform.PersonalDesktop, ".DesktopBubbles-restore-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var input = File.OpenRead(Resolve(target)))
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
                if (Hash(temp) != record.Sha256) throw new IOException("Wiederherstellung konnte nicht geprüft werden.");
                try { File.Move(temp, candidate, false); destination = candidate; break; }
                catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate)) { }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        record.RestoredPath = destination; record.Phase = "Restored"; WriteRecord(record);
        return destination;
    }
    public IReadOnlyList<string> Recover(ISet<string> referenced)
    {
        var notices = new List<string>();
        foreach (var target in referenced.Where(ManagedTargets.IsManaged))
        {
            try
            {
                var record = ReadRecord(target);
                if (record is null) { notices.Add("Wiederherstellungsdaten fehlen: " + target); continue; }
                Verify(target, record);
                if (record.Phase == "Prepared")
                {
                    // A crash may happen on either side of the commit. Recovery never deletes desktop files.
                    record.Phase = File.Exists(record.Source) ? "Copied" : "Owned"; WriteRecord(record);
                    notices.Add("Unterbrochene Übernahme wiederhergestellt. Vorhandene Desktop-Originale wurden beibehalten.");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            { notices.Add("Gespeicherte Verknüpfung prüfen: " + target + " · " + ex.Message); }
        }
        return notices;
    }
    public void ExportPayload(string target, Stream output)
    {
        var record = ReadRecord(target) ?? throw new IOException("Verknüpfung fehlt."); Verify(target, record);
        using var file = File.OpenRead(Resolve(target)); file.CopyTo(output);
    }
    public void ImportPayload(string target, byte[] bytes)
    {
        if (!ManagedTargets.TryFileName(target, out var name) || bytes.Length > MaxLinkBytes) throw new InvalidDataException("Ungültige Verknüpfung im Archiv.");
        var path = Resolve(target); string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (File.Exists(path))
        {
            if (Hash(path) != hash) throw new IOException("Konflikt mit einer vorhandenen gespeicherten Verknüpfung.");
            if (ReadRecord(target) is not null) return;
        }
        else
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(bytes); file.Flush(true);
        }
        // Imported bundles cannot claim ownership of local desktop files.
        WriteRecord(new ShortcutRecord { FileName = name, Sha256 = hash, Phase = "Imported" });
    }
}

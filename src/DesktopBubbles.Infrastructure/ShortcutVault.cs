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
    // Where the icon sat on the desktop; it returns there while the app is not running.
    public int? DesktopX { get; set; }
    public int? DesktopY { get; set; }
    // Phase "Parked": laid back onto the desktop at exit, taken over again at the next start.
    public string? ParkedPath { get; set; }
    public int? ParkedX { get; set; }
    public int? ParkedY { get; set; }
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
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException(Loc.T("Der Verknüpfungsspeicher darf kein Dateisystem-Link sein.", "The shortcut storage must not be a file system link."));
    }
    public string Resolve(string target)
    {
        if (!target.StartsWith(ManagedTargets.Prefix, StringComparison.Ordinal)) return target;
        if (!ManagedTargets.TryFileName(target, out var name)) throw new InvalidDataException(Loc.T("Ungültige gespeicherte Verknüpfung.", "Invalid saved shortcut."));
        var path = Path.Combine(root, name);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(Loc.T("Verknüpfungsspeicher enthält einen Dateisystem-Link.", "The shortcut storage contains a file system link."));
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
        if (new FileInfo(path).Length > 65536 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException(Loc.T("Ungültiger Wiederherstellungseintrag.", "Invalid recovery entry."));
        var record = JsonSerializer.Deserialize<ShortcutRecord>(File.ReadAllText(path), StateStore.JsonOptions) ?? throw new InvalidDataException(Loc.T("Leerer Wiederherstellungseintrag.", "Empty recovery entry."));
        if (ManagedTargets.Prefix + record.FileName != target || record.Sha256 is not { Length: 64 } || record.Source is null)
            throw new InvalidDataException(Loc.T("Ungültiger Wiederherstellungseintrag.", "Invalid recovery entry."));
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
            // A spot remembered earlier wins over wherever an installer just dropped the file.
            if (move) { previous.MoveRequested = true; previous.Phase = "Prepared"; if (previous.DesktopX is null) RememberPosition(previous, source); WriteRecord(previous); }
            return new(existing, move);
        }
        if (!File.Exists(source)) throw new FileNotFoundException(Loc.T("Desktop-Verknüpfung wurde nicht gefunden.", "Desktop shortcut not found."), source);
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException(Loc.T("Diese Datei ist ein Dateisystem-Link und kann nicht sicher übernommen werden. Das Original bleibt erhalten.", "This file is a file system link and cannot be adopted safely. The original stays."));
        if (new FileInfo(source).Length > MaxLinkBytes) throw new IOException(Loc.T("Diese Verknüpfung überschreitet 1 MB und wird nicht übernommen.", "This shortcut exceeds 1 MB and is not adopted."));
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(source).ToLowerInvariant();
        var target = ManagedTargets.Prefix + name; var destination = Resolve(target);
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
        if (Hash(source) != Hash(destination) || !platform.ValidateRelocatedShortcut(source, destination))
        {
            File.Delete(destination);
            throw new IOException(Loc.T("Diese Verknüpfung lässt sich nicht sicher umziehen. Das Original bleibt erhalten. Bitte die Anwendung direkt über „Installierte App“ oder ihre Programmdatei hinzufügen.", "This shortcut cannot be moved safely. The original stays. Please add the application directly via “Installed app” or its program file."));
        }
        var record = new ShortcutRecord { FileName = name, Source = source, Sha256 = Hash(destination), MoveRequested = move };
        if (move) RememberPosition(record, source);
        WriteRecord(record);
        return new(target, move);
    }
    private void RememberPosition(ShortcutRecord record, string path)
    {
        if (platform.DesktopIconPosition(path) is { } position) (record.DesktopX, record.DesktopY) = position;
    }
    public void Complete(PreparedShortcut prepared)
    {
        var record = ReadRecord(prepared.Target); if (record is null) return;
        if (prepared.RemoveOriginal && File.Exists(record.Source))
        {
            if (!platform.IsPersonalDesktopShortcut(record.Source)) throw new IOException(Loc.T("Nur eigene Desktop-Verknüpfungen können übernommen werden.", "Only your own desktop shortcuts can be adopted."));
            Verify(prepared.Target, record);
            // The Windows adapter locks the source against writers and marks this exact handle for deletion.
            platform.DeleteOriginalIfUnchanged(record.Source, record.Sha256);
        }
        record.Phase = prepared.RemoveOriginal ? "Owned" : "Copied"; WriteRecord(record);
    }
    private void Verify(string target, ShortcutRecord record)
    {
        if (!Matches(Resolve(target), record.Sha256)) throw new IOException(Loc.T("Die gesicherte Verknüpfung fehlt oder wurde verändert. Das Desktop-Original bleibt erhalten.", "The saved shortcut is missing or was changed. The desktop original stays."));
    }
    public bool OwnsDesktopOriginal(string target)
    {
        try
        {
            return ReadRecord(target) is { MoveRequested: true };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { return false; }
    }
    // MoveRequested is only ever set for a link taken from the personal desktop of its time
    // (Prepare checks it; imports never set it). Returns always go to today's personal desktop,
    // also after the desktop folder was moved (e.g. C:\Users\…\Desktop → D:\Desktop). Only new
    // files are written there; nothing at the former location is touched.
    public string? Restore(string target)
    {
        var record = ReadRecord(target);
        if (record is null || !record.MoveRequested) return null;
        Verify(target, record);
        if (record.RestoredPath is { } restored && platform.IsPersonalDesktopShortcut(restored) && Matches(restored, record.Sha256)) return restored;
        // A parked copy already lies on the desktop; it simply stays there.
        var destination = record.Phase == "Parked" && record.ParkedPath is { } parked && platform.IsPersonalDesktopShortcut(parked) && Matches(parked, record.Sha256)
            ? parked : PlaceOnDesktop(target, record);
        record.RestoredPath = destination; record.Phase = "Restored"; record.ParkedPath = null; WriteRecord(record);
        return destination;
    }
    public ParkedShortcut? Park(string target)
    {
        var record = ReadRecord(target);
        if (record is null || !record.MoveRequested) return null;
        if (record.Phase == "Parked" && record.ParkedPath is { } parked && platform.IsPersonalDesktopShortcut(parked) && Matches(parked, record.Sha256))
            return new(target, parked, record.DesktopX, record.DesktopY);
        if (record.Phase != "Owned") return null;
        Verify(target, record);
        var path = PlaceOnDesktop(target, record);
        record.Phase = "Parked"; record.ParkedPath = path; record.ParkedX = record.ParkedY = null; WriteRecord(record);
        return new(target, path, record.DesktopX, record.DesktopY);
    }
    public void Supersede(string old, string replacement)
    {
        var previous = ReadRecord(old); var next = ReadRecord(replacement);
        if (previous is null) return;
        if (next is not null && previous.DesktopX is not null) { (next.DesktopX, next.DesktopY) = (previous.DesktopX, previous.DesktopY); WriteRecord(next); }
        // Kept for old backups, but never laid onto the desktop again.
        previous.MoveRequested = false; previous.Phase = "Superseded"; WriteRecord(previous);
    }
    public bool PlacedByRequest(string path)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            var target = ManagedTargets.Prefix + Path.GetFileNameWithoutExtension(file);
            if (!ManagedTargets.IsManaged(target)) continue;
            try
            {
                var record = ReadRecord(target);
                // Laid back on request, or deliberately kept beside a copy-mode bubble entry.
                if (record is { Phase: "Restored", RestoredPath: { } restored } && string.Equals(restored, path, StringComparison.OrdinalIgnoreCase)) return true;
                if (record is { Phase: "Copied" } && string.Equals(record.Source, path, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { }
        }
        return false;
    }
    public void ConfirmPlacement(string target, int x, int y)
    {
        var record = ReadRecord(target); if (record is not { Phase: "Parked" }) return;
        record.ParkedX = x; record.ParkedY = y; WriteRecord(record);
    }
    public string? Unpark(string target)
    {
        var record = ReadRecord(target);
        if (record is not { Phase: "Parked" }) return null;
        var path = record.ParkedPath;
        if (path is null || !platform.IsPersonalDesktopShortcut(path) || !File.Exists(path))
        {
            // Deleted from the desktop meanwhile: the bubble keeps its verified copy.
            record.Phase = "Owned"; record.ParkedPath = null; WriteRecord(record);
            return Loc.T("Geparkte Desktop-Verknüpfung fehlt; die Bubble verwendet ihre Kopie: ", "Parked desktop shortcut missing; the bubble uses its copy: ") + Path.GetFileName(record.Source);
        }
        if (!Matches(path, record.Sha256))
        {
            // Edited on the desktop: never delete it. It stays there; the bubble keeps its copy.
            record.Phase = "Copied"; record.MoveRequested = false; record.ParkedPath = null; WriteRecord(record);
            return Loc.T("Desktop-Verknüpfung wurde verändert und bleibt auf dem Desktop: ", "The desktop shortcut was changed and stays on the desktop: ") + path;
        }
        // Where we placed it is not news; a different spot means the user moved it.
        if (platform.DesktopIconPosition(path) is { } now && (record.ParkedX is null ? record.DesktopX is null : now != (record.ParkedX.Value, record.ParkedY!.Value)))
            (record.DesktopX, record.DesktopY) = now;
        platform.DeleteOriginalIfUnchanged(path, record.Sha256);
        record.Phase = "Owned"; record.ParkedPath = null; record.ParkedX = record.ParkedY = null; WriteRecord(record);
        return null;
    }
    // Copies the vault file onto the personal desktop under its original name or a free variant.
    private string PlaceOnDesktop(string target, ShortcutRecord record)
    {
        Directory.CreateDirectory(platform.PersonalDesktop);
        var destination = Path.Combine(platform.PersonalDesktop, Path.GetFileName(record.Source));
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
                if (Hash(temp) != record.Sha256) throw new IOException(Loc.T("Wiederherstellung konnte nicht geprüft werden.", "Recovery could not be verified."));
                try { File.Move(temp, candidate, false); destination = candidate; break; }
                catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate)) { }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
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
                if (record is null) { notices.Add(Loc.T("Wiederherstellungsdaten fehlen: ", "Recovery data missing: ") + target); continue; }
                Verify(target, record);
                if (record.Phase == "Prepared")
                {
                    // A crash may happen on either side of the commit. Recovery never deletes desktop files.
                    record.Phase = File.Exists(record.Source) ? "Copied" : "Owned"; WriteRecord(record);
                    notices.Add(Loc.T("Unterbrochene Übernahme wiederhergestellt. Vorhandene Desktop-Originale wurden beibehalten.", "Interrupted takeover restored. Existing desktop originals were kept."));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            { notices.Add(Loc.T("Gespeicherte Verknüpfung prüfen: ", "Check saved shortcut: ") + target + " · " + ex.Message); }
        }
        return notices;
    }
    public void ExportPayload(string target, Stream output)
    {
        var record = ReadRecord(target) ?? throw new IOException(Loc.T("Verknüpfung fehlt.", "Shortcut missing.")); Verify(target, record);
        using var file = File.OpenRead(Resolve(target)); file.CopyTo(output);
    }
    public void ImportPayload(string target, byte[] bytes)
    {
        if (!ManagedTargets.TryFileName(target, out var name) || bytes.Length > MaxLinkBytes) throw new InvalidDataException(Loc.T("Ungültige Verknüpfung im Archiv.", "Invalid shortcut in the archive."));
        var path = Resolve(target); string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (File.Exists(path))
        {
            if (Hash(path) != hash) throw new IOException(Loc.T("Konflikt mit einer vorhandenen gespeicherten Verknüpfung.", "Conflict with an existing saved shortcut."));
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

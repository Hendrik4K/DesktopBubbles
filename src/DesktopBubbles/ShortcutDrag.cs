using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using DesktopBubbles.Core;

namespace DesktopBubbles;

/// <summary>The Shell receives a disposable copy, never an EXE or an internal vault payload.</summary>
internal sealed class ShortcutDrag
{
    public const string Format = "DesktopBubbles.Transfer";
    public string Token { get; } = Guid.NewGuid().ToString("N");
    public BubbleGroup Group { get; }
    public AppEntry Entry { get; }
    public string FilePath { get; }
    public string DesktopPath { get; }
    public string Hash { get; }
    public bool Internal { get; set; }
    public DataObject Data { get; }
    public ShortcutDrag(App app, BubbleGroup group, AppEntry entry, string? desktopOverride = null)
    {
        Group = group; Entry = entry;
        string source = app.Resolve(entry.Target);
        string extension = Path.GetExtension(source).ToLowerInvariant();
        bool copy = extension is ".lnk" or ".url" && File.Exists(source);
        extension = copy ? extension : Targets.IsWeb(source) ? ".url" : ".lnk";
        string name = SafeName(entry.Name);
        string desktop = desktopOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string candidate = name + extension;
        for (int i = 1; File.Exists(Path.Combine(desktop, candidate)) || Directory.Exists(Path.Combine(desktop, candidate)); i++) candidate = name + $" (Bubble {i})" + extension;
        DesktopPath = Path.Combine(desktop, candidate);
        string staging = Path.Combine(app.DataDirectory, "drag-out", Token); Directory.CreateDirectory(staging); FilePath = Path.Combine(staging, candidate);
        if (copy)
        {
            if (new FileInfo(source).Length > ShortcutVault.MaxLinkBytes) throw new IOException("Die Verknüpfung ist größer als 1 MB.");
            using (var input = File.OpenRead(source)) using (var output = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
            if (!new WindowsShortcutPlatform().ValidateRelocatedShortcut(source, FilePath) || ShortcutVault.Hash(source) != ShortcutVault.Hash(FilePath))
                throw new IOException("Diese Verknüpfung lässt sich nicht sicher herausziehen. Sie bleibt in der Bubble.");
        }
        else if (Targets.IsWeb(source)) File.WriteAllText(FilePath, "[InternetShortcut]\r\nURL=" + source + "\r\n");
        else CreateShortcut(source, FilePath);
        Hash = ShortcutVault.Hash(FilePath);
        Data = new DataObject(); Data.SetData(Format, new MemoryStream(Encoding.UTF8.GetBytes(Token)), false); Data.SetData(DataFormats.FileDrop, new[] { FilePath }, false);
        Data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes((int)DragDropEffects.Copy)), false);
    }
    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars(); string value = new string(name.Where(c => !invalid.Contains(c)).Take(80).ToArray()).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(value)) value = "Anwendung";
        string stem = value.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3]))) value = "App " + value;
        return value;
    }
    internal string WriteToDesktop()
    {
        string desktop = Path.GetDirectoryName(DesktopPath)!;
        Directory.CreateDirectory(desktop);
        string name = SafeName(Entry.Name), extension = Path.GetExtension(FilePath);
        string temporary = Path.Combine(desktop, ".DesktopBubbles-return-" + Token + ".tmp");
        bool Matches(string path) => File.Exists(path) && new FileInfo(path).Length <= ShortcutVault.MaxLinkBytes
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && ShortcutVault.Hash(path) == Hash
            && new WindowsShortcutPlatform().ValidateRelocatedShortcut(FilePath, path);
        try
        {
            using (var input = File.OpenRead(FilePath))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { input.CopyTo(output); output.Flush(true); }
            if (ShortcutVault.Hash(temporary) != Hash) throw new IOException("Die Desktop-Kopie konnte nicht geprüft werden.");
            for (int i = 0; i < 10000; i++)
            {
                string destination = Path.Combine(desktop, name + (i == 0 ? "" : $" (Bubble {i})") + extension);
                if (Matches(destination)) return destination;
                if (File.Exists(destination) || Directory.Exists(destination)) continue;
                try { File.Move(temporary, destination, false); }
                catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { continue; }
                if (!Matches(destination)) throw new IOException("Die Desktop-Verknüpfung konnte nicht geprüft werden. Der Eintrag bleibt in der Gruppe.");
                return destination;
            }
            throw new IOException("Kein freier Dateiname auf dem Desktop gefunden.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void CreateShortcut(string target, string destination)
    {
        if (!Targets.Exists(target)) throw new FileNotFoundException("Das Ziel fehlt; es wurde nichts herausgezogen.", target);
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!); shortcut = ((dynamic)shell!).CreateShortcut(destination);
            if (Targets.IsStore(target))
            {
                ((dynamic)shortcut).TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                ((dynamic)shortcut).Arguments = "\"" + target + "\"";
            }
            else { ((dynamic)shortcut).TargetPath = target; ((dynamic)shortcut).WorkingDirectory = Directory.Exists(target) ? target : Path.GetDirectoryName(target) ?? ""; }
            ((dynamic)shortcut).Save();
        }
        finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }
    private int Effect(string format)
    {
        try
        {
            object value = Data.GetData(format, false);
            if (value is int integer) return integer;
            if (value is byte[] bytes && bytes.Length >= 4) return BitConverter.ToInt32(bytes);
            if (value is MemoryStream stream && stream.Length >= 4) return BitConverter.ToInt32(stream.ToArray());
        }
        catch (Exception ex) when (ex is COMException or IOException or NotSupportedException or System.Runtime.Serialization.SerializationException) { Log.Write(ex.Message); }
        return 0;
    }
    internal bool DesktopConfirmed()
    {
        try { return File.Exists(DesktopPath) && new FileInfo(DesktopPath).Length <= ShortcutVault.MaxLinkBytes && (File.GetAttributes(DesktopPath) & FileAttributes.ReparsePoint) == 0 && ShortcutVault.Hash(DesktopPath) == Hash; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    internal void Cleanup()
    {
        // Only this operation's generated file and empty GUID directory; never recurse or touch targets.
        try { File.Delete(FilePath); Directory.Delete(Path.GetDirectoryName(FilePath)!); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write("Kept outgoing staging file: " + ex.Message); }
    }
    internal async Task<bool> Confirmed(DragDropEffects result)
    {
        if (result is DragDropEffects.None || Internal) return false;
        const int accepted = (int)(DragDropEffects.Copy | DragDropEffects.Move);
        if ((Effect("Logical Performed DropEffect") & accepted) != 0 || (Effect("Performed DropEffect") & accepted) != 0 || (Effect("Paste Succeeded") & accepted) != 0) return true;
        // Some targets do not write Shell acknowledgement formats. Confirm the personal desktop by actual bytes.
        for (int i = 0; i < 20; i++) { if (DesktopConfirmed()) return true; await Task.Delay(100); }
        return false;
    }
}

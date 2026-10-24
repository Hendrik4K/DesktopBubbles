using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace DesktopBubbles;

internal sealed class WindowsShortcutPlatform(string? desktopOverride = null) : IShortcutPlatform
{
    // Environment uses the Windows known folder, including OneDrive redirection.
    public string PersonalDesktop => desktopOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public bool IsPersonalDesktopShortcut(string path)
    {
        if (!Path.IsPathFullyQualified(path) || string.IsNullOrEmpty(PersonalDesktop)) return false;
        return string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(PersonalDesktop).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase));
    }
    public bool ValidateRelocatedShortcut(string source, string copy)
    {
        if (Path.GetExtension(source).Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            string? url = File.ReadLines(copy).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
            return url is not null && Uri.TryCreate(url[4..], UriKind.Absolute, out _);
        }
        object? shell = null, original = null, relocated = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            original = ((dynamic)shell!).CreateShortcut(source); relocated = ((dynamic)shell).CreateShortcut(copy);
            string target = ((dynamic)original).TargetPath;
            if (string.IsNullOrWhiteSpace(target) || !Path.IsPathFullyQualified(target)) return false;
            // Resolve relative links in both locations; preserve every byte of the actual .lnk.
            return string.Equals(target, (string)((dynamic)relocated).TargetPath, StringComparison.OrdinalIgnoreCase)
                && (string)((dynamic)original).Arguments == (string)((dynamic)relocated).Arguments
                && (string)((dynamic)original).WorkingDirectory == (string)((dynamic)relocated).WorkingDirectory;
        }
        finally
        {
            if (relocated is not null) Marshal.FinalReleaseComObject(relocated);
            if (original is not null) Marshal.FinalReleaseComObject(original);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
    public void DeleteOriginalIfUnchanged(string path, string sha256)
    {
        if (!IsPersonalDesktopShortcut(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Diese Datei wird nicht vom Desktop entfernt.");
        // DELETE + GENERIC_READ; sharing only readers prevents a hash/delete race with writers.
        using var handle = CreateFile(path, 0x80010000, 1, 0, 3, 0x00200080, 0); // OPEN_REPARSE_POINT
        if (handle.IsInvalid) throw new IOException("Desktop-Original ist gesperrt oder schreibgeschützt.", new Win32Exception(Marshal.GetLastWin32Error()));
        using var file = new FileStream(handle, FileAccess.Read);
        if (file.Length > ShortcutVault.MaxLinkBytes) throw new IOException("Das Desktop-Original wurde inzwischen verändert.");
        if (Convert.ToHexString(SHA256.HashData(file)) != sha256) throw new IOException("Das Desktop-Original wurde inzwischen verändert.");
        var info = new Disposition { DeleteFile = 1 };
        if (!SetFileInformationByHandle(handle, 4, ref info, (uint)Marshal.SizeOf<Disposition>()))
            throw new IOException("Desktop-Original konnte nicht entfernt werden.", new Win32Exception(Marshal.GetLastWin32Error()));
    }
    [StructLayout(LayoutKind.Sequential)] private struct Disposition { public byte DeleteFile; }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, ref Disposition information, uint size);
}

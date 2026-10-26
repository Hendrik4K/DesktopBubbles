using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopBubbles.Core;
using Microsoft.Win32;

namespace DesktopBubbles;

internal static class Log
{
    public static string DirectoryPath { get; set; } = AppContext.BaseDirectory;
    public static void Write(string message)
    {
        try
        {
            var path = Path.Combine(DirectoryPath, "DesktopBubbles.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

internal static class LaunchService
{
    public static void Launch(AppEntry entry)
    {
        if (!Targets.IsAllowed(entry.Target)) throw new InvalidOperationException("Ungültiges Ziel.");
        if (!Targets.Exists(entry.Target)) throw new FileNotFoundException("Das Ziel wurde verschoben oder gelöscht. Bitte den Eintrag neu hinzufügen.", entry.Target);
        if (Targets.IsStore(entry.Target))
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { entry.Target } });
        else Process.Start(new ProcessStartInfo(entry.Target) { UseShellExecute = true });
    }

    public static string Describe(AppEntry entry)
    {
        if (!Targets.Exists(entry.Target)) return "Ziel fehlt: " + entry.Target;
        if (!entry.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return entry.Target;
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(entry.Target);
            string target = ((dynamic)shortcut).TargetPath;
            string args = ((dynamic)shortcut).Arguments;
            return string.IsNullOrWhiteSpace(target) ? entry.Target : target + (args.Length > 0 ? " " + args : "")
                + (!File.Exists(target) && !Directory.Exists(target) && Path.IsPathFullyQualified(target) ? "\nVerknüpfungsziel fehlt möglicherweise." : "");
        }
        catch (COMException) { return entry.Target; }
        finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }

    internal static string? FriendlyName(string original)
    {
        string target = original, description = "";
        object? shell = null, shortcut = null;
        try
        {
            if (original.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                shortcut = ((dynamic)shell!).CreateShortcut(original);
                target = ((dynamic)shortcut).TargetPath;
                description = ((dynamic)shortcut).Description;
            }
            if (File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var info = FileVersionInfo.GetVersionInfo(target);
                if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
                if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                description = description.Trim();
                if (description.StartsWith("Den ", StringComparison.OrdinalIgnoreCase) && description.EndsWith(" starten", StringComparison.OrdinalIgnoreCase))
                    description = description[4..^8];
                return description;
            }
            var stem = Path.GetFileNameWithoutExtension(target);
            return string.IsNullOrWhiteSpace(stem) ? null : stem;
        }
        finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }

    internal static void ShowDesktop()
    {
        object? shell = null;
        try { shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!); ((dynamic)shell!).MinimizeAll(); }
        finally { if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }
}

internal sealed class ShellIcons : IDisposable
{
    private readonly BlockingCollection<Action> queue = new();
    private readonly Dictionary<string, BitmapSource?> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> order = new();
    private readonly Thread worker;
    private bool disposed;
    public int CachedCount { get { lock (cache) return cache.Count; } }
    public ShellIcons()
    {
        worker = new Thread(() => { foreach (var task in queue.GetConsumingEnumerable()) task(); }) { IsBackground = true, Name = "Shell icon STA" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }
    public Task<BitmapSource?> Get(string target)
    {
        lock (cache) if (cache.TryGetValue(target, out var found)) return Task.FromResult(found);
        if (disposed) return Task.FromResult<BitmapSource?>(null);
        var completion = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(() =>
        {
            BitmapSource? result = null;
            try { result = Extract(target); }
            catch (Exception ex)
            { Log.Write("Icon: " + ex.Message); }
            lock (cache)
            {
                if (!cache.ContainsKey(target)) order.Enqueue(target);
                cache[target] = result;
                while (order.Count > 384) cache.Remove(order.Dequeue());
            }
            completion.SetResult(result);
        });
        return completion.Task;
    }

    public Task<string?> FriendlyName(string target)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (disposed) { completion.SetResult(null); return completion.Task; }
        queue.Add(() =>
        {
            try { completion.SetResult(LaunchService.FriendlyName(target)); }
            catch (Exception ex) { Log.Write("Name: " + ex.Message); completion.SetResult(null); }
        });
        return completion.Task;
    }

    private static BitmapSource? Extract(string target)
    {
        if (File.Exists(target) && new[] { ".png", ".jpg", ".jpeg", ".ico", ".bmp" }.Contains(Path.GetExtension(target).ToLowerInvariant()))
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 128; bitmap.UriSource = new Uri(target); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        object? item = null;
        nint handle = 0;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(Targets.IsWeb(target) ? ".url" : target, 0, ref iid, out item) == 0)
            {
                var factory = (IShellItemImageFactory)item!;
                if (factory.GetImage(new Size { Cx = 128, Cy = 128 }, 0x4, out handle) == 0 && handle != 0)
                {
                    var image = Imaging.CreateBitmapSourceFromHBitmap(handle, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze(); return image;
                }
            }
        }
        finally { if (handle != 0) Native.DeleteObject(handle); if (item is not null) Marshal.FinalReleaseComObject(item); }
        var info = new SHFILEINFO();
        var value = SHGetFileInfo(Targets.IsWeb(target) ? ".url" : target, 0x80, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 | 0x0 | 0x10);
        if (value == 0 || info.Icon == 0) return null;
        try
        {
            var icon = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            icon.Freeze(); return icon;
        }
        finally { Native.DestroyIcon(info.Icon); }
    }

    public Task<List<AppEntry>> StoreApps()
    {
        var tcs = new TaskCompletionSource<List<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(() =>
        {
            object? shell = null, folder = null, items = null;
            var result = new List<AppEntry>();
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
                folder = ((dynamic)shell!).Namespace("shell:AppsFolder");
                items = ((dynamic)folder!).Items();
                int count = ((dynamic)items).Count;
                for (int i = 0; i < count; i++)
                {
                    object? item = null;
                    try
                    {
                        item = ((dynamic)items).Item(i);
                        string name = ((dynamic)item!).Name;
                        string path = ((dynamic)item).Path;
                        if (string.IsNullOrWhiteSpace(path)) continue;
                        var target = Path.IsPathFullyQualified(path) || Targets.IsStore(path) ? path : Targets.AppsPrefix + path;
                        if (Targets.IsAllowed(target)) result.Add(Targets.Create(target, name));
                    }
                    finally { if (item is not null) Marshal.FinalReleaseComObject(item); }
                }
                tcs.SetResult(result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
            }
            catch (Exception ex) { tcs.SetException(ex); }
            finally
            {
                if (items is not null) Marshal.FinalReleaseComObject(items);
                if (folder is not null) Marshal.FinalReleaseComObject(folder);
                if (shell is not null) Marshal.FinalReleaseComObject(shell);
            }
        });
        return tcs.Task;
    }
    public void Dispose() { disposed = true; queue.CompleteAdding(); }
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Cx, Cy; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [PreserveSig] int GetImage(Size size, uint flags, out nint bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object? item);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SHFILEINFO
    { public nint Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
}

internal static class StartupService
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string? Read() { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("DesktopBubbles") as string; }
    public static bool HasRegistration => Read() is not null;
    public static bool EnabledFor(bool portable, string dataDirectory) => StartupRegistration.Matches(Read(), Environment.ProcessPath!, portable, dataDirectory);
    public static void Set(bool enabled, bool portable, string dataDirectory)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("DesktopBubbles", StartupRegistration.Command(Environment.ProcessPath!, portable, dataDirectory));
        else key.DeleteValue("DesktopBubbles", false);
    }
}

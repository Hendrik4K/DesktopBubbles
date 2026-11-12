using System.Runtime.InteropServices;
using System.Text;

namespace DesktopBubbles;

// Reads and sets icon positions in Explorer's desktop view through the documented shell
// interfaces: ShellWindows finds the desktop browser, its active view is an IFolderView.
// Every call is best effort; without a desktop view (Explorer restarting, session ending)
// positions are simply unknown and Explorer places new icons itself, starting top left.
internal static class DesktopIcons
{
    internal readonly record struct Item(string Path, int X, int Y);

    internal static (int X, int Y)? Position(string path)
        => Items()?.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)) is { Path: not null } item ? (item.X, item.Y) : null;

    internal static List<Item>? Items()
    {
        try
        {
            using var view = DesktopView.Open(); if (view is null) return null;
            var items = new List<Item>();
            view.Folder.ItemCount(SvgioAllView, out int count);
            for (int i = 0; i < count; i++)
            {
                if (view.Folder.Item(i, out nint pidl) != 0 || pidl == 0) continue;
                try
                {
                    if (view.Name(pidl) is { } name && view.Folder.GetItemPosition(pidl, out var point) == 0) items.Add(new(name, point.X, point.Y));
                }
                finally { Marshal.FreeCoTaskMem(pidl); }
            }
            return items;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException)
        { Log.Write("Desktop icon positions unavailable: " + ex.Message); return null; }
    }

    // Moves each icon to its spot unless another icon already sits there; Explorer keeps
    // its own placement (from the top left) for those. Placed: moved. Settled: placed or
    // deliberately left to Explorer. Icons not yet visible in the view are neither.
    internal static (HashSet<string> Placed, HashSet<string> Settled) Place(IReadOnlyCollection<(string Path, int X, int Y)> wanted)
    {
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var settled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var view = DesktopView.Open(); if (view is null) return (placed, settled);
            if (view.Folder.GetAutoArrange() == 0) { settled.UnionWith(wanted.Select(w => w.Path)); return (placed, settled); } // Explorer arranges everything itself.
            view.Folder.GetSpacing(out var spacing);
            var others = new List<(string Path, nint Pidl, Native.Point Point)>();
            view.Folder.ItemCount(SvgioAllView, out int count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (view.Folder.Item(i, out nint pidl) != 0 || pidl == 0) continue;
                    if (view.Name(pidl) is { } name && view.Folder.GetItemPosition(pidl, out var point) == 0) others.Add((name, pidl, point));
                    else Marshal.FreeCoTaskMem(pidl);
                }
                foreach (var (path, x, y) in wanted)
                {
                    int index = others.FindIndex(o => string.Equals(o.Path, path, StringComparison.OrdinalIgnoreCase));
                    if (index < 0) continue;
                    bool occupied = others.Any(o => !string.Equals(o.Path, path, StringComparison.OrdinalIgnoreCase)
                        && Math.Abs(o.Point.X - x) < Math.Max(8, spacing.X / 2) && Math.Abs(o.Point.Y - y) < Math.Max(8, spacing.Y / 2));
                    settled.Add(path);
                    if (occupied) continue;
                    var point = new Native.Point { X = x, Y = y };
                    if (view.Folder.SelectAndPositionItems(1, [others[index].Pidl], [point], SvsiPositionItem) == 0)
                    {
                        placed.Add(path); others[index] = (path, others[index].Pidl, point);
                    }
                }
            }
            finally { foreach (var other in others) Marshal.FreeCoTaskMem(other.Pidl); }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException)
        { Log.Write("Desktop icons could not be positioned: " + ex.Message); }
        return (placed, settled);
    }

    private const uint SvgioAllView = 2, SvsiPositionItem = 0x80, ShgdnForParsing = 0x8000;

    private sealed class DesktopView : IDisposable
    {
        public IFolderView Folder { get; }
        private readonly IShellFolder shellFolder;
        private DesktopView(IFolderView folder, IShellFolder shellFolder) { Folder = folder; this.shellFolder = shellFolder; }

        public static DesktopView? Open()
        {
            var windows = (IShellWindows)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"))!)!;
            object location = 0, root = null!; // CSIDL_DESKTOP, VT_EMPTY
            var dispatch = windows.FindWindowSW(ref location, ref root, 8, out _, 1); // SWC_DESKTOP, SWFO_NEEDDISPATCH
            if (dispatch is not IServiceProvider provider) return null;
            var topLevel = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"); var browserId = new Guid("000214E2-0000-0000-C000-000000000046");
            if (provider.QueryService(ref topLevel, ref browserId, out var browserObject) != 0 || browserObject is not IShellBrowser browser) return null;
            if (browser.QueryActiveShellView(out var view) != 0 || view is not IFolderView folder) return null;
            var folderId = new Guid("000214E6-0000-0000-C000-000000000046");
            if (folder.GetFolder(ref folderId, out var shellFolderObject) != 0 || shellFolderObject is not IShellFolder shellFolder) return null;
            return new DesktopView(folder, shellFolder);
        }

        public string? Name(nint pidl)
        {
            var buffer = Marshal.AllocCoTaskMem(1024);
            try
            {
                if (shellFolder.GetDisplayNameOf(pidl, ShgdnForParsing, buffer) != 0) return null;
                var text = new StringBuilder(1024);
                return StrRetToBuf(buffer, pidl, text, text.Capacity) == 0 ? text.ToString() : null;
            }
            finally { Marshal.FreeCoTaskMem(buffer); }
        }

        public void Dispose()
        {
            Marshal.FinalReleaseComObject(shellFolder);
            Marshal.FinalReleaseComObject(Folder);
        }
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] private static extern int StrRetToBuf(nint strret, nint pidl, StringBuilder buffer, int size);

    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IShellWindows
    {
        int Count { get; }
        [return: MarshalAs(UnmanagedType.IDispatch)] object Item(object index);
        [return: MarshalAs(UnmanagedType.IUnknown)] object _NewEnum();
        void Register([MarshalAs(UnmanagedType.IDispatch)] object dispatch, int hwnd, int windowClass, out int cookie);
        void RegisterPending(int threadId, ref object location, ref object root, int windowClass, out int cookie);
        void Revoke(int cookie);
        void OnNavigate(int cookie, ref object location);
        void OnActivated(int cookie, bool active);
        [return: MarshalAs(UnmanagedType.IDispatch)] object FindWindowSW(ref object location, ref object root, int windowClass, out int hwnd, int options);
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid service, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow(out nint hwnd);
        void ContextSensitiveHelp(bool enter);
        void InsertMenusSB(nint menu, nint widths);
        void SetMenuSB(nint menu, nint holder, nint activeObject);
        void RemoveMenusSB(nint menu);
        void SetStatusTextSB(nint text);
        void EnableModelessSB(bool enable);
        void TranslateAcceleratorSB(nint message, ushort id);
        void BrowseObject(nint pidl, uint flags);
        void GetViewStateStream(uint mode, out nint stream);
        void GetControlWindow(uint id, out nint hwnd);
        void SendControlMsg(uint id, uint message, nint wParam, nint lParam, out nint result);
        [PreserveSig] int QueryActiveShellView([MarshalAs(UnmanagedType.IUnknown)] out object view);
    }

    [ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFolderView
    {
        [PreserveSig] int GetCurrentViewMode(out uint mode);
        [PreserveSig] int SetCurrentViewMode(uint mode);
        [PreserveSig] int GetFolder(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object folder);
        [PreserveSig] int Item(int index, out nint pidl);
        [PreserveSig] int ItemCount(uint flags, out int count);
        [PreserveSig] int Items(uint flags, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object items);
        [PreserveSig] int GetSelectionMarkedItem(out int item);
        [PreserveSig] int GetFocusedItem(out int item);
        [PreserveSig] int GetItemPosition(nint pidl, out Native.Point point);
        [PreserveSig] int GetSpacing(out Native.Point point);
        [PreserveSig] int GetDefaultSpacing(out Native.Point point);
        [PreserveSig] int GetAutoArrange();
        [PreserveSig] int SelectItem(int item, uint flags);
        [PreserveSig] int SelectAndPositionItems(uint count, [MarshalAs(UnmanagedType.LPArray)] nint[] pidls, [MarshalAs(UnmanagedType.LPArray)] Native.Point[] points, uint flags);
    }

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(nint hwnd, nint bindContext, [MarshalAs(UnmanagedType.LPWStr)] string name, out uint eaten, out nint pidl, ref uint attributes);
        [PreserveSig] int EnumObjects(nint hwnd, uint flags, out nint enumerator);
        [PreserveSig] int BindToObject(nint pidl, nint bindContext, ref Guid riid, out nint result);
        [PreserveSig] int BindToStorage(nint pidl, nint bindContext, ref Guid riid, out nint result);
        [PreserveSig] int CompareIDs(nint lParam, nint first, nint second);
        [PreserveSig] int CreateViewObject(nint owner, ref Guid riid, out nint result);
        [PreserveSig] int GetAttributesOf(uint count, ref nint pidls, ref uint attributes);
        [PreserveSig] int GetUIObjectOf(nint owner, uint count, ref nint pidls, ref Guid riid, nint reserved, out nint result);
        [PreserveSig] int GetDisplayNameOf(nint pidl, uint flags, nint name);
    }
}

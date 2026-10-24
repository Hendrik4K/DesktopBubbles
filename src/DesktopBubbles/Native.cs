using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal static class Native
{
    internal const int GwlStyle = -16, GwlExStyle = -20;
    internal const long WsChild = 0x40000000, WsPopup = 0x80000000, WsExToolWindow = 0x80, WsExAppWindow = 0x40000, WsExNoActivate = 0x08000000;
    internal const uint NoSize = 1, NoMove = 2, NoActivate = 0x10;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public readonly PixelRect Pixels => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential)] internal struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);
    internal delegate bool EnumProc(nint hwnd, nint data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, nint data);
    [DllImport("user32.dll")] internal static extern bool EnumChildWindows(nint hwnd, EnumProc callback, nint data);
    [DllImport("user32.dll")] internal static extern nint GetShellWindow();
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint hwnd, ref Point point);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] internal static extern nint GetWindowDpiAwarenessContext(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] internal static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] internal static extern int GetAwarenessFromDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(nint a, nint b);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] internal static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("user32.dll")] internal static extern int GetWindowRgn(nint hwnd, nint region);
    [DllImport("gdi32.dll")] internal static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder buffer, int max);
    [DllImport("user32.dll")] internal static extern bool SendNotifyMessage(nint hwnd, uint msg, nint wp, nint lp);

    internal static nint Handle(Window window) => new WindowInteropHelper(window).EnsureHandle();
    internal static void ToolWindow(Window window, bool noActivate = false)
    {
        var h = Handle(window);
        long ex = (long)GetWindowLongPtr(h, GwlExStyle);
        SetWindowLongPtr(h, GwlExStyle, (nint)((ex | WsExToolWindow | (noActivate ? WsExNoActivate : 0)) & ~WsExAppWindow));
    }
    internal static PixelRect Bounds(Window window)
    {
        GetWindowRect(Handle(window), out var r);
        return r.Pixels;
    }
    internal static void Position(Window window, double x, double y)
    {
        var h = Handle(window);
        var p = new Point { X = (int)Math.Round(x), Y = (int)Math.Round(y) };
        if (((long)GetWindowLongPtr(h, GwlStyle) & WsChild) != 0) ScreenToClient(GetParent(h), ref p);
        SetWindowPos(h, 0, p.X, p.Y, 0, 0, NoSize | NoActivate | 4);
    }
    internal static PixelRect WorkArea(double x, double y)
        => System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)x, (int)y)).WorkingArea is var a
            ? new(a.X, a.Y, a.Width, a.Height) : default;
    internal static double MonitorDpi(double x, double y)
    {
        var monitor = MonitorFromPoint(new Point { X = (int)x, Y = (int)y }, 2);
        return GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi : 96;
    }

    internal static nint FindDesktopHost()
    {
        nint result = 0;
        GetWindowThreadProcessId(GetShellWindow(), out var shellPid);
        if (shellPid == 0) return 0;
        bool Check(nint view)
        {
            var parent = GetParent(view);
            var cls = new StringBuilder(128); GetClassName(parent, cls, cls.Capacity);
            GetWindowThreadProcessId(parent, out var pid);
            if (pid != shellPid || cls.ToString() is not ("Progman" or "WorkerW")) return false;
            result = parent; return true;
        }
        EnumWindows((h, _) =>
        {
            var cls = new StringBuilder(128); GetClassName(h, cls, cls.Capacity);
            if (cls.ToString() is not ("Progman" or "WorkerW")) return true;
            GetWindowThreadProcessId(h, out var hostPid);
            if (hostPid != shellPid) return true;
            var direct = FindWindowEx(h, 0, "SHELLDLL_DefView", null);
            if (direct != 0 && Check(direct)) return false;
            // Some Windows 11 builds nest the existing icon view under another WorkerW.
            // Discover only; never send Explorer the undocumented 0x052C message.
            EnumChildWindows(h, (child, _) =>
            {
                var childClass = new StringBuilder(128); GetClassName(child, childClass, childClass.Capacity);
                return childClass.ToString() != "SHELLDLL_DefView" || !Check(child);
            }, 0);
            return result == 0;
        }, 0);
        return result;
    }
}

internal sealed class DesktopHost
{
    public string Status { get; private set; } = "Noch nicht verbunden";
    public bool Attach(Window window, bool enabled)
    {
        var h = Native.Handle(window);
        Native.ToolWindow(window, true);
        // Keep WPF's transparent surface top-level. A foreign WS_CHILD can pass
        // native hit tests while being absent from Explorer's DWM composition.
        var owner = enabled ? Native.GetShellWindow() : 0;
        if (owner != 0)
        {
            Marshal.SetLastPInvokeError(0);
            Native.SetWindowLongPtr(h, -8, owner); // GWLP_HWNDPARENT changes OWNER, not parent.
            int error = Marshal.GetLastPInvokeError();
            if (Native.GetWindow(h, 4) == owner)
            {
                Native.SetWindowPos(h, (nint)1, 0, 0, 0, 0, Native.NoMove | Native.NoSize | Native.NoActivate);
                Status = "Desktop-Modus · eigenes Bubble-Fenster";
                Log.Write($"Desktop owner=0x{owner:X}; independent top-level DWM surface.");
                return true;
            }
            Log.Write($"Desktop owner assignment failed: Win32={error}");
        }
        Status = enabled ? "Fenstermodus · Desktop-Zuordnung nicht verfügbar" : "Fenstermodus · Desktop-Zuordnung ausgeschaltet";
        Native.SetWindowPos(h, (nint)1, 0, 0, 0, 0, Native.NoMove | Native.NoSize | Native.NoActivate);
        return false;
    }

}

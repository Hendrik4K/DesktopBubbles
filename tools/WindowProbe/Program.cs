using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

var bubblePids = Process.GetProcessesByName("DesktopBubbles").Select(p => p.Id).ToHashSet();
var results = new List<object>();
void Inspect(nint h)
{
    Native.GetWindowThreadProcessId(h, out var pid);
    var cls = new StringBuilder(256); Native.GetClassName(h, cls, cls.Capacity);
    bool own = bubblePids.Contains((int)pid) || cls.ToString().StartsWith("HwndWrapper[DesktopBubbles");
    if (!own && (cls.ToString() is not ("Progman" or "WorkerW" or "SHELLDLL_DefView" or "SysListView32") || !Native.IsWindowVisible(h))) return;
    Native.GetWindowRect(h, out var rect);
    var title = new StringBuilder(256); if (own) Native.GetWindowText(h, title, title.Capacity);
    Native.DwmGetWindowAttribute(h, 14, out var cloak, 4);
    var point = new Native.Point { X = (rect.L + rect.R) / 2, Y = rect.T + 38 };
    var hit = Native.WindowFromPoint(point); var hitClass = new StringBuilder(256); Native.GetClassName(hit, hitClass, hitClass.Capacity);
    var parent = Native.GetParent(h);
    var siblings = new List<string>();
    for (var sibling = Native.GetWindow(h, 0); sibling != 0 && siblings.Count < 12; sibling = Native.GetWindow(sibling, 2))
    { var name = new StringBuilder(256); Native.GetClassName(sibling, name, name.Capacity); siblings.Add($"{sibling:X}:{name}"); }
    results.Add(new { Handle = $"{h:X}", Class = cls.ToString(), Title = title.ToString(), Pid = pid, Parent = $"{parent:X}",
        Visible = Native.IsWindowVisible(h), Cloaked = cloak, Style = $"{Native.GetWindowLongPtr(h, -16):X}", ExStyle = $"{Native.GetWindowLongPtr(h, -20):X}",
        Bounds = new { rect.L, rect.T, rect.R, rect.B }, Hit = $"{hit:X}:{hitClass}", Siblings = siblings });
}
Native.EnumWindows((h, _) => { Inspect(h); Native.EnumChildWindows(h, (c, _) => { Inspect(c); return true; }, 0); return true; }, 0);
Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
static class Native
{
    public delegate bool Callback(nint h, nint d);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback c,nint d);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(nint h,Callback c,nint d);
    [DllImport("user32.dll")] public static extern nint GetParent(nint h);
    [DllImport("user32.dll")] public static extern nint GetWindow(nint h,uint cmd);
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(Point p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint h,out Rect r);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint h,int index);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint h,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(nint h,StringBuilder s,int max);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(nint h,StringBuilder s,int max);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint h,int attr,out int v,int size);
}

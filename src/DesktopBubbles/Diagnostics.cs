using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DesktopBubbles;

internal static class Diagnostics
{
    [DllImport("user32.dll")] private static extern int GetGuiResources(nint process, int flags);
    public static async Task<string> Measure(App app, int seconds = 10)
    {
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var cpu = process.TotalProcessorTime; int handles = process.HandleCount;
        var watch = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        process.Refresh(); watch.Stop();
        double single = (process.TotalProcessorTime - cpu).TotalMilliseconds / watch.Elapsed.TotalMilliseconds * 100;
        var data = new
        {
            Timestamp = DateTimeOffset.Now, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(),
            DurationSeconds = watch.Elapsed.TotalSeconds, LogicalProcessors = Environment.ProcessorCount,
            CpuPercentOneCore = single, CpuPercentAllCores = single / Environment.ProcessorCount,
            WorkingSetMiB = process.WorkingSet64 / 1048576d, PrivateMiB = process.PrivateMemorySize64 / 1048576d,
            ManagedMiB = GC.GetTotalMemory(false) / 1048576d, HandlesBefore = handles, HandlesAfter = process.HandleCount,
            GdiObjects = GetGuiResources(process.Handle, 0), UserObjects = GetGuiResources(process.Handle, 1),
            IconCache = app.Icons.CachedCount, Groups = app.State.Groups.Count,
            DesktopMode = app.Host.Status, PopupOpen = app.HasFlyout,
            Gpu = "Nicht gemessen", GamingFrametimes = "Nicht gemessen",
            Note = "Momentaufnahme dieses Prozesses. Keine Aussage über Spiele-Frametimes oder alle Hardwarekonfigurationen."
        };
        var path = Path.Combine(app.DataDirectory, "diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(data, StateStore.JsonOptions));
        return path;
    }
}

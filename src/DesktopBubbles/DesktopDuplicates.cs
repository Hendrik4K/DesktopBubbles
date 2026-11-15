using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DesktopBubbles.Core;

namespace DesktopBubbles;

// Program updates often recreate their desktop shortcut although the program already lives
// in a bubble. Such a link points at the same program (target and arguments, or the same URL)
// as a bubble entry. On the personal desktop it is taken back into the bubble through the
// verified takeover; on the shared desktop, which needs administrator rights, it is reported
// and can be removed on request. Links without a matching entry are never touched.
internal sealed class DesktopDuplicates : IDisposable
{
    private readonly App app;
    private readonly string personal, shared;
    private readonly DispatcherTimer settle = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

    public DesktopDuplicates(App app, string personalDesktop)
    {
        this.app = app; personal = personalDesktop;
        shared = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        settle.Tick += (_, _) => { settle.Stop(); Scan(); };
    }

    // Duplicates on the shared desktop, found by the last scan.
    public IReadOnlyList<(string Path, string Bubble)> Shared { get; private set; } = [];

    public void Start()
    {
        foreach (var folder in new[] { personal, shared }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
                watcher.Created += Changed; watcher.Renamed += Changed; watcher.Changed += Changed;
                watcher.EnableRaisingEvents = true; watchers.Add(watcher);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Log.Write("Desktop watch unavailable: " + ex.Message); }
        }
        Scan(); // Catch up on whatever updates did while the app was not running.
    }

    private void Changed(object sender, FileSystemEventArgs e)
    {
        if (!IsLink(e.FullPath)) return;
        // Installers write links in several steps; wait until the desktop is quiet.
        app.Dispatcher.BeginInvoke(() => { settle.Stop(); settle.Start(); });
    }

    public void Scan()
    {
        // Copy mode keeps desktop originals by choice; nothing is collected then.
        if (!app.State.Settings.CollectDesktopDuplicates || !app.State.Settings.MoveDesktopShortcuts || app.Exiting) return;
        // A drag-out just put a link on the desktop on purpose; let it settle first.
        if (app.DragQuiet) { settle.Stop(); settle.Start(); return; }
        var index = Index();
        if (index.Count == 0) return;
        bool changed = false;
        foreach (var file in Links(personal))
        {
            if (Identity(file) is not { } key || !index.TryGetValue(key, out var matches)) continue;
            try
            {
                if (app.Shortcuts.Absorb(app.State, file, matches.Select(m => m.Entry).ToList()))
                {
                    Log.Write($"Desktop duplicate taken back into bubble {matches[0].Group.Name}: {Path.GetFileName(file)}");
                    changed = true;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
            { Log.Write("Desktop duplicate kept: " + file + " · " + ex.Message); }
        }
        if (changed) app.Changed();
        var shared = new List<(string, string)>();
        if (!string.Equals(this.shared, personal, StringComparison.OrdinalIgnoreCase))
            foreach (var file in Links(this.shared))
                if (Identity(file) is { } key && index.TryGetValue(key, out var matches)) shared.Add((file, matches[0].Group.Name));
        Shared = shared;
        var fresh = shared.Where(s => reported.Add(s.Item1)).ToList();
        if (fresh.Count > 0) app.ReportSharedDuplicates(fresh.Select(f => Path.GetFileNameWithoutExtension(f.Item1)).ToList());
    }

    // Bubble entries by the program they open.
    private Dictionary<string, List<(BubbleGroup Group, AppEntry Entry)>> Index()
    {
        var index = new Dictionary<string, List<(BubbleGroup, AppEntry)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in app.State.Groups)
            foreach (var entry in group.Items)
            {
                string path;
                try { path = app.Resolve(entry.Target); } catch (Exception ex) when (ex is IOException or InvalidDataException) { continue; }
                string? key = IsLink(path) ? (File.Exists(path) ? Identity(path) : null) : Path.IsPathFullyQualified(path) && File.Exists(path) ? Path.GetFullPath(path) + "|" : null;
                if (key is null) continue;
                if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                list.Add((group, entry));
            }
        return index;
    }

    private static IEnumerable<string> Links(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        try { return Directory.EnumerateFiles(folder).Where(IsLink).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private static bool IsLink(string path)
        => Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase);

    // What a link opens: resolved target plus arguments, or the URL. Null when unclear
    // (advertised installer links, store links without a path): those are never matched.
    internal static string? Identity(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                var url = File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                return url is null ? null : "url:" + url[4..].Trim();
            }
            object? shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                link = ((dynamic)shell!).CreateShortcut(path);
                string target = ((dynamic)link).TargetPath, arguments = ((dynamic)link).Arguments;
                if (string.IsNullOrWhiteSpace(target) || !Path.IsPathFullyQualified(target)) return null;
                return Path.GetFullPath(target) + "|" + arguments.Trim();
            }
            finally
            {
                if (link is not null) Marshal.FinalReleaseComObject(link);
                if (shell is not null) Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException) { return null; }
    }

    public void Dispose()
    {
        settle.Stop();
        foreach (var watcher in watchers) watcher.Dispose();
        watchers.Clear();
    }
}

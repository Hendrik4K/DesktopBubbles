using System.Text.Json;
using DesktopBubbles.Core;

namespace DesktopBubbles.Infrastructure;

public sealed class StateStore(string path)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public string? RecoveryNotice { get; private set; }
    private bool preserveBackup;
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public BubbleState Load()
    {
        if (!File.Exists(Path)) return new();
        try { return Read(Path); }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            if (File.Exists(Path + ".bak"))
            {
                try
                {
                    var recovered = Read(Path + ".bak");
                    PreserveDamaged();
                    preserveBackup = true;
                    RecoveryNotice = "Die Konfiguration war beschädigt. Die letzte Sicherung wurde geladen.";
                    return recovered;
                }
                catch (Exception backup) when (backup is IOException or JsonException or InvalidDataException or UnauthorizedAccessException) { }
            }
            PreserveDamaged();
            RecoveryNotice = "Die Konfiguration konnte nicht gelesen werden. Eine Kopie bleibt als .damaged-Datei erhalten.";
            return new();
        }
    }

    private void PreserveDamaged() => File.Copy(Path, Path + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff"), false);

    public static BubbleState Read(string path)
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Die Konfiguration ist größer als 8 MB.");
        var state = JsonSerializer.Deserialize<BubbleState>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Leere Konfiguration.");
        Validate(state);
        return state;
    }

    public static void Validate(BubbleState state)
    {
        if (state.Version is not (1 or 2 or 3 or 4 or 5 or 6)) throw new InvalidDataException("Diese Konfigurationsversion wird nicht unterstützt.");
        if (state.Version < 3 && state.Settings is { } legacy)
        {
            // Update unchanged old presentation defaults to the selected compact glass design.
            if (Math.Abs(legacy.Opacity - .78) < .0001) legacy.Opacity = .35;
            if (legacy.Columns == 4) legacy.Columns = 3;
            if (Math.Abs(legacy.TileSize - 92) < .0001) legacy.TileSize = 116;
            if (legacy.Theme == "System") legacy.Theme = "Light";
        }
        if (state.Settings is null || state.Groups is null || state.Groups.Count > 100)
            throw new InvalidDataException("Ungültige Gruppen oder Einstellungen (maximal 100 Gruppen).");
        bool splitWindow = state.Version < 4;
        if (splitWindow)
        {
            state.Settings.WindowColor = state.Settings.BubbleColor;
            state.Settings.WindowOpacity = state.Settings.Opacity;
        }
        bool unifyAppearance = state.Version < 5;
        if (unifyAppearance)
        {
            var previousAppearance = state.Settings;
            double width = Math.Clamp(double.IsFinite(previousAppearance.TileSize) ? previousAppearance.TileSize : 116, 76, 160) * Math.Clamp(previousAppearance.Columns, 2, 8) + 48;
            previousAppearance.CornerRoundness = double.IsFinite(previousAppearance.WindowCornerRadius) ? previousAppearance.WindowCornerRadius / width * 100 : 14;
            if (previousAppearance.BubbleColor == "#7B6BFF" && previousAppearance.WindowColor != "#7B6BFF") previousAppearance.BubbleColor = previousAppearance.WindowColor;
        }
        bool globalTransparency = state.Version < 6;
        var ids = new HashSet<Guid>();
        foreach (var g in state.Groups)
        {
            if (g is null || !ids.Add(g.Id) || g.Id == Guid.Empty || g.Items is null || g.Items.Count > 300)
                throw new InvalidDataException("Ungültige oder doppelte Gruppe.");
            g.Name = CleanName(g.Name);
            if (splitWindow) { g.WindowColor = g.Color; g.WindowOpacity = g.Opacity; }
            if (unifyAppearance) g.Color ??= g.WindowColor;
            g.Color = g.Color is null ? null : ColorValue.Normalize(g.Color);
            g.WindowColor = g.Color; // Legacy alias; one shared folder color from schema 5 onwards.
            if (g.Opacity is { } opacity) g.Opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, .95) : null;
            if (g.WindowOpacity is { } windowOpacity) g.WindowOpacity = double.IsFinite(windowOpacity) ? Math.Clamp(windowOpacity, 0, .95) : null;
            if (!double.IsFinite(g.X) || !double.IsFinite(g.Y) || Math.Abs(g.X) > 1000000 || Math.Abs(g.Y) > 1000000)
                throw new InvalidDataException("Ungültige Desktop-Position.");
            if (g.CustomIcon is not null && !Targets.IsAllowed(g.CustomIcon)) throw new InvalidDataException("Ungültiges Gruppensymbol.");
            var entryIds = new HashSet<Guid>();
            foreach (var e in g.Items)
            {
                if (e is null || e.Id == Guid.Empty || !entryIds.Add(e.Id) || !Targets.IsAllowed(e.Target))
                    throw new InvalidDataException("Ungültiger Eintrag.");
                e.Name = CleanName(e.Name);
            }
        }
        var s = state.Settings;
        // A former selected-group override becomes the shared value exactly once.
        // Group order is stable; migration never depends on the UI's current selection.
        if (globalTransparency && state.Groups.FirstOrDefault() is { } first)
        {
            s.Opacity = first.Opacity ?? s.Opacity;
            s.WindowOpacity = first.WindowOpacity ?? s.WindowOpacity;
        }
        foreach (var group in state.Groups) group.Opacity = group.WindowOpacity = null;
        if (s.LinkBubbleColors) foreach (var group in state.Groups) group.Color = group.WindowColor = null;
        state.Version = 6;
        if (s.Theme is not ("System" or "Light" or "Dark")) s.Theme = "System";
        s.AppColor = ColorValue.Normalize(s.AppColor); s.BubbleColor = ColorValue.Normalize(s.BubbleColor);
        s.LightModeColor = ColorValue.TryParse(s.LightModeColor, out var light) ? light.Hex : null;
        s.DarkModeColor = ColorValue.TryParse(s.DarkModeColor, out var dark) ? dark.Hex : null;
        s.LightModeOpacity = double.IsFinite(s.LightModeOpacity) ? Math.Clamp(s.LightModeOpacity, 0, 1) : 1;
        s.DarkModeOpacity = double.IsFinite(s.DarkModeOpacity) ? Math.Clamp(s.DarkModeOpacity, 0, 1) : 1;
        s.WindowColor = s.BubbleColor;
        s.WindowOpacity = double.IsFinite(s.WindowOpacity) ? Math.Clamp(s.WindowOpacity, 0, .95) : .35;
        s.FrostedFolderOpacity = double.IsFinite(s.FrostedFolderOpacity) ? Math.Clamp(s.FrostedFolderOpacity, 0, 1) : .55;
        s.AppCornerRadius = 8;
        s.CornerRoundness = double.IsFinite(s.CornerRoundness) ? Math.Clamp(s.CornerRoundness, 0, 50) : 14;
        s.AppOpacity = double.IsFinite(s.AppOpacity) ? Math.Clamp(s.AppOpacity, .2, .95) : .65;
        s.Opacity = double.IsFinite(s.Opacity) ? Math.Clamp(s.Opacity, 0, .95) : .35;
        s.Glass = double.IsFinite(s.Glass) ? Math.Clamp(s.Glass, 0, 1) : .55;
        s.TileSize = double.IsFinite(s.TileSize) ? Math.Clamp(s.TileSize, 76, 160) : 116;
        s.MiniatureSize = double.IsFinite(s.MiniatureSize) ? Math.Clamp(s.MiniatureSize, 48, 144) : 76;
        s.Columns = Math.Clamp(s.Columns, 2, 8);
        // Compatibility aliases; real corners use the same proportion of the shorter edge.
        s.BubbleCornerRadius = AppearanceGeometry.Radius(s.CornerRoundness, s.MiniatureSize, s.MiniatureSize);
        s.WindowCornerRadius = AppearanceGeometry.Radius(s.CornerRoundness, s.Columns * s.TileSize + 48, s.Columns * s.TileSize + 48);
        s.AnimationMilliseconds = Math.Clamp(s.AnimationMilliseconds, 60, 450);
    }

    public static string CleanName(string? name)
    {
        var cleaned = new string((name ?? "").Where(c => !char.IsControl(c)).Take(80).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Unbenannt" : cleaned;
    }

    public void Save(BubbleState state)
    {
        Validate(state);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(fs, state, JsonOptions);
                if (fs.Length > 8 * 1024 * 1024) throw new InvalidDataException("Die Konfiguration überschreitet die maximalen 8 MB. Bitte weniger oder kürzere Einträge verwenden.");
                fs.Flush(true);
            }
            if (File.Exists(Path)) File.Replace(temp, Path, preserveBackup ? null : Path + ".bak", true);
            else File.Move(temp, Path);
            preserveBackup = false;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

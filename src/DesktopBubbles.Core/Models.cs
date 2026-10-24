namespace DesktopBubbles.Core;

public sealed class BubbleState
{
    public int Version { get; set; } = 6;
    public BubbleSettings Settings { get; set; } = new();
    public List<BubbleGroup> Groups { get; set; } = [];
}

public sealed class BubbleSettings
{
    public string Theme { get; set; } = "Light";
    public string AppColor { get; set; } = "#7B6BFF";
    public string? LightModeColor { get; set; }
    public string? DarkModeColor { get; set; }
    public double LightModeOpacity { get; set; } = 1;
    public double DarkModeOpacity { get; set; } = 1;
    public string BubbleColor { get; set; } = "#7B6BFF";
    public bool LinkBubbleColors { get; set; }
    public double MiniatureSize { get; set; } = 76;
    public double AppOpacity { get; set; } = .65;
    public string WindowColor { get; set; } = "#7B6BFF";
    public double WindowOpacity { get; set; } = .35;
    public double AppCornerRadius { get; set; } = 8;
    public double BubbleCornerRadius { get; set; } = 20;
    public double WindowCornerRadius { get; set; } = 40;
    public double CornerRoundness { get; set; } = 14;
    public double Opacity { get; set; } = .35;
    public double Glass { get; set; } = .55;
    public bool Blur { get; set; } = true;
    public bool Animations { get; set; } = true;
    public int AnimationMilliseconds { get; set; } = 160;
    public bool HoverOpen { get; set; } // Legacy configuration field; hover opening is no longer used.
    public int Columns { get; set; } = 3;
    public double TileSize { get; set; } = 116;
    public bool DesktopIntegration { get; set; } = true;
    public bool MoveDesktopShortcuts { get; set; } = true;
}

public sealed class BubbleGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Neue Gruppe";
    public bool ShowName { get; set; } = true;
    // Physical virtual-screen pixels. Negative coordinates are valid.
    public double X { get; set; } = 150;
    public double Y { get; set; } = 150;
    public string? Monitor { get; set; }
    public string? CustomIcon { get; set; }
    public string? Color { get; set; }
    public double? Opacity { get; set; } // Legacy override, migrated to the shared setting in schema 6.
    public string? WindowColor { get; set; }
    public double? WindowOpacity { get; set; } // Legacy override, migrated to the shared setting in schema 6.
    public List<AppEntry> Items { get; set; } = [];
}

public sealed class AppEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Anwendung";
    // External path/URI or a portable ManagedTargets reference. Never flatten .lnk arguments.
    public string Target { get; set; } = "";
}

public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

public static class Placement
{
    public static (double X, double Y) Clamp(double x, double y, double width, double height, PixelRect area)
        => (Math.Clamp(double.IsFinite(x) ? x : area.X, area.X, Math.Max(area.X, area.Right - width)),
            Math.Clamp(double.IsFinite(y) ? y : area.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));

    public static PixelRect Nearest(double x, double y, IReadOnlyList<PixelRect> monitors)
    {
        if (monitors.Count == 0) return new PixelRect(0, 0, 1920, 1080);
        return monitors.MinBy(r => Math.Pow(x - Math.Clamp(x, r.X, r.Right), 2)
                                  + Math.Pow(y - Math.Clamp(y, r.Y, r.Bottom), 2));
    }

    public static (double X, double Y) Flyout(PixelRect anchor, double width, double height, PixelRect work)
    {
        var x = anchor.X + anchor.Width / 2 - width / 2;
        var y = anchor.Y + anchor.Height / 2 - height / 2;
        return Clamp(x, y, width, height, work);
    }
}

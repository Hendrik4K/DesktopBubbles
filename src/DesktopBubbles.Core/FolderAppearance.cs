namespace DesktopBubbles.Core;

public static class FolderAppearance
{
    public const double FrostedOpacity = .55;
    // Liquid glass takes precedence over frost and follows the normal folder transparency.
    public static bool Frosted(BubbleSettings settings) => settings.FrostedFolders && !settings.LiquidGlass;
    public static double Opacity(BubbleSettings settings) => Frosted(settings) ? FrostedOpacity : settings.WindowOpacity;

    public static void SetTransparency(BubbleSettings settings, double percent)
    {
        double opacity = 1 - Math.Clamp(percent, 0, 100) / 100;
        // Frost has a fixed material. A hidden control must never modify either profile.
        if (!Frosted(settings)) settings.WindowOpacity = opacity;
    }
}

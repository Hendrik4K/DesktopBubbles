namespace DesktopBubbles.Core;

public static class FolderAppearance
{
    public const double FrostedOpacity = .55;
    public static double Opacity(BubbleSettings settings) => settings.FrostedFolders ? FrostedOpacity : settings.WindowOpacity;

    public static void SetTransparency(BubbleSettings settings, double percent)
    {
        double opacity = 1 - Math.Clamp(percent, 0, 100) / 100;
        // Frost has a fixed material. A hidden control must never modify either profile.
        if (!settings.FrostedFolders) settings.WindowOpacity = opacity;
    }
}

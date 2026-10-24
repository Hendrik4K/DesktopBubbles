namespace DesktopBubbles.Core;

public static class AppearanceGeometry
{
    public static double Radius(double percent, double width, double height)
        => Math.Max(0, Math.Min(width, height)) * Math.Clamp(double.IsFinite(percent) ? percent : 14, 0, 50) / 100;

    // Native folder content has 22 DIP padding and a 1 DIP border on each side.
    public static PixelRect Folder(BubbleSettings settings, int count)
    {
        int rows = count <= 0 ? 2 : (int)Math.Ceiling(count / (double)settings.Columns);
        return new(0, 0, settings.Columns * settings.TileSize + 48, Math.Min(640, rows * settings.TileSize + 46));
    }
}

using System.Globalization;

namespace DesktopBubbles.Core;

public readonly record struct ColorValue(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    public static bool TryParse(string? input, out ColorValue color)
    {
        color = default;
        string text = (input ?? "").Trim().TrimStart('#');
        if (text.Length != 6 || !uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value)) return false;
        color = new((byte)(value >> 16), (byte)(value >> 8), (byte)value); return true;
    }
    public static string Normalize(string? input, string fallback = "#7B6BFF") => TryParse(input, out var color) ? color.Hex : fallback;
    public static ColorValue FromHsv(double hue, double saturation, double brightness)
    {
        hue = double.IsFinite(hue) ? (hue % 360 + 360) % 360 : 0;
        saturation = double.IsFinite(saturation) ? Math.Clamp(saturation, 0, 1) : 0;
        brightness = double.IsFinite(brightness) ? Math.Clamp(brightness, 0, 1) : 0;
        double chroma = brightness * saturation, x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1)), m = brightness - chroma;
        (double r, double g, double b) = hue switch
        {
            < 60 => (chroma, x, 0d), < 120 => (x, chroma, 0d), < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma), < 300 => (x, 0d, chroma), _ => (chroma, 0d, x)
        };
        return new((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
    public (double H, double S, double V) ToHsv()
    {
        double r = R / 255d, g = G / 255d, b = B / 255d, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        double hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return ((hue + 360) % 360, max == 0 ? 0 : delta / max, max);
    }
}

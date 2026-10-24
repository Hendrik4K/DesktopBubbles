namespace DesktopBubbles.Core;

/// <summary>Separate mode colors; null retains the original palette for existing configurations.</summary>
public sealed record ThemePalette(string Surface, string ControlFill, string HoverFill, string Line, string Ink, string Muted, string MaterialBasis)
{
    public static string Color(BubbleSettings settings, bool dark) => (dark ? settings.DarkModeColor : settings.LightModeColor) ?? (dark ? "#252234" : "#EBE9F4");

    public static ThemePalette Create(BubbleSettings settings, bool dark, string? draft = null, double? opacityDraft = null)
    {
        double opacity = opacityDraft ?? (dark ? settings.DarkModeOpacity : settings.LightModeOpacity);
        opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
        string Alpha(string color)
        {
            if (opacity == 1) return color;
            int alpha = color.Length == 9 ? Convert.ToInt32(color.Substring(1, 2), 16) : 255;
            return "#" + ((byte)Math.Round(alpha * opacity)).ToString("X2") + color[^6..];
        }
        ThemePalette Finish(ThemePalette palette) => palette with { Surface = Alpha(palette.Surface), ControlFill = Alpha(palette.ControlFill), HoverFill = Alpha(palette.HoverFill), Line = Alpha(palette.Line) };
        string? custom = draft ?? (dark ? settings.DarkModeColor : settings.LightModeColor);
        if (!ColorValue.TryParse(custom, out var basis))
            return Finish(dark ? new("#252234", "#383348", "#514961", "#75678391", "#F3F1FF", "#BDB9D2", "#1E1B2B")
                : new("#EBE9F4", "#70FFFFFF", "#62BDB4FA", "#70A29ABD", "#201D3B", "#66637E", "#EBE9F4"));
        // Choose the higher-contrast text for the actual color, even when a mode uses an unusual hue.
        double luminance = Luminance(basis);
        bool lightInk = (1.05 / (luminance + .05)) >= ((luminance + .05) / .05);
        var ink = lightInk ? new ColorValue(255, 255, 255) : new ColorValue(0, 0, 0);
        var shade = lightInk ? new ColorValue(0, 0, 0) : new ColorValue(255, 255, 255);
        var control = Mix(basis, shade, .08);
        return Finish(new(basis.Hex, control.Hex, Mix(basis, shade, .18).Hex, Mix(basis, ink, .38).Hex,
            ink.Hex, Mix(basis, ink, .9).Hex, basis.Hex));
    }

    private static ColorValue Mix(ColorValue a, ColorValue b, double amount) => new(
        (byte)Math.Round(a.R + (b.R - a.R) * amount), (byte)Math.Round(a.G + (b.G - a.G) * amount), (byte)Math.Round(a.B + (b.B - a.B) * amount));
    private static double Luminance(ColorValue color)
    {
        static double Linear(byte value) { double s = value / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
}

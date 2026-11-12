using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopBubbles;

// A rounded lens over a background sample. The bezel bends pixels from just outside the
// outline into the glass (with slight color separation), the body stays calm and lightly
// softened, and a directional rim catches the light. The same signed distance field yields
// an antialiased alpha edge, so DWM never sees a square.
// Rendering has two stages: Prepare softens a sample once; Render refracts any rectangle of
// it. Moving glass prepares once and renders every frame, rows in parallel and, while in
// motion, at reduced resolution.
internal static class LiquidGlass
{
    internal readonly record struct Options(double Radius, double Scale, bool Dark, Color Tint, double TintAmount, double Strength = .6);

    // Softened BGRA pixels of a background sample; X/Y place it in physical screen pixels.
    internal sealed record Backdrop(byte[] Pixels, int Width, int Height, double X = 0, double Y = 0);

    // Light falls from the upper left, like the system highlight on iOS glass.
    private const double LightX = -.6, LightY = -.8;

    internal static Backdrop Prepare(BitmapSource background, double scale, double x = 0, double y = 0)
    {
        var source = new FormatConvertedBitmap(background, PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight;
        var pixels = new byte[width * height * 4]; source.CopyPixels(pixels, width * 4, 0);
        // Two box passes approximate the light Gaussian softening, in a few milliseconds even
        // for a whole folder plus its travel path.
        int radius = Math.Max(1, (int)Math.Round(1.6 * scale));
        for (int pass = 0; pass < 2; pass++) { Box(pixels, width, height, radius, horizontal: true); Box(pixels, width, height, radius, horizontal: false); }
        return new(pixels, width, height, x, y);
    }

    private static void Box(byte[] pixels, int width, int height, int radius, bool horizontal)
    {
        int lines = horizontal ? height : width, length = horizontal ? width : height;
        int stride = horizontal ? 4 : width * 4, lineStep = horizontal ? width * 4 : 4;
        Parallel.For(0, lines, () => new int[length * 4], (line, _, buffer) =>
        {
            int start = line * lineStep;
            for (int i = 0; i < length; i++) for (int c = 0; c < 4; c++) buffer[i * 4 + c] = pixels[start + i * stride + c];
            for (int c = 0; c < 4; c++)
            {
                int sum = 0, span = radius * 2 + 1;
                for (int k = -radius; k <= radius; k++) sum += buffer[Math.Clamp(k, 0, length - 1) * 4 + c];
                for (int i = 0; i < length; i++)
                {
                    pixels[start + i * stride + c] = (byte)(sum / span);
                    sum += buffer[Math.Min(i + radius + 1, length - 1) * 4 + c] - buffer[Math.Max(i - radius, 0) * 4 + c];
                }
            }
            return buffer;
        }, _ => { });
    }

    internal static BitmapSource Render(BitmapSource background, Int32Rect lens, Options options)
        => Render(Prepare(background, options.Scale), lens.X, lens.Y, lens.Width, lens.Height, options);

    // Renders the lens whose top-left lies at (left, top) inside the backdrop. A resolution
    // below 1 renders fewer pixels for the same lens; the brush stretches them back.
    internal static BitmapSource Render(Backdrop backdrop, double left, double top, int w, int h, Options options, double resolution = 1)
    {
        resolution = Math.Clamp(resolution, .1, 1);
        int ow = Math.Max(1, (int)Math.Ceiling(w * resolution)), oh = Math.Max(1, (int)Math.Ceiling(h * resolution));
        double step = w / (double)ow, stepY = h / (double)oh;
        var src = backdrop.Pixels; int sw = backdrop.Width, sh = backdrop.Height;
        var dst = new byte[ow * oh * 4];
        double hx = w / 2d, hy = h / 2d;
        double radius = Math.Clamp(options.Radius, 0, Math.Min(hx, hy));
        // Strength deepens the bezel relative to the tile, so small miniatures bend as visibly as folders.
        double strength = Math.Clamp(options.Strength, 0, 1);
        double bezel = Math.Clamp(Math.Min(w, h) * (.1 + .24 * strength), 5 * options.Scale, 40 * options.Scale);
        double split = .03 + .09 * strength;
        // Normals come from a contour at least as round as the bezel is deep, so tight
        // corners never show a hard diagonal seam in the refraction.
        double shapeRadius = Math.Min(Math.Max(radius, bezel), Math.Min(hx, hy));
        double reach = Math.Min(bezel * (.5 + .9 * strength), 34); // Stays inside the capture's 36 px bleed.
        double rim = 1.3 * options.Scale;
        double tint = Math.Clamp(options.TintAmount, 0, 1);
        var color = options.Tint; bool dark = options.Dark;

        Parallel.For(0, oh, oy =>
        {
            double y = (oy + .5) * stepY, cy = y - hy;
            for (int ox = 0; ox < ow; ox++)
            {
                double x = (ox + .5) * step, cx = x - hx;
                double inside = -Distance(cx, cy, hx, hy, radius);
                double alpha = Math.Clamp(inside / step + .5, 0, 1);
                if (alpha <= 0) continue;
                double depth = -Distance(cx, cy, hx, hy, shapeRadius);
                Normal(cx, cy, hx, hy, shapeRadius, out double nx, out double ny);

                // Convex bezel: strongest bend at the outline, none past the bezel.
                double u = Math.Clamp(depth / bezel, 0, 1), fall = (1 - u) * (1 - u) * (1 - u);
                double shift = reach * fall;
                double px = left + x, py = top + y;
                double r = Sample(src, sw, sh, px + nx * shift * (1 + split), py + ny * shift * (1 + split), 2);
                double g = Sample(src, sw, sh, px + nx * shift, py + ny * shift, 1);
                double b = Sample(src, sw, sh, px + nx * shift * (1 - split), py + ny * shift * (1 - split), 0);

                // Glass lifts saturation a little and settles toward the mode's brightness.
                double luma = .299 * r + .587 * g + .114 * b;
                r = luma + (r - luma) * 1.3; g = luma + (g - luma) * 1.3; b = luma + (b - luma) * 1.3;
                if (dark) { r *= .8; g *= .8; b *= .82; }
                else { r = r * .86 + 255 * .14; g = g * .86 + 255 * .14; b = b * .86 + 255 * .14; }
                r += (color.R - r) * tint; g += (color.G - g) * tint; b += (color.B - b) * tint;

                double facing = nx * LightX + ny * LightY;
                double edge = Math.Exp(-Math.Max(0, inside) / Math.Max(rim, step));
                double specular = edge * (.22 + .78 * Math.Pow(Math.Max(0, facing), 2) + .45 * Math.Pow(Math.Max(0, -facing), 2));
                double glow = (1 - u) * (1 - u) * (dark ? .07 : .12);
                double light = Math.Min(1, specular * (dark ? .7 : .9) + glow);
                r += (255 - r) * light; g += (255 - g) * light; b += (255 - b) * light;

                int i = (oy * ow + ox) * 4;
                dst[i] = Channel(b * alpha); dst[i + 1] = Channel(g * alpha); dst[i + 2] = Channel(r * alpha);
                dst[i + 3] = Channel(255 * alpha);
            }
        });
        var result = BitmapSource.Create(ow, oh, 96, 96, PixelFormats.Pbgra32, null, dst, ow * 4);
        result.Freeze(); return result;
    }

    // The shared transparency slider works on liquid glass too. The default opacity shows the
    // pure lens; more transparent fades the lens out (100 % shows nothing, as in the other
    // styles), less transparent lays an increasingly dense milky layer over it.
    internal const double PureOpacity = .35;
    internal static Brush Material(BitmapSource lens, double opacity, bool dark)
    {
        double clear = Math.Clamp(opacity / PureOpacity, 0, 1), milk = Math.Clamp((opacity - PureOpacity) / (.95 - PureOpacity), 0, 1);
        if (clear <= 0) return Brushes.Transparent;
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            var bounds = new Rect(0, 0, 1, 1);
            context.PushOpacity(clear); context.DrawImage(lens, bounds); context.Pop();
            if (milk > 0)
            {
                // The surrounding Border clips this layer to the same rounded contour as the lens.
                var layer = new SolidColorBrush(dark ? Color.FromRgb(38, 43, 52) : Color.FromRgb(244, 246, 250));
                context.PushOpacity(milk * .78); context.DrawRectangle(layer, null, bounds); context.Pop();
            }
        }
        drawing.Freeze();
        var brush = new DrawingBrush(drawing) { Stretch = Stretch.Fill }; brush.Freeze(); return brush;
    }

    private static double Distance(double x, double y, double hx, double hy, double r)
    {
        double qx = Math.Abs(x) - (hx - r), qy = Math.Abs(y) - (hy - r);
        double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
        return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
    }

    private static void Normal(double x, double y, double hx, double hy, double r, out double nx, out double ny)
    {
        double qx = Math.Abs(x) - (hx - r), qy = Math.Abs(y) - (hy - r);
        if (qx > 0 && qy > 0) { double length = Math.Sqrt(qx * qx + qy * qy); nx = qx / length; ny = qy / length; }
        else if (qx > qy) { nx = 1; ny = 0; }
        else { nx = 0; ny = 1; }
        nx *= Math.Sign(x) == 0 ? 1 : Math.Sign(x); ny *= Math.Sign(y) == 0 ? 1 : Math.Sign(y);
    }

    private static double Sample(byte[] pixels, int width, int height, double x, double y, int channel)
    {
        x = Math.Clamp(x - .5, 0, width - 1); y = Math.Clamp(y - .5, 0, height - 1);
        int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
        double fx = x - x0, fy = y - y0;
        double top = pixels[(y0 * width + x0) * 4 + channel] * (1 - fx) + pixels[(y0 * width + x1) * 4 + channel] * fx;
        double bottom = pixels[(y1 * width + x0) * 4 + channel] * (1 - fx) + pixels[(y1 * width + x1) * 4 + channel] * fx;
        return top * (1 - fy) + bottom * fy;
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}

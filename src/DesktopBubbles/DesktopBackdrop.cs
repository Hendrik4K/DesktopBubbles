using System.Drawing;
using System.Drawing.Imaging;

namespace DesktopBubbles;

// What the desktop itself shows (wallpaper and icons), rendered by Explorer without any of
// our windows. A glass sample fills a tile's own spot from here instead of hiding the tile
// from screen capture, so recordings (OBS, Snipping Tool) never see a tile blink away.
internal static class DesktopBackdrop
{
    private static readonly object gate = new();
    private static Bitmap? image;
    private static Rectangle area;
    private static DateTime stamp;
    private static bool refreshing, dirty;
    private static readonly TimeSpan freshFor = TimeSpan.FromSeconds(20);
    internal static int Renders { get; private set; }

    // True when a rendering is at hand. A stale one is still used while a fresh one is made
    // in the background, so a sample never waits for Explorer.
    internal static bool Ready
    {
        get
        {
            bool ready, stale;
            lock (gate) { ready = image is not null; stale = dirty || DateTime.UtcNow - stamp > freshFor; }
            if (stale) Refresh();
            return ready;
        }
    }

    // Wallpaper, slideshow or display layout changed: the next use brings a new rendering.
    internal static void Invalidate() { lock (gate) dirty = true; Refresh(); }

    internal static void Refresh()
    {
        lock (gate) { if (refreshing) return; refreshing = true; dirty = false; }
        Task.Run(() =>
        {
            Bitmap? next = null; Rectangle bounds = default;
            try { next = Render(out bounds); }
            catch (Exception ex) { Log.Write("Desktop backdrop unavailable: " + ex.Message); }
            lock (gate)
            {
                refreshing = false;
                if (next is null) return;
                image?.Dispose(); image = next; area = bounds; stamp = DateTime.UtcNow; Renders++;
            }
        });
    }

    // Renders synchronously; for tests and the very first use.
    internal static bool RefreshNow()
    {
        try
        {
            if (Render(out var bounds) is not { } next) return false;
            lock (gate) { image?.Dispose(); image = next; area = bounds; stamp = DateTime.UtcNow; dirty = false; Renders++; }
            return true;
        }
        catch (Exception ex) { Log.Write("Desktop backdrop unavailable: " + ex.Message); return false; }
    }

    private static Bitmap? Render(out Rectangle bounds)
    {
        bounds = default;
        var desktop = Native.GetShellWindow();
        if (desktop == 0 || !Native.GetWindowRect(desktop, out var r)) return null;
        bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        if (bounds.Width <= 0 || bounds.Height <= 0 || (long)bounds.Width * bounds.Height > 80_000_000) return null;
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
        bool ok;
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var dc = graphics.GetHdc();
            try { ok = Native.PrintWindow(desktop, dc, 2); } // PW_RENDERFULLCONTENT: the DWM content, wallpaper included.
            finally { graphics.ReleaseHdc(dc); }
        }
        if (ok) return bitmap;
        bitmap.Dispose(); return null;
    }

    // Where each window was last filled: while a tile moves, the composed screen may still show
    // it one frame behind, so that spot is filled as well.
    private static readonly Dictionary<nint, Rectangle> lastSpot = [];
    internal static void Forget(nint window) { lock (gate) lastSpot.Remove(window); }

    // Paints the desktop over one window's spot in a sample of the screen. sampleArea is where
    // the sample lies on screen, both in physical pixels. False leaves the sample untouched.
    internal static bool Fill(Bitmap sample, Rectangle sampleArea, nint window)
    {
        if (window == 0 || !Native.GetWindowRect(window, out var r)) return false;
        var spot = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        lock (gate)
        {
            if (image is null) return false;
            bool hadLast = lastSpot.TryGetValue(window, out var last);
            lastSpot[window] = spot;
            using var graphics = Graphics.FromImage(sample);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            bool Paint(Rectangle where)
            {
                var hole = Rectangle.Intersect(where, sampleArea);
                if (hole.Width <= 0 || hole.Height <= 0) return true;
                if (!area.Contains(hole)) return false;
                graphics.DrawImage(image, new Rectangle(hole.X - sampleArea.X, hole.Y - sampleArea.Y, hole.Width, hole.Height),
                    new Rectangle(hole.X - area.X, hole.Y - area.Y, hole.Width, hole.Height), GraphicsUnit.Pixel);
                return true;
            }
            if (hadLast && last != spot) Paint(last);
            return Paint(spot);
        }
    }
}

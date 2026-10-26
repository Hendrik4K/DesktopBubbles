using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DesktopBubbles.Core;
using Microsoft.Win32;

namespace DesktopBubbles;

// A bounded, in-memory sample made while the flyout is still transparent. The
// resulting brush belongs to the same alpha surface as the rim and icons; DWM
// never gets a second rectangular backdrop to draw outside our corner geometry.
internal static class FolderFrost
{
    internal static BitmapSource? Capture(PixelRect bounds)
    {
        if (SystemParameters.HighContrast) return null;
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1) is int value && value == 0) return null;
        const int bleed = 36;
        var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
        var sample = System.Drawing.Rectangle.Intersect(screen, new System.Drawing.Rectangle(
            (int)Math.Floor(bounds.X) - bleed, (int)Math.Floor(bounds.Y) - bleed,
            (int)Math.Ceiling(bounds.Width) + bleed * 2, (int)Math.Ceiling(bounds.Height) + bleed * 2));
        if (sample.Width <= 0 || sample.Height <= 0 || (long)sample.Width * sample.Height > 4_000_000) return null;
        try
        {
            using var bitmap = new System.Drawing.Bitmap(sample.Width, sample.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                var destination = graphics.GetHdc(); var desktop = Native.GetDC(0);
                try
                {
                    // SRCCOPY | CAPTUREBLT includes layered desktop windows. Graphics.CopyFromScreen
                    // rejects this documented native flag combination in its enum validation.
                    if (desktop == 0 || !Native.BitBlt(destination, 0, 0, sample.Width, sample.Height, desktop, sample.X, sample.Y, 0x40CC0020))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                finally { if (desktop != 0) Native.ReleaseDC(0, desktop); graphics.ReleaseHdc(destination); }
            }
            var locked = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, sample.Width, sample.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            BitmapSource source;
            try
            {
                var pixels = new byte[locked.Stride * sample.Height]; Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);
                source = BitmapSource.Create(sample.Width, sample.Height, 96, 96, PixelFormats.Bgr32, null, pixels, locked.Stride);
            }
            finally { bitmap.UnlockBits(locked); }
            source.Freeze();
            int x = Math.Max(0, (int)Math.Round(bounds.X) - sample.Left), y = Math.Max(0, (int)Math.Round(bounds.Y) - sample.Top);
            var crop = new Int32Rect(x, y, Math.Min((int)Math.Ceiling(bounds.Width), sample.Width - x), Math.Min((int)Math.Ceiling(bounds.Height), sample.Height - y));
            return Blur(source, crop);
        }
        catch (Exception ex) when (ex is Win32Exception or ExternalException or ArgumentException or InvalidOperationException)
        {
            Log.Write("Folder frost sample unavailable; using neutral rounded material. " + ex.Message);
            return null;
        }
    }

    internal static BitmapSource Blur(BitmapSource source, Int32Rect crop)
    {
        var image = new Image { Source = source, Stretch = Stretch.Fill,
            Effect = new BlurEffect { Radius = 24, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance } };
        var size = new Size(source.PixelWidth, source.PixelHeight);
        image.Measure(size); image.Arrange(new Rect(size)); image.UpdateLayout();
        var target = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(image); target.Freeze();
        var result = new CroppedBitmap(target, crop); result.Freeze(); return result;
    }

    internal static Brush Material(BitmapSource sample, Brush tint)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawImage(sample, new Rect(0, 0, 1, 1));
            context.DrawRectangle(tint, null, new Rect(0, 0, 1, 1));
        }
        drawing.Freeze();
        var result = new DrawingBrush(drawing) { Stretch = Stretch.Fill }; result.Freeze(); return result;
    }
}

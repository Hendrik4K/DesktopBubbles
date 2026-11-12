using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopBubbles.Core;

namespace DesktopBubbles;

// Closed-form damped spring moving 0 → 1, parameterised like SwiftUI: response is the
// undamped period in seconds, damping 1 is critical (no overshoot), below 1 bounces.
internal readonly record struct Spring(double Response, double Damping)
{
    public double Value(double t)
    {
        if (t <= 0) return 0;
        double omega = 2 * Math.PI / Response;
        if (Damping >= 1) return 1 - Math.Exp(-omega * t) * (1 + omega * t);
        double decay = Damping * omega, damped = omega * Math.Sqrt(1 - Damping * Damping);
        return 1 - Math.Exp(-decay * t) * (Math.Cos(damped * t) + decay / damped * Math.Sin(damped * t));
    }

    // Last moment the motion is further than half a percent from rest.
    public double SettleTime
    {
        get
        {
            double last = 0;
            for (double t = 0; t < 3; t += .002) if (Math.Abs(1 - Value(t)) > .005) last = t;
            return last + .002;
        }
    }
}

// Opens a folder out of its miniature and folds it back in, like an iOS folder.
// The folder window only covers the folder, so for the duration of the motion everything
// travels in a click-through overlay spanning miniature and folder, stacked directly below
// the folder: the glass and every icon. Icons start in their cell of the miniature's 3×3
// grid and fly to their place; width and height ride on slightly different springs, so
// glass and icons stretch and squash on the way instead of scaling rigidly.
// Handovers between miniature, overlay and folder always overlap: the next window has been
// composed before the previous one steps aside, so no frame shows a gap.
internal sealed class FolderMotion
{
    // Horizontal and vertical springs differ a little: that difference is the jelly.
    private static readonly Spring GlassX = new(.46, .8), GlassY = new(.52, .78);
    private static readonly Spring IconMove = new(.44, .76), IconWidth = new(.38, .62), IconHeight = new(.5, .6);
    private static readonly Spring CloseX = new(.36, .94), CloseY = new(.4, .92), CloseIcon = new(.34, .96);
    private const double IconStagger = .012, ExtraFade = .1;

    private readonly Window folder;
    private readonly Border surface;
    private readonly IReadOnlyList<Button> buttons;
    private readonly PixelRect from, to; // Physical pixels: miniature glass and folder surface.
    private readonly double fromRadius, toRadius; // Physical pixels.
    private readonly Action hideSurface, showSurface;
    // Refracts the real background for the current shape; p runs 0 (miniature) to 1 (folder).
    private readonly Func<PixelRect, double, double, Brush?>? live;
    // The miniature's own glass and rim, faded out as the glass becomes the folder (and back in on close).
    private readonly Brush? miniature, miniatureRim;
    private readonly Stopwatch clock = new();
    private GlassOverlay? overlay;
    private Track[] tracks = [];
    private Func<double, bool>? step;
    private bool pending;

    private sealed record Track(FrameworkElement Real, Image Ghost, PixelRect Mini, PixelRect Final, bool InMiniature, double Delay);

    public FolderMotion(Window folder, Border surface, IReadOnlyList<Button> buttons, PixelRect from, double fromRadius, double toRadius, Action hideSurface, Action showSurface,
        Func<PixelRect, double, double, Brush?>? live = null, Brush? miniature = null, Brush? miniatureRim = null)
    {
        this.live = live; this.miniature = miniature; this.miniatureRim = miniatureRim;
        this.folder = folder; this.surface = surface; this.buttons = buttons; this.from = from; this.fromRadius = fromRadius; this.toRadius = toRadius;
        this.hideSurface = hideSurface; this.showSurface = showSurface;
        to = Native.Bounds(folder);
    }

    public bool Running => pending || step is not null;

    public void Open(Action hideAnchor)
    {
        Prepare();
        double end = Math.Max(Math.Max(GlassX.SettleTime, GlassY.SettleTime), tracks.Select(x => x.Delay).DefaultIfEmpty(0).Max()
            + Math.Max(IconMove.SettleTime, Math.Max(IconWidth.SettleTime, IconHeight.SettleTime)));
        Start(t =>
        {
            double grow = Math.Min(GlassX.Value(t), GlassY.Value(t));
            Glass(Lerp(from, to, GlassX.Value(t), GlassY.Value(t)), Radius(grow), grow);
            foreach (var track in tracks)
            {
                double local = t - track.Delay;
                var start = track.InMiniature ? track.Mini : Center(from);
                overlay!.Ghost(track.Ghost, Travel(start, track.Final, IconMove.Value(local), IconWidth.Value(local), IconHeight.Value(local)),
                    track.InMiniature ? 1 : Math.Clamp(local / ExtraFade, 0, 1));
            }
            if (t < end) return false;
            Land(); return true;
        }, ready: hideAnchor); // The overlay already shows the miniature, so it can step aside.
    }

    public void Close(Action reveal, Action done)
    {
        Stop();
        Prepare();
        double end = Math.Max(Math.Max(CloseX.SettleTime, CloseY.SettleTime), CloseIcon.SettleTime);
        Start(t =>
        {
            double px = 1 - CloseX.Value(t), py = 1 - CloseY.Value(t);
            // Shrinks all the way onto the miniature; no fading on the way.
            Glass(Lerp(from, to, px, py), Radius(Math.Min(px, py)), Math.Min(px, py));
            double q = 1 - CloseIcon.Value(t);
            foreach (var track in tracks)
            {
                var target = track.InMiniature ? track.Mini : Center(from);
                overlay!.Ghost(track.Ghost, Travel(target, track.Final, q, q, q), track.InMiniature ? 1 : Math.Clamp(1 - t / ExtraFade, 0, 1));
            }
            if (t < end) return false;
            // The overlay now covers the miniature exactly: reveal it underneath, then retire.
            reveal(); overlay!.Retire(); overlay = null; done(); return true;
        }, ready: null);
    }

    public void Stop()
    {
        if (!Running) return;
        CompositionTarget.Rendering -= OnRendering; step = null; pending = false;
        showSurface();
        foreach (var track in tracks) track.Real.Opacity = 1;
        overlay?.Close(); overlay = null;
    }

    // Snapshot the glass and collect where every icon starts and lands.
    private void Prepare()
    {
        var glass = Snapshot();
        double scale = Native.GetDpiForWindow(Native.Handle(folder)) / 96d;
        double pad = from.Width * 10 / 76, cell = (from.Width - 2 * pad) / 3, inset = from.Width / 76;
        var center = Center(from);
        var list = new List<Track>();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].Content is not Image image || image.Source is null || image.ActualWidth <= 0) continue;
            Point origin;
            try { origin = image.TranslatePoint(new Point(), surface); } catch (InvalidOperationException) { continue; }
            var final = new PixelRect(to.X + origin.X * scale, to.Y + origin.Y * scale, image.ActualWidth * scale, image.ActualHeight * scale);
            var mini = new PixelRect(from.X + pad + i % 3 * cell + inset, from.Y + pad + i / 3 * cell + inset, cell - 2 * inset, cell - 2 * inset);
            list.Add(new Track(image, new Image { Source = image.Source, Stretch = Stretch.Fill }, mini, final, i < 9, 0));
        }
        // Closest icons leave first, so the grid unfolds outward from the miniature.
        tracks = list.OrderBy(x => Math.Pow(x.Final.X + x.Final.Width / 2 - center.X, 2) + Math.Pow(x.Final.Y + x.Final.Height / 2 - center.Y, 2))
            .Select((x, rank) => x with { Delay = rank * IconStagger }).ToArray();
        overlay = new GlassOverlay(folder, Union(from, to), glass, tracks.Select(x => x.Ghost), miniature, miniatureRim);
    }

    // Shows the overlay with the first frame already laid out, waits until DWM has composed
    // it, and only then empties the folder window and lets the clock run.
    private void Start(Func<double, bool> frame, Action? ready)
    {
        pending = true;
        var target = overlay!;
        target.Present(() => frame(0), () =>
        {
            if (!pending || overlay != target) return;
            pending = false;
            ready?.Invoke();
            hideSurface(); surface.Opacity = 1;
            foreach (var track in tracks) track.Real.Opacity = 0;
            clock.Restart(); step = frame;
            CompositionTarget.Rendering += OnRendering;
        });
    }

    // Hand everything back to the folder window first; the overlay below leaves a little later.
    private void Land()
    {
        showSurface();
        foreach (var track in tracks) track.Real.Opacity = 1;
        overlay?.Retire(); overlay = null;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (step?.Invoke(clock.Elapsed.TotalSeconds) == true) { CompositionTarget.Rendering -= OnRendering; step = null; }
    }

    // The glass without its icons, at device resolution, for the overlay to stretch.
    private BitmapSource Snapshot()
    {
        var child = surface.Child; double opacity = surface.Opacity;
        if (child is not null) child.Visibility = Visibility.Hidden;
        surface.Opacity = 1; surface.UpdateLayout();
        double scale = Native.GetDpiForWindow(Native.Handle(folder)) / 96d;
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(surface.ActualWidth * scale)), Math.Max(1, (int)Math.Ceiling(surface.ActualHeight * scale)), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) context.DrawRectangle(new VisualBrush(surface), null, new Rect(0, 0, surface.ActualWidth, surface.ActualHeight));
        bitmap.Render(visual); bitmap.Freeze();
        if (child is not null) child.Visibility = Visibility.Visible;
        surface.Opacity = opacity;
        return bitmap;
    }

    private void Glass(PixelRect rect, double radius, double p)
    {
        p = Math.Clamp(p, 0, 1);
        // The miniature's look is gone by three quarters of the way; smoothstep keeps both ends soft.
        double fade = Math.Clamp(p / .75, 0, 1); fade = fade * fade * (3 - 2 * fade);
        overlay!.Glass(rect, radius, live?.Invoke(rect, radius, p), miniature is null && miniatureRim is null ? 0 : 1 - fade);
    }

    // Screen area the glass can cover while travelling, overshoot included.
    internal static PixelRect Path(PixelRect from, PixelRect to) => Union(from, to);

    private double Radius(double p) => fromRadius + (toRadius - fromRadius) * Math.Clamp(p, 0, 1);

    private static PixelRect Lerp(PixelRect a, PixelRect b, double px, double py)
        => new(a.X + (b.X - a.X) * px, a.Y + (b.Y - a.Y) * py, a.Width + (b.Width - a.Width) * px, a.Height + (b.Height - a.Height) * py);

    // Centre moves on one spring, width and height on two others: the icon stretches along the way.
    private static PixelRect Travel(PixelRect a, PixelRect b, double move, double width, double height)
    {
        double cx = a.X + a.Width / 2 + (b.X + b.Width / 2 - a.X - a.Width / 2) * move;
        double cy = a.Y + a.Height / 2 + (b.Y + b.Height / 2 - a.Y - a.Height / 2) * move;
        double w = Math.Max(0, a.Width + (b.Width - a.Width) * width), h = Math.Max(0, a.Height + (b.Height - a.Height) * height);
        return new(cx - w / 2, cy - h / 2, w, h);
    }

    // Icons past the ninth have no cell in the miniature; they grow out of its centre.
    private static PixelRect Center(PixelRect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2, 0, 0);

    private static PixelRect Union(PixelRect a, PixelRect b)
    {
        const int room = 56; // Space for the springs' overshoot.
        double left = Math.Min(a.X, b.X) - room, top = Math.Min(a.Y, b.Y) - room;
        return new(left, top, Math.Max(a.X + a.Width, b.X + b.Width) + room - left, Math.Max(a.Y + a.Height, b.Y + b.Height) + room - top);
    }

    private sealed class GlassOverlay : Window
    {
        private readonly Window folder;
        private readonly Border blob, mini;
        private readonly Brush snapshot;
        private readonly PixelRect bounds;
        private double scale = 1;
        private bool closed;

        public GlassOverlay(Window folder, PixelRect bounds, BitmapSource glass, IEnumerable<Image> icons, Brush? miniature, Brush? miniatureRim)
        {
            this.folder = folder; this.bounds = bounds;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ShowActivated = false; Focusable = false; IsHitTestVisible = false; WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -32000; Top = -32000; Width = 1; Height = 1;
            snapshot = new ImageBrush(glass) { Stretch = Stretch.Fill }; snapshot.Freeze();
            blob = new Border { Background = snapshot };
            // The miniature's material rides on top of the folder's and crossfades with it.
            mini = new Border { Background = miniature, BorderBrush = miniatureRim, BorderThickness = new Thickness(miniatureRim is null ? 0 : 1), Opacity = 0 };
            var canvas = new Canvas(); canvas.Children.Add(blob); canvas.Children.Add(mini);
            foreach (var icon in icons) canvas.Children.Add(icon);
            Content = canvas;
            Closed += (_, _) => closed = true;
        }

        // first lays out frame zero before anything is drawn; ready runs once DWM shows it.
        public void Present(Action first, Action ready)
        {
            SourceInitialized += (_, _) =>
            {
                var h = Native.Handle(this);
                long ex = (long)Native.GetWindowLongPtr(h, Native.GwlExStyle);
                const long transparent = 0x20, noActivate = 0x08000000, toolWindow = 0x80;
                Native.SetWindowLongPtr(h, Native.GwlExStyle, (nint)(ex | transparent | noActivate | toolWindow));
                // Directly below the folder window, which stays in front until the overlay is ready.
                Native.SetWindowPos(h, Native.Handle(folder), (int)Math.Round(bounds.X), (int)Math.Round(bounds.Y), (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 0x0010);
                scale = Native.GetDpiForWindow(h) / 96d;
                first();
            };
            ContentRendered += (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
            {
                if (closed) return;
                Native.DwmFlush(); // Wait for the composition that contains the overlay.
                ready();
            });
            Show();
        }

        // Leave after the window above has had two compositions to show its replacement.
        public void Retire()
        {
            Dispatcher.InvokeAsync(async () => { Native.DwmFlush(); await Task.Delay(50); if (!closed) Close(); });
        }

        public void Glass(PixelRect rect, double radius, Brush? material, double miniature)
        {
            var corner = new CornerRadius(Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2) / scale);
            Place(blob, rect);
            blob.Background = material ?? snapshot; blob.CornerRadius = corner;
            Place(mini, rect); mini.CornerRadius = corner; mini.Opacity = miniature;
            // Material on material would double up mid-way; the folder layer fades in as the miniature fades out.
            blob.Opacity = miniature <= 0 ? 1 : Math.Clamp(1.25 - miniature, 0, 1);
        }

        public void Ghost(Image icon, PixelRect rect, double opacity) { Place(icon, rect); icon.Opacity = opacity; }

        private void Place(FrameworkElement element, PixelRect rect)
        {
            Canvas.SetLeft(element, (rect.X - bounds.X) / scale); Canvas.SetTop(element, (rect.Y - bounds.Y) / scale);
            element.Width = Math.Max(0, rect.Width) / scale; element.Height = Math.Max(0, rect.Height) / scale;
        }
    }
}

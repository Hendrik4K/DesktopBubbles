using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DesktopBubbles;

// The line between the bubble list and the page runs from the title bar to the bottom edge and
// doubles as the scroll bar: whichever area scrolls (the list on the left or the page on the right)
// shows its thumb on the line, and the thumb can be dragged there. It fades away when idle.
// Switching between the two areas morphs the thumb to its new length and place.
internal sealed class DividerScroll : Canvas
{
    private readonly Rectangle line = new() { Width = 1 };
    private readonly Border thumb = new() { Width = 4, CornerRadius = new CornerRadius(2), Opacity = 0 };
    private readonly Border grip = new() { Width = 14, Background = Brushes.Transparent, Cursor = Cursors.Arrow };
    private readonly System.Windows.Threading.DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(1.1) };
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private ScrollViewer? active;
    private double dragStart, offsetStart;
    // Where the thumb is drawn, where its scroll area wants it, and where a morph started.
    private double shownTop, shownLength, goalTop, goalLength, fromTop, fromLength, morphStart;
    private bool morphing, following;
    private const double MorphSeconds = .42;
    internal ScrollViewer? Active => active;
    internal Border Thumb => thumb;

    public DividerScroll()
    {
        Width = 14; HorizontalAlignment = HorizontalAlignment.Left; ClipToBounds = false;
        line.SetResourceReference(Shape.FillProperty, "Line"); thumb.SetResourceReference(Border.BackgroundProperty, "Muted");
        SetLeft(line, 7); SetLeft(thumb, 5); SetLeft(grip, 0);
        Children.Add(line); Children.Add(thumb); Children.Add(grip);
        SizeChanged += (_, _) => { line.Height = ActualHeight; Place(); };
        idle.Tick += (_, _) => { if (!grip.IsMouseCaptured && !grip.IsMouseOver) Show(false); };
        grip.MouseEnter += (_, _) => { if (active is not null && active.ScrollableHeight > 0) Show(true); };
        grip.MouseLeave += (_, _) => { if (!grip.IsMouseCaptured) idle.Start(); };
        grip.MouseLeftButtonDown += (_, e) =>
        {
            if (active is null) return;
            dragStart = e.GetPosition(this).Y; offsetStart = active.VerticalOffset; grip.CaptureMouse(); e.Handled = true;
        };
        grip.MouseMove += (_, e) =>
        {
            if (!grip.IsMouseCaptured || active is null || active.ExtentHeight <= 0) return;
            double perPixel = active.ExtentHeight / Math.Max(1, active.ViewportHeight);
            active.ScrollToVerticalOffset(Math.Clamp(offsetStart + (e.GetPosition(this).Y - dragStart) * perPixel, 0, active.ScrollableHeight));
        };
        grip.MouseLeftButtonUp += (_, _) => { grip.ReleaseMouseCapture(); idle.Start(); };
        Unloaded += (_, _) => { idle.Stop(); Follow(false); };
    }

    // The scroll area keeps scrolling by wheel and keys; only its own bar is hidden.
    internal void Track(ScrollViewer viewer)
    {
        viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        viewer.ScrollChanged += (_, e) =>
        {
            if (e.VerticalChange == 0 && e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0) return;
            if (e.VerticalChange == 0 && active is not null && active != viewer && active.IsVisible) return; // Layout of the other side only.
            // A thumb that is still on screen travels to the other area instead of jumping there.
            if (active != viewer && active is { IsVisible: true } && thumb.Opacity > .02 && shownLength > 0)
            {
                // Starts from wherever the thumb is drawn now, also in the middle of another morph.
                morphing = true; fromTop = shownTop; fromLength = shownLength; morphStart = clock.Elapsed.TotalSeconds;
            }
            active = viewer; Place();
            if (e.VerticalChange != 0) { Show(true); idle.Stop(); idle.Start(); }
        };
        viewer.Unloaded += (_, _) => { if (active == viewer) { active = null; Show(false); } };
    }

    private void Place()
    {
        if (active is null || !active.IsVisible || active.ScrollableHeight <= 0 || active.ExtentHeight <= 0) { goalLength = 0; Draw(goalTop, 0); return; }
        double top; try { top = active.TranslatePoint(new Point(), this).Y; } catch (InvalidOperationException) { return; }
        double span = active.ViewportHeight, length = Math.Max(28, span * span / active.ExtentHeight);
        goalLength = length; goalTop = top + (span - length) * (active.VerticalOffset / active.ScrollableHeight);
        if (morphing) { Follow(true); Frame(null, EventArgs.Empty); } else Draw(goalTop, goalLength);
    }

    // A morph blends from the old shape to the live goal along a soft S-curve over a fixed time.
    // The goal keeps following the scroll position, so scrolling works from the first moment and
    // simply shows up in the blend.
    private void Follow(bool on)
    {
        if (on == following) return;
        following = on;
        if (on) CompositionTarget.Rendering += Frame; else CompositionTarget.Rendering -= Frame;
    }
    private void Frame(object? sender, EventArgs e)
    {
        double t = Math.Clamp((clock.Elapsed.TotalSeconds - morphStart) / MorphSeconds, 0, 1);
        if (t >= 1) { morphing = false; Follow(false); Draw(goalTop, goalLength); return; }
        double p = t * t * t * (t * (t * 6 - 15) + 10);
        Draw(fromTop + (goalTop - fromTop) * p, fromLength + (goalLength - fromLength) * p);
    }
    private void Draw(double top, double length)
    {
        shownTop = top; shownLength = length;
        thumb.Height = length; SetTop(thumb, top); grip.Height = length > 0 ? length + 12 : 0; SetTop(grip, top - 6);
    }

    private void Show(bool visible)
    {
        if (visible && thumb.Height <= 0) return;
        thumb.BeginAnimation(OpacityProperty, new DoubleAnimation(visible ? 1 : 0, TimeSpan.FromSeconds(visible ? .16 : .45)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}

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
    // Where the thumb is drawn, and where its scroll area wants it.
    private double shownTop, shownLength, goalTop, goalLength, lastFrame;
    private bool morphing, following;
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
            if (active != viewer) morphing = active is { IsVisible: true } && thumb.Opacity > .02;
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
        if (morphing && shownLength > 0) Follow(true); else Draw(goalTop, goalLength);
    }

    // During a morph the drawn thumb eases towards the goal every frame, so it also keeps up with
    // scrolling that continues meanwhile; once there, it follows the scroll directly again.
    private void Follow(bool on)
    {
        if (on == following) return;
        following = on; lastFrame = clock.Elapsed.TotalSeconds;
        if (on) CompositionTarget.Rendering += Frame; else CompositionTarget.Rendering -= Frame;
    }
    private void Frame(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds, dt = Math.Min(.05, now - lastFrame); lastFrame = now;
        double k = 1 - Math.Exp(-dt / .07);
        double top = shownTop + (goalTop - shownTop) * k, length = shownLength + (goalLength - shownLength) * k;
        if (Math.Abs(goalTop - top) < .3 && Math.Abs(goalLength - length) < .3) { morphing = false; Follow(false); Draw(goalTop, goalLength); return; }
        Draw(top, length);
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

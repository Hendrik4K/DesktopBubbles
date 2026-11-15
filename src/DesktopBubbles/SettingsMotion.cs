using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DesktopBubbles.Core;

namespace DesktopBubbles;

// Small motions for the management window: everything glides, fades or follows instead of jumping,
// and hover states reverse from wherever they are when the mouse changes its mind.
internal static class SettingsMotion
{
    internal static bool Enabled(App app) => app.State.Settings.Animations && SystemParameters.ClientAreaAnimation;

    // Animates towards a value from wherever the property is now, so a reversal mid-way is smooth.
    internal static void FadeTo(UIElement element, double opacity, double seconds)
        => element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromSeconds(seconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

    internal static void Glide(TranslateTransform shift, DependencyProperty axis, double from, double to, Spring spring)
    {
        double duration = spring.SettleTime;
        shift.BeginAnimation(axis, new DoubleAnimation(from, to, TimeSpan.FromSeconds(duration)) { EasingFunction = new SpringEasing(spring, duration) { EasingMode = EasingMode.EaseIn } });
    }

    // A rounded track with an accent pill that slides to the chosen segment. The segments stay
    // plain buttons (same names and texts as before) for keyboard, automation and tests.
    internal static Grid Segments(string[] labels, int selected, int previous, Action<int> choose, Func<int, string>? tooltip = null)
    {
        var track = new Grid { Height = 40 };
        track.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = Ui.Resource("ControlFill"), BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(1) });
        var shift = new TranslateTransform();
        var pill = new Border { CornerRadius = new CornerRadius(11), Background = Ui.Resource("Accent"), Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = shift,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = .18, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance } };
        track.Children.Add(pill);
        // Dark labels everywhere; a white copy is cut out in the pill's exact shape and travels with
        // it, so every letter is white precisely where the pill is, also mid-slide.
        var row = new Grid(); track.Children.Add(row);
        var lit = new Grid { IsHitTestVisible = false }; track.Children.Add(lit);
        var window = new RectangleGeometry { RadiusX = 11, RadiusY = 11, Transform = shift }; lit.Clip = window;
        for (int i = 0; i < labels.Length; i++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition()); lit.ColumnDefinitions.Add(new ColumnDefinition());
            int index = i;
            var segment = new Button { Style = (Style)System.Windows.Application.Current.FindResource("PlainButton"), Content = labels[i], HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            segment.SetResourceReference(Control.ForegroundProperty, "Ink");
            if (tooltip is not null) segment.ToolTip = tooltip(i);
            System.Windows.Automation.AutomationProperties.SetName(segment, labels[i]);
            segment.Click += (_, _) => { if (index != selected) choose(index); };
            Grid.SetColumn(segment, i); row.Children.Add(segment);
            var white = new TextBlock { Text = labels[i], Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(white, i); lit.Children.Add(white);
        }
        bool placed = false;
        track.SizeChanged += (_, _) =>
        {
            double width = Math.Max(0, (track.ActualWidth - 8) / labels.Length); pill.Width = width;
            window.Rect = new Rect(4, 4, width, Math.Max(0, track.ActualHeight - 8));
            if (placed) { shift.BeginAnimation(TranslateTransform.XProperty, null); shift.X = selected * width; return; }
            placed = true;
            if (previous >= 0 && previous != selected) Glide(shift, TranslateTransform.XProperty, previous * width, selected * width, new Spring(.38, .86));
            else shift.X = selected * width;
        };
        return track;
    }

    // Scroll bars only while the mouse is over the list they scroll.
    internal static void AutoHideScrollBar(ScrollViewer viewer)
    {
        void Apply(double opacity, double seconds)
        {
            if (viewer.Template?.FindName("PART_VerticalScrollBar", viewer) is ScrollBar bar) FadeTo(bar, opacity, seconds);
            if (viewer.Template?.FindName("PART_HorizontalScrollBar", viewer) is ScrollBar wide) FadeTo(wide, opacity, seconds);
        }
        viewer.Loaded += (_, _) => { viewer.ApplyTemplate(); Apply(viewer.IsMouseOver ? 1 : 0, 0); };
        viewer.MouseEnter += (_, _) => Apply(1, .18);
        viewer.MouseLeave += (_, _) => Apply(0, .35);
    }

    // Content slides out softly at the top and bottom edge instead of being cut off. Each fade
    // grows with the distance to its end, so arriving at the top or bottom eases the edge back to
    // solid instead of snapping it.
    internal static void EdgeFade(ScrollViewer viewer, double fade = 26)
    {
        GradientStop top = new(Colors.Black, 0), topInner = new(Colors.Black, 0), bottomInner = new(Colors.Black, 1), bottom = new(Colors.Black, 1);
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        mask.GradientStops.Add(top); mask.GradientStops.Add(topInner); mask.GradientStops.Add(bottomInner); mask.GradientStops.Add(bottom);
        byte Alpha(double distance) { double k = Math.Clamp(distance / fade, 0, 1); k = k * k * (3 - 2 * k); return (byte)Math.Round(255 * (1 - k)); }
        void Update()
        {
            double height = viewer.ActualHeight; if (height <= 0) return;
            double edge = Math.Min(.35, fade / height);
            topInner.Offset = edge; bottomInner.Offset = 1 - edge;
            top.Color = Color.FromArgb(Alpha(viewer.VerticalOffset), 0, 0, 0);
            bottom.Color = Color.FromArgb(Alpha(viewer.ScrollableHeight - viewer.VerticalOffset), 0, 0, 0);
            if (viewer.OpacityMask != mask) viewer.OpacityMask = mask;
        }
        viewer.ScrollChanged += (_, _) => Update(); viewer.SizeChanged += (_, _) => Update();
    }

    // The whole row of a slider (from its title down to its value, up to the next title) is its
    // grip. A click moves the thumb there smoothly; holding drags it, and it follows the mouse with
    // a slight, soft delay. The final value snaps to whole steps.
    internal static void SmoothZone(Panel zone, Slider slider)
    {
        slider.IsHitTestVisible = false; slider.IsSnapToTickEnabled = false;
        double target = slider.Value; bool dragging = false, running = false;
        var clock = System.Diagnostics.Stopwatch.StartNew(); double last = 0;
        // Upright sliders (the colour picker's saturation) run from the bottom (minimum) to the top.
        double ValueAt(Point point)
        {
            var at = zone.TranslatePoint(point, slider); const double thumb = 18;
            double share = slider.Orientation == Orientation.Vertical
                ? 1 - (at.Y - thumb / 2) / Math.Max(1, slider.ActualHeight - thumb)
                : (at.X - thumb / 2) / Math.Max(1, slider.ActualWidth - thumb);
            return slider.Minimum + Math.Clamp(share, 0, 1) * (slider.Maximum - slider.Minimum);
        }
        void Frame(object? sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Min(.05, now - last); last = now;
            double next = slider.Value + (target - slider.Value) * (1 - Math.Exp(-dt / .075));
            if (!dragging && Math.Abs(target - next) < (slider.Maximum - slider.Minimum) * .0002)
            {
                CompositionTarget.Rendering -= Frame; running = false;
                slider.Value = Math.Round(target / slider.SmallChange) * slider.SmallChange; return;
            }
            slider.Value = next;
        }
        void Run() { if (running) return; running = true; last = clock.Elapsed.TotalSeconds; CompositionTarget.Rendering += Frame; }
        zone.Background ??= Brushes.Transparent; zone.Cursor = Cursors.Hand;
        zone.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!slider.IsEnabled) return;
            dragging = true; target = ValueAt(e.GetPosition(zone)); zone.CaptureMouse(); Run(); e.Handled = true;
        };
        zone.PreviewMouseMove += (_, e) => { if (dragging) target = ValueAt(e.GetPosition(zone)); };
        zone.PreviewMouseLeftButtonUp += (_, _) => { if (!dragging) return; dragging = false; zone.ReleaseMouseCapture(); };
        zone.LostMouseCapture += (_, _) => dragging = false;
        zone.Unloaded += (_, _) => { if (running) { CompositionTarget.Rendering -= Frame; running = false; } };
    }

    // Every drop-down list glides open to its real height and glides closed again, on a classic
    // curve (starts gently, speeds up, brakes softly). The popup is opened and closed from here
    // (not bound to IsDropDownOpen), so it can stay on screen while it folds up. While it is still
    // opening, clicks on the box are dropped: the opening always plays out. Hooked from the combo
    // styles (an EventSetter: WPF only raises Loaded on elements with their own handler). The box
    // reports "closed" only once its popup has closed, so the open state itself is watched, and only
    // while the box is on screen, so no box is kept alive by the watch.
    private sealed class DropWatch { public EventHandler? Changed; public bool Watching; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, DropWatch> smoothed = new();
    private static readonly System.ComponentModel.DependencyPropertyDescriptor openState = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ComboBox.IsDropDownOpenProperty, typeof(ComboBox));
    internal static void SmoothDropDown(ComboBox box)
    {
        if (!smoothed.TryGetValue(box, out var watch)) watch = Hook(box);
        if (watch?.Changed is { } changed && !watch.Watching) { openState.AddValueChanged(box, changed); watch.Watching = true; }
    }
    private static DropWatch? Hook(ComboBox box)
    {
        box.ApplyTemplate();
        if (box.Template?.FindName("PART_Popup", box) is not Popup popup) return null;
        var watch = new DropWatch(); smoothed.Add(box, watch);
        // A template without the named list keeps the plain behaviour: the popup simply follows.
        if (box.Template.FindName("Drop", box) is not Border drop) { popup.SetBinding(Popup.IsOpenProperty, new System.Windows.Data.Binding(nameof(ComboBox.IsDropDownOpen)) { Source = box }); return watch; }
        box.Unloaded += (_, _) => { if (watch.Watching && watch.Changed is { } changed) { openState.RemoveValueChanged(box, changed); watch.Watching = false; } };
        var reveal = new ScaleTransform(1, 0);
        var mask = new RectangleGeometry(new Rect(0, 0, 100000, 1)) { Transform = reveal };
        bool opening = false;
        int generation = 0;
        static bool Animate() => System.Windows.Application.Current is App app && Enabled(app);
        void Glide(double to, double ms, Action done)
        {
            var motion = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            motion.Completed += (_, _) => done();
            reveal.BeginAnimation(ScaleTransform.ScaleYProperty, motion);
        }
        box.PreviewMouseLeftButtonDown += (_, e) => { if (opening) e.Handled = true; };
        void Open()
        {
            int run = ++generation;
            drop.IsHitTestVisible = true;
            if (!Animate()) { drop.Clip = null; popup.IsOpen = true; return; }
            // Closed before the first frame (or still folding up: it continues from where it is).
            if (!popup.IsOpen) { reveal.BeginAnimation(ScaleTransform.ScaleYProperty, null); reveal.ScaleY = 0; }
            drop.Clip = mask; popup.IsOpen = true; opening = true;
            bool started = false;
            void Start()
            {
                if (started || run != generation || drop.ActualHeight <= 0) return;
                started = true; drop.SizeChanged -= Sized;
                Glide(drop.ActualHeight + 2, 380, () => { if (run != generation) return; opening = false; if (box.IsDropDownOpen) drop.Clip = null; });
            }
            void Sized(object? s, SizeChangedEventArgs e) => Start();
            drop.SizeChanged += Sized;
            box.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, Start);
        }
        void Close()
        {
            int run = ++generation; opening = false;
            if (!popup.IsOpen) return;
            if (!Animate()) { popup.IsOpen = false; return; }
            drop.IsHitTestVisible = false;
            if (drop.Clip != mask) { reveal.BeginAnimation(ScaleTransform.ScaleYProperty, null); reveal.ScaleY = drop.ActualHeight + 2; drop.Clip = mask; }
            Glide(0, 280, () => { if (run == generation && !box.IsDropDownOpen) popup.IsOpen = false; });
        }
        watch.Changed = (_, _) => { if (box.IsDropDownOpen) Open(); else Close(); };
        if (box.IsDropDownOpen) popup.IsOpen = true; // Opened before it was hooked.
        return watch;
    }
    internal static BitmapSource? Snapshot(FrameworkElement element)
    {
        if (!element.IsVisible || element.ActualWidth < 1 || element.ActualHeight < 1) return null;
        double scale = VisualTreeHelper.GetDpi(element).DpiScaleX;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        // Exactly the element's own box, 1:1. By default a VisualBrush stretches whatever it draws to
        // fill the rectangle, so a page whose content ended halfway down came out stretched tall.
        var brush = new VisualBrush(element) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, element.ActualWidth, element.ActualHeight) };
        using (var context = visual.RenderOpen()) context.DrawRectangle(brush, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private sealed class Ghost : Adorner
    {
        private readonly BitmapSource picture;
        private readonly Point at;
        // Never spills past the element it covers, also while a window shrinks underneath it.
        public Ghost(UIElement adorned, BitmapSource picture, Point at = default) : base(adorned) { this.picture = picture; this.at = at; ClipToBounds = true; IsHitTestVisible = false; }
        // Drawn at the size it was taken, so a taller or shorter new page does not stretch it.
        protected override void OnRender(DrawingContext drawing) => drawing.DrawImage(picture, new Rect(at.X, at.Y, picture.Width, picture.Height));
    }
    // A pill as tall and round as its circle: the circle at one end, the segment button beside it.
    internal static Border Pill(UIElement circle, Button segment, bool circleRight = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (circleRight) { row.Children.Add(segment); row.Children.Add(circle); } else { row.Children.Add(circle); row.Children.Add(segment); }
        segment.Height = 34; segment.VerticalAlignment = VerticalAlignment.Center;
        var pill = new Border { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(0), Height = 36, Child = row, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        pill.SetResourceReference(Border.BackgroundProperty, "ControlFill"); pill.SetResourceReference(Border.BorderBrushProperty, "Line");
        return pill;
    }

    // Floods the whole pill (also around its circle) with a colour, or hands it back to the theme.
    internal static void TintPill(Border pill, Color? color, bool animate)
    {
        var from = (pill.Background as SolidColorBrush)?.Color ?? Colors.Transparent;
        var to = color ?? ((SolidColorBrush)Ui.Resource("ControlFill")).Color;
        var fill = new SolidColorBrush(from); pill.Background = fill;
        pill.BorderBrush = color is { } solid ? new SolidColorBrush(solid) : Ui.Resource("Line");
        var flow = new ColorAnimation(to, TimeSpan.FromMilliseconds(animate ? 240 : 0));
        if (color is null) flow.Completed += (_, _) => { if (pill.Background == fill) { pill.SetResourceReference(Border.BackgroundProperty, "ControlFill"); pill.SetResourceReference(Border.BorderBrushProperty, "Line"); } };
        fill.BeginAnimation(SolidColorBrush.ColorProperty, flow);
    }

    // The label fades out, changes and fades back in; the button keeps its size.
    internal static void SwapContent(Button button, string text)
    {
        if (Equals(button.Content, text)) return;
        button.Content = text; // State changes at once (callers and tests read it); only the look eases.
        if (!button.IsLoaded || button.Template?.FindName("Text", button) is not UIElement presenter) return;
        presenter.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // The label turns around its vertical axis like a card: it narrows to an edge, the text changes
    // and it opens again. Clockwise leans it one way on the way, counter-clockwise the other.
    internal static void FlipContent(Button button, string text, bool clockwise)
    {
        if (!button.IsLoaded || button.Template?.FindName("Text", button) is not FrameworkElement face) { button.Content = text; return; }
        var turn = new ScaleTransform(1, 1); var lean = new SkewTransform(0, 0);
        face.RenderTransformOrigin = new Point(.5, .5); face.RenderTransform = new TransformGroup { Children = { lean, turn } };
        double tilt = clockwise ? 14 : -14;
        var close = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } };
        close.Completed += (_, _) =>
        {
            button.Content = text;
            turn.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
            lean.BeginAnimation(SkewTransform.AngleYProperty, new DoubleAnimation(-tilt, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        };
        turn.BeginAnimation(ScaleTransform.ScaleXProperty, close);
        lean.BeginAnimation(SkewTransform.AngleYProperty, new DoubleAnimation(0, tilt, TimeSpan.FromMilliseconds(140)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } });
    }

    // Page change in two beats, like the bubble hand-over: the old page fades out completely, only
    // then the new one fades in. The old page's picture is pinned on "stage", an element already on
    // screen, exactly where the old page was ("at"), and the new page is held invisible from the
    // start. The clock only starts with the first frame after the new page has been built, so a
    // heavy build (liquid glass, folder icons) neither shows an empty frame nor eats the fade.
    internal const double PageOut = .22, PageIn = .3;
    internal static void PageTransition(FrameworkElement stage, Point at, BitmapSource? before, UIElement arriving)
    {
        arriving.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0, TimeSpan.Zero));
        var layer = before is null ? null : AdornerLayer.GetAdornerLayer(stage);
        Ghost? ghost = null;
        if (layer is not null) { ghost = new Ghost(stage, before!, at); layer.Add(ghost); }
        bool begun = false;
        void Begin()
        {
            if (begun) return;
            begun = true; CompositionTarget.Rendering -= First;
            double wait = ghost is null ? 0 : PageOut;
            var appear = new DoubleAnimationUsingKeyFrames();
            appear.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            appear.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(wait))));
            appear.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(wait + PageIn)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            appear.Completed += (_, _) => arriving.BeginAnimation(UIElement.OpacityProperty, null);
            arriving.BeginAnimation(UIElement.OpacityProperty, appear);
            if (ghost is null) return;
            var vanish = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(PageOut)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            vanish.Completed += (_, _) => layer!.Remove(ghost);
            ghost.BeginAnimation(UIElement.OpacityProperty, vanish);
        }
        void First(object? sender, EventArgs e) => Begin();
        CompositionTarget.Rendering += First;
        // Should no frame come (hidden window), the hand-over still completes.
        var fallback = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        fallback.Tick += (_, _) => { fallback.Stop(); Begin(); }; fallback.Start();
    }
    // The same two beats with live elements instead of a picture: "leaving" fades out first, then
    // "arriving" fades in. Both stay visible (and readable for code) throughout; whoever is left
    // over is only hidden once it has faded out completely.
    internal static void Handover(UIElement leaving, UIElement arriving, Action? left = null)
    {
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(PageOut)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fadeOut.Completed += (_, _) => { left?.Invoke(); leaving.BeginAnimation(UIElement.OpacityProperty, null); };
        leaving.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        var appear = new DoubleAnimationUsingKeyFrames();
        appear.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        appear.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PageOut))));
        appear.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(PageOut + PageIn)), new CubicEase { EasingMode = EasingMode.EaseOut }));
        appear.Completed += (_, _) => arriving.BeginAnimation(UIElement.OpacityProperty, null);
        arriving.BeginAnimation(UIElement.OpacityProperty, appear);
    }

    // The mouse wheel glides: each notch adds to a target the offset eases towards.
    internal static void SmoothWheel(ScrollViewer viewer)
    {
        double target = 0; bool running = false; var clock = System.Diagnostics.Stopwatch.StartNew(); double last = 0;
        void Frame(object? sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Min(.05, now - last); last = now;
            double next = viewer.VerticalOffset + (target - viewer.VerticalOffset) * (1 - Math.Exp(-dt / .09));
            if (Math.Abs(target - next) < .5) { viewer.ScrollToVerticalOffset(target); CompositionTarget.Rendering -= Frame; running = false; return; }
            viewer.ScrollToVerticalOffset(next);
        }
        viewer.PreviewMouseWheel += (_, e) =>
        {
            if (viewer.ScrollableHeight <= 0) return;
            if (!running) target = viewer.VerticalOffset;
            target = Math.Clamp(target - e.Delta * .6, 0, viewer.ScrollableHeight); e.Handled = true;
            if (!running) { running = true; last = clock.Elapsed.TotalSeconds; CompositionTarget.Rendering += Frame; }
        };
        viewer.Unloaded += (_, _) => { if (running) { CompositionTarget.Rendering -= Frame; running = false; } };
    }

    // No hover background: the content swells a little instead.
    internal static void Swell(FrameworkElement element, double factor = 1.18)
    {
        var scale = new ScaleTransform(1, 1); element.RenderTransformOrigin = new Point(.5, .5); element.RenderTransform = scale;
        void To(double value, double seconds)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(value, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(value, TimeSpan.FromSeconds(seconds)) { EasingFunction = ease });
        }
        element.MouseEnter += (_, _) => To(factor, .18);
        element.MouseLeave += (_, _) => To(1, .26);
    }

    // The same closed-form spring as the folder motion, as a WPF easing curve.
    internal sealed class SpringEasing(Spring spring, double duration) : EasingFunctionBase
    {
        protected override double EaseInCore(double t) => t >= 1 ? 1 : spring.Value(t * duration);
        protected override Freezable CreateInstanceCore() => new SpringEasing(spring, duration);
    }
}

// Moves and resizes a window with one native call per frame, so all four edges travel together.
// (Separate WPF animations of Top and Height make the edges tremble against each other.)
// Starts gently, gathers speed and brakes softly.
internal sealed class WindowGlide(Window window)
{
    private readonly System.Diagnostics.Stopwatch clock = new();
    private PixelRect from, to;
    private double seconds;
    internal bool Running { get; private set; }
    internal PixelRect Target => to;
    internal void To(PixelRect target, double duration, bool animate)
    {
        from = Native.Bounds(window); to = target; seconds = duration;
        if (!animate || duration <= 0) { Stop(); Apply(target); return; }
        clock.Restart();
        if (!Running) { Running = true; CompositionTarget.Rendering += Frame; }
    }
    internal void Stop() { if (!Running) return; Running = false; CompositionTarget.Rendering -= Frame; }
    private void Frame(object? sender, EventArgs e)
    {
        double t = Math.Clamp(clock.Elapsed.TotalSeconds / seconds, 0, 1);
        double p = t * t * t * (t * (t * 6 - 15) + 10); // Smootherstep: no jolt at either end.
        Apply(new PixelRect(from.X + (to.X - from.X) * p, from.Y + (to.Y - from.Y) * p, from.Width + (to.Width - from.Width) * p, from.Height + (to.Height - from.Height) * p));
        if (t >= 1) Stop();
    }
    // Edges are rounded, not sizes, so the far edges never wobble by a pixel.
    private void Apply(PixelRect r)
    {
        int left = (int)Math.Round(r.X), top = (int)Math.Round(r.Y), right = (int)Math.Round(r.Right), bottom = (int)Math.Round(r.Bottom);
        Native.SetWindowPos(Native.Handle(window), 0, left, top, right - left, bottom - top, Native.NoActivate | 4);
    }
}

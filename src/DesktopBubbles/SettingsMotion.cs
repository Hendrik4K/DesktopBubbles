using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

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

    // Content slides out softly at the top and bottom edge instead of being cut off.
    internal static void EdgeFade(ScrollViewer viewer, double fade = 26)
    {
        void Update()
        {
            double height = viewer.ActualHeight; if (height <= 0) return;
            double edge = Math.Min(.35, fade / height);
            bool above = viewer.VerticalOffset > .5, below = viewer.VerticalOffset < viewer.ScrollableHeight - .5;
            var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            mask.GradientStops.Add(new GradientStop(above ? Colors.Transparent : Colors.Black, 0));
            mask.GradientStops.Add(new GradientStop(Colors.Black, edge));
            mask.GradientStops.Add(new GradientStop(Colors.Black, 1 - edge));
            mask.GradientStops.Add(new GradientStop(below ? Colors.Transparent : Colors.Black, 1));
            mask.Freeze(); viewer.OpacityMask = mask;
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
        double ValueAt(Point point)
        {
            var x = zone.TranslatePoint(point, slider).X; double thumb = 18, usable = Math.Max(1, slider.ActualWidth - thumb);
            return slider.Minimum + Math.Clamp((x - thumb / 2) / usable, 0, 1) * (slider.Maximum - slider.Minimum);
        }
        void Frame(object? sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Min(.05, now - last); last = now;
            double next = slider.Value + (target - slider.Value) * (1 - Math.Exp(-dt / .075));
            if (!dragging && Math.Abs(target - next) < .02)
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

    // The old picture fades out over the new one, which fades in: a soft cross-dissolve.
    internal static void Crossfade(FrameworkElement element, BitmapSource? before)
    {
        if (before is null || AdornerLayer.GetAdornerLayer(element) is not { } layer) return;
        var ghost = new Ghost(element, before); layer.Add(ghost);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        fade.Completed += (_, _) => layer.Remove(ghost);
        ghost.BeginAnimation(UIElement.OpacityProperty, fade);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    internal static BitmapSource? Snapshot(FrameworkElement element)
    {
        if (!element.IsVisible || element.ActualWidth < 1 || element.ActualHeight < 1) return null;
        double scale = VisualTreeHelper.GetDpi(element).DpiScaleX;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen()) context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private sealed class Ghost(UIElement adorned, BitmapSource picture) : Adorner(adorned)
    {
        // Drawn at the size it was taken, so a taller or shorter new page does not stretch it.
        protected override void OnRender(DrawingContext drawing) => drawing.DrawImage(picture, new Rect(0, 0, picture.Width, picture.Height));
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

    // The label fades out, changes and fades back in; the button keeps its size.
    internal static void SwapContent(Button button, string text)
    {
        if (Equals(button.Content, text)) return;
        button.Content = text; // State changes at once (callers and tests read it); only the look eases.
        if (!button.IsLoaded || button.Template?.FindName("Text", button) is not UIElement presenter) return;
        presenter.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // Page change in two beats: the old page dissolves into a growing Gaussian blur first, then the
    // new one sharpens in. Nothing of the old page lingers over the new one.
    internal static void PageTransition(FrameworkElement host, BitmapSource? before)
    {
        if (AdornerLayer.GetAdornerLayer(host) is not { } layer) return;
        const double outMs = 190, inMs = 300, blur = 12;
        var sharpen = new System.Windows.Media.Effects.BlurEffect { Radius = blur, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        host.Effect = sharpen;
        double wait = before is null ? 0 : outMs * .8;
        var radius = new DoubleAnimationUsingKeyFrames();
        radius.KeyFrames.Add(new DiscreteDoubleKeyFrame(blur, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        radius.KeyFrames.Add(new DiscreteDoubleKeyFrame(blur, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait))));
        radius.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + inMs)), new CubicEase { EasingMode = EasingMode.EaseOut }));
        sharpen.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, radius);
        var appear = new DoubleAnimationUsingKeyFrames();
        appear.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        appear.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait))));
        appear.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(wait + inMs)), new CubicEase { EasingMode = EasingMode.EaseOut }));
        appear.Completed += (_, _) => { if (host.Effect == sharpen) host.Effect = null; host.BeginAnimation(UIElement.OpacityProperty, null); };
        host.BeginAnimation(UIElement.OpacityProperty, appear);
        if (before is null) return;
        var smear = new System.Windows.Media.Effects.BlurEffect { Radius = 0, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        var ghost = new Ghost(host, before) { Effect = smear, IsHitTestVisible = false };
        layer.Add(ghost);
        smear.BeginAnimation(System.Windows.Media.Effects.BlurEffect.RadiusProperty, new DoubleAnimation(0, blur, TimeSpan.FromMilliseconds(outMs)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        var vanish = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(outMs)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        vanish.Completed += (_, _) => layer.Remove(ghost);
        ghost.BeginAnimation(UIElement.OpacityProperty, vanish);
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

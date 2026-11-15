using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopBubbles.Core;

namespace DesktopBubbles;

// A smooth CPU curve over the last 20 seconds. It glides to the left continuously, new values enter
// softly from the right edge and old ones fade out at the left. Display only: the averages below
// still cover the whole run.
internal sealed class CpuTrace : FrameworkElement
{
    private const double Span = 20;
    private readonly List<(double Time, double Value)> points = [];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double shownMax = 1, frozenAt = double.NaN, lastFrame;
    private bool rendering;
    private readonly Func<bool> animate;
    private double Now => double.IsNaN(frozenAt) ? clock.Elapsed.TotalSeconds : frozenAt;
    public CpuTrace(Func<bool> animate)
    {
        this.animate = animate; ClipToBounds = true;
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0)); mask.GradientStops.Add(new GradientStop(Colors.Black, .35)); mask.Freeze();
        OpacityMask = mask;
        IsVisibleChanged += (_, _) => Run();
        Unloaded += (_, _) => { if (rendering) { CompositionTarget.Rendering -= Frame; rendering = false; } };
    }
    public double Latest => points.Count > 0 ? points[^1].Value : 0;
    public void Add(double value)
    {
        if (points.Count == 0) BeginAnimation(OpacityProperty, animate() ? new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) : null); // The curve starts softly.
        frozenAt = double.NaN; points.Add((clock.Elapsed.TotalSeconds, value));
        points.RemoveAll(p => clock.Elapsed.TotalSeconds - p.Time > Span + 3);
        Run(); InvalidateVisual();
    }
    public void Clear() { points.Clear(); frozenAt = double.NaN; shownMax = 1; Run(); InvalidateVisual(); }
    public void Hold() { if (double.IsNaN(frozenAt)) frozenAt = clock.Elapsed.TotalSeconds; Run(); }
    private void Run()
    {
        bool want = IsVisible && double.IsNaN(frozenAt) && points.Count > 0 && animate();
        if (want == rendering) return;
        rendering = want; lastFrame = clock.Elapsed.TotalSeconds;
        if (want) CompositionTarget.Rendering += Frame; else CompositionTarget.Rendering -= Frame;
    }
    private void Frame(object? sender, EventArgs e) => InvalidateVisual();
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing); if (points.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;
        double now = Now, dt = Math.Clamp(clock.Elapsed.TotalSeconds - lastFrame, 0, .1); lastFrame = clock.Elapsed.TotalSeconds;
        double target = Math.Max(.5, points.Where(p => now - p.Time <= Span + 1).Select(p => p.Value).DefaultIfEmpty(0).Max() * 1.15);
        // The scale eases instead of jumping when a peak enters or leaves the window.
        shownMax = rendering ? shownMax + (target - shownMax) * (1 - Math.Exp(-dt / .35)) : target;
        // One second behind real time, so the newest value glides in from beyond the right edge.
        Point At((double Time, double Value) p) => new(ActualWidth - (now - p.Time - 1) / Span * ActualWidth, ActualHeight - 3 - Math.Min(1, p.Value / shownMax) * (ActualHeight - 6));
        var at = points.Select(At).ToArray();
        var path = new StreamGeometry();
        using (var context = path.Open())
        {
            context.BeginFigure(at[0], false, false);
            for (int i = 0; i < at.Length - 1; i++)
            {
                // Catmull-Rom through the samples, flattened a little so the line reads calm.
                Point before = at[Math.Max(0, i - 1)], from = at[i], to = at[i + 1], after = at[Math.Min(at.Length - 1, i + 2)];
                const double k = 1 / 6.5;
                var c1 = new Point(from.X + (to.X - before.X) * k, from.Y + (to.Y - before.Y) * k);
                var c2 = new Point(to.X - (after.X - from.X) * k, to.Y - (after.Y - from.Y) * k);
                context.BezierTo(c1, c2, to, true, true);
            }
        }
        path.Freeze();
        var pen = new Pen(Ui.Resource("Accent"), 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze();
        drawing.DrawGeometry(null, pen, path);
    }
}

// A small speedometer: arc, ticks and a needle that follows the CPU load while tracking runs.
internal sealed class Gauge : Canvas
{
    private readonly RotateTransform needle = new(-50, 12, 17);
    public Gauge()
    {
        Width = 24; Height = 20; SnapsToDevicePixels = false;
        var arc = new Path { Data = Geometry.Parse("M 2.5,17 A 9.5,9.5 0 0 1 21.5,17 M 2.5,17 H 4.6 M 21.5,17 H 19.4") };
        Ink(arc); Children.Add(arc);
        foreach (double degrees in new[] { 150d, 120, 90, 60, 30 })
        {
            double r = degrees * Math.PI / 180, cos = Math.Cos(r), sin = Math.Sin(r);
            var tick = new Line { X1 = 12 + cos * 9.5, Y1 = 17 - sin * 9.5, X2 = 12 + cos * 7.4, Y2 = 17 - sin * 7.4 }; Ink(tick); Children.Add(tick);
        }
        var hand = new Path { Data = Geometry.Parse("M 11.1,16.2 L 12,9.2 L 12.9,16.2 Z"), RenderTransform = needle };
        hand.SetResourceReference(Shape.FillProperty, "Ink"); Children.Add(hand);
        var hub = new Ellipse { Width = 3.6, Height = 3.6 }; SetLeft(hub, 10.2); SetTop(hub, 15.2); Ink(hub); Children.Add(hub);
    }
    private static void Ink(Shape shape)
    {
        shape.SetResourceReference(Shape.StrokeProperty, "Ink"); shape.StrokeThickness = 1.5;
        shape.StrokeStartLineCap = shape.StrokeEndLineCap = PenLineCap.Round; shape.StrokeLineJoin = PenLineJoin.Round;
    }
    // 0..1 maps to the arc from left to right; the needle swings there with a soft spring.
    public void Aim(double share, bool animate)
    {
        double angle = -62 + 124 * Math.Clamp(share, 0, 1);
        if (!animate) { needle.BeginAnimation(RotateTransform.AngleProperty, null); needle.Angle = angle; return; }
        needle.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, TimeSpan.FromMilliseconds(700)) { EasingFunction = new BackEase { Amplitude = .3, EasingMode = EasingMode.EaseOut } });
    }
}

// Bottom bar next to "Neu": a speedometer with an arrow. A click folds out the curve, the values and
// play/stop to the left of it, pushing the speedometer to the right; the arrow turns to fold it back.
internal sealed class LiveMeasurementBar : Grid, IDisposable
{
    private readonly Window window;
    private readonly Func<bool> animate;
    private Process? process;
    private readonly Stopwatch clock = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CpuSeries series = new(Environment.ProcessorCount);
    private readonly TextBlock cpu = Value(), ram = Value(), duration = Value();
    private readonly CpuTrace trace;
    private readonly Gauge gauge = new();
    private readonly Button toggle, expander;
    private readonly TextBlock toggleIcon = Ui.Symbol("\uE768", 13), arrow = Ui.Symbol("\uE76C", 12);
    private readonly Ellipse dot = new() { Width = 7, Height = 7, Margin = new Thickness(5, 1, 1, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border drawer;
    private readonly StackPanel drawerContent = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly RotateTransform arrowTurn = new(0);
    private bool requested, disposed, open;
    public int SampleCount => series.Samples;
    public bool IsSampling => timer.IsEnabled;
    public bool IsTracking => requested;
    public bool IsOpen => open;
    internal Button TrackingButton => toggle;
    internal Button Expander => expander;
    public LiveMeasurementBar(Window window, Func<bool> animate)
    {
        this.window = window; this.animate = animate; Margin = new Thickness(10, 10, 6, 10);
        trace = new CpuTrace(animate) { Width = 76, Height = 34, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; Children.Add(row);
        // Curve and values first, play/stop right next to the speedometer.
        drawerContent.Children.Add(trace);
        drawerContent.Children.Add(Metric("CPU Ø", cpu, 64)); drawerContent.Children.Add(Metric("RAM", ram, 68));
        var time = new StackPanel { Orientation = Orientation.Horizontal }; time.Children.Add(duration); time.Children.Add(dot);
        var timeBox = new StackPanel { Width = 62, VerticalAlignment = VerticalAlignment.Center }; timeBox.Children.Add(Ui.Text(" ", 11, "Muted")); timeBox.Children.Add(time); duration.Margin = new Thickness(0, 2, 0, 0);
        drawerContent.Children.Add(timeBox);
        toggle = new Button { Style = (Style)System.Windows.Application.Current.FindResource("PlainButton"), Width = 32, Height = 32, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
        var round = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Child = toggleIcon };
        toggleIcon.HorizontalAlignment = HorizontalAlignment.Center; toggle.Content = round;
        toggle.Click += (_, _) => Toggle();
        drawerContent.Children.Add(toggle);
        // The drawer's content hangs on its right edge, so it is revealed from the speedometer outwards.
        drawer = new Border { Child = drawerContent, Width = 0, ClipToBounds = true, Opacity = 0, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(drawer);
        // Speedometer and arrow form one control; on hover a soft white glow and the full ink colour
        // fade in, and fade back out when the mouse leaves.
        arrow.Foreground = arrowInk; arrow.RenderTransformOrigin = new Point(.5, .5); arrow.RenderTransform = arrowTurn; arrow.Margin = new Thickness(6, 0, 0, 0);
        var face = new StackPanel { Orientation = Orientation.Horizontal }; face.Children.Add(gauge); face.Children.Add(arrow);
        var glow = new Border { CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)), Opacity = 0, Margin = new Thickness(-8, -7, -8, -7), IsHitTestVisible = false };
        var plate = new Grid(); plate.Children.Add(glow); plate.Children.Add(face);
        expander = new Button { Style = (Style)System.Windows.Application.Current.FindResource("PlainButton"), Content = plate, Padding = new Thickness(12, 9, 10, 9), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Verbrauch messen" };
        expander.Click += (_, _) => Fold(!open);
        expander.MouseEnter += (_, _) => { SettingsMotion.FadeTo(glow, 1, .2); Tint(true); };
        expander.MouseLeave += (_, _) => { SettingsMotion.FadeTo(glow, 0, .35); Tint(false); };
        System.Windows.Automation.AutomationProperties.SetName(expander, "Verbrauchsmessung ein- oder ausklappen");
        row.Children.Add(expander);
        ToolTip = "Manuell gestartetes Ressourcen-Tracking dieses Prozesses: zeitgewichteter CPU-Durchschnitt über alle logischen Prozessoren; RAM = aktueller Working Set. Die Kurve zeigt die letzten 20 Sekunden, die Durchschnittswerte gelten für die ganze Messung. Stoppen hält die letzten Werte fest; erneutes Starten beginnt eine neue Messung. Bei ausgeblendeter/minimierter Verwaltung wird nicht gemessen.";
        SizeChanged += (_, _) => { if (open) Fold(true, instant: true); };
        timer.Tick += (_, _) => Sample();
        window.IsVisibleChanged += VisibilityChanged; window.StateChanged += StateChanged;
        Loaded += (_, _) => { Tint(false, instant: true); UpdateRunning(); };
        System.Windows.Automation.AutomationProperties.SetName(this, "Ressourcen-Tracking von DesktopBubbles");
        UpdateButton();
    }
    private readonly SolidColorBrush arrowInk = new(Colors.Gray);
    private void Tint(bool hot, bool instant = false)
    {
        var color = ((SolidColorBrush)Ui.Resource(hot ? "Ink" : "Muted")).Color;
        if (instant || !animate()) { arrowInk.BeginAnimation(SolidColorBrush.ColorProperty, null); arrowInk.Color = color; return; }
        arrowInk.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(hot ? 200 : 350)));
    }
    // Fixed widths and even-width digits: ticking numbers never push their neighbours around.
    private static TextBlock Value()
    {
        var text = Ui.Text("–", 14, weight: FontWeights.SemiBold); System.Windows.Documents.Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular); return text;
    }
    private static UIElement Metric(string label, TextBlock value, double width)
    {
        var stack = new StackPanel { Width = width, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }; stack.Children.Add(Ui.Text(label, 11, "Muted")); value.Margin = new Thickness(0, 2, 0, 0); stack.Children.Add(value); return stack;
    }
    // Width follows the content; the curve steps aside when the window is too narrow, so nothing
    // (like the status dot) is ever cut off at the edge.
    private double OpenWidth()
    {
        double room = Math.Max(0, ActualWidth - expander.ActualWidth - expander.Margin.Left - 2);
        trace.Visibility = Visibility.Visible; drawerContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (drawerContent.DesiredSize.Width > room) { trace.Visibility = Visibility.Collapsed; drawerContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        return Math.Min(room, Math.Ceiling(drawerContent.DesiredSize.Width));
    }
    internal void Fold(bool show, bool instant = false)
    {
        open = show; double width = show ? OpenWidth() : 0;
        bool smooth = !instant && animate() && IsLoaded;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut }; var time = TimeSpan.FromMilliseconds(440);
        if (!smooth)
        {
            drawer.BeginAnimation(WidthProperty, null); drawer.Width = width; drawer.BeginAnimation(OpacityProperty, null); drawer.Opacity = show ? 1 : 0;
            arrowTurn.BeginAnimation(RotateTransform.AngleProperty, null); arrowTurn.Angle = show ? 180 : 0; return;
        }
        drawer.BeginAnimation(WidthProperty, new DoubleAnimation(width, time) { EasingFunction = ease });
        drawer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(show ? 380 : 240)) { BeginTime = TimeSpan.FromMilliseconds(show ? 80 : 0) });
        arrowTurn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(show ? 180 : 0, time) { EasingFunction = ease });
    }    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateRunning();
    private void StateChanged(object? sender, EventArgs e) => UpdateRunning();
    internal void Toggle()
    {
        if (disposed) return;
        if (requested)
        {
            if (timer.IsEnabled) Sample();
            requested = false; UpdateRunning(); clock.Stop(); trace.Hold();
            gauge.Aim(.1, animate());
        }
        else
        {
            process ??= Process.GetCurrentProcess(); series = new CpuSeries(Environment.ProcessorCount); clock.Restart();
            cpu.Text = ram.Text = "–"; duration.Text = "00:00";
            trace.Clear(); requested = true; UpdateRunning();
        }
    }
    private void UpdateButton()
    {
        string name = requested ? "Tracking stoppen" : "Tracking starten";
        System.Windows.Automation.AutomationProperties.SetName(toggle, name); toggle.ToolTip = name;
        string glyph = requested ? "" : "";
        if (toggleIcon.Text != glyph)
        {
            toggleIcon.Text = glyph; // Play becomes stop and back with a short fade.
            if (animate() && IsLoaded) toggleIcon.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
        }
        var tone = requested ? Color.FromRgb(0xD9, 0x3B, 0x3B) : Color.FromRgb(0x24, 0xA8, 0x5A);
        toggleIcon.Foreground = new SolidColorBrush(tone);
        ((Border)toggle.Content).Background = new SolidColorBrush(Color.FromArgb(0x2C, tone.R, tone.G, tone.B));
        dot.Fill = timer.IsEnabled ? Ui.Brush("#32BC68") : Ui.Resource("Muted");
    }
    private void UpdateRunning()
    {
        if (disposed) return;
        bool run = requested && window.IsVisible && window.WindowState != WindowState.Minimized;
        if (run && !timer.IsEnabled) { series.Break(); Sample(); if (requested) timer.Start(); }
        if (!run && timer.IsEnabled) { timer.Stop(); series.Break(); }
        UpdateButton();
    }
    private void Sample()
    {
        if (disposed || !requested || process is null) return;
        try
        {
            process.Refresh();
            bool fresh = series.Observe(clock.Elapsed.TotalSeconds, process.TotalProcessorTime.TotalSeconds, process.WorkingSet64);
            cpu.Text = series.Samples > 0 ? $"{series.Mean:0.00} %" : "–"; ram.Text = $"{series.WorkingSetBytes / 1048576d:0} MiB";
            var time = TimeSpan.FromSeconds(series.ActiveSeconds); duration.Text = time.TotalHours >= 1 ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}" : time.ToString(@"mm\:ss");
            if (fresh)
            {
                trace.Add(series.Current);
                double peak = Math.Max(2, series.History.DefaultIfEmpty(0).TakeLast(20).Max() * 1.2);
                gauge.Aim(series.Current / peak, animate());
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        { requested = false; timer.Stop(); UpdateButton(); Log.Write(ex.Message); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; timer.Stop(); clock.Stop(); window.IsVisibleChanged -= VisibilityChanged; window.StateChanged -= StateChanged; process?.Dispose();
    }
}

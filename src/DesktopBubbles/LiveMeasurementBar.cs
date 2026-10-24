using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal sealed class CpuTrace : FrameworkElement
{
    public IReadOnlyCollection<double> Values { get; set; } = Array.Empty<double>();
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing); if (Values.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;
        var values = Values.ToArray(); double max = Math.Max(.1, values.Max()), step = ActualWidth / (values.Length - 1);
        var path = new StreamGeometry();
        using (var context = path.Open())
        {
            context.BeginFigure(new Point(0, ActualHeight - 2 - values[0] / max * (ActualHeight - 4)), false, false);
            for (int i = 1; i < values.Length; i++) context.LineTo(new Point(i * step, ActualHeight - 2 - values[i] / max * (ActualHeight - 4)), true, false);
        }
        path.Freeze(); drawing.DrawGeometry(null, new Pen(Ui.Resource("Accent"), 1.5), path);
    }
}

internal sealed class LiveMeasurementBar : Grid, IDisposable
{
    private readonly Window window;
    private Process? process;
    private readonly Stopwatch clock = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CpuSeries series = new(Environment.ProcessorCount);
    private readonly TextBlock cpu = Ui.Text("–", 12), ram = Ui.Text("–", 12), duration = Ui.Text("00:00", 12), spread = Ui.Text("Ressourcen-Tracking", 10, "Muted");
    private readonly CpuTrace trace = new() { Width = 56, Height = 26, Margin = new Thickness(0, 4, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button toggle;
    private readonly System.Windows.Shapes.Ellipse dot = new() { Width = 6, Height = 6, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private bool requested, disposed;
    public int SampleCount => series.Samples;
    public bool IsSampling => timer.IsEnabled;
    public bool IsTracking => requested;
    internal Button TrackingButton => toggle;
    public LiveMeasurementBar(Window window)
    {
        this.window = window; Margin = new Thickness(16, 10, 12, 10);
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Children.Add(trace);
        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; SetColumn(center, 1); Children.Add(center);
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(Metric("CPU Ø", cpu)); line.Children.Add(Metric("RAM", ram));
        duration.VerticalAlignment = VerticalAlignment.Center; line.Children.Add(duration); line.Children.Add(dot); center.Children.Add(line);
        spread.Margin = new Thickness(0, 5, 0, 0); center.Children.Add(spread);
        ToolTip = "Manuell gestartetes Ressourcen-Tracking dieses Prozesses: zeitgewichteter CPU-Durchschnitt über alle logischen Prozessoren; RAM = aktueller Working Set. Schwankung in Prozentpunkten, keine Messgenauigkeit. Stoppen hält die letzten Werte fest; erneutes Starten beginnt eine neue Messung. Bei ausgeblendeter/minimierter Verwaltung wird nicht gemessen.";
        toggle = Ui.Button("Tracking starten", Toggle); toggle.FontSize = 11; toggle.Width = 122; toggle.Padding = new Thickness(8, 10, 8, 10); toggle.Margin = new Thickness(6, 0, 0, 0); toggle.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(toggle, 2); Children.Add(toggle);
        timer.Tick += (_, _) => Sample();
        window.IsVisibleChanged += VisibilityChanged; window.StateChanged += StateChanged;
        Loaded += (_, _) => UpdateRunning();
        System.Windows.Automation.AutomationProperties.SetName(this, "Ressourcen-Tracking von DesktopBubbles");
        UpdateButton();
    }
    private static UIElement Metric(string label, TextBlock value)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; stack.Children.Add(Ui.Text(label, 10, "Muted")); value.Margin = new Thickness(0, 4, 0, 0); stack.Children.Add(value); return stack;
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateRunning();
    private void StateChanged(object? sender, EventArgs e) => UpdateRunning();
    internal void Toggle()
    {
        if (disposed) return;
        if (requested)
        {
            if (timer.IsEnabled) Sample();
            requested = false; UpdateRunning(); clock.Stop();
            spread.Text = "Gestoppt · letzte Werte";
        }
        else
        {
            process ??= Process.GetCurrentProcess(); series = new CpuSeries(Environment.ProcessorCount); clock.Restart();
            cpu.Text = ram.Text = "–"; duration.Text = "00:00"; spread.Text = "Verlauf wird gesammelt";
            trace.Values = Array.Empty<double>(); trace.InvalidateVisual(); requested = true; UpdateRunning();
        }
    }
    private void UpdateButton()
    {
        toggle.Content = requested ? "Tracking stoppen" : "Tracking starten";
        System.Windows.Automation.AutomationProperties.SetName(toggle, (string)toggle.Content);
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
            process.Refresh(); series.Observe(clock.Elapsed.TotalSeconds, process.TotalProcessorTime.TotalSeconds, process.WorkingSet64);
            cpu.Text = series.Samples > 0 ? $"{series.Mean:0.00} %" : "–"; ram.Text = $"{series.WorkingSetBytes / 1048576d:0} MiB";
            var time = TimeSpan.FromSeconds(series.ActiveSeconds); duration.Text = time.TotalHours >= 1 ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}" : time.ToString(@"mm\:ss");
            spread.Text = series.Samples < 2 ? "Verlauf wird gesammelt" : $"Schwankung ±{series.Spread:0.00} %-Pkt.";
            trace.Values = series.History; trace.InvalidateVisual();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        { requested = false; timer.Stop(); spread.Text = "Messung nicht verfügbar"; UpdateButton(); Log.Write(ex.Message); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; timer.Stop(); clock.Stop(); window.IsVisibleChanged -= VisibilityChanged; window.StateChanged -= StateChanged; process?.Dispose();
    }
}

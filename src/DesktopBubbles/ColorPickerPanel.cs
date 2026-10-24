using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal sealed class ColorWheel : FrameworkElement
{
    private static readonly BitmapSource bitmap = MakeWheel();
    public double Hue { get; set; }
    public double Saturation { get; set; }
    public event Action? Edited;
    public ColorWheel()
    {
        Width = Height = 208; Focusable = true; Cursor = Cursors.Cross;
        System.Windows.Automation.AutomationProperties.SetName(this, "Farbkreis");
        System.Windows.Automation.AutomationProperties.SetHelpText(this, "Links/Rechts: Farbton. Oben/Unten: Sättigung. Alternativ HEX-Eingabe.");
        MouseLeftButtonDown += (_, e) => { Focus(); CaptureMouse(); Select(e.GetPosition(this)); e.Handled = true; };
        MouseMove += (_, e) => { if (IsMouseCaptured) Select(e.GetPosition(this)); };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
        KeyDown += (_, e) =>
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            if (e.Key == Key.Left) Hue = (Hue - step + 360) % 360;
            else if (e.Key == Key.Right) Hue = (Hue + step) % 360;
            else if (e.Key == Key.Up) Saturation = Math.Min(1, Saturation + step / 100);
            else if (e.Key == Key.Down) Saturation = Math.Max(0, Saturation - step / 100);
            else return;
            InvalidateVisual(); Edited?.Invoke(); e.Handled = true;
        };
    }
    private void Select(Point point)
    {
        double dx = point.X - ActualWidth / 2, dy = point.Y - ActualHeight / 2;
        Hue = (Math.Atan2(-dy, dx) * 180 / Math.PI - 90 + 360) % 360;
        Saturation = Math.Clamp(Math.Sqrt(dx * dx + dy * dy) / (Math.Min(ActualWidth, ActualHeight) / 2 - 4), 0, 1);
        InvalidateVisual(); Edited?.Invoke();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        drawing.DrawImage(bitmap, new Rect(4, 4, ActualWidth - 8, ActualHeight - 8));
        double radius = (Math.Min(ActualWidth, ActualHeight) / 2 - 4) * Saturation, angle = (Hue + 90) * Math.PI / 180;
        var center = new Point(ActualWidth / 2 + radius * Math.Cos(angle), ActualHeight / 2 - radius * Math.Sin(angle));
        drawing.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(85, 40, 20, 60)), 5), center, 7, 7);
        drawing.DrawEllipse(null, new Pen(Brushes.White, 2.5), center, 7, 7);
    }
    private static BitmapSource MakeWheel()
    {
        const int size = 256; var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            double dx = (x - 127.5) / 127, dy = (y - 127.5) / 127, saturation = Math.Sqrt(dx * dx + dy * dy);
            if (saturation > 1) continue;
            double hue = (Math.Atan2(-dy, dx) * 180 / Math.PI - 90 + 360) % 360;
            var color = ColorValue.FromHsv(hue, saturation, 1); int i = (y * size + x) * 4;
            pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = 255;
        }
        var image = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4); image.Freeze(); return image;
    }
}

internal sealed class ColorPickerPanel : UserControl
{
    internal ColorWheel Wheel { get; } = new();
    internal TextBox HexInput { get; }
    internal Slider Brightness { get; }
    internal Slider Saturation { get; }
    public string SelectedHex { get; private set; }
    private readonly TextBlock percent;
    private readonly Border colorSample;
    private bool syncing;
    internal bool IsValid => ColorValue.TryParse(HexInput.Text, out _);
    internal event Action? Edited;
    internal event Action? CancelRequested;
    public ColorPickerPanel(string initial)
    {
        SelectedHex = ColorValue.Normalize(initial);
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var frame = new Border { CornerRadius = new CornerRadius(14), BorderBrush = Ui.GlassRim(), BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = root };
        frame.SetResourceReference(Border.BackgroundProperty, "ControlFill"); Content = frame;
        var left = new StackPanel(); root.Children.Add(left);
        var saturationLabel = Ui.Text("Sättigung", 12); saturationLabel.Margin = new Thickness(0, 0, 0, 4); left.Children.Add(saturationLabel);
        var wheelRow = new Grid(); wheelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) }); wheelRow.ColumnDefinitions.Add(new ColumnDefinition()); left.Children.Add(wheelRow);
        Saturation = new Slider { Minimum = 0, Maximum = 1, SmallChange = .01, LargeChange = .1, Orientation = Orientation.Vertical, Width = 24, Height = 144, Style = (Style)FindResource("SaturationSlider") };
        System.Windows.Automation.AutomationProperties.SetName(Saturation, "Sättigung"); wheelRow.Children.Add(Saturation);
        Wheel.Width = Wheel.Height = 144; Grid.SetColumn(Wheel, 1); wheelRow.Children.Add(Wheel);
        var controls = new StackPanel(); Grid.SetColumn(controls, 2); root.Children.Add(controls);
        controls.Children.Add(Ui.Text("Helligkeit", 12));
        var level = new DockPanel { Margin = new Thickness(0, 4, 0, 10) };
        percent = Ui.Text("100 %", 12); percent.Width = 42; percent.TextAlignment = TextAlignment.Right; percent.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(percent, Dock.Right); level.Children.Add(percent);
        Brightness = new Slider { Minimum = 0, Maximum = 1, Value = 1, SmallChange = .01, LargeChange = .1, Foreground = Brushes.Transparent };
        System.Windows.Automation.AutomationProperties.SetName(Brightness, "Helligkeit"); level.Children.Add(Brightness); controls.Children.Add(level);
        var hexLabel = Ui.Text("HEX", 12); hexLabel.Margin = new Thickness(0, 0, 0, 4); controls.Children.Add(hexLabel);
        var hexRow = new DockPanel();
        colorSample = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(7), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(colorSample, Dock.Right); hexRow.Children.Add(colorSample);
        HexInput = new TextBox { Text = SelectedHex, MaxLength = 7, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13, Padding = new Thickness(8, 6, 8, 6) };
        System.Windows.Automation.AutomationProperties.SetName(HexInput, "HEX-Farbe, sechs Stellen"); hexRow.Children.Add(HexInput); controls.Children.Add(hexRow);
        var cancel = Ui.Button("Abbrechen", () => CancelRequested?.Invoke()); cancel.Padding = new Thickness(6, 4, 6, 4); cancel.Margin = new Thickness(0, 4, 0, 0); cancel.HorizontalAlignment = HorizontalAlignment.Right; cancel.Background = Brushes.Transparent; cancel.BorderThickness = new Thickness(0); cancel.ToolTip = "Farbänderung verwerfen · Esc"; controls.Children.Add(cancel);
        ColorValue.TryParse(SelectedHex, out var start); var hsv = start.ToHsv(); Wheel.Hue = hsv.H; Wheel.Saturation = hsv.S; Saturation.Value = hsv.S; Brightness.Value = hsv.V;
        Wheel.Edited += UpdateFromWheel; Brightness.ValueChanged += (_, _) => { if (!syncing) UpdateFromWheel(); };
        Saturation.ValueChanged += (_, _) => { if (!syncing) { Wheel.Saturation = Saturation.Value; UpdateFromWheel(); } };
        HexInput.TextChanged += (_, _) =>
        {
            if (syncing) return;
            bool valid = ColorValue.TryParse(HexInput.Text, out var color);
            HexInput.ToolTip = valid ? "HEX-Farbe" : "Bitte sechs Hex-Ziffern eingeben, zum Beispiel #7B6BFF.";
            if (!valid) { Edited?.Invoke(); return; }
            syncing = true;
            SelectedHex = color.Hex; var value = color.ToHsv(); Wheel.Hue = value.H; Wheel.Saturation = value.S; Saturation.Value = value.S; Brightness.Value = value.V;
            UpdateVisuals(); syncing = false; Edited?.Invoke();
        };
        UpdateVisuals();
    }
    private void UpdateFromWheel()
    {
        syncing = true; SelectedHex = ColorValue.FromHsv(Wheel.Hue, Wheel.Saturation, Brightness.Value).Hex;
        HexInput.Text = SelectedHex; Saturation.Value = Wheel.Saturation; UpdateVisuals(); syncing = false; Edited?.Invoke();
    }
    private void UpdateVisuals()
    {
        Wheel.InvalidateVisual();
        var color = ColorValue.FromHsv(Wheel.Hue, Wheel.Saturation, 1);
        var brush = new LinearGradientBrush(Colors.Black, Color.FromRgb(color.R, color.G, color.B), 0); brush.Freeze();
        Brightness.Background = brush; percent.Text = Brightness.Value.ToString("P0");
        var neutral = ColorValue.FromHsv(Wheel.Hue, 0, Brightness.Value); var saturated = ColorValue.FromHsv(Wheel.Hue, 1, Brightness.Value);
        var saturationBrush = new LinearGradientBrush(Color.FromRgb(neutral.R, neutral.G, neutral.B), Color.FromRgb(saturated.R, saturated.G, saturated.B), new Point(0, 1), new Point(0, 0)); saturationBrush.Freeze(); Saturation.Background = saturationBrush;
        colorSample.Background = Ui.Brush(SelectedHex);
    }
}

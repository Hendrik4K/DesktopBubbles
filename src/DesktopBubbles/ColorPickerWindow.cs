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

internal sealed class ColorPickerWindow : Window
{
    internal ColorWheel Wheel { get; } = new();
    internal TextBox HexInput { get; }
    internal Button DoneButton { get; }
    internal Slider Brightness { get; }
    public string SelectedHex { get; private set; }
    private readonly TextBlock percent;
    private bool syncing, finished;
    internal bool Applied { get; private set; }
    public ColorPickerWindow(Window owner, string initial, BubbleSettings settings, Action<string> apply)
    {
        Owner = owner; Title = "Farbe wählen"; Width = 292; Height = 360; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual; Ui.Chrome(this, radius: () => 18, perPixel: true);
        SelectedHex = ColorValue.Normalize(initial);
        var root = new StackPanel();
        Content = new Border { CornerRadius = new CornerRadius(18), Background = Ui.AppGlass(settings), BorderBrush = Ui.GlassRim(), BorderThickness = new Thickness(1), Padding = new Thickness(16), Child = root };
        root.Children.Add(Ui.Text("Farbe wählen", 15, weight: FontWeights.SemiBold));
        Wheel.Margin = new Thickness(0, 6, 0, 8); Wheel.HorizontalAlignment = HorizontalAlignment.Center; root.Children.Add(Wheel);
        root.Children.Add(Ui.Text("Helligkeit", 12));
        var level = new DockPanel { Margin = new Thickness(0, 2, 0, 12) };
        percent = Ui.Text("100 %", 12); percent.Width = 48; percent.TextAlignment = TextAlignment.Right; DockPanel.SetDock(percent, Dock.Right); level.Children.Add(percent);
        Brightness = new Slider { Minimum = 0, Maximum = 1, Value = 1, SmallChange = .01, LargeChange = .1 };
        System.Windows.Automation.AutomationProperties.SetName(Brightness, "Helligkeit"); level.Children.Add(Brightness); root.Children.Add(level);
        var footer = new DockPanel();
        DoneButton = Ui.Button("Fertig", () => { if (ColorValue.TryParse(HexInput!.Text, out var value)) { Applied = true; SelectedHex = value.Hex; apply(SelectedHex); finished = true; Close(); } });
        DoneButton.Background = Ui.Resource("Accent"); DoneButton.Foreground = Brushes.White; DoneButton.Margin = new Thickness(8, 0, 0, 0); DoneButton.IsDefault = true; DockPanel.SetDock(DoneButton, Dock.Right); footer.Children.Add(DoneButton);
        HexInput = new TextBox { Text = SelectedHex, MaxLength = 7, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13, Padding = new Thickness(9, 7, 9, 7) };
        System.Windows.Automation.AutomationProperties.SetName(HexInput, "HEX-Farbe, sechs Stellen"); footer.Children.Add(HexInput); root.Children.Add(footer);
        ColorValue.TryParse(initial, out var start); var hsv = start.ToHsv(); Wheel.Hue = hsv.H; Wheel.Saturation = hsv.S; Brightness.Value = hsv.V;
        Wheel.Edited += UpdateFromWheel; Brightness.ValueChanged += (_, _) => { if (!syncing) UpdateFromWheel(); };
        HexInput.TextChanged += (_, _) =>
        {
            if (syncing) return;
            bool valid = ColorValue.TryParse(HexInput.Text, out var color); DoneButton.IsEnabled = valid;
            HexInput.ToolTip = valid ? "HEX-Farbe" : "Bitte sechs Hex-Ziffern eingeben, zum Beispiel #7B6BFF.";
            if (!valid) return;
            syncing = true;
            SelectedHex = color.Hex; var value = color.ToHsv(); Wheel.Hue = value.H; Wheel.Saturation = value.S; Brightness.Value = value.V;
            Wheel.InvalidateVisual(); UpdateBrightnessBar(); syncing = false;
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { finished = true; Close(); e.Handled = true; } };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() => { if (!finished && IsVisible && HexInput.ContextMenu?.IsOpen != true) { finished = true; Close(); } });
        SourceInitialized += (_, _) => { Native.ToolWindow(this); Ui.Backdrop(this, settings); };
        Closed += (_, _) => finished = true;
        UpdateBrightnessBar();
    }
    private void UpdateFromWheel()
    {
        syncing = true; SelectedHex = ColorValue.FromHsv(Wheel.Hue, Wheel.Saturation, Brightness.Value).Hex;
        HexInput.Text = SelectedHex; DoneButton.IsEnabled = true; UpdateBrightnessBar(); syncing = false;
    }
    private void UpdateBrightnessBar()
    {
        var color = ColorValue.FromHsv(Wheel.Hue, Wheel.Saturation, 1);
        var brush = new LinearGradientBrush(Colors.Black, Color.FromRgb(color.R, color.G, color.B), 0); brush.Freeze();
        Brightness.Background = brush; Brightness.Foreground = Brushes.Transparent; percent.Text = Brightness.Value.ToString("P0");
    }
    internal void ShowAt(FrameworkElement anchor)
    {
        var point = anchor.PointToScreen(new Point(anchor.ActualWidth / 2, anchor.ActualHeight + 7));
        Native.Position(this, point.X - Width / 2, point.Y);
        Loaded += (_, _) =>
        {
            var bounds = Native.Bounds(this); var work = Native.WorkArea(point.X, point.Y);
            var place = Placement.Clamp(point.X - bounds.Width / 2, point.Y, bounds.Width, bounds.Height, work);
            Native.Position(this, place.X, place.Y); Wheel.Focus();
        };
        Show(); Activate();
    }
}

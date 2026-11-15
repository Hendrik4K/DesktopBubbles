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
        Width = Height = 208; Focusable = true; FocusVisualStyle = null; Cursor = Cursors.Cross;
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
    internal Slider? ColorOpacity { get; }
    internal Button Eyedropper { get; }
    internal Button CloseButton { get; }
    internal double SelectedOpacity => ColorOpacity?.Value ?? 1;
    public string SelectedHex { get; private set; }
    private readonly TextBlock percent;
    private readonly Border frame;
    private readonly Grid root;
    private bool syncing;
    internal bool IsValid => ColorValue.TryParse(HexInput.Text, out _);
    internal event Action? Edited;
    internal event Action? CancelRequested;
    public ColorPickerPanel(string initial, double? opacity = null)
    {
        SelectedHex = ColorValue.Normalize(initial);
        root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(182) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        frame = new Border { CornerRadius = new CornerRadius(14), BorderBrush = Ui.GlassRim(), BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = root };
        frame.SetResourceReference(Border.BackgroundProperty, "ControlFill"); Content = frame;
        var left = new StackPanel(); root.Children.Add(left);
        var saturationLabel = Ui.Text("Sättigung", 12); saturationLabel.Margin = new Thickness(0, 0, 0, 4); left.Children.Add(saturationLabel);
        var wheelRow = new Grid(); wheelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) }); wheelRow.ColumnDefinitions.Add(new ColumnDefinition()); left.Children.Add(wheelRow);
        Saturation = new Slider { Minimum = 0, Maximum = 1, SmallChange = .01, LargeChange = .1, Orientation = Orientation.Vertical, Width = 24, Height = 144, Style = (Style)FindResource("SaturationSlider"), HorizontalAlignment = HorizontalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(Saturation, "Sättigung");
        // Generous grips, as everywhere else: the whole column (and each whole row on the right)
        // moves its slider smoothly, not just the thin track.
        var saturationZone = new Grid { Margin = new Thickness(0, -8, 4, -8) }; saturationZone.Children.Add(Saturation); wheelRow.Children.Add(saturationZone);
        SettingsMotion.SmoothZone(saturationZone, Saturation);
        Wheel.Width = Wheel.Height = 144; Grid.SetColumn(Wheel, 1); wheelRow.Children.Add(Wheel);
        var controls = new StackPanel(); Grid.SetColumn(controls, 2); root.Children.Add(controls);
        var brightnessZone = new StackPanel { Margin = new Thickness(0, 0, 0, 10) }; brightnessZone.Children.Add(Ui.Text("Helligkeit", 12));
        var level = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        percent = Ui.Text("100 %", 12); percent.Width = 42; percent.TextAlignment = TextAlignment.Right; percent.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(percent, Dock.Right); level.Children.Add(percent);
        Brightness = new Slider { Minimum = 0, Maximum = 1, Value = 1, SmallChange = .01, LargeChange = .1, Foreground = Brushes.Transparent };
        System.Windows.Automation.AutomationProperties.SetName(Brightness, "Helligkeit"); level.Children.Add(Brightness); brightnessZone.Children.Add(level); controls.Children.Add(brightnessZone);
        SettingsMotion.SmoothZone(brightnessZone, Brightness);
        if (opacity is { } initialOpacity)
        {
            var opacityZone = new StackPanel { Margin = new Thickness(0, 0, 0, 10) }; opacityZone.Children.Add(Ui.Text("Deckkraft", 12));
            var opacityRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var amount = Ui.Text($"{initialOpacity:P0}", 12); amount.Width = 42; amount.TextAlignment = TextAlignment.Right; amount.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(amount, Dock.Right); opacityRow.Children.Add(amount);
            ColorOpacity = new Slider { Minimum = 0, Maximum = 1, SmallChange = .01, LargeChange = .1, Value = initialOpacity, ToolTip = "Deckkraft der Steuerelement-Hintergründe. Helligkeit und Text bleiben unabhängig." };
            System.Windows.Automation.AutomationProperties.SetName(ColorOpacity, "Deckkraft der Steuerelemente");
            ColorOpacity.ValueChanged += (_, _) => { amount.Text = ColorOpacity.Value.ToString("P0"); if (!syncing) Edited?.Invoke(); };
            opacityRow.Children.Add(ColorOpacity); opacityZone.Children.Add(opacityRow); controls.Children.Add(opacityZone);
            SettingsMotion.SmoothZone(opacityZone, ColorOpacity);
        }
        var hexLabel = Ui.Text("HEX", 12); hexLabel.Margin = new Thickness(0, 0, 0, 4); controls.Children.Add(hexLabel);
        // The HEX field itself shows the colour; right of it the eyedropper and the close cross.
        var hexRow = new DockPanel();
        CloseButton = Icon("\uE711", "Farbänderung verwerfen · Esc", () => CancelRequested?.Invoke()); DockPanel.SetDock(CloseButton, Dock.Right);
        Eyedropper = Icon("\uEF3C", "Farbe vom Bildschirm aufnehmen", PickFromScreen); DockPanel.SetDock(Eyedropper, Dock.Right);
        hexRow.Children.Add(CloseButton); hexRow.Children.Add(Eyedropper);
        HexInput = new TextBox { Text = SelectedHex, MaxLength = 7, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13, Padding = new Thickness(8, 6, 8, 6), FontWeight = FontWeights.SemiBold };
        System.Windows.Automation.AutomationProperties.SetName(HexInput, "HEX-Farbe, sechs Stellen"); hexRow.Children.Add(HexInput); controls.Children.Add(hexRow);
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
        // When the panel is taller than its content, the surplus fades softly into transparency
        // instead of a white block running to the bottom.
        frame.SizeChanged += (_, _) => FadeSurplus();
        UpdateVisuals();
    }
    private Button Icon(string glyph, string name, Action action)
    {
        var shape = Ui.GlyphPath(glyph, 16); shape.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Ink");
        var button = new Button { Style = (Style)FindResource("PlainButton"), Content = shape, Width = 34, Height = 34, Margin = new Thickness(6, 0, 0, 0), ToolTip = name, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        SettingsMotion.Swell(shape, 1.2);
        button.Click += (_, _) => action(); return button;
    }
    private void FadeSurplus()
    {
        root.Measure(new Size(Math.Max(0, frame.ActualWidth - 26), double.PositiveInfinity));
        double content = root.DesiredSize.Height + 26, height = frame.ActualHeight;
        if (height <= content + 12) { frame.OpacityMask = null; return; }
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0)); mask.GradientStops.Add(new GradientStop(Colors.Black, (content + 6) / height));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, Math.Min(1, (content + 90) / height))); mask.Freeze();
        frame.OpacityMask = mask;
    }
    // Eyedropper: the screen becomes a crosshair; the colour under it previews live, a click takes
    // it, Esc or a right click returns to the colour from before.
    private void PickFromScreen()
    {
        string before = HexInput.Text;
        ScreenColorPicker.Pick(Window.GetWindow(this), hex => HexInput.Text = hex, hex => HexInput.Text = hex ?? before);
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
        // The field wears the colour; its text stays readable on light and dark colours alike.
        var fill = Ui.Brush(SelectedHex); var c = fill.Color;
        HexInput.Background = fill; HexInput.BorderBrush = fill;
        HexInput.Foreground = (.2126 * c.R + .7152 * c.G + .0722 * c.B) / 255 > .55 ? Brushes.Black : Brushes.White;
        HexInput.CaretBrush = HexInput.Foreground;
    }
}

// The eyedropper: a nearly invisible layer over every screen with a crosshair. It reads the pixel
// under the pointer as it moves (live preview) and on click; Esc, a right click or losing focus
// cancel and restore the previous colour.
internal sealed class ScreenColorPicker : Window
{
    private readonly Action<string> preview;
    private readonly Action<string?> done;
    private bool finished;
    private ScreenColorPicker(Action<string> preview, Action<string?> done)
    {
        this.preview = preview; this.done = done;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; ShowInTaskbar = false; Topmost = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); Cursor = Cursors.Cross; WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Top = -32000; Width = Height = 1;
        SourceInitialized += (_, _) =>
        {
            var area = System.Windows.Forms.SystemInformation.VirtualScreen;
            Native.SetWindowPos(Native.Handle(this), -1, area.X, area.Y, area.Width, area.Height, Native.NoActivate);
        };
        MouseMove += (_, _) => { if (Sample() is { } hex) this.preview(hex); };
        MouseLeftButtonDown += (_, e) => { e.Handled = true; Finish(Sample()); };
        MouseRightButtonDown += (_, e) => { e.Handled = true; Finish(null); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Finish(null); } };
        Deactivated += (_, _) => Finish(null);
    }
    internal static void Pick(Window? owner, Action<string> preview, Action<string?> done)
    {
        var picker = new ScreenColorPicker(preview, done); picker.Show(); picker.Activate(); picker.Focus();
    }
    private static string? Sample()
    {
        if (!Native.GetCursorPos(out var at)) return null;
        nint screen = Native.GetDC(0); if (screen == 0) return null;
        try
        {
            uint value = Native.GetPixel(screen, at.X, at.Y);
            if (value == 0xFFFFFFFF) return null;
            return $"#{value & 0xFF:X2}{(value >> 8) & 0xFF:X2}{(value >> 16) & 0xFF:X2}";
        }
        finally { Native.ReleaseDC(0, screen); }
    }
    private void Finish(string? hex)
    {
        if (finished) return;
        finished = true; done(hex); Close();
    }
}
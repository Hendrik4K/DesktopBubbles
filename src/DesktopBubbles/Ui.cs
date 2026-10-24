using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Runtime.CompilerServices;
using DesktopBubbles.Core;
using Microsoft.Win32;

namespace DesktopBubbles;

internal static class Ui
{
    public const double AppRadius = 8, BubbleRadius = 20;
    public static bool Dark { get; private set; }
    private static Color materialBasis = Color.FromRgb(235, 233, 244);
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    public static SolidColorBrush Resource(string name) => (SolidColorBrush)System.Windows.Application.Current.FindResource(name);
    public static void ApplyTheme(BubbleSettings settings, string? modeDraft = null, double? modeOpacityDraft = null)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        Dark = settings.Theme == "Dark" || (settings.Theme == "System" && key?.GetValue("AppsUseLightTheme") is int v && v == 0);
        var r = System.Windows.Application.Current.Resources;
        var palette = ThemePalette.Create(settings, Dark, modeDraft, modeOpacityDraft);
        r["Ink"] = Brush(palette.Ink); r["Muted"] = Brush(palette.Muted);
        r["Surface"] = Brush(palette.Surface); r["ControlFill"] = Brush(palette.ControlFill);
        r["HoverFill"] = Brush(palette.HoverFill); r["Line"] = Brush(palette.Line);
        materialBasis = Brush(palette.MaterialBasis).Color;
        r["Accent"] = Brush(settings.AppColor);
        if (SystemParameters.HighContrast)
        {
            r["Ink"] = SystemColors.WindowTextBrush; r["Muted"] = SystemColors.WindowTextBrush;
            r["Surface"] = SystemColors.WindowBrush; r["ControlFill"] = SystemColors.WindowBrush;
            r["HoverFill"] = SystemColors.HighlightBrush; r["Line"] = SystemColors.WindowTextBrush; r["Accent"] = SystemColors.HotTrackBrush;
        }
    }
    public static TextBlock Text(string text, double size = 13, string color = "Ink", FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, color); return block;
    }
    public static Button Button(string label, Action action)
    {
        var b = new Button { Content = label };
        b.Click += (_, _) => action(); return b;
    }
    public static MenuItem Menu(string text, Action action)
    { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); return item; }
    public static TextBlock Symbol(string glyph, double size = 16) => new()
    { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
    public static Button IconButton(string glyph, string name, Action action)
    {
        var button = Button("", action); button.Content = Symbol(glyph); button.ToolTip = name;
        button.Padding = new Thickness(9); button.Margin = new Thickness(0); button.Background = Brushes.Transparent;
        System.Windows.Automation.AutomationProperties.SetName(button, name); return button;
    }
    public static void Error(Exception ex)
    {
        Log.Write(ex.ToString());
        MessageBox.Show(ex.Message, "DesktopBubbles", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private static readonly ConditionalWeakTable<Window, RoundedWindow> rounded = new();
    public static void Chrome(Window window, bool resize = false, Func<double>? radius = null, bool perPixel = false)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = resize ? ResizeMode.CanResize : ResizeMode.NoResize;
        window.AllowsTransparency = perPixel;
        window.Background = Brushes.Transparent;
        // Standard host: let DWM draw its fixed, small corners. Custom folders: one alpha surface, no DWM rectangle.
        if (!perPixel) WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(-1), ResizeBorderThickness = new Thickness(resize ? 6 : 0) });
        window.SourceInitialized += (_, _) =>
        {
            var h = Native.Handle(window); int corner = perPixel ? 1 : 2; int dark = Dark ? 1 : 0;
            Native.DwmSetWindowAttribute(h, 33, ref corner, 4);
            Native.DwmSetWindowAttribute(h, 20, ref dark, 4);
        };
        rounded.Add(window, new RoundedWindow(window, radius ?? (() => AppRadius)));
    }
    public static void RefreshRounding(Window window) { if (rounded.TryGetValue(window, out var shape)) shape.Update(); }
    // Clip both our content and the native backdrop; a WPF Border alone leaves DWM's outer corners visible.
    private sealed class RoundedWindow
    {
        private readonly Window window;
        private readonly Func<double> radius;
        private (int W, int H, int R) previous;
        private bool updating;
        public RoundedWindow(Window window, Func<double> radius)
        {
            this.window = window; this.radius = radius;
            window.SourceInitialized += (_, _) => Update();
            window.SizeChanged += (_, _) => Update();
            window.LocationChanged += (_, _) => Update(); // DPI can change between monitors without a DIP-size change.
        }
        public void Update()
        {
            if (updating || new System.Windows.Interop.WindowInteropHelper(window).Handle == 0) return;
            updating = true;
            try
            {
                var h = Native.Handle(window); var bounds = Native.Bounds(window);
                double r = Math.Min(radius(), Math.Min(window.ActualWidth, window.ActualHeight) / 2);
                if (window.Content is FrameworkElement content && window.ActualWidth > 0 && window.ActualHeight > 0)
                {
                    content.Clip = new RectangleGeometry(new Rect(0, 0, window.ActualWidth, window.ActualHeight), r, r);
                    if (content is Border border) border.CornerRadius = new CornerRadius(r);
                }
                if (!window.AllowsTransparency) return; // DWM owns the standard host's fixed contour.
                double scale = Native.GetDpiForWindow(h) / 96d;
                var next = ((int)bounds.Width, (int)bounds.Height, (int)Math.Round(r * scale));
                if (next.Item1 <= 0 || next.Item2 <= 0 || next == previous) return;
                var region = Native.CreateRoundRectRgn(0, 0, next.Item1 + 1, next.Item2 + 1, next.Item3 * 2, next.Item3 * 2);
                if (region == 0) { Log.Write("Rounded window region could not be allocated."); return; }
                // SetWindowRgn transfers ownership to Windows on success; only delete on failure.
                if (Native.SetWindowRgn(h, region, window.IsVisible) != 0) previous = next;
                else { Native.DeleteObject(region); Log.Write("Rounded window region could not be applied."); }
            }
            finally { updating = false; }
        }
    }
    public static void Backdrop(Window window, BubbleSettings settings)
    {
        var h = Native.Handle(window);
        bool use = !window.AllowsTransparency && settings.Blur && !SystemParameters.HighContrast;
        var source = System.Windows.Interop.HwndSource.FromHwnd(h);
        if (source?.CompositionTarget is not null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
        var margins = new Native.Margins { Left = use ? -1 : 0 };
        Native.DwmExtendFrameIntoClientArea(h, ref margins);
        int value = use ? 3 : 1;
        int result = Native.DwmSetWindowAttribute(h, 38, ref value, 4);
        window.Background = window.AllowsTransparency || (use && result == 0) ? Brushes.Transparent : Resource("Surface");
        Log.Write($"DWM backdrop={use}, HRESULT=0x{result:X8}");
    }
    public static Brush AppGlass(BubbleSettings settings) => Material(settings.AppColor, settings.AppOpacity, settings.Glass > .65);
    public static Brush ColorDraft(BubbleSettings settings, string color, bool application = false)
        => Material(color, application ? settings.AppOpacity : settings.Opacity, settings.Glass > .65);
    public static Brush Glass(BubbleSettings settings, BubbleGroup? group = null)
    {
        return Material(AppearancePreferences.Color(settings, group), Math.Clamp(settings.Opacity, 0, .95), settings.Glass > .65);
    }
    public static Brush WindowGlass(BubbleSettings settings, BubbleGroup? group = null)
        => Material(AppearancePreferences.Color(settings, group), Math.Clamp(settings.WindowOpacity, 0, .95), settings.Glass > .65);
    public static Brush BubbleRim(BubbleSettings settings, BubbleGroup group)
        => !SystemParameters.HighContrast && settings.Opacity <= 0 ? Brushes.Transparent : GlassRim();
    private static Brush Material(string tint, double opacity, bool matte)
    {
        if (SystemParameters.HighContrast) return SystemColors.WindowBrush;
        if (opacity <= 0) return Brushes.Transparent;
        // Matte has a denser neutral body; clear leaves more of the real background visible.
        // Every highlight scales with opacity, so zero really removes the whole background.
        double amount = matte ? .12 : .48;
        double alpha = opacity * (matte ? 1 : .48);
        ColorValue.TryParse(tint, out var rgb);
        var basis = materialBasis;
        var color = Color.FromArgb((byte)(255 * alpha), (byte)(basis.R * (1 - amount) + rgb.R * amount),
            (byte)(basis.G * (1 - amount) + rgb.G * amount), (byte)(basis.B * (1 - amount) + rgb.B * amount));
        var white = Color.FromArgb(color.A, (byte)Math.Min(255, color.R + 12), (byte)Math.Min(255, color.G + 12), (byte)Math.Min(255, color.B + 12));
        var reflection = Color.FromArgb((byte)Math.Min(255 * opacity, color.A * (matte ? 1 : 1.35)), (byte)Math.Min(255, color.R + 30), (byte)Math.Min(255, color.G + 30), (byte)Math.Min(255, color.B + 30));
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(.65, 1) };
        brush.GradientStops.Add(new GradientStop(reflection, 0));
        brush.GradientStops.Add(new GradientStop(white, .16));
        brush.GradientStops.Add(new GradientStop(color, .58));
        brush.GradientStops.Add(new GradientStop(white, 1));
        brush.Freeze(); return brush;
    }
    public static Brush GlassRim()
    {
        if (SystemParameters.HighContrast) return SystemColors.WindowTextBrush;
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(40, 204, 235, 245), .45));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(110, 255, 255, 255), 1));
        brush.Freeze(); return brush;
    }
    public static Border Card(UIElement content, Thickness? padding = null) => new()
    {
        Background = Resource("ControlFill"), BorderBrush = Resource("Line"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14), Padding = padding ?? new Thickness(18), Child = content, Margin = new Thickness(0, 0, 0, 12)
    };
    public static string? Ask(Window? owner, string title, string label, string initial = "")
    {
        var w = new Window { Title = title, Width = 430, SizeToContent = SizeToContent.Height, WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Background = Resource("Surface"), Foreground = Resource("Ink"), ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize };
        if (owner is not null && owner.IsVisible) w.Owner = owner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Text(label, 14));
        var input = new TextBox { Text = initial, Margin = new Thickness(0, 12, 0, 20), MaxLength = 32767 }; panel.Children.Add(input);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Abbrechen", () => w.DialogResult = false); cancel.IsCancel = true;
        var ok = Button("Übernehmen", () => { if (!string.IsNullOrWhiteSpace(input.Text)) w.DialogResult = true; }); ok.IsDefault = true;
        row.Children.Add(cancel); row.Children.Add(ok); panel.Children.Add(row); w.Content = panel;
        w.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return w.ShowDialog() == true ? input.Text.Trim() : null;
    }
}

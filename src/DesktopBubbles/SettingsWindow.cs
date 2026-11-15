using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal enum AppearanceScope { App, Bubbles, Window }

internal sealed class SettingsWindow : Window
{
    private readonly App app;
    private readonly Border surface, preview = new() { Name = "AppearancePreview" };
    private readonly StackPanel groups = new();
    private readonly TranslateTransform pillShift = new();
    private readonly Border selectionPill;
    private double? pillY;
    private readonly ContentControl pane = new();
    private readonly Button settingsButton;
    private readonly Dictionary<Guid, Border> thumbnails = [];
    private Border? swatch;
    private Border? modeSwatch;
    private ContentControl? modeAction;
    private Button? modeApplyButton;
    private ComboBox? themeSelector;
    private bool editingModeColor, editingDarkMode;
    internal Button? ModeColorButton { get; private set; }
    private Canvas? folderCanvas;
    private Grid? folderStage;
    private ScrollViewer? folderViewport;
    private TextBlock? previewDimensions;
    private double previewScale = 1d / 3;
    private UIElement? standardDetails;
    private ContentControl? colorOverlay;
    internal Button? ColorButton { get; private set; }
    internal Button? PreviewScaleButton { get; private set; }
    internal CheckBox? LinkColorsCheckBox { get; private set; }
    internal ComboBox? GlassSelector { get; private set; }
    internal Button? FrostedFoldersButton { get; private set; }
    internal Border AppearancePreview => preview;
    internal PixelRect PreviewFootprint => AppearanceGeometry.Folder(app.State.Settings, selected?.Items.Count ?? 0);
    private BubbleGroup? selected;
    private AppearanceScope lastTabScope = AppearanceScope.Bubbles;
    internal AppearanceScope Scope { get; private set; } = AppearanceScope.Bubbles;
    internal Slider? TransparencySlider { get; private set; }
    internal Slider? SizeSlider { get; private set; }
    internal Slider? RoundingSlider { get; private set; }
    internal Slider? LiquidStrengthSlider { get; private set; }
    internal CheckBox? AutoCloseCheckBox { get; private set; }
    internal CheckBox? CollectDuplicatesCheckBox { get; private set; }
    internal TextBlock? AutoCloseSeconds { get; private set; }
    internal LiveMeasurementBar Measurement { get; }
    internal DividerScroll Divider { get; } = new();
    // The gear swells on hover; it turns 45° to the left and lights up while the settings are open.
    private readonly ScaleTransform gearScale = new(1, 1);
    private readonly RotateTransform gearTurn = new(0);
    private readonly SolidColorBrush gearInk = new(Colors.Black);
    private static readonly Color GearLit = Color.FromRgb(0xF2, 0xB1, 0x1B);
    private void GearSwell(double factor)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut }; var time = TimeSpan.FromSeconds(factor > 1 ? .18 : .26);
        gearScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(factor, time) { EasingFunction = ease });
        gearScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(factor, time) { EasingFunction = ease });
    }
    private void SetGear(bool open)
    {
        double angle = open ? -45 : 0; var ink = open ? GearLit : ((SolidColorBrush)Ui.Resource("Ink")).Color;
        if (!IsLoaded || !SettingsMotion.Enabled(app))
        { gearTurn.BeginAnimation(RotateTransform.AngleProperty, null); gearTurn.Angle = angle; gearInk.BeginAnimation(SolidColorBrush.ColorProperty, null); gearInk.Color = ink; return; }
        if (Math.Abs(gearTurn.Angle - angle) < .1 && gearInk.Color == ink) return;
        // Turns, overshoots a touch and settles into place like a detent.
        gearTurn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, TimeSpan.FromMilliseconds(460)) { EasingFunction = new BackEase { Amplitude = .45, EasingMode = EasingMode.EaseOut } });
        gearInk.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(ink, TimeSpan.FromMilliseconds(320)));
    }
    internal ColorPickerPanel? Picker { get; private set; }
    public SettingsWindow(App app)
    {
        this.app = app; Title = "DesktopBubbles";
        selectionPill = new Border { CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top, RenderTransform = pillShift, Visibility = Visibility.Hidden, IsHitTestVisible = false };
        selectionPill.SetResourceReference(Border.BackgroundProperty, "HoverFill");
        MinWidth = 600; MinHeight = 510;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        // Default: tall enough for the whole folder preview at 1:3. A size the user chose is kept
        // until the next app version.
        var stored = app.State.Settings;
        bool keep = stored.SettingsWindowVersion == AppVersion && stored.SettingsWindowWidth is > 0 && stored.SettingsWindowHeight is > 0;
        Width = Math.Clamp(keep ? stored.SettingsWindowWidth!.Value : 660, MinWidth, MaxWidth);
        Height = Math.Clamp(keep ? stored.SettingsWindowHeight!.Value : 760, MinHeight, MaxHeight);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Ui.Chrome(this, true);
        var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(86) });
        surface = new Border { CornerRadius = new CornerRadius(Ui.AppRadius), BorderBrush = Ui.GlassRim(), BorderThickness = new Thickness(1), Child = layout }; Content = surface;
        var titlebar = new DockPanel { Margin = new Thickness(18, 5, 8, 5), LastChildFill = true };
        var close = Ui.IconButton("\uE8BB", "Verwaltung schließen", Close); close.BorderThickness = new Thickness(0); DockPanel.SetDock(close, Dock.Right); titlebar.Children.Add(close);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(Logo()); brand.Children.Add(Ui.Text("DesktopBubbles", 14)); titlebar.Children.Add(brand);
        brand.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { DragMove(); } catch (InvalidOperationException) { } };
        layout.Children.Add(titlebar);
        var body = new Grid(); Grid.SetRow(body, 1); Grid.SetRowSpan(body, 2); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) }); body.ColumnDefinitions.Add(new ColumnDefinition()); layout.Children.Add(body);
        // The list reaches down to just above "Neu" and fades out there instead of passing behind it.
        var rail = new Border { Padding = new Thickness(12, 14, 10, 0), Margin = new Thickness(0, 0, 0, 72) };
        // Selection pill beneath the rows; it glides to the last clicked group and stays there.
        var railContent = new Grid(); railContent.Children.Add(selectionPill); railContent.Children.Add(groups);
        var railScroll = new ScrollViewer { Content = railContent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Divider.Track(railScroll); SettingsMotion.EdgeFade(railScroll); SettingsMotion.SmoothWheel(railScroll);
        rail.Child = railScroll; body.Children.Add(rail);
        pane.Margin = new Thickness(28, 18, 26, 100); Grid.SetColumn(pane, 1); body.Children.Add(pane);
        var bottom = new Grid(); Grid.SetRow(bottom, 2); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) }); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        layout.Children.Add(bottom);
        Divider.Margin = new Thickness(176 - 7.5, 0, 0, 0); Grid.SetRow(Divider, 1); Grid.SetRowSpan(Divider, 2); Panel.SetZIndex(Divider, 2); layout.Children.Add(Divider);
        var create = Ui.Button("Neu", app.CreateGroup); create.Style = (Style)FindResource("DashedButton"); create.Margin = new Thickness(20, 18, 20, 18); create.ToolTip = "Neue Bubble"; bottom.Children.Add(create);
        Measurement = new LiveMeasurementBar(this, () => SettingsMotion.Enabled(app)); Grid.SetColumn(Measurement, 1); bottom.Children.Add(Measurement);
        settingsButton = Ui.IconButton("\uE713", "Einstellungen", () => SelectScope(Scope == AppearanceScope.App ? AppearanceScope.Bubbles : AppearanceScope.App));
        // Drawn as a shape with room around it: it turns and swells crisply and is never cut off.
        var gear = Ui.GlyphPath("", 20); gear.Fill = gearInk;
        gear.RenderTransformOrigin = new Point(.5, .5); gear.RenderTransform = new TransformGroup { Children = { gearScale, gearTurn } };
        settingsButton.Style = (Style)FindResource("PlainButton"); settingsButton.Padding = new Thickness(0); settingsButton.Content = gear;
        settingsButton.HorizontalContentAlignment = HorizontalAlignment.Center; settingsButton.VerticalContentAlignment = VerticalAlignment.Center;
        settingsButton.MouseEnter += (_, _) => GearSwell(1.2); settingsButton.MouseLeave += (_, _) => GearSwell(1);
        settingsButton.ToolTip = "Anwendung und allgemeine Einstellungen"; settingsButton.Width = settingsButton.Height = 36;
        settingsButton.VerticalAlignment = VerticalAlignment.Center; settingsButton.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(settingsButton, 2); bottom.Children.Add(settingsButton);
        SourceInitialized += (_, _) => Ui.Backdrop(this, app.State.Settings);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (Picker is not null) CancelColorEdit(); else Close(); e.Handled = true; }
            if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { app.CreateGroup(); e.Handled = true; }
        };
        glide = new WindowGlide(this);
        Closing += (_, e) =>
        {
            if (e.Cancel) return;
            // Room made for 1:1 or the colour picker is temporary; remember the size from before it
            // (or the size a glide back is heading for).
            var settings = app.State.Settings; double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            PixelRect? basis = restBounds ?? (glide.Running ? glide.Target : null);
            settings.SettingsWindowWidth = basis is { } wide ? wide.Width / scale : ActualWidth;
            settings.SettingsWindowHeight = basis is { } tall ? tall.Height / scale : ActualHeight;
            settings.SettingsWindowVersion = AppVersion;
            glide.Stop(); restBounds = grownBounds = null; roomFor = null;
        };
        Closed += (_, _) => { CancelColorEdit(); Measurement.Dispose(); app.RequestSave(); app.Flush(); };
        RefreshGroups(); RefreshPage();
    }
    // Temporary room: the window grows to the right and down just enough to show something whole
    // (moving only if the screen edge forces it), and later glides back to exactly where it was.
    private readonly WindowGlide glide;
    private PixelRect? restBounds, grownBounds;
    private string? roomFor;
    private void MakeRoom(string reason, double wider, double taller)
    {
        if (!IsVisible || WindowState != WindowState.Normal) return;
        double scale = Native.GetDpiForWindow(Native.Handle(this)) / 96d;
        // While still gliding back from an earlier expansion, its destination is the size to return to.
        var now = Native.Bounds(this); var rest = restBounds ?? (glide.Running ? glide.Target : now);
        var work = Native.WorkArea(now.X + now.Width / 2, now.Y + now.Height / 2);
        double width = Math.Min(work.Width, Math.Max(rest.Width, Math.Ceiling(now.Width + wider * scale)));
        double height = Math.Min(work.Height, Math.Max(rest.Height, Math.Ceiling(now.Height + taller * scale)));
        if (Math.Abs(width - rest.Width) < 1 && Math.Abs(height - rest.Height) < 1) { GiveBackRoom(reason); return; }
        var target = new PixelRect(Math.Clamp(now.X, work.X, Math.Max(work.X, work.Right - width)), Math.Clamp(now.Y, work.Y, Math.Max(work.Y, work.Bottom - height)), width, height);
        restBounds ??= rest; grownBounds = target; roomFor = reason;
        glide.To(target, .52, SettingsMotion.Enabled(app));
    }
    private void GiveBackRoom(string reason)
    {
        if (roomFor != reason || restBounds is not { } rest) return;
        var grown = grownBounds; restBounds = grownBounds = null; roomFor = null;
        if (!IsVisible || WindowState != WindowState.Normal) return;
        // If the window was moved meanwhile, it keeps that move.
        var now = glide.Running ? glide.Target : Native.Bounds(this);
        double dx = grown is { } g ? now.X - g.X : 0, dy = grown is { } h ? now.Y - h.Y : 0;
        glide.To(new PixelRect(rest.X + dx, rest.Y + dy, rest.Width, rest.Height), .52, SettingsMotion.Enabled(app));
    }
    private static string AppVersion => typeof(SettingsWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "";
    private static UIElement Logo()
    {
        var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit();
        bitmap.UriSource = new Uri("pack://application:,,,/Assets/DesktopBubbles.png"); bitmap.DecodePixelWidth = 80; bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.EndInit(); bitmap.Freeze();
        var logo = new Image { Source = bitmap, Width = 26, Height = 26, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 10, 0) };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality); return logo;
    }
    internal void UpdateStatus() => Title = "DesktopBubbles · " + app.Host.Status;
    internal void RefreshMaterial()
    {
        surface.Background = Ui.AppGlass(app.State.Settings); surface.BorderBrush = Ui.GlassRim(); SetGear(Scope == AppearanceScope.App); Measurement.RefreshInk();
        Ui.RefreshRounding(this);
        preview.Background = Scope switch { AppearanceScope.App => Ui.AppGlass(app.State.Settings), AppearanceScope.Window => Ui.WindowGlass(app.State.Settings, selected), _ => Ui.Glass(app.State.Settings, selected) };
        double radius = AppearanceGeometry.Radius(app.State.Settings.CornerRoundness, preview.Width, preview.Height);
        preview.CornerRadius = new CornerRadius(Math.Clamp(radius, 0, 54));
        if (swatch is not null) swatch.Background = Ui.Brush(CurrentColor());
        if (modeSwatch is not null) modeSwatch.Background = Ui.Brush(ThemePalette.Color(app.State.Settings, Ui.Dark));
        double opacity = Scope == AppearanceScope.Window ? FolderAppearance.Opacity(app.State.Settings) : app.State.Settings.Opacity;
        preview.BorderBrush = !SystemParameters.HighContrast && opacity <= 0 ? Brushes.Transparent : Ui.GlassRim();
        UpdateFolderPreview();
        foreach (var group in app.State.Groups) if (thumbnails.TryGetValue(group.Id, out var thumbnail)) { thumbnail.Background = Ui.Glass(app.State.Settings, group); thumbnail.CornerRadius = new CornerRadius(AppearanceGeometry.Radius(app.State.Settings.CornerRoundness, 56, 56)); }
        RefreshColorDraft();
    }
    internal void RefreshGroups()
    {
        bool changedSelection = selected is null || !app.State.Groups.Contains(selected);
        if (changedSelection) selected = app.State.Groups.FirstOrDefault();
        groups.Children.Clear(); thumbnails.Clear(); selectedRow = null;
        foreach (var group in app.State.Groups)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var thumb = Preview(group, 56); thumbnails[group.Id] = thumb; thumb.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(thumb);
            // Long single words shrink to fit instead of breaking mid-word ("Benchmark s").
            var label = Ui.Text(group.Name, 12, weight: FontWeights.Medium); label.TextWrapping = TextWrapping.NoWrap;
            var fit = new Viewbox { Child = label, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(fit, 1); row.Children.Add(fit);
            // Hover sheen: fades in and out and turns around mid-way when the mouse changes its mind.
            var sheen = new Border { CornerRadius = new CornerRadius(12), Opacity = 0, Margin = new Thickness(-8, -10, -8, -10), IsHitTestVisible = false };
            sheen.SetResourceReference(Border.BackgroundProperty, "HoverFill"); Grid.SetColumnSpan(sheen, 2); row.Children.Insert(0, sheen);
            var button = new Button { Style = (Style)FindResource("PlainButton"), Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 9), Padding = new Thickness(8, 10, 8, 10) };
            button.Click += (_, _) => SelectGroup(group);
            button.MouseEnter += (_, _) => SettingsMotion.FadeTo(sheen, group == selected ? 0 : .55, .18);
            button.MouseLeave += (_, _) => SettingsMotion.FadeTo(sheen, 0, .3);
            if (group == selected) selectedRow = button;
            button.ToolTip = group.Name + " · Rechtsklick für Aktionen";
            button.ContextMenu = GroupActionsMenu.Create(app, group); button.AllowDrop = true;
            button.DragOver += (_, e) => { e.Effects = app.DropEffect(e.Data, group); e.Handled = true; };
            button.Drop += (_, e) => { app.ReceiveDrop(group, e.Data, true); e.Handled = true; };
            groups.Children.Add(button);
        }
        if (app.State.Groups.Count == 0) { var empty = Ui.Text("Noch keine Bubbles", 12, "Muted"); empty.Margin = new Thickness(8, 10, 8, 0); groups.Children.Add(empty); }
        RenderPreview(); if (changedSelection && IsLoaded) RefreshPage(); UpdateStatus();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, PlacePill);
    }
    private Button? selectedRow;
    private Border? colorPill;
    private FrameworkElement? pageBody; // Everything below the heading and the switch.
    private Grid? detailLayer; // Holds the page and the color picker that replaces it.
    // 1:1 widens the window to the right and down until the real-size folder fits whole; 1:3 glides
    // it back to where it was. Meanwhile the old preview blurs away and the new one sharpens in.
    private void FitPreview(System.Windows.Media.Imaging.BitmapSource? before = null)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (folderViewport is null || folderStage is null) return;
            if (before is not null && SettingsMotion.Enabled(app)) SettingsMotion.PageTransition(folderViewport, before);
            if (previewScale != 1) { GiveBackRoom("preview"); return; }
            folderStage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            MakeRoom("preview", folderStage.DesiredSize.Width - folderViewport.ActualWidth + 2, folderStage.DesiredSize.Height - folderViewport.ActualHeight + 2);
        });
    }
    private void SelectGroup(BubbleGroup group)
    {
        CancelColorEdit();
        // The preview hands over to the new group like a page: the old one blurs away, the new one
        // sharpens in. Coming from the settings, the whole page changes that way.
        bool fromSettings = Scope == AppearanceScope.App, animate = group != selected && SettingsMotion.Enabled(app) && IsLoaded;
        FrameworkElement? Host() => fromSettings ? pane : Scope == AppearanceScope.Window ? folderViewport : preview;
        var before = animate && Host() is { } old ? SettingsMotion.Snapshot(old) : null;
        selected = group; if (fromSettings) Scope = AppearanceScope.Bubbles;
        RefreshGroups(); RefreshPage();
        if (before is not null) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => { if (Host() is { } next) SettingsMotion.PageTransition(next, before); });
        if (Scope == AppearanceScope.Window && previewScale == 1) FitPreview(); // Another folder size at 1:1.
    }
    // The pill glides from the previous group to the selected one; first placement is instant.
    private void PlacePill()
    {
        if (selectedRow is null || !selectedRow.IsVisible) { selectionPill.Visibility = Visibility.Hidden; return; }
        var y = selectedRow.TranslatePoint(new Point(), groups).Y;
        selectionPill.Height = selectedRow.ActualHeight; selectionPill.Visibility = Visibility.Visible;
        if (pillY is { } from && Math.Abs(from - y) > .5 && SettingsMotion.Enabled(app)) SettingsMotion.Glide(pillShift, TranslateTransform.YProperty, from, y, new Spring(.4, .86));
        else { pillShift.BeginAnimation(TranslateTransform.YProperty, null); pillShift.Y = y; }
        pillY = y;
    }
    private static string ScopeLabel(AppearanceScope scope) => scope == AppearanceScope.Bubbles ? "Miniatur" : scope == AppearanceScope.Window ? "Ordner" : "Einstellungen";
    internal void SelectScope(AppearanceScope scope)
    {
        CancelColorEdit();
        // Pages dissolve through a soft blur: between "Aussehen" and "Einstellungen" the whole page,
        // between Miniatur and Ordner only the part below the sliding switch.
        bool animate = scope != Scope && SettingsMotion.Enabled(app) && IsLoaded;
        bool pageChange = (scope == AppearanceScope.App) != (Scope == AppearanceScope.App);
        var before = !animate ? null : pageChange ? SettingsMotion.Snapshot(pane) : pageBody is null ? null : SettingsMotion.Snapshot(pageBody);
        // 1:1 is a quick look: leaving the folder tab returns to 1:3 and gives the room back.
        if (Scope == AppearanceScope.Window && scope != AppearanceScope.Window && previewScale == 1) { previewScale = 1d / 3; GiveBackRoom("preview"); }
        Scope = scope; RefreshPage();
        if (animate) SettingsMotion.PageTransition(pageChange ? pane : pageBody ?? pane, before);
    }
    internal void RefreshPage()
    {
        CancelColorEdit();
        var layout = new DockPanel { LastChildFill = true };
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var headingRow = new Grid { Height = 40 };
        var heading = Ui.Text(Scope == AppearanceScope.App ? "Einstellungen" : "Aussehen", 27, weight: FontWeights.SemiBold); heading.TextAlignment = TextAlignment.Center; heading.VerticalAlignment = VerticalAlignment.Center;
        // The back arrow sits right before the title: no background, it swells on hover, and its
        // generous padding makes a click near it count too.
        var titleLine = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (Scope == AppearanceScope.App)
        {
            headingRow.Margin = new Thickness(0, 0, 0, 24);
            var arrow = Ui.Symbol("\uE72B", 20); arrow.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
            var back = new Button { Style = (Style)FindResource("PlainButton"), Content = arrow, Padding = new Thickness(14, 10, 12, 10), Margin = new Thickness(-26, -12, 0, -12), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Zurück" };
            back.Click += (_, _) => SelectScope(AppearanceScope.Bubbles);
            System.Windows.Automation.AutomationProperties.SetName(back, "Zurück zur Miniaturansicht");
            SettingsMotion.Swell(arrow);
            titleLine.Children.Add(back);
        }
        titleLine.Children.Add(heading); headingRow.Children.Add(titleLine); header.Children.Add(headingRow);
        SetGear(Scope == AppearanceScope.App);
        if (Scope == AppearanceScope.App) lastTabScope = AppearanceScope.Bubbles;

        if (Scope != AppearanceScope.App)
        {
            // One rounded track; the accent pill slides from the last view to this one.
            var scopes = new[] { AppearanceScope.Bubbles, AppearanceScope.Window };
            int previousTab = Array.IndexOf(scopes, lastTabScope);
            var tabs = SettingsMotion.Segments(scopes.Select(ScopeLabel).ToArray(), Array.IndexOf(scopes, Scope), SettingsMotion.Enabled(app) ? previousTab : -1,
                index => SelectScope(scopes[index]), index => scopes[index] == AppearanceScope.Bubbles ? "Mini-Bubbles auf dem Desktop" : "Geöffnetes App-Raster");
            tabs.Margin = new Thickness(0, 16, 0, 18);
            header.Children.Add(tabs);
            lastTabScope = Scope;
        }
        var content = new DockPanel { LastChildFill = true }; layout.Children.Add(content); pageBody = content;
        var root = new StackPanel(); var settings = app.State.Settings;
        swatch = null; colorPill = null; ColorButton = null; GlassSelector = null; folderCanvas = null; folderStage = null; folderViewport = null; previewDimensions = null; PreviewScaleButton = null; LinkColorsCheckBox = null;
        FrostedFoldersButton = null; TransparencySlider = null;
        modeSwatch = null; ModeColorButton = null; modeAction = null; modeApplyButton = null; themeSelector = null;
        if (preview.Parent is Panel previous) previous.Children.Remove(preview);
        if (Scope != AppearanceScope.Window)
        {
            var colorRow = new Grid { MinHeight = Scope == AppearanceScope.App ? 80 : 108 };
            colorRow.ColumnDefinitions.Add(new ColumnDefinition());
            if (Scope == AppearanceScope.Bubbles) { colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) }); colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) }); }
            var controls = new StackPanel(); controls.Children.Add(Ui.Text(Scope == AppearanceScope.App ? "App-Farbe" : "Farbe", 15, weight: FontWeights.SemiBold));
            // The colour circle with a pill folded out to its right, as tall and round as the circle.
            // Its width fits both "Ändern" and "Anwenden"; only the text changes.
            swatch = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), BorderBrush = Brushes.White, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
            ColorButton = new Button { Style = (Style)FindResource("PillSegment"), Content = "Ändern", Width = 98 };
            ColorButton.Click += (_, _) => { if (Picker is null) OpenPicker(); else ApplyColorEdit(); };
            colorPill = SettingsMotion.Pill(swatch, ColorButton); colorPill.Margin = new Thickness(0, 12, 0, 0);
            controls.Children.Add(colorPill); colorRow.Children.Add(controls);
            if (Scope == AppearanceScope.Bubbles)
            {
                var glass = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; glass.Children.Add(Ui.Text("Glas", 15, weight: FontWeights.SemiBold));
                GlassSelector = new ComboBox { ItemsSource = new[] { "Klar", "Matt", "Liquid" }, SelectedIndex = settings.LiquidGlass ? 2 : settings.Glass > .65 ? 1 : 0, MinWidth = 0, Margin = new Thickness(0, 12, 0, 0), FontSize = 14 };
                GlassSelector.ToolTip = "Gemeinsamer Glasstil: Klar ist leichter, Matt dichter. Liquid bricht den echten Hintergrund an den Kanten wie eine Glaslinse und gilt für Miniaturen und Ordner.";
                GlassSelector.SelectionChanged += (_, _) =>
                {
                    bool liquid = GlassSelector.SelectedIndex == 2, toggled = liquid != settings.LiquidGlass;
                    settings.LiquidGlass = liquid;
                    if (!liquid) settings.Glass = GlassSelector.SelectedIndex == 1 ? .85 : .45;
                    app.ApplyAppearance();
                    if (toggled) Dispatcher.BeginInvoke(RefreshPage); // Show or hide the strength slider.
                };
                glass.Children.Add(GlassSelector); Grid.SetColumn(glass, 1); colorRow.Children.Add(glass);
                preview.Width = preview.Height = 108; preview.Padding = new Thickness(12); preview.BorderThickness = new Thickness(1); preview.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(preview, 2); colorRow.Children.Add(preview);
            }
            DockPanel.SetDock(colorRow, Dock.Top); content.Children.Add(colorRow);
            if (Scope == AppearanceScope.App)
            {
                colorRow.ColumnDefinitions[0].Width = new GridLength(154);
                colorRow.ColumnDefinitions.Add(new ColumnDefinition());
                var options = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
                options.Children.Add(Ui.Text("Farbmodus", 13, weight: FontWeights.SemiBold));
                var themes = new[] { "Hell", "Dunkel", "Windows-Standard" }; var values = new[] { "Light", "Dark", "System" };
                var theme = new ComboBox { ItemsSource = themes, SelectedIndex = Array.IndexOf(values, settings.Theme), MinWidth = 0 };
                System.Windows.Automation.AutomationProperties.SetName(theme, "Farbmodus der Anwendung");
                theme.SelectionChanged += (_, _) => { if (theme.SelectedIndex >= 0) { settings.Theme = values[theme.SelectedIndex]; app.ApplySettings(settings); } }; themeSelector = theme;
                var modeRow = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = true };
                modeSwatch = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(4), Background = Ui.Brush(ThemePalette.Color(settings, Ui.Dark)), BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(1) };
                ModeColorButton = Ui.Button("", () => { if (!editingModeColor) { CancelColorEdit(); OpenPicker(modeColor: true); } }); ModeColorButton.Content = modeSwatch;
                ModeColorButton.Width = 36; ModeColorButton.Height = 34; ModeColorButton.Padding = new Thickness(3); ModeColorButton.Margin = new Thickness(8, 0, 0, 0);
                ModeColorButton.ToolTip = (Ui.Dark ? "Dunkle" : "Helle") + " Modusfarbe ändern · Flächen und Steuerelemente";
                System.Windows.Automation.AutomationProperties.SetName(ModeColorButton, "Modusfarbe ändern");
                DockPanel.SetDock(ModeColorButton, Dock.Right); modeRow.Children.Add(ModeColorButton);
                modeAction = new ContentControl { Content = theme, HorizontalContentAlignment = HorizontalAlignment.Stretch }; modeRow.Children.Add(modeAction); options.Children.Add(modeRow);
                Grid.SetColumn(options, 1); colorRow.Children.Add(options);
            }
        }
        bool frosted = Scope == AppearanceScope.Window && FolderAppearance.Frosted(settings);
        double opacity = Scope switch { AppearanceScope.App => settings.AppOpacity, AppearanceScope.Window => FolderAppearance.Opacity(settings), _ => settings.Opacity };
        if (!frosted)
        {
            TransparencySlider = AddSlider(root, "Transparenz", 5, Scope == AppearanceScope.App ? 80 : 100,
                (1 - opacity) * 100, value =>
                {
                    double next = 1 - value / 100;
                    if (Scope == AppearanceScope.App) settings.AppOpacity = next;
                    else if (Scope == AppearanceScope.Window) FolderAppearance.SetTransparency(settings, value);
                    else settings.Opacity = next;
                    app.ApplyAppearance();
                }, value => $"{value:0} %");
            System.Windows.Automation.AutomationProperties.SetName(TransparencySlider, "Transparenz · " + ScopeLabel(Scope));
            if (Scope != AppearanceScope.App) TransparencySlider.ToolTip = "Gilt für alle " + (Scope == AppearanceScope.Bubbles ? "Miniaturen" : "Ordner") + ". 100 % blendet Fläche und Rahmen aus. App-Symbole bleiben sichtbar.";
        }
        LiquidStrengthSlider = null;
        if (Scope != AppearanceScope.App && settings.LiquidGlass)
        {
            // One shared value, shown on both tabs right below the transparency.
            LiquidStrengthSlider = AddSlider(root, "Liquid-Stärke", 0, 100, settings.LiquidStrength * 100, value => { settings.LiquidStrength = value / 100; app.ApplyAppearance(); }, value => $"{value:0} %");
            System.Windows.Automation.AutomationProperties.SetName(LiquidStrengthSlider, "Liquid-Stärke · " + ScopeLabel(Scope));
            LiquidStrengthSlider.ToolTip = "Wie tief die Glaskante ist und wie stark sie den Hintergrund bricht. Gilt gemeinsam für Miniaturen und Ordner; Ordner übernehmen sie beim nächsten Öffnen.";
        }
        SizeSlider = null;
        if (Scope == AppearanceScope.Window)
        {
            int columns = settings.Columns;
            SizeSlider = AddSlider(root, "Größe", columns * 76 + 48, columns * 160 + 48, columns * settings.TileSize + 48,
                value => { settings.TileSize = (value - 48) / columns; app.ApplyAppearance(); }, value => $"{value:0} px");
            SizeSlider.ToolTip = "Breite bei 100 % Windows-Skalierung. Symbole und Kontur wachsen mit. Gilt für alle Ordner.";
            var previewBar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            // One pill: the round scale button on the right, "Milchglas" permanently folded out to its left.
            PreviewScaleButton = new Button { Style = (Style)FindResource("PillSegment"), Content = previewScale == 1 ? "1:1" : "1:3", Width = 34, Padding = new Thickness(0), Background = Brushes.Transparent };
            PreviewScaleButton.Click += (_, _) => { previewScale = previewScale == 1 ? 1d / 3 : 1; var before = SettingsMotion.Snapshot(folderViewport!); UpdateFolderPreview(); FitPreview(before); };
            PreviewScaleButton.ToolTip = "Zwischen verkleinerter Vorschau und Originalgröße wechseln. 1:1 entspricht der aktuellen Windows-Skalierung.";
            var scaleCircle = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Child = PreviewScaleButton };
            scaleCircle.SetResourceReference(Border.BackgroundProperty, "HoverFill");
            FrostedFoldersButton = new Button { Style = (Style)FindResource("PillSegment"), Content = "Milchglas", Padding = new Thickness(14, 0, 12, 0) };
            FrostedFoldersButton.Click += (_, _) => { settings.FrostedFolders = !settings.FrostedFolders; app.ApplyAppearance(); RefreshPage(); };
            FrostedFoldersButton.Background = frosted ? Ui.Brush("#087F4B") : Brushes.Transparent;
            FrostedFoldersButton.Foreground = frosted ? Brushes.White : Ui.Resource("Ink");
            FrostedFoldersButton.ToolTip = frosted ? "Milchglas ausschalten · normale Farbe und Transparenz wieder verwenden" : "Milchglas für Ordner einschalten";
            System.Windows.Automation.AutomationProperties.SetName(FrostedFoldersButton, frosted ? "Milchglas für Ordner ausschalten" : "Milchglas für Ordner einschalten");
            if (settings.LiquidGlass)
            {
                FrostedFoldersButton.IsEnabled = false;
                ToolTipService.SetShowOnDisabled(FrostedFoldersButton, true);
                FrostedFoldersButton.ToolTip = "Liquid ist aktiv und hat Vorrang vor Milchglas. Unter Miniaturen → Glas umstellen.";
            }
            var previewActions = SettingsMotion.Pill(scaleCircle, FrostedFoldersButton, circleRight: true);
            DockPanel.SetDock(previewActions, Dock.Right); previewBar.Children.Add(previewActions);
            previewDimensions = Ui.Text("", 12, "Muted"); previewDimensions.VerticalAlignment = VerticalAlignment.Center; previewBar.Children.Add(previewDimensions); root.Children.Add(previewBar);
            folderStage = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 8, 0, 0) };
            preview.Padding = new Thickness(0); preview.BorderThickness = new Thickness(1d / 3); preview.HorizontalAlignment = HorizontalAlignment.Center; preview.VerticalAlignment = VerticalAlignment.Center;
            folderStage.Children.Add(preview);
            folderViewport = new ScrollViewer { Content = folderStage, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(folderViewport);
        }
        RoundingSlider = null;
        if (Scope == AppearanceScope.Bubbles)
        {
            SizeSlider = AddSlider(root, "Größe", 48, 144, settings.MiniatureSize, value => { settings.MiniatureSize = value; app.ApplyAppearance(); }, value => $"{value:0} px");
            SizeSlider.ToolTip = "Kantenlänge der Miniatur bei 100 % Windows-Skalierung. Gilt für alle Miniaturen; die Beschriftung bleibt lesbar.";
            RoundingSlider = AddSlider(root, "Eckenrundung", 0, 50, settings.CornerRoundness, value => { settings.CornerRoundness = value; app.ApplyAppearance(); }, value => $"{value:0} %");
            RoundingSlider.ToolTip = "Gemeinsame Rundung als Anteil der kürzeren Kante. Miniatur und Ordner bleiben proportional gleich gerundet.";
        }
        if (Scope == AppearanceScope.App) AddGeneralSettings(root);
        if (Scope == AppearanceScope.Window && folderViewport is not null)
        {
            root.Children.Remove(folderViewport);
            var folderLayout = new Grid();
            folderLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); folderLayout.RowDefinitions.Add(new RowDefinition());
            folderLayout.Children.Add(root); Grid.SetRow(folderViewport, 1); folderLayout.Children.Add(folderViewport); standardDetails = folderLayout;
        }
        else
        {
            var pageScroll = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Divider.Track(pageScroll); SettingsMotion.EdgeFade(pageScroll, 18); SettingsMotion.SmoothWheel(pageScroll); standardDetails = pageScroll;
        }
        if (folderViewport is not null) { SettingsMotion.AutoHideScrollBar(folderViewport); SettingsMotion.SmoothWheel(folderViewport); }
        detailLayer = new Grid(); detailLayer.Children.Add(standardDetails);
        colorOverlay = new ContentControl { Visibility = Visibility.Collapsed, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
        Panel.SetZIndex(colorOverlay, 1); detailLayer.Children.Add(colorOverlay); content.Children.Add(detailLayer);
        pane.Content = layout; RenderPreview(); RefreshMaterial();
        if (IsLoaded) Ui.Backdrop(this, settings);
    }
    private static Slider AddSlider(Panel root, string label, double minimum, double maximum, double value, Action<double> changed, Func<double, string> format)
    {
        // Title and slider row share one grip area, from the title down to the value.
        var zone = new StackPanel { Background = Brushes.Transparent };
        var title = Ui.Text(label, 13, weight: FontWeights.SemiBold); title.Margin = new Thickness(0, 8, 0, 5); zone.Children.Add(title);
        var row = new DockPanel(); var number = Ui.Text(format(value), 12); number.Width = 58; number.TextAlignment = TextAlignment.Right; number.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 10 };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { number.Text = format(slider.Value); changed(slider.Value); }; row.Children.Add(slider); zone.Children.Add(row);
        SettingsMotion.SmoothZone(zone, slider); root.Children.Add(zone); return slider;
    }
    private string CurrentColor() => Scope == AppearanceScope.App ? app.State.Settings.AppColor : AppearancePreferences.Color(app.State.Settings, selected);
    internal void OpenPicker(bool modeColor = false)
    {
        if (Picker is not null || ColorButton is null || colorOverlay is null || standardDetails is null) return;
        editingModeColor = modeColor; editingDarkMode = Ui.Dark;
        Picker = new ColorPickerPanel(modeColor ? ThemePalette.Color(app.State.Settings, editingDarkMode) : CurrentColor(), modeColor ? editingDarkMode ? app.State.Settings.DarkModeOpacity : app.State.Settings.LightModeOpacity : null); Picker.Edited += RefreshColorDraft; Picker.CancelRequested += CancelColorEdit;
        var before = SettingsMotion.Enabled(app) && detailLayer is not null ? SettingsMotion.Snapshot(detailLayer) : null;
        standardDetails.Visibility = Visibility.Hidden;
        colorOverlay.Content = new ScrollViewer { Content = Picker, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 8, 0, 0) };
        colorOverlay.Visibility = Visibility.Visible;
        if (before is not null && detailLayer is not null) SettingsMotion.PageTransition(detailLayer, before);
        var apply = ColorButton;
        if (modeColor && modeAction is not null)
        {
            modeApplyButton = Ui.Button("Anwenden", ApplyColorEdit); modeApplyButton.Margin = new Thickness(0); modeApplyButton.Padding = new Thickness(10, 7, 10, 7);
            modeAction.Content = modeApplyButton; apply = modeApplyButton; ColorButton.IsEnabled = false;
        }
        SettingsMotion.SwapContent(apply, "Anwenden"); apply.Foreground = Brushes.White; apply.IsDefault = true;
        // The whole pill turns green, also around the colour circle.
        if (apply == ColorButton && colorPill is not null) SettingsMotion.TintPill(colorPill, Ui.Brush("#087F4B").Color, SettingsMotion.Enabled(app));
        else apply.Background = Ui.Brush("#087F4B");
        RefreshColorDraft(); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => Picker?.Wheel.Focus());
        // The window grows just enough to show the whole picker; closing it gives the room back.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (Picker is not null && colorOverlay?.Content is ScrollViewer viewer && viewer.ExtentHeight - viewer.ViewportHeight > .5)
                MakeRoom("picker", 0, viewer.ExtentHeight - viewer.ViewportHeight + 4);
        });
    }
    private void RefreshColorDraft()
    {
        if (Picker is null) return;
        if (editingModeColor)
        {
            if (modeApplyButton is not null) modeApplyButton.IsEnabled = Picker.IsValid;
            if (modeSwatch is not null) modeSwatch.Background = Ui.Brush(Picker.SelectedHex);
            Ui.ApplyTheme(app.State.Settings, Picker.SelectedHex, Picker.SelectedOpacity); surface.Background = Ui.AppGlass(app.State.Settings);
            return;
        }
        if (ColorButton is not null) ColorButton.IsEnabled = Picker.IsValid;
        if (swatch is not null) swatch.Background = Ui.Brush(Picker.SelectedHex);
        if (Scope == AppearanceScope.App) surface.Background = Ui.ColorDraft(app.State.Settings, Picker.SelectedHex, application: true);
        else preview.Background = Ui.ColorDraft(app.State.Settings, Picker.SelectedHex);
    }
    internal void ApplyColorEdit()
    {
        if (Picker is null || !Picker.IsValid) return;
        string color = Picker.SelectedHex;
        if (editingModeColor)
        {
            if (editingDarkMode) { app.State.Settings.DarkModeColor = color; app.State.Settings.DarkModeOpacity = Picker.SelectedOpacity; }
            else { app.State.Settings.LightModeColor = color; app.State.Settings.LightModeOpacity = Picker.SelectedOpacity; }
            CancelColorEdit(); app.ApplySettings(app.State.Settings); return;
        }
        if (Scope == AppearanceScope.App) app.State.Settings.AppColor = color;
        else AppearancePreferences.SetColor(app.State, selected, color);
        CancelColorEdit(); app.ApplyAppearance();
    }
    internal void CancelColorEdit()
    {
        if (Picker is null) return;
        Picker.Edited -= RefreshColorDraft; Picker.CancelRequested -= CancelColorEdit; Picker = null;
        if (editingModeColor)
        {
            editingModeColor = false; Ui.ApplyTheme(app.State.Settings);
            if (modeApplyButton is not null) modeApplyButton.IsDefault = false;
            if (modeAction is not null) modeAction.Content = themeSelector;
            modeApplyButton = null;
        }
        var before = IsVisible && SettingsMotion.Enabled(app) && detailLayer is not null ? SettingsMotion.Snapshot(detailLayer) : null;
        if (colorOverlay is not null) { colorOverlay.Content = null; colorOverlay.Visibility = Visibility.Collapsed; }
        if (standardDetails is not null) standardDetails.Visibility = Visibility.Visible;
        if (before is not null && detailLayer is not null) SettingsMotion.PageTransition(detailLayer, before);
        if (ColorButton is not null)
        {
            SettingsMotion.SwapContent(ColorButton, "Ändern"); ColorButton.Background = Brushes.Transparent; ColorButton.Foreground = Ui.Resource("Ink"); ColorButton.IsDefault = false; ColorButton.IsEnabled = true;
            if (IsVisible) ColorButton.Focus();
        }
        if (colorPill is not null && colorPill.Background is not null && colorPill.Background != Ui.Resource("ControlFill")) SettingsMotion.TintPill(colorPill, null, IsVisible && SettingsMotion.Enabled(app));
        GiveBackRoom("picker");
        RefreshMaterial();
    }
    internal void OpenPickerForTest() => OpenPicker();
    private void RenderPreview()
    {
        if (Scope == AppearanceScope.App) return;
        if (Scope == AppearanceScope.Bubbles) { preview.Child = Preview(selected, 82, false); return; }
        folderCanvas = new Canvas { IsHitTestVisible = false };
        foreach (var entry in selected?.Items ?? [])
        {
            var image = new Image { Stretch = Stretch.Uniform }; folderCanvas.Children.Add(image); _ = SetIcon(image, app.Resolve(entry.Target));
        }
        preview.Child = new Viewbox { Stretch = Stretch.Fill, Child = folderCanvas };
        UpdateFolderPreview();
    }
    private void UpdateFolderPreview()
    {
        if (Scope != AppearanceScope.Window || folderCanvas is null || folderStage is null) return;
        var footprint = PreviewFootprint; var settings = app.State.Settings;
        preview.Width = footprint.Width * previewScale; preview.Height = footprint.Height * previewScale;
        preview.BorderThickness = new Thickness(previewScale);
        preview.CornerRadius = new CornerRadius(AppearanceGeometry.Radius(settings.CornerRoundness, footprint.Width, footprint.Height) * previewScale);
        folderStage.Width = preview.Width + 8; folderStage.Height = preview.Height + 8;
        folderCanvas.Width = footprint.Width; folderCanvas.Height = footprint.Height;
        folderCanvas.Clip = new RectangleGeometry(new Rect(0, 0, footprint.Width, footprint.Height));
        double icon = Math.Min(112, settings.TileSize * 72 / 116);
        for (int i = 0; i < folderCanvas.Children.Count; i++)
        {
            var image = (Image)folderCanvas.Children[i]; image.Width = image.Height = icon;
            Canvas.SetLeft(image, 22 + (i % settings.Columns) * settings.TileSize + (settings.TileSize - icon) / 2);
            Canvas.SetTop(image, 22 + (i / settings.Columns) * settings.TileSize + (settings.TileSize - icon) / 2);
        }
        if (previewDimensions is not null) previewDimensions.Text = $"{footprint.Width:0} × {footprint.Height:0} px · Vorschau";
        if (PreviewScaleButton is not null) SettingsMotion.SwapContent(PreviewScaleButton, previewScale == 1 ? "1:1" : "1:3");
    }
    private Border Preview(BubbleGroup? group, double size, bool material = true)
    {
        var grid = new UniformGrid { Rows = 3, Columns = 3 };
        var border = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(AppearanceGeometry.Radius(app.State.Settings.CornerRoundness, size, size)), Background = material ? Ui.Glass(app.State.Settings, group) : Brushes.Transparent, Padding = material ? new Thickness(7) : new Thickness(0), Child = grid };
        foreach (var entry in group?.Items.Take(9) ?? []) { var image = new Image { Margin = new Thickness(2), Stretch = Stretch.Uniform }; grid.Children.Add(image); _ = SetIcon(image, app.Resolve(entry.Target)); }
        if (group is null || group.Items.Count == 0) for (int i = 0; i < 9; i++) grid.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = Ui.Resource("Muted"), Opacity = .16, Margin = new Thickness(3) });
        return border;
    }
    private async Task SetIcon(Image image, string target) => image.Source = await app.Icons.Get(target);
    private void AddGeneralSettings(StackPanel root)
    {
        var label = Ui.Text("Allgemein", 17, weight: FontWeights.SemiBold); label.Margin = new Thickness(0, 24, 0, 8); root.Children.Add(label);
        var colors = new Grid { Margin = new Thickness(0, 0, 0, 8) }; colors.ColumnDefinitions.Add(new ColumnDefinition()); colors.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        LinkColorsCheckBox = new CheckBox { Content = new TextBlock { Text = "Bubble-Farben für alle übernehmen", TextWrapping = TextWrapping.Wrap }, IsChecked = app.State.Settings.LinkBubbleColors, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        LinkColorsCheckBox.ToolTip = "Übernimmt die Farbe der ausgewählten Bubble für alle Miniaturen und Ordner. Danach gilt jede Farbänderung für alle. Die App-Farbe bleibt separat.";
        LinkColorsCheckBox.Click += (_, _) => { AppearancePreferences.LinkColors(app.State, selected, LinkColorsCheckBox.IsChecked == true); app.ApplyAppearance(); };
        colors.Children.Add(LinkColorsCheckBox);
        var reset = Ui.Button("Farben zurücksetzen", () => { CancelColorEdit(); AppearancePreferences.ResetColors(app.State); app.ApplySettings(app.State.Settings); });
        reset.Style = (Style)FindResource("ResetColorsButton");
        reset.ToolTip = "Setzt App- und alle Bubble-Farben zurück. Transparenz, Größe und Rundung bleiben erhalten.";
        reset.Margin = new Thickness(0); reset.Padding = new Thickness(10, 7, 10, 7); reset.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(reset, 1); colors.Children.Add(reset); root.Children.Add(colors);
        // Auto close: switch on the left, seconds stepper on the right.
        var closing = new Grid { Margin = new Thickness(0, 0, 0, 8) }; closing.ColumnDefinitions.Add(new ColumnDefinition()); closing.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var s = app.State.Settings;
        AutoCloseCheckBox = new CheckBox { Content = new TextBlock { Text = "Bubble automatisch schließen", TextWrapping = TextWrapping.Wrap }, IsChecked = s.AutoCloseFolders, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        AutoCloseCheckBox.ToolTip = "Schließt einen geöffneten Ordner, wenn die Maus so lange nicht auf ihm war. Mit der Maus darauf läuft keine Zeit ab.";
        var seconds = Ui.Text("", 13); seconds.Width = 52; seconds.TextWrapping = TextWrapping.NoWrap; seconds.TextAlignment = TextAlignment.Center; seconds.VerticalAlignment = VerticalAlignment.Center;
        var stepper = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Button less = null!, more = null!;
        Button Step(string glyph, string name, int delta)
        {
            var button = Ui.IconButton(glyph, name, () => { s.AutoCloseSeconds = Math.Clamp(s.AutoCloseSeconds + delta, 1, 120); ShowSeconds(); app.ApplyAutoClose(); });
            // No focus: a focused control is scrolled into view, which made the whole page jump.
            button.Padding = new Thickness(7); button.Focusable = false; return button;
        }
        less = Step("", "Eine Sekunde weniger", -1); more = Step("", "Eine Sekunde mehr", 1);
        void ShowSeconds()
        {
            seconds.Text = $"{s.AutoCloseSeconds} s"; less.IsEnabled = s.AutoCloseFolders && s.AutoCloseSeconds > 1; more.IsEnabled = s.AutoCloseFolders && s.AutoCloseSeconds < 120;
            seconds.Opacity = stepper.Opacity = s.AutoCloseFolders ? 1 : .45;
            System.Windows.Automation.AutomationProperties.SetName(seconds, $"Ordner schließen nach {s.AutoCloseSeconds} Sekunden");
        }
        AutoCloseCheckBox.Click += (_, _) => { s.AutoCloseFolders = AutoCloseCheckBox.IsChecked == true; ShowSeconds(); app.ApplyAutoClose(); };
        stepper.Children.Add(less); stepper.Children.Add(seconds); stepper.Children.Add(more); ShowSeconds();
        AutoCloseSeconds = seconds;
        closing.Children.Add(AutoCloseCheckBox); Grid.SetColumn(stepper, 1); closing.Children.Add(stepper); root.Children.Add(closing);
        CheckBox Check(string text, bool value, Action<bool> set)
        {
            var box = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = value };
            box.Click += (_, _) => { bool next = box.IsChecked == true; try { set(next); } catch (Exception ex) { box.IsChecked = !next; Ui.Error(ex); } }; root.Children.Add(box);
            return box;
        }
        bool auto = false, otherStartup = false;
        try { auto = StartupService.EnabledFor(app.Portable, app.DataDirectory); otherStartup = !auto && StartupService.HasRegistration; } catch (Exception ex) { Log.Write(ex.Message); }
        var startup = Check("Mit Windows starten", auto, value => StartupService.Set(value, app.Portable, app.DataDirectory));
        startup.ToolTip = otherStartup
            ? "Eine andere DesktopBubbles-Ausgabe oder ein anderer Datenordner ist für den Windows-Start eingetragen. Aktiviere diese Option, um stattdessen diese Ausgabe mit ihren aktuellen Gruppen zu starten."
            : "Startet diese Ausgabe von DesktopBubbles mit genau ihrem aktuellen Datenordner.";
        Check("Desktop-Originale nach Übernahme entfernen", app.State.Settings.MoveDesktopShortcuts, value => { app.State.Settings.MoveDesktopShortcuts = value; app.RequestSave(); });
        var collect = Check("Von Updates neu angelegte Verknüpfungen einsammeln", app.State.Settings.CollectDesktopDuplicates, value => { app.State.Settings.CollectDesktopDuplicates = value; app.RequestSave(); if (value) app.Duplicates?.Scan(); });
        collect.ToolTip = "Legt ein Programm-Update eine Desktop-Verknüpfung neu an, obwohl das Programm schon in einer Bubble ist, wandert sie automatisch zurück in die Bubble. Andere Verknüpfungen bleiben unberührt. Wirkt nur, wenn Desktop-Originale entfernt werden.";
        CollectDuplicatesCheckBox = collect;
        Check("Desktop-Zuordnung verwenden", app.State.Settings.DesktopIntegration, value => { app.State.Settings.DesktopIntegration = value; app.Changed(true); });
        Check("Hintergrund weichzeichnen", app.State.Settings.Blur, value => { app.State.Settings.Blur = value; app.ApplySettings(app.State.Settings); });
        var actions = Ui.Text("Daten & Werkzeuge", 17, weight: FontWeights.SemiBold); actions.Margin = new Thickness(0, 20, 0, 10); root.Children.Add(actions);
        void Action(string label, Action action, string help) { var button = Ui.Button(label, action); button.ToolTip = help; button.Margin = new Thickness(0, 0, 0, 8); button.HorizontalContentAlignment = HorizontalAlignment.Left; root.Children.Add(button); }
        Action("Sicherung exportieren …", app.Export, "Speichert Gruppen, Einstellungen und verwaltete Verknüpfungen als .bubbles-Datei. Programme werden nicht mitkopiert.");
        Action("Sicherung importieren …", app.Import, "Lädt eine Sicherung nach Rückfrage. Der bisherige Zustand wird zusätzlich gesichert.");
        Action("Doppelte Verknüpfungen vom öffentlichen Desktop entfernen …", app.RemoveSharedDuplicates, "Zeigt Verknüpfungen auf dem öffentlichen Desktop, deren Programm schon in einer Bubble ist, und entfernt sie nach Bestätigung. Windows fragt dafür nach Administratorrechten.");
        Action("Verknüpfungen auf Desktop zurücklegen", app.RestoreAllShortcuts, "Legt gesicherte Desktop-Verknüpfungen zusätzlich auf den Desktop. Die Einträge bleiben in ihren Gruppen. Hilfreich vor dem Entfernen der App.");
        Action("Programmordner öffnen", () => { try { Process.Start(new ProcessStartInfo(AppContext.BaseDirectory) { UseShellExecute = true }); } catch (Exception ex) { Ui.Error(ex); } }, "Öffnet den Ordner mit DesktopBubbles.exe.");
        Action("Datenordner öffnen", () => { try { Process.Start(new ProcessStartInfo(app.DataDirectory) { UseShellExecute = true }); } catch (Exception ex) { Ui.Error(ex); } }, "Öffnet den tatsächlich verwendeten Speicher für Gruppen, Einstellungen und interne Verknüpfungen: " + app.DataDirectory);
        Action("Desktop-Bubbles neu verbinden", app.Rebuild, "Baut die Desktop-Fenster neu auf, zum Beispiel nach einem Explorer-Neustart. Inhalte und Positionen bleiben erhalten.");
        Action("Diagnosebericht speichern", async () => { try { await Diagnostics.Measure(app); } catch (Exception ex) { Ui.Error(ex); } }, "Misst diesen Prozess zehn Sekunden lang und speichert einen lokalen Bericht. Es wird nichts hochgeladen.");
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal enum AppearanceScope { App, Bubbles, Window }

internal sealed class SettingsWindow : Window
{
    private readonly App app;
    private readonly Border surface, preview = new() { Name = "AppearancePreview" };
    private readonly StackPanel groups = new();
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
    internal AppearanceScope Scope { get; private set; } = AppearanceScope.Bubbles;
    internal Slider? TransparencySlider { get; private set; }
    internal Slider? SizeSlider { get; private set; }
    internal Slider? RoundingSlider { get; private set; }
    internal LiveMeasurementBar Measurement { get; }
    internal ColorPickerPanel? Picker { get; private set; }
    public SettingsWindow(App app)
    {
        this.app = app; Title = "DesktopBubbles";
        Width = 660; Height = 576; MinWidth = 600; MinHeight = 510;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
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
        var body = new Grid(); Grid.SetRow(body, 1); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) }); body.ColumnDefinitions.Add(new ColumnDefinition()); layout.Children.Add(body);
        var rail = new Border { BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(12, 14, 10, 14) };
        rail.Child = new ScrollViewer { Content = groups, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; body.Children.Add(rail);
        pane.Margin = new Thickness(28, 18, 26, 14); Grid.SetColumn(pane, 1); body.Children.Add(pane);
        var bottom = new Grid(); Grid.SetRow(bottom, 2); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) }); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        var bottomLine = new Border { BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Child = bottom }; Grid.SetRow(bottomLine, 2); layout.Children.Add(bottomLine);
        var create = Ui.Button("Neu", app.CreateGroup); create.Margin = new Thickness(20, 18, 20, 18); create.ToolTip = "Neue Bubble"; bottom.Children.Add(create);
        Measurement = new LiveMeasurementBar(this); Grid.SetColumn(Measurement, 1); bottom.Children.Add(Measurement);
        settingsButton = Ui.IconButton("\uE713", "Einstellungen", () => SelectScope(Scope == AppearanceScope.App ? AppearanceScope.Bubbles : AppearanceScope.App));
        settingsButton.ToolTip = "Anwendung und allgemeine Einstellungen"; settingsButton.Width = settingsButton.Height = 36;
        settingsButton.VerticalAlignment = VerticalAlignment.Center; settingsButton.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(settingsButton, 2); bottom.Children.Add(settingsButton);
        SourceInitialized += (_, _) => Ui.Backdrop(this, app.State.Settings);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { if (Picker is not null) CancelColorEdit(); else Close(); e.Handled = true; }
            if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { app.CreateGroup(); e.Handled = true; }
        };
        Closed += (_, _) => { CancelColorEdit(); Measurement.Dispose(); app.Flush(); };
        RefreshGroups(); RefreshPage();
    }
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
        surface.Background = Ui.AppGlass(app.State.Settings); surface.BorderBrush = Ui.GlassRim();
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
        groups.Children.Clear(); thumbnails.Clear();
        foreach (var group in app.State.Groups)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var thumb = Preview(group, 56); thumbnails[group.Id] = thumb; thumb.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(thumb);
            var label = Ui.Text(group.Name, 12, weight: FontWeights.Medium); label.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(label, 1); row.Children.Add(label);
            var button = Ui.Button("", () => { CancelColorEdit(); selected = group; if (Scope == AppearanceScope.App) Scope = AppearanceScope.Bubbles; RefreshGroups(); RefreshPage(); });
            button.Content = row; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Margin = new Thickness(0, 0, 0, 9); button.Padding = new Thickness(8, 10, 8, 10);
            if (group == selected) button.SetResourceReference(Button.BackgroundProperty, "HoverFill"); else button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0); button.ToolTip = group.Name + " · Rechtsklick für Aktionen";
            button.ContextMenu = GroupActionsMenu.Create(app, group); button.AllowDrop = true;
            button.DragOver += (_, e) => { e.Effects = app.IsInternalDrag(e.Data) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            button.Drop += (_, e) => { app.ReceiveDrop(group, e.Data, true); e.Handled = true; };
            groups.Children.Add(button);
        }
        if (app.State.Groups.Count == 0) { var empty = Ui.Text("Noch keine Bubbles", 12, "Muted"); empty.Margin = new Thickness(8, 10, 8, 0); groups.Children.Add(empty); }
        RenderPreview(); if (changedSelection && IsLoaded) RefreshPage(); UpdateStatus();
    }
    private static string ScopeLabel(AppearanceScope scope) => scope == AppearanceScope.Bubbles ? "Miniatur" : scope == AppearanceScope.Window ? "Ordner" : "Einstellungen";
    internal void SelectScope(AppearanceScope scope) { CancelColorEdit(); Scope = scope; RefreshPage(); }
    internal void RefreshPage()
    {
        CancelColorEdit();
        var layout = new DockPanel { LastChildFill = true };
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        var headingRow = new Grid();
        var heading = Ui.Text(Scope == AppearanceScope.App ? "Einstellungen" : "Aussehen", 27, weight: FontWeights.SemiBold); heading.TextAlignment = TextAlignment.Center; heading.VerticalAlignment = VerticalAlignment.Center;
        headingRow.Children.Add(heading); header.Children.Add(headingRow);
        if (Scope == AppearanceScope.App)
        {
            heading.Margin = new Thickness(40, 0, 40, 0); headingRow.Margin = new Thickness(0, 0, 0, 24);
            var back = Ui.IconButton("\uE72B", "Zurück zur Miniaturansicht", () => SelectScope(AppearanceScope.Bubbles));
            back.Width = back.Height = 36; back.HorizontalAlignment = HorizontalAlignment.Left; back.VerticalAlignment = VerticalAlignment.Center;
            headingRow.Children.Add(back);
        }
        settingsButton.Background = Scope == AppearanceScope.App ? Ui.Resource("Accent") : Brushes.Transparent;
        settingsButton.Foreground = Scope == AppearanceScope.App ? Brushes.White : Ui.Resource("Ink");
        if (Scope != AppearanceScope.App)
        {
            var tabs = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 16, 0, 18) };
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (var scope in new[] { AppearanceScope.Bubbles, AppearanceScope.Window })
            {
                var button = Ui.Button(ScopeLabel(scope), () => SelectScope(scope));
                button.Margin = new Thickness(0); button.Padding = new Thickness(10, 8, 10, 8);
                button.ToolTip = scope switch { AppearanceScope.App => "Anwendung und allgemeine Einstellungen", AppearanceScope.Bubbles => "Mini-Bubbles auf dem Desktop", _ => "Geöffnetes App-Raster" };
                System.Windows.Automation.AutomationProperties.SetName(button, scope == AppearanceScope.App ? "Einstellungen" : ScopeLabel(scope));
                if (Scope == scope) { button.Background = Ui.Resource("Accent"); button.Foreground = Brushes.White; }
                Grid.SetColumn(button, scope == AppearanceScope.Bubbles ? 0 : 1); tabs.Children.Add(button);
            }
            header.Children.Add(tabs);
        }
        var content = new DockPanel { LastChildFill = true }; layout.Children.Add(content);
        var root = new StackPanel(); var settings = app.State.Settings;
        swatch = null; ColorButton = null; GlassSelector = null; folderCanvas = null; folderStage = null; folderViewport = null; previewDimensions = null; PreviewScaleButton = null; LinkColorsCheckBox = null;
        FrostedFoldersButton = null; TransparencySlider = null;
        modeSwatch = null; ModeColorButton = null; modeAction = null; modeApplyButton = null; themeSelector = null;
        if (preview.Parent is Panel previous) previous.Children.Remove(preview);
        if (Scope != AppearanceScope.Window)
        {
            var colorRow = new Grid { MinHeight = Scope == AppearanceScope.App ? 80 : 108 };
            colorRow.ColumnDefinitions.Add(new ColumnDefinition());
            if (Scope == AppearanceScope.Bubbles) { colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) }); colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) }); }
            var controls = new StackPanel(); controls.Children.Add(Ui.Text(Scope == AppearanceScope.App ? "App-Farbe" : "Farbe", 13, weight: FontWeights.SemiBold));
            var change = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            swatch = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderBrush = Brushes.White, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
            change.Children.Add(swatch);
            ColorButton = Ui.Button("Ändern", () => { if (Picker is null) OpenPicker(); else ApplyColorEdit(); }); ColorButton.Margin = new Thickness(10, 0, 0, 0); ColorButton.Padding = new Thickness(15, 7, 15, 7); change.Children.Add(ColorButton);
            controls.Children.Add(change); colorRow.Children.Add(controls);
            if (Scope == AppearanceScope.Bubbles)
            {
                var glass = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; glass.Children.Add(Ui.Text("Glas", 13, weight: FontWeights.SemiBold));
                GlassSelector = new ComboBox { ItemsSource = new[] { "Klar", "Matt" }, SelectedIndex = settings.Glass > .65 ? 1 : 0, MinWidth = 0, Margin = new Thickness(0, 14, 0, 0) };
                GlassSelector.ToolTip = "Gemeinsamer Glasstil: Klar ist leichter, Matt dichter. Gilt für Miniaturen und Ordner ohne eigenen Milchglas-Effekt.";
                GlassSelector.SelectionChanged += (_, _) => { settings.Glass = GlassSelector.SelectedIndex == 1 ? .85 : .45; app.ApplyAppearance(); };
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
        bool frosted = Scope == AppearanceScope.Window && settings.FrostedFolders;
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
        SizeSlider = null;
        if (Scope == AppearanceScope.Window)
        {
            int columns = settings.Columns;
            SizeSlider = AddSlider(root, "Größe", columns * 76 + 48, columns * 160 + 48, columns * settings.TileSize + 48,
                value => { settings.TileSize = (value - 48) / columns; app.ApplyAppearance(); }, value => $"{value:0} px");
            SizeSlider.ToolTip = "Breite bei 100 % Windows-Skalierung. Symbole und Kontur wachsen mit. Gilt für alle Ordner.";
            var previewBar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            PreviewScaleButton = Ui.Button(previewScale == 1 ? "1:1" : "1:3", () => { previewScale = previewScale == 1 ? 1d / 3 : 1; UpdateFolderPreview(); });
            PreviewScaleButton.ToolTip = "Zwischen verkleinerter Vorschau und Originalgröße wechseln. 1:1 entspricht der aktuellen Windows-Skalierung.";
            PreviewScaleButton.Padding = new Thickness(12, 5, 12, 5); PreviewScaleButton.Margin = new Thickness(0);
            var previewActions = new StackPanel { Orientation = Orientation.Horizontal };
            FrostedFoldersButton = Ui.Button("Milchglas", () => { settings.FrostedFolders = !settings.FrostedFolders; app.ApplyAppearance(); RefreshPage(); });
            FrostedFoldersButton.Padding = new Thickness(12, 5, 12, 5); FrostedFoldersButton.Margin = new Thickness(0, 0, 8, 0);
            FrostedFoldersButton.Background = frosted ? Ui.Brush("#087F4B") : Ui.Resource("ControlFill");
            FrostedFoldersButton.Foreground = frosted ? Brushes.White : Ui.Resource("Ink");
            FrostedFoldersButton.ToolTip = frosted ? "Milchglas ausschalten · normale Farbe und Transparenz wieder verwenden" : "Milchglas für Ordner einschalten";
            System.Windows.Automation.AutomationProperties.SetName(FrostedFoldersButton, frosted ? "Milchglas für Ordner ausschalten" : "Milchglas für Ordner einschalten");
            previewActions.Children.Add(FrostedFoldersButton); previewActions.Children.Add(PreviewScaleButton);
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
        else standardDetails = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var detailLayer = new Grid(); detailLayer.Children.Add(standardDetails);
        colorOverlay = new ContentControl { Visibility = Visibility.Collapsed, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
        Panel.SetZIndex(colorOverlay, 1); detailLayer.Children.Add(colorOverlay); content.Children.Add(detailLayer);
        pane.Content = layout; RenderPreview(); RefreshMaterial();
        if (IsLoaded) Ui.Backdrop(this, settings);
    }
    private static Slider AddSlider(Panel root, string label, double minimum, double maximum, double value, Action<double> changed, Func<double, string> format)
    {
        var title = Ui.Text(label, 13, weight: FontWeights.SemiBold); title.Margin = new Thickness(0, 8, 0, 5); root.Children.Add(title);
        var row = new DockPanel(); var number = Ui.Text(format(value), 12); number.Width = 58; number.TextAlignment = TextAlignment.Right; number.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 10 };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { number.Text = format(slider.Value); changed(slider.Value); }; row.Children.Add(slider); root.Children.Add(row); return slider;
    }
    private string CurrentColor() => Scope == AppearanceScope.App ? app.State.Settings.AppColor : AppearancePreferences.Color(app.State.Settings, selected);
    internal void OpenPicker(bool modeColor = false)
    {
        if (Picker is not null || ColorButton is null || colorOverlay is null || standardDetails is null) return;
        editingModeColor = modeColor; editingDarkMode = Ui.Dark;
        Picker = new ColorPickerPanel(modeColor ? ThemePalette.Color(app.State.Settings, editingDarkMode) : CurrentColor(), modeColor ? editingDarkMode ? app.State.Settings.DarkModeOpacity : app.State.Settings.LightModeOpacity : null); Picker.Edited += RefreshColorDraft; Picker.CancelRequested += CancelColorEdit;
        standardDetails.Visibility = Visibility.Hidden;
        colorOverlay.Content = new ScrollViewer { Content = Picker, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 8, 0, 0) };
        colorOverlay.Visibility = Visibility.Visible;
        var apply = ColorButton;
        if (modeColor && modeAction is not null)
        {
            modeApplyButton = Ui.Button("Anwenden", ApplyColorEdit); modeApplyButton.Margin = new Thickness(0); modeApplyButton.Padding = new Thickness(10, 7, 10, 7);
            modeAction.Content = modeApplyButton; apply = modeApplyButton; ColorButton.IsEnabled = false;
        }
        apply.Content = "Anwenden"; apply.Background = Ui.Brush("#087F4B"); apply.Foreground = Brushes.White; apply.IsDefault = true;
        RefreshColorDraft(); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => Picker?.Wheel.Focus());
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
        if (colorOverlay is not null) { colorOverlay.Content = null; colorOverlay.Visibility = Visibility.Collapsed; }
        if (standardDetails is not null) standardDetails.Visibility = Visibility.Visible;
        if (ColorButton is not null)
        {
            ColorButton.Content = "Ändern"; ColorButton.Background = Ui.Resource("ControlFill"); ColorButton.Foreground = Ui.Resource("Ink"); ColorButton.IsDefault = false; ColorButton.IsEnabled = true;
            if (IsVisible) ColorButton.Focus();
        }
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
        if (PreviewScaleButton is not null) PreviewScaleButton.Content = previewScale == 1 ? "1:1" : "1:3";
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
        Check("Desktop-Zuordnung verwenden", app.State.Settings.DesktopIntegration, value => { app.State.Settings.DesktopIntegration = value; app.Changed(true); });
        Check("Hintergrund weichzeichnen", app.State.Settings.Blur, value => { app.State.Settings.Blur = value; app.ApplySettings(app.State.Settings); });
        var actions = Ui.Text("Daten & Werkzeuge", 17, weight: FontWeights.SemiBold); actions.Margin = new Thickness(0, 20, 0, 10); root.Children.Add(actions);
        void Action(string label, Action action, string help) { var button = Ui.Button(label, action); button.ToolTip = help; button.Margin = new Thickness(0, 0, 0, 8); button.HorizontalContentAlignment = HorizontalAlignment.Left; root.Children.Add(button); }
        Action("Sicherung exportieren …", app.Export, "Speichert Gruppen, Einstellungen und verwaltete Verknüpfungen als .bubbles-Datei. Programme werden nicht mitkopiert.");
        Action("Sicherung importieren …", app.Import, "Lädt eine Sicherung nach Rückfrage. Der bisherige Zustand wird zusätzlich gesichert.");
        Action("Verknüpfungen auf Desktop zurücklegen", app.RestoreAllShortcuts, "Legt gesicherte Desktop-Verknüpfungen zusätzlich auf den Desktop. Die Einträge bleiben in ihren Gruppen. Hilfreich vor dem Entfernen der App.");
        Action("Programmordner öffnen", () => { try { Process.Start(new ProcessStartInfo(AppContext.BaseDirectory) { UseShellExecute = true }); } catch (Exception ex) { Ui.Error(ex); } }, "Öffnet den Ordner mit DesktopBubbles.exe.");
        Action("Datenordner öffnen", () => { try { Process.Start(new ProcessStartInfo(app.DataDirectory) { UseShellExecute = true }); } catch (Exception ex) { Ui.Error(ex); } }, "Öffnet den tatsächlich verwendeten Speicher für Gruppen, Einstellungen und interne Verknüpfungen: " + app.DataDirectory);
        Action("Desktop-Bubbles neu verbinden", app.Rebuild, "Baut die Desktop-Fenster neu auf, zum Beispiel nach einem Explorer-Neustart. Inhalte und Positionen bleiben erhalten.");
        Action("Diagnosebericht speichern", async () => { try { await Diagnostics.Measure(app); } catch (Exception ex) { Ui.Error(ex); } }, "Misst diesen Prozess zehn Sekunden lang und speichert einen lokalen Bericht. Es wird nichts hochgeladen.");
    }
}

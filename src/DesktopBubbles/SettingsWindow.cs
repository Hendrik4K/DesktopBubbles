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
    private readonly Dictionary<Guid, Border> thumbnails = [];
    private Border? swatch;
    private Canvas? folderCanvas;
    private Grid? folderStage;
    private TextBlock? previewDimensions;
    internal ComboBox? GlassSelector { get; private set; }
    internal Border AppearancePreview => preview;
    internal PixelRect PreviewFootprint => AppearanceGeometry.Folder(app.State.Settings, selected?.Items.Count ?? 0);
    private BubbleGroup? selected;
    internal AppearanceScope Scope { get; private set; } = AppearanceScope.Bubbles;
    internal Slider? TransparencySlider { get; private set; }
    internal Slider? SizeSlider { get; private set; }
    internal Slider? RoundingSlider { get; private set; }
    internal LiveMeasurementBar Measurement { get; }
    internal ColorPickerWindow? Picker { get; private set; }
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
        var bottom = new Grid(); Grid.SetRow(bottom, 2); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) }); bottom.ColumnDefinitions.Add(new ColumnDefinition());
        var bottomLine = new Border { BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Child = bottom }; Grid.SetRow(bottomLine, 2); layout.Children.Add(bottomLine);
        var create = Ui.Button("Neu", app.CreateGroup); create.Margin = new Thickness(20, 18, 20, 18); create.ToolTip = "Neue Bubble"; bottom.Children.Add(create);
        Measurement = new LiveMeasurementBar(this); Grid.SetColumn(Measurement, 1); bottom.Children.Add(Measurement);
        SourceInitialized += (_, _) => Ui.Backdrop(this, app.State.Settings);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { app.CreateGroup(); e.Handled = true; }
        };
        Closed += (_, _) => { Picker?.Close(); Measurement.Dispose(); app.Flush(); };
        RefreshGroups(); RefreshPage();
    }
    private static UIElement Logo()
    {
        var grid = new UniformGrid { Rows = 2, Columns = 2, Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0) };
        foreach (var color in new[] { "#1185A6", "#79CBD8", "#ADDDE7", "#30637D" }) grid.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = Ui.Brush(color), Margin = new Thickness(1) });
        return grid;
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
        double opacity = Scope == AppearanceScope.Window ? selected?.WindowOpacity ?? app.State.Settings.WindowOpacity : selected?.Opacity ?? app.State.Settings.Opacity;
        preview.BorderBrush = !SystemParameters.HighContrast && opacity <= 0 ? Brushes.Transparent : Ui.GlassRim();
        UpdateFolderPreview();
        foreach (var group in app.State.Groups) if (thumbnails.TryGetValue(group.Id, out var thumbnail)) { thumbnail.Background = Ui.Glass(app.State.Settings, group); thumbnail.CornerRadius = new CornerRadius(app.State.Settings.BubbleCornerRadius * 56 / 76); }
    }
    internal void RefreshGroups()
    {
        bool changedSelection = selected is null || !app.State.Groups.Contains(selected);
        if (changedSelection) selected = app.State.Groups.FirstOrDefault();
        groups.Children.Clear(); thumbnails.Clear();
        foreach (var group in app.State.Groups)
        {
            var row = new DockPanel { LastChildFill = true };
            var thumb = Preview(group, 56); thumbnails[group.Id] = thumb; thumb.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(thumb, Dock.Left); row.Children.Add(thumb);
            var label = Ui.Text(group.Name, 12, weight: FontWeights.Medium); label.VerticalAlignment = VerticalAlignment.Center; label.MaxWidth = 68; row.Children.Add(label);
            var button = Ui.Button("", () => { selected = group; if (Scope == AppearanceScope.App) Scope = AppearanceScope.Bubbles; RefreshGroups(); RefreshPage(); });
            button.Content = row; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Margin = new Thickness(0, 0, 0, 9); button.Padding = new Thickness(8, 10, 8, 10);
            button.Background = group == selected ? Ui.Resource("HoverFill") : Brushes.Transparent; button.BorderThickness = new Thickness(0); button.ToolTip = group.Name + " · Rechtsklick für Aktionen";
            button.ContextMenu = GroupMenu(group); button.AllowDrop = true;
            button.DragOver += (_, e) => { e.Effects = app.IsInternalDrag(e.Data) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            button.Drop += (_, e) => { app.ReceiveDrop(group, e.Data, true); e.Handled = true; };
            groups.Children.Add(button);
        }
        if (app.State.Groups.Count == 0) { var empty = Ui.Text("Noch keine Bubbles", 12, "Muted"); empty.Margin = new Thickness(8, 10, 8, 0); groups.Children.Add(empty); }
        RenderPreview(); if (changedSelection && IsLoaded) RefreshPage(); UpdateStatus();
    }
    private ContextMenu GroupMenu(BubbleGroup group)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Ui.Menu("Auf dem Desktop finden", () => app.ShowDesktop(group)));
        menu.Items.Add(Ui.Menu("Öffnen", () => { var bubble = app.Bubbles.FirstOrDefault(b => b.Group == group); if (bubble is not null) app.Open(bubble); }));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("Dateien / Verknüpfungen hinzufügen …", () => app.AddFiles(group)));
        menu.Items.Add(Ui.Menu("Installierte App hinzufügen …", () => app.PickApp(group)));
        menu.Items.Add(Ui.Menu("Ordner hinzufügen …", () => app.AddFolder(group)));
        menu.Items.Add(Ui.Menu("Webadresse hinzufügen …", () => app.AddWeb(group)));
        menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("Umbenennen …", () => app.Rename(group)));
        var name = Ui.Menu("Name unter Bubble anzeigen", () => app.ToggleName(group)); name.IsCheckable = true; name.IsChecked = group.ShowName; menu.Items.Add(name);
        menu.Items.Add(Ui.Menu("Gruppensymbol wählen …", () => app.SetIcon(group)));
        menu.Items.Add(Ui.Menu("Mini-Icons verwenden", () => { group.CustomIcon = null; app.Changed(true); }));
        menu.Items.Add(Ui.Menu("Standardfarben und Transparenz verwenden", () => { group.Color = null; group.Opacity = null; group.WindowColor = null; group.WindowOpacity = null; app.ApplyAppearance(); RefreshPage(); }));
        menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("Bubble entfernen …", () => app.Delete(group))); return menu;
    }
    private static string ScopeLabel(AppearanceScope scope) => scope == AppearanceScope.Bubbles ? "Miniatur" : scope == AppearanceScope.Window ? "Ordner" : "Einstellungen";
    internal void SelectScope(AppearanceScope scope) { Picker?.Close(); Scope = scope; RefreshPage(); }
    internal void RefreshPage()
    {
        var layout = new DockPanel { LastChildFill = true };
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        header.Children.Add(Ui.Text(Scope == AppearanceScope.App ? "Einstellungen" : "Aussehen", 27, weight: FontWeights.SemiBold));
        var tabs = new Grid { Width = 296, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 18) };
        tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        tabs.ColumnDefinitions.Add(new ColumnDefinition()); tabs.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var scope in new[] { AppearanceScope.App, AppearanceScope.Bubbles, AppearanceScope.Window })
        {
            var button = scope == AppearanceScope.App ? Ui.IconButton("\uE713", "Einstellungen der Anwendung", () => SelectScope(scope))
                : Ui.Button(ScopeLabel(scope), () => SelectScope(scope));
            button.Margin = new Thickness(0); button.Padding = new Thickness(10, 8, 10, 8);
            button.ToolTip = scope switch { AppearanceScope.App => "Anwendung und allgemeine Einstellungen", AppearanceScope.Bubbles => "Mini-Bubbles auf dem Desktop", _ => "Geöffnetes App-Raster" };
            System.Windows.Automation.AutomationProperties.SetName(button, scope == AppearanceScope.App ? "Einstellungen" : ScopeLabel(scope));
            if (Scope == scope) { button.Background = Ui.Resource("Accent"); button.Foreground = Brushes.White; }
            Grid.SetColumn(button, (int)scope); tabs.Children.Add(button);
        }
        header.Children.Add(tabs);
        var root = new StackPanel(); var settings = app.State.Settings;
        swatch = null; GlassSelector = null; folderCanvas = null; folderStage = null; previewDimensions = null;
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
            var edit = Ui.Button("Ändern", () => { }); edit.Click += (_, _) => OpenPicker(edit); edit.Margin = new Thickness(10, 0, 0, 0); edit.Padding = new Thickness(15, 7, 15, 7); change.Children.Add(edit);
            controls.Children.Add(change); colorRow.Children.Add(controls);
            if (Scope == AppearanceScope.Bubbles)
            {
                var glass = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; glass.Children.Add(Ui.Text("Glas", 13, weight: FontWeights.SemiBold));
                GlassSelector = new ComboBox { ItemsSource = new[] { "Klar", "Matt" }, SelectedIndex = settings.Glass > .65 ? 1 : 0, MinWidth = 0, Margin = new Thickness(0, 14, 0, 0) };
                GlassSelector.ToolTip = "Gemeinsamer Glasstil: Klar ist leichter, Matt dichter. Gilt für Miniatur und Ordner.";
                GlassSelector.SelectionChanged += (_, _) => { settings.Glass = GlassSelector.SelectedIndex == 1 ? .85 : .45; app.ApplyAppearance(); };
                glass.Children.Add(GlassSelector); Grid.SetColumn(glass, 1); colorRow.Children.Add(glass);
                preview.Width = preview.Height = 108; preview.Padding = new Thickness(12); preview.BorderThickness = new Thickness(1); preview.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(preview, 2); colorRow.Children.Add(preview);
            }
            root.Children.Add(colorRow);
        }
        double opacity = Scope switch { AppearanceScope.App => settings.AppOpacity, AppearanceScope.Window => selected?.WindowOpacity ?? settings.WindowOpacity, _ => selected?.Opacity ?? settings.Opacity };
        TransparencySlider = AddSlider(root, "Transparenz", 5, Scope == AppearanceScope.App ? 80 : 100,
            (1 - opacity) * 100, value =>
            {
                double next = 1 - value / 100;
                if (Scope == AppearanceScope.App) settings.AppOpacity = next;
                else if (Scope == AppearanceScope.Window) { if (selected is not null) selected.WindowOpacity = next; else settings.WindowOpacity = next; }
                else if (selected is not null) selected.Opacity = next; else settings.Opacity = next;
                app.ApplyAppearance();
            }, value => $"{value:0} %");
        System.Windows.Automation.AutomationProperties.SetName(TransparencySlider, "Transparenz · " + ScopeLabel(Scope));
        if (Scope != AppearanceScope.App) TransparencySlider.ToolTip = "100 % blendet Fläche und Rahmen aus. Die App-Symbole bleiben sichtbar; auf sie kannst du weiterhin Verknüpfungen ziehen.";
        SizeSlider = null;
        if (Scope == AppearanceScope.Window)
        {
            int columns = settings.Columns;
            SizeSlider = AddSlider(root, "Größe", columns * 76 + 48, columns * 160 + 48, columns * settings.TileSize + 48,
                value => { settings.TileSize = (value - 48) / columns; app.ApplyAppearance(); }, value => $"{value:0} px");
            SizeSlider.ToolTip = "Breite bei 100 % Windows-Skalierung. Vorschau im Maßstab 1:3; Symbole und Kontur wachsen mit. Gilt für alle Ordner.";
            folderStage = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 8, 0, 0) };
            preview.Padding = new Thickness(0); preview.BorderThickness = new Thickness(1d / 3); preview.HorizontalAlignment = HorizontalAlignment.Center; preview.VerticalAlignment = VerticalAlignment.Center;
            folderStage.Children.Add(preview); root.Children.Add(folderStage);
            previewDimensions = Ui.Text("", 12, "Muted"); previewDimensions.TextAlignment = TextAlignment.Center; previewDimensions.Margin = new Thickness(0, 4, 0, 0); root.Children.Add(previewDimensions);
        }
        RoundingSlider = null;
        if (Scope == AppearanceScope.Bubbles)
        {
            RoundingSlider = AddSlider(root, "Eckenrundung", 0, 50, settings.CornerRoundness, value => { settings.CornerRoundness = value; app.ApplyAppearance(); }, value => $"{value:0} %");
            RoundingSlider.ToolTip = "Gemeinsame Rundung als Anteil der kürzeren Kante. Miniatur und Ordner bleiben proportional gleich gerundet.";
        }
        if (Scope == AppearanceScope.App) AddGeneralSettings(root);
        layout.Children.Add(new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
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
    private string CurrentColor() => Scope == AppearanceScope.App ? app.State.Settings.AppColor : selected?.Color ?? app.State.Settings.BubbleColor;
    internal void OpenPicker(FrameworkElement anchor)
    {
        Picker?.Close(); var group = selected; var scope = Scope;
        Picker = new ColorPickerWindow(this, CurrentColor(), app.State.Settings, color =>
        {
            if (scope == AppearanceScope.App) app.State.Settings.AppColor = color;
            else if (group is not null) group.Color = color; else app.State.Settings.BubbleColor = color;
            app.ApplyAppearance();
        });
        var current = Picker; current.Closed += (_, _) => { if (Picker == current) Picker = null; }; current.ShowAt(anchor);
    }
    internal void OpenPickerForTest() { if (swatch is not null) OpenPicker(swatch); }
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
        preview.Width = footprint.Width / 3; preview.Height = footprint.Height / 3;
        preview.CornerRadius = new CornerRadius(AppearanceGeometry.Radius(settings.CornerRoundness, footprint.Width, footprint.Height) / 3);
        folderStage.Height = footprint.Height / 3 + 8;
        folderCanvas.Width = footprint.Width; folderCanvas.Height = footprint.Height;
        folderCanvas.Clip = new RectangleGeometry(new Rect(0, 0, footprint.Width, footprint.Height));
        double icon = Math.Min(112, settings.TileSize * 72 / 116);
        for (int i = 0; i < folderCanvas.Children.Count; i++)
        {
            var image = (Image)folderCanvas.Children[i]; image.Width = image.Height = icon;
            Canvas.SetLeft(image, 22 + (i % settings.Columns) * settings.TileSize + (settings.TileSize - icon) / 2);
            Canvas.SetTop(image, 22 + (i / settings.Columns) * settings.TileSize + (settings.TileSize - icon) / 2);
        }
        if (previewDimensions is not null) previewDimensions.Text = $"{footprint.Width:0} × {footprint.Height:0} px · Vorschau 1:3";
    }
    private Border Preview(BubbleGroup? group, double size, bool material = true)
    {
        var grid = new UniformGrid { Rows = 3, Columns = 3 };
        var border = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(app.State.Settings.BubbleCornerRadius * size / 76), Background = material ? Ui.Glass(app.State.Settings, group) : Brushes.Transparent, Padding = material ? new Thickness(7) : new Thickness(0), Child = grid };
        if (group is { CustomIcon: not null }) { var image = new Image { Stretch = Stretch.Uniform }; border.Child = image; _ = SetIcon(image, group.CustomIcon); return border; }
        foreach (var entry in group?.Items.Take(9) ?? []) { var image = new Image { Margin = new Thickness(2), Stretch = Stretch.Uniform }; grid.Children.Add(image); _ = SetIcon(image, app.Resolve(entry.Target)); }
        if (group is null || group.Items.Count == 0) for (int i = 0; i < 9; i++) grid.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = Ui.Resource("Muted"), Opacity = .16, Margin = new Thickness(3) });
        return border;
    }
    private async Task SetIcon(Image image, string target) => image.Source = await app.Icons.Get(target);
    private void AddGeneralSettings(StackPanel root)
    {
        var label = Ui.Text("Allgemein", 17, weight: FontWeights.SemiBold); label.Margin = new Thickness(0, 24, 0, 8); root.Children.Add(label);
        root.Children.Add(Ui.Text("Design", 13));
        var themes = new[] { "Hell", "Dunkel", "Windows-Standard" }; var values = new[] { "Light", "Dark", "System" };
        var theme = new ComboBox { ItemsSource = themes, SelectedIndex = Array.IndexOf(values, app.State.Settings.Theme), Margin = new Thickness(0, 8, 0, 12), MinWidth = 0 };
        theme.SelectionChanged += (_, _) => { if (theme.SelectedIndex >= 0) { app.State.Settings.Theme = values[theme.SelectedIndex]; app.ApplySettings(app.State.Settings); } }; root.Children.Add(theme);
        void Check(string text, bool value, Action<bool> set)
        {
            var box = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = value };
            box.Click += (_, _) => { bool next = box.IsChecked == true; try { set(next); } catch (Exception ex) { box.IsChecked = !next; Ui.Error(ex); } }; root.Children.Add(box);
        }
        Check("Desktop-Originale nach Übernahme entfernen", app.State.Settings.MoveDesktopShortcuts, value => { app.State.Settings.MoveDesktopShortcuts = value; app.RequestSave(); });
        Check("Beim Darüberfahren öffnen", app.State.Settings.HoverOpen, value => { app.State.Settings.HoverOpen = value; app.Changed(true); });
        bool auto = false; try { auto = StartupService.Enabled; } catch (Exception ex) { Log.Write(ex.Message); }
        Check("Mit Windows starten", auto, value => StartupService.Set(value, app.Portable, app.DataDirectory));
        Check("Desktop-Zuordnung verwenden", app.State.Settings.DesktopIntegration, value => { app.State.Settings.DesktopIntegration = value; app.Changed(true); });
        Check("Hintergrund weichzeichnen", app.State.Settings.Blur, value => { app.State.Settings.Blur = value; app.ApplySettings(app.State.Settings); });
        var actions = Ui.Text("Daten & Werkzeuge", 17, weight: FontWeights.SemiBold); actions.Margin = new Thickness(0, 20, 0, 10); root.Children.Add(actions);
        void Action(string label, Action action) { var button = Ui.Button(label, action); button.Margin = new Thickness(0, 0, 0, 8); button.HorizontalContentAlignment = HorizontalAlignment.Left; root.Children.Add(button); }
        Action("Zum Desktop", () => app.ShowDesktop());
        Action("Sicherung exportieren …", app.Export); Action("Sicherung importieren …", app.Import);
        Action("Desktop-Verknüpfungen wiederherstellen", app.RestoreAllShortcuts);
        Action("Datenordner öffnen", () => { try { Process.Start(new ProcessStartInfo(app.DataDirectory) { UseShellExecute = true }); } catch (Exception ex) { Ui.Error(ex); } });
        Action("Desktop-Verbindung erneuern", app.Rebuild);
        Action("Diagnosebericht speichern", async () => { try { await Diagnostics.Measure(app); } catch (Exception ex) { Ui.Error(ex); } });
    }
}

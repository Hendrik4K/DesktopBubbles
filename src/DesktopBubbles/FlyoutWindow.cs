using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal sealed class FlyoutWindow : Window
{
    private readonly App app;
    public BubbleGroup Group { get; }
    private readonly BubbleWindow anchor;
    private readonly WrapPanel grid = new();
    private readonly ScrollViewer scroll;
    private readonly Border surface;
    private bool busy;
    private bool placementQueued;
    private Point down;
    private bool dragged;
    private readonly Dictionary<Guid, Button> buttons = [];
    public FlyoutWindow(App app, BubbleWindow anchor)
    {
        this.app = app; this.anchor = anchor; Group = anchor.Group;
        Title = Group.Name; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.Manual;
        var settings = app.State.Settings;
        Width = settings.Columns * settings.TileSize + 48;
        SizeToContent = SizeToContent.Height; MaxHeight = 640;
        Ui.Chrome(this, radius: () => AppearanceGeometry.Radius(app.State.Settings.CornerRoundness, ActualWidth, ActualHeight), perPixel: true);
        surface = new Border { CornerRadius = new CornerRadius(settings.WindowCornerRadius), Background = Ui.WindowGlass(settings, Group), BorderBrush = FrameBrush(), BorderThickness = new Thickness(1), Padding = new Thickness(22) };
        var root = new StackPanel(); surface.Child = root; Content = surface;
        scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = grid };
        root.Children.Add(scroll);
        System.Windows.Automation.AutomationProperties.SetName(this, Group.Name);
        ContextMenu = CreateAddMenu();
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = app.IsInternalDrag(e.Data) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) =>
        {
            app.ReceiveDrop(Group, e.Data); _ = Populate();
            e.Handled = true;
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(() => { if (!busy && !IsActive && !HasContextMenu() && IsVisible) Close(); });
        SourceInitialized += (_, _) => { Native.ToolWindow(this); Ui.Backdrop(this, settings); };
        SizeChanged += (_, _) =>
        {
            if (placementQueued) return;
            placementQueued = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                placementQueued = false;
                if (IsVisible) Place();
            });
        };
        Loaded += async (_, _) =>
        {
            await Populate(); Place();
            if (settings.Animations && SystemParameters.ClientAreaAnimation)
            {
                var animation = new DoubleAnimation(.55, 1, TimeSpan.FromMilliseconds(settings.AnimationMilliseconds)) { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                surface.BeginAnimation(OpacityProperty, animation);
            }
            buttons.Values.FirstOrDefault()?.Focus();
        };
    }
    private bool HasContextMenu() => buttons.Values.Any(b => b.ContextMenu?.IsOpen == true) || ContextMenu?.IsOpen == true;
    internal void RefreshAppearance()
    {
        surface.Background = Ui.WindowGlass(app.State.Settings, Group); surface.BorderBrush = FrameBrush(); Ui.RefreshRounding(this);
    }
    private Brush FrameBrush() => !SystemParameters.HighContrast && app.State.Settings.WindowOpacity <= 0 ? Brushes.Transparent : Ui.GlassRim();
    private void Place()
    {
        UpdateLayout();
        var a = Native.Bounds(anchor); var work = Native.WorkArea(a.X + a.Width / 2, a.Y + a.Height / 2);
        double scale = Native.GetDpiForWindow(Native.Handle(this)) / 96d;
        MaxHeight = Math.Max(100, work.Height / scale - 28);
        scroll.MaxHeight = Math.Max(80, MaxHeight - 48);
        MaxWidth = Math.Max(180, work.Width / scale - 24);
        UpdateLayout(); var bounds = Native.Bounds(this);
        var p = Placement.Flyout(a, bounds.Width, bounds.Height, work);
        Native.Position(this, p.X, p.Y);
    }
    public async Task Populate()
    {
        grid.Children.Clear(); buttons.Clear();
        var size = app.State.Settings.TileSize;
        grid.MinHeight = Group.Items.Count == 0 ? size * 2 : 0;
        foreach (var entry in Group.Items.ToList())
        {
            var image = new Image { Width = Math.Min(112, size * 72 / 116), Height = Math.Min(112, size * 72 / 116), Stretch = Stretch.Uniform };
            var button = new Button { Style = (Style)FindResource("AppIcon"), Content = image, Width = size, Height = size, ToolTip = entry.Name + "\n" + app.EntryLocation(entry), AllowDrop = true };
            System.Windows.Automation.AutomationProperties.SetName(button, entry.Name);
            button.PreviewMouseLeftButtonDown += (_, e) => { down = e.GetPosition(button); dragged = false; };
            button.MouseMove += async (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || dragged) return;
                var p = e.GetPosition(button);
                if (Math.Abs(p.X - down.X) + Math.Abs(p.Y - down.Y) < 8) return;
                dragged = true; busy = true;
                try { await app.DragEntry(button, Group, entry); }
                finally { busy = false; if (IsVisible) { if (!IsActive && !HasContextMenu()) Close(); else await Populate(); } }
            };
            button.Click += (_, _) =>
            {
                if (dragged) { dragged = false; return; }
                try { app.Launch(entry); Close(); } catch (Exception ex) { busy = true; Ui.Error(ex); busy = false; }
            };
            button.Drop += (_, e) =>
            {
                app.ReceiveDrop(Group, e.Data, false, Group.Items.FindIndex(x => x.Id == entry.Id)); _ = Populate(); e.Handled = true;
            };
            var menu = new ContextMenu(); button.ContextMenu = menu;
            menu.Items.Add(Ui.Menu("Starten", () => { try { app.Launch(entry); Close(); } catch (Exception ex) { Ui.Error(ex); } }));
            menu.Items.Add(Ui.Menu("Zurück zum Desktop", () => WithDialog(() => { app.RemoveEntry(Group, entry); _ = Populate(); Place(); })));
            var delete = Ui.Menu("Aus Gruppe löschen", () => WithDialog(() => { app.DeleteEntry(Group, entry); _ = Populate(); Place(); }));
            delete.ToolTip = "Entfernt nur diesen Eintrag. Keine Dateien oder Programme werden gelöscht und keine Verknüpfung wird auf dem Desktop angelegt.";
            menu.Items.Add(delete);
            grid.Children.Add(button); buttons[entry.Id] = button;
            _ = SetImage(image, app.Resolve(entry.Target));
        }
        await Task.CompletedTask;
    }
    private async Task SetImage(Image image, string target) { image.Source = await app.Icons.Get(target); }
    private void WithDialog(Action action) { busy = true; try { action(); } finally { busy = false; if (IsVisible) Activate(); } }
    private ContextMenu CreateAddMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Ui.Menu("Dateien / Verknüpfungen hinzufügen …", () => WithDialog(() => { app.AddFiles(Group, false); _ = Populate(); Place(); })));
        menu.Items.Add(Ui.Menu("Ordner hinzufügen …", () => WithDialog(() => { app.AddFolder(Group, false); _ = Populate(); Place(); })));
        menu.Items.Add(Ui.Menu("Installierte App hinzufügen …", () => WithDialog(() => { app.PickApp(Group, false); _ = Populate(); Place(); })));
        menu.Items.Add(Ui.Menu("Webadresse hinzufügen …", () => WithDialog(() => { app.AddWeb(Group, this, false); _ = Populate(); Place(); })));
        return menu;
    }
}

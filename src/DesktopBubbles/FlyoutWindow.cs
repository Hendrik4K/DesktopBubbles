using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
    internal FolderReorder Reorder { get; }
    internal bool Frosted { get; }
    internal bool Liquid { get; }
    internal int FrostCaptureCount { get; private set; }
    internal bool HasFrostSample => frostSample is not null;
    internal bool HasLiquidSample => liquidSample is not null;
    private BitmapSource? frostSample, liquidSample;
    private LiquidGlass.Backdrop? motionBackdrop;
    private bool closed;
    private FolderMotion? motion;
    private bool folding, folded;
    private bool Animate => app.State.Settings.Animations && SystemParameters.ClientAreaAnimation && !app.Exiting;
    internal bool Animating => motion?.Running == true;
    // An animated folder hides its miniature itself, once the travelling glass has replaced it.
    internal bool HidesAnchor => Animate;
    private readonly Dictionary<Guid, Button> buttons = [];
    public FlyoutWindow(App app, BubbleWindow anchor)
    {
        this.app = app; this.anchor = anchor; Group = anchor.Group;
        Title = Group.Name; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.Manual;
        var settings = app.State.Settings;
        Width = settings.Columns * settings.TileSize + 48;
        SizeToContent = SizeToContent.Height; MaxHeight = 640;
        Liquid = settings.LiquidGlass; Frosted = !Liquid && settings.FrostedFolders;
        Ui.Chrome(this, radius: () => AppearanceGeometry.Radius(app.State.Settings.CornerRoundness, ActualWidth, ActualHeight), perPixel: true);
        surface = new Border { CornerRadius = new CornerRadius(settings.WindowCornerRadius), Background = Ui.WindowGlass(settings, Group), BorderBrush = FrameBrush(), BorderThickness = new Thickness(1), Padding = new Thickness(22) };
        // Commit an empty alpha surface before sampling the background, or before the glass travels in.
        if (Frosted || Liquid || Animate) surface.Opacity = 0;
        var root = new StackPanel(); surface.Child = root; Content = surface;
        scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = grid };
        Reorder = new FolderReorder(this, scroll,
            (first, second) => WithDialog(() => { try { app.SwapEntries(Group, first, second); } catch (Exception ex) { Ui.Error(ex); } _ = Populate(); }),
            async (button, entry) =>
            {
                busy = true;
                try { await app.DragEntry(button, Group, entry); }
                finally { busy = false; if (IsVisible) { if (!IsActive && !HasContextMenu()) Close(); else await Populate(); } }
            }, () => app.State.Settings.Animations);
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
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (Reorder.IsSorting) Reorder.Stop(); else Close(); e.Handled = true; } };
        Deactivated += (_, _) =>
        {
            if (Reorder.IsSorting) Reorder.Stop(); else Reorder.CancelPress();
            Dispatcher.BeginInvoke(() => { if (!busy && !IsActive && !HasContextMenu() && IsVisible) Close(); });
        };
        Closed += (_, _) => { closed = true; motion?.Stop(); Reorder.Dispose(); frostSample = liquidSample = null; motionBackdrop = null; surface.Background = null; };
        SourceInitialized += (_, _) => { Native.ToolWindow(this); Ui.Backdrop(this, settings); Ui.RefreshRounding(this); };
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
            if (Frosted || Liquid)
            {
                // The miniature is still on screen and the folder may open over it; keep it out of the sample.
                bool excluded = anchor.ExcludeFromCapture(true);
                if (!excluded) anchor.SetExpanded(true);
                // A folder still folding away elsewhere must not end up in this glass.
                for (int wait = 0; wait < 16 && FolderMotion.Active && !closed; wait++) await Task.Delay(50);
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                if (closed) { if (excluded) anchor.ExcludeFromCapture(false); return; }
                Native.DwmFlush();
                FrostCaptureCount++;
                try
                {
                    if (Liquid) liquidSample = RenderLiquid();
                    else frostSample = FolderFrost.Capture(Native.Bounds(this));
                }
                finally { if (excluded) anchor.ExcludeFromCapture(false); }
                surface.Background = SurfaceBrush(); surface.BorderBrush = FrameBrush();
            }
            if (!closed && !folding && Animate && CreateMotion() is { } next) { motion = next; next.Open(hideAnchor: () => anchor.SetExpanded(true)); }
            else { surface.Opacity = 1; if (!closed) anchor.SetExpanded(true); }
            buttons.Values.FirstOrDefault()?.Focus();
        };
    }
    private FolderMotion? CreateMotion()
    {
        if (anchor.GlassBounds() is not { Width: > 0 } glass) return null;
        UpdateLayout();
        var settings = app.State.Settings;
        double scale = Native.GetDpiForWindow(Native.Handle(this)) / 96d;
        double fromRadius = settings.BubbleCornerRadius * glass.Width / Math.Max(1, settings.MiniatureSize);
        double toRadius = AppearanceGeometry.Radius(settings.CornerRoundness, ActualWidth, ActualHeight) * scale;
        return new FolderMotion(this, surface, buttons.Values.ToList(), glass, anchor.GridLayout(glass), fromRadius, toRadius,
            hideSurface: () => { surface.Background = Brushes.Transparent; surface.BorderBrush = Brushes.Transparent; },
            showSurface: () => { surface.Opacity = 1; surface.Background = SurfaceBrush(); surface.BorderBrush = FrameBrush(); },
            live: liquidSample is not null ? LiveGlass : null,
            // Without a live lens, the miniature's own glass crossfades into the folder's material.
            miniature: liquidSample is null ? Ui.Glass(settings, Group) : null,
            miniatureRim: liquidSample is null ? Ui.BubbleRim(settings, Group) : null);
    }
    // Closing folds the glass back into the miniature first; the window closes once it has landed.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || folded || !IsLoaded || !Animate) { motion?.Stop(); return; }
        e.Cancel = true;
        if (folding) return;
        folding = true; IsHitTestVisible = false;
        if (Reorder.IsSorting) Reorder.Stop(); else Reorder.CancelPress();
        motion?.Stop();
        motion = CreateMotion();
        if (motion is null) { folded = true; Dispatcher.BeginInvoke(Close); return; }
        motion.Close(reveal: () => anchor.SetExpanded(false), done: () => { folded = true; Close(); });
    }
    private bool HasContextMenu() => buttons.Values.Any(b => b.ContextMenu?.IsOpen == true) || ContextMenu?.IsOpen == true;
    internal void RefreshAppearance()
    {
        surface.Background = SurfaceBrush(); surface.BorderBrush = FrameBrush(); Ui.RefreshRounding(this);
    }
    private Brush SurfaceBrush() => liquidSample is not null ? LiquidGlass.Material(liquidSample, FolderAppearance.Opacity(app.State.Settings), Ui.Dark)
        : frostSample is null ? Ui.WindowGlass(app.State.Settings, Group)
        : FolderFrost.Material(frostSample, Ui.WindowGlass(app.State.Settings, Group));
    internal void SetFrostSampleForTest(BitmapSource sample) { frostSample = sample; surface.Background = SurfaceBrush(); }
    // The lens carries its own lit rim; a second drawn border would double the outline.
    private Brush FrameBrush() => liquidSample is not null || (!SystemParameters.HighContrast && FolderAppearance.Opacity(app.State.Settings) <= 0) ? Brushes.Transparent : Ui.GlassRim();
    // One capture covers the folder and, when animated, the whole path from the miniature.
    // The resting folder glass and every frame of the motion refract the same background.
    private BitmapSource? RenderLiquid()
    {
        var bounds = Native.Bounds(this);
        var area = Animate && anchor.GlassBounds() is { Width: > 0 } glass ? FolderMotion.Path(glass, bounds) : bounds;
        if (FolderFrost.CaptureSharp(area, out _, out var origin) is not { } sample) return null;
        double scale = Native.GetDpiForWindow(Native.Handle(this)) / 96d;
        var backdrop = motionBackdrop = LiquidGlass.Prepare(sample, scale, origin.X, origin.Y);
        double radius = AppearanceGeometry.Radius(app.State.Settings.CornerRoundness, ActualWidth, ActualHeight) * scale;
        return LiquidGlass.Render(backdrop, bounds.X - backdrop.X, bounds.Y - backdrop.Y, (int)Math.Round(bounds.Width), (int)Math.Round(bounds.Height),
            Ui.LiquidOptions(app.State.Settings, Group, radius, scale));
    }
    // A frame of travelling liquid glass; small shapes render fully, large ones at half resolution.
    // Its transparency moves from the miniature's setting (p = 0) to the folder's (p = 1).
    private Brush? LiveGlass(PixelRect rect, double radius, double p)
    {
        if (motionBackdrop is not { } backdrop || rect.Width < 2 || rect.Height < 2) return null;
        var settings = app.State.Settings;
        double scale = Native.GetDpiForWindow(Native.Handle(this)) / 96d;
        var lens = LiquidGlass.Render(backdrop, rect.X - backdrop.X, rect.Y - backdrop.Y, (int)Math.Round(rect.Width), (int)Math.Round(rect.Height),
            // Near either end the frame must match the static glass exactly, so it renders in full.
            Ui.LiquidOptions(settings, Group, radius, scale), rect.Width * rect.Height > 60_000 && p is > .03 and < .97 ? .5 : 1);
        double opacity = settings.Opacity + (FolderAppearance.Opacity(settings) - settings.Opacity) * p;
        return LiquidGlass.Material(lens, opacity, Ui.Dark);
    }
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
        Reorder.ClearButtons(); grid.Children.Clear(); buttons.Clear();
        var size = app.State.Settings.TileSize;
        grid.MinHeight = Group.Items.Count == 0 ? size * 2 : 0;
        foreach (var entry in Group.Items.ToList())
        {
            var image = new Image { Width = Math.Min(112, size * 72 / 116), Height = Math.Min(112, size * 72 / 116), Stretch = Stretch.Uniform };
            var button = new Button { Style = (Style)FindResource("AppIcon"), Content = image, Width = size, Height = size, ToolTip = entry.Name + "\n" + app.EntryLocation(entry), AllowDrop = true };
            System.Windows.Automation.AutomationProperties.SetName(button, entry.Name);
            Reorder.Attach(button, entry);
            button.Click += (_, _) =>
            {
                if (Reorder.ConsumeClick()) return;
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

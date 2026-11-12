using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal sealed class BubbleWindow : Window
{
    private readonly App app;
    public BubbleGroup Group { get; }
    private readonly Border glass;
    private readonly Grid canvas;
    private Native.Point startCursor;
    private PixelRect startWindow;
    private bool dragging, pressed;
    private bool closing;
    // Liquid glass: one background sample per placement, retried while other windows cover the tile.
    private System.Windows.Media.Imaging.BitmapSource? lens;
    private readonly System.Windows.Threading.DispatcherTimer resample;
    private bool sampling, live;
    private readonly System.Diagnostics.Stopwatch liveClock = new();
    private TimeSpan liveFrame;
    internal bool HasLiquidSample => lens is not null;
    public bool Expanded { get; private set; }
    public bool Embedded { get; private set; }
    public BubbleWindow(App app, BubbleGroup group)
    {
        this.app = app; Group = group;
        double size = app.State.Settings.MiniatureSize;
        Title = group.Name; Width = size + 28; Height = size + 40; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
        AllowDrop = true; Cursor = Cursors.Hand;
        var root = new StackPanel { Margin = new Thickness(8, 3, 8, 0) };
        glass = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(app.State.Settings.BubbleCornerRadius), BorderThickness = new Thickness(1),
            BorderBrush = Ui.BubbleRim(app.State.Settings, group), Background = Ui.Glass(app.State.Settings, group), Padding = new Thickness(size * 10 / 76),
        };
        root.Children.Add(glass);
        var name = Ui.Text(group.Name, 12); name.Foreground = Brushes.White; name.TextAlignment = TextAlignment.Center;
        name.MaxHeight = 32; name.Margin = new Thickness(-4, 6, -4, 0);
        name.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = .8, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        name.Visibility = group.ShowName ? Visibility.Visible : Visibility.Collapsed;
        root.Children.Add(name);
        resample = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        resample.Tick += (_, _) => { resample.Stop(); _ = SampleLiquid(); };
        canvas = new Grid { Width = size + 28, Height = size + 40 }; canvas.Children.Add(root);
        Content = new Viewbox { Stretch = Stretch.Fill, Child = canvas };
        System.Windows.Automation.AutomationProperties.SetName(this, group.Name);
        ToolTip = $"{group.Name} · {group.Items.Count} Einträge\nZiehen zum Verschieben · Rechtsklick zum Bearbeiten";
        ContextMenu = GroupActionsMenu.Create(app, group, settings: true);
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            Native.GetCursorPos(out startCursor); startWindow = Native.Bounds(this);
            BeginLiveGlass(); // Set the capture exclusion once per press, never mid-drag.
            pressed = true; dragging = false; CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, _) =>
        {
            if (!pressed) return;
            Native.GetCursorPos(out var p);
            double dx = p.X - startCursor.X, dy = p.Y - startCursor.Y;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) < 6) return;
            if (!dragging) { app.CloseFlyout(); resample.Stop(); }
            dragging = true; Native.Position(this, startWindow.X + dx, startWindow.Y + dy);
            LiveGlass();
        };
        PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!pressed) return;
            bool moved = dragging;
            pressed = dragging = false; ReleaseMouseCapture(); // A finished drag must not block the next glass sample.
            EndLiveGlass();
            if (moved) { SavePosition(); app.Changed(); }
            else app.Toggle(this);
            e.Handled = true;
        };
        LostMouseCapture += (_, _) =>
        {
            bool moved = pressed && dragging;
            pressed = dragging = false;
            EndLiveGlass();
            if (moved) { SavePosition(); app.Changed(); }
        };
        DragOver += (_, e) => { e.Effects = app.IsInternalDrag(e.Data) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { app.ReceiveDrop(Group, e.Data, true); e.Handled = true; };
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(Native.Handle(this));
            source.AddHook((nint h, int m, nint w, nint l, ref bool handled) =>
            {
                if (m == 0x21) { handled = true; return (nint)3; } // MA_NOACTIVATE
                if (m == 0x82 && !closing) app.ScheduleRecovery();
                return 0;
            });
        };
        Loaded += async (_, _) =>
        {
            Embedded = app.Host.Attach(this, app.State.Settings.DesktopIntegration);
            RestorePosition();
            await LoadIcons();
            ScheduleLiquid();
        };
        Closed += (_, _) => { closing = true; resample.Stop(); lens = null; };
    }
    private void ScheduleLiquid(double milliseconds = 400)
    {
        if (closing || !app.State.Settings.LiquidGlass) return;
        resample.Stop(); resample.Interval = TimeSpan.FromMilliseconds(milliseconds); resample.Start();
    }
    private void ApplyMaterial()
    {
        var settings = app.State.Settings;
        bool liquid = lens is not null && settings.LiquidGlass;
        glass.Background = liquid ? LiquidGlass.Material(lens!) : Ui.Glass(settings, Group);
        // The lens carries its own lit rim and must map 1:1 onto the tile's pixels.
        glass.BorderBrush = liquid ? Brushes.Transparent : Ui.BubbleRim(settings, Group);
        glass.BorderThickness = new Thickness(liquid ? 0 : 1);
    }
    private async Task SampleLiquid()
    {
        if (closing || sampling || !app.State.Settings.LiquidGlass) return;
        if (dragging || pressed) { ScheduleLiquid(); return; } // Never touch the affinity under a held mouse button.
        if (Expanded || !IsVisible || GlassBounds() is not { } bounds) { ScheduleLiquid(4000); return; }
        if (bounds.Width < 4 || Covered(bounds)) { ScheduleLiquid(4000); return; }
        sampling = true;
        var content = (UIElement)Content;
        bool excluded = ExcludeFromCapture(true);
        try
        {
            // Excluded from capture, the tile stays on screen; an embedded child window cannot be
            // excluded and turns transparent for the moment of the sample instead.
            if (!excluded) content.Opacity = 0;
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            if (closing) return;
            if (dragging || pressed) { ScheduleLiquid(); return; }
            Native.DwmFlush();
            RenderLens(bounds);
        }
        finally { sampling = false; if (excluded && !live) ExcludeFromCapture(false); if (!closing) content.Opacity = Expanded ? 0 : 1; }
    }
    private bool RenderLens(PixelRect bounds)
    {
        var sample = FolderFrost.CaptureSharp(bounds, out var crop);
        if (sample is null || !app.State.Settings.LiquidGlass) return false;
        double scale = bounds.Width / Math.Max(1, glass.ActualWidth);
        lens = LiquidGlass.Render(sample, crop, Ui.LiquidOptions(app.State.Settings, Group, app.State.Settings.BubbleCornerRadius * scale, scale));
        ApplyMaterial(); return true;
    }
    // While dragging, a tile excluded from capture can re-render its lens from what lies beneath
    // at its current spot: one small capture and refraction pass, at most every 33 ms.
    // An embedded tile cannot be excluded and keeps its last lens until it is dropped.
    private void BeginLiveGlass()
    {
        live = app.State.Settings.LiquidGlass && ExcludeFromCapture(true);
        liveClock.Restart(); liveFrame = TimeSpan.FromSeconds(-1); // First move renders at once.
    }
    private void LiveGlass()
    {
        if (!live || closing || liveClock.Elapsed - liveFrame < TimeSpan.FromMilliseconds(33) || GlassBounds() is not { Width: >= 4 } bounds) return;
        liveFrame = liveClock.Elapsed;
        RenderLens(bounds);
    }
    private void EndLiveGlass()
    {
        if (!live) return;
        live = false; ExcludeFromCapture(false);
    }
    // WDA_EXCLUDEFROMCAPTURE: screen captures see what lies behind, the screen still shows the tile.
    // Only top-level windows support it; returns false for a tile embedded in the desktop.
    internal bool ExcludeFromCapture(bool value)
        => !closing && Native.SetWindowDisplayAffinity(Native.Handle(this), value ? 0x11u : 0u);
    // Physical screen pixels of the glass tile, without the name label.
    internal PixelRect? GlassBounds()
    {
        if (PresentationSource.FromVisual(glass) is null) return null;
        var topLeft = glass.PointToScreen(new Point()); var bottomRight = glass.PointToScreen(new Point(glass.ActualWidth, glass.ActualHeight));
        return new PixelRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }
    // Any foreign window over the tile would end up inside the glass.
    private bool Covered(PixelRect bounds)
    {
        var hwnd = Native.Handle(this);
        foreach (var (fx, fy) in new[] { (.5, .5), (.15, .15), (.85, .15), (.15, .85), (.85, .85) })
        {
            var hit = Native.WindowFromPoint(new Native.Point { X = (int)(bounds.X + bounds.Width * fx), Y = (int)(bounds.Y + bounds.Height * fy) });
            if (hit != hwnd && !Native.IsChild(hwnd, hit)) return true;
        }
        return false;
    }
    public void Finish() { closing = true; Close(); }
    public void RefreshAppearance()
    {
        double size = app.State.Settings.MiniatureSize;
        if (!app.State.Settings.LiquidGlass) lens = null;
        ApplyMaterial();
        glass.CornerRadius = new CornerRadius(app.State.Settings.BubbleCornerRadius);
        glass.Width = glass.Height = size; glass.Padding = new Thickness(size * 10 / 76);
        canvas.Width = size + 28; canvas.Height = size + 40; RestorePosition();
    }
    public void SetExpanded(bool value)
    {
        if (closing) return;
        Expanded = value;
        // Keep the HWND and its physical bounds for placement and DPI. Transparent pixels pass through.
        ((UIElement)Content).Opacity = value ? 0 : 1;
        IsHitTestVisible = !value;
    }
    public void RestorePosition()
    {
        // A desktop host can span monitors while its child HWNDs inherit one DPI.
        // Scale the small closed tile explicitly for its destination monitor.
        double ratio = ((long)Native.GetWindowLongPtr(Native.Handle(this), Native.GwlStyle) & Native.WsChild) != 0
            ? Native.MonitorDpi(Group.X + (app.State.Settings.MiniatureSize + 28) / 2, Group.Y + (app.State.Settings.MiniatureSize + 40) / 2) / Math.Max(96, Native.GetDpiForWindow(Native.Handle(this))) : 1;
        Width = (app.State.Settings.MiniatureSize + 28) * ratio; Height = (app.State.Settings.MiniatureSize + 40) * ratio; UpdateLayout();
        var area = Native.WorkArea(Group.X, Group.Y);
        var bounds = Native.Bounds(this);
        var p = Placement.Clamp(Group.X, Group.Y, bounds.Width, bounds.Height, area);
        Native.Position(this, p.X, p.Y);
        if (Math.Abs(Group.X - p.X) > .5 || Math.Abs(Group.Y - p.Y) > .5) app.RequestSave();
        Group.X = p.X; Group.Y = p.Y;
        Group.Monitor = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)p.X, (int)p.Y)).DeviceName;
        ScheduleLiquid(); // Placement, size, DPI and wallpaper changes all pass through here.
    }
    private void SavePosition()
    {
        var bounds = Native.Bounds(this); Group.X = bounds.X; Group.Y = bounds.Y; RestorePosition();
    }
    public async Task LoadIcons()
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 3, Columns = 3 };
        glass.Child = grid;
        if (Group.Items.Count == 0)
        {
            for (int i = 0; i < 9; i++) grid.Children.Add(new Border { Background = Ui.Resource("Muted"), Opacity = .16, CornerRadius = new CornerRadius(3), Margin = new Thickness(1) });
        }
        foreach (var item in Group.Items.Take(9))
        {
            var image = new Image { Margin = new Thickness(1), Stretch = Stretch.Uniform };
            grid.Children.Add(image); image.Source = await app.Icons.Get(app.Resolve(item.Target));
        }
    }
}

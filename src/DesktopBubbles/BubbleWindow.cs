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
        canvas = new Grid { Width = size + 28, Height = size + 40 }; canvas.Children.Add(root);
        Content = new Viewbox { Stretch = Stretch.Fill, Child = canvas };
        System.Windows.Automation.AutomationProperties.SetName(this, group.Name);
        ToolTip = $"{group.Name} · {group.Items.Count} Einträge\nZiehen zum Verschieben · Rechtsklick zum Bearbeiten";
        ContextMenu = GroupActionsMenu.Create(app, group, settings: true);
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            Native.GetCursorPos(out startCursor); startWindow = Native.Bounds(this);
            pressed = true; dragging = false; CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, _) =>
        {
            if (!pressed) return;
            Native.GetCursorPos(out var p);
            double dx = p.X - startCursor.X, dy = p.Y - startCursor.Y;
            if (!dragging && Math.Abs(dx) + Math.Abs(dy) < 6) return;
            if (!dragging) app.CloseFlyout();
            dragging = true; Native.Position(this, startWindow.X + dx, startWindow.Y + dy);
        };
        PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!pressed) return;
            pressed = false; ReleaseMouseCapture();
            if (dragging) { SavePosition(); app.Changed(); }
            else app.Toggle(this);
            e.Handled = true;
        };
        LostMouseCapture += (_, _) => { if (pressed && dragging) { SavePosition(); app.Changed(); } pressed = false; };
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
        };
        Closed += (_, _) => closing = true;
    }
    public void Finish() { closing = true; Close(); }
    public void RefreshAppearance()
    {
        double size = app.State.Settings.MiniatureSize;
        glass.Background = Ui.Glass(app.State.Settings, Group); glass.BorderBrush = Ui.BubbleRim(app.State.Settings, Group);
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

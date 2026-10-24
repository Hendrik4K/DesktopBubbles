using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DesktopBubbles.Core;

namespace DesktopBubbles;

/// <summary>Captured in-window sorting never exports or resolves a shortcut.</summary>
internal sealed class FolderReorder : IDisposable
{
    private readonly Window owner;
    private readonly FrameworkElement viewport;
    private readonly Action<AppEntry, AppEntry> swap;
    private readonly Func<Button, AppEntry, Task> export;
    private readonly Func<bool> animate;
    private readonly DispatcherTimer hold = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Dictionary<Button, AppEntry> entries = [];
    private Button? pressed, target;
    private AppEntry? picked;
    private Point down;
    private bool dragged, releasing;
    private TranslateTransform? translation;
    internal bool IsSorting { get; private set; }
    internal bool HoldPending => hold.IsEnabled;
    internal bool HasPickedEntry => picked is not null;

    public FolderReorder(Window owner, FrameworkElement viewport, Action<AppEntry, AppEntry> swap, Func<Button, AppEntry, Task> export, Func<bool> animate)
    {
        this.owner = owner; this.viewport = viewport; this.swap = swap; this.export = export; this.animate = animate;
        hold.Tick += HoldElapsed; owner.PreviewMouseLeftButtonDown += BackgroundDown;
    }
    public void ClearButtons()
    {
        CancelPress();
        foreach (var button in entries.Keys) StopWiggle(button);
        entries.Clear(); dragged = false;
    }
    public void Attach(Button button, AppEntry entry)
    {
        entries.Add(button, entry);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, "Kurz klicken zum Starten · Gedrückt halten zum Sortieren");
        button.PreviewMouseLeftButtonDown += (_, e) => { BeginPress(button, e.GetPosition(owner)); if (IsSorting) e.Handled = true; };
        button.PreviewMouseLeftButtonUp += (_, e) =>
        {
            hold.Stop();
            if (picked is not null)
            {
                ReleaseAt(e.GetPosition(owner)); e.Handled = true;
            }
            else pressed = null;
        };
        button.MouseMove += async (_, e) =>
        {
            if (pressed != button || releasing) return;
            // Capture changes can generate hover moves; only button-up, lost
            // capture or window deactivation end a press.
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(owner);
            if (picked is not null)
            {
                if (translation is not null) { translation.X = point.X - down.X; translation.Y = point.Y - down.Y; }
                SetTarget(TargetAt(point)); return;
            }
            if (dragged || (point - down).Length < 8) return;
            hold.Stop(); dragged = true; pressed = null;
            await export(button, entry);
        };
        button.LostMouseCapture += (_, _) => { if (pressed == button && !releasing) CancelPress(); };
        button.ContextMenuOpening += (_, _) => CancelPress();
        if (IsSorting) Wiggle(button, entries.Count - 1);
    }
    internal void BeginPress(Button button, Point point)
    {
        CancelPress(); dragged = false; pressed = button; down = point;
        if (IsSorting) Pick(button); else hold.Start();
    }
    private void HoldElapsed(object? sender, EventArgs e)
    {
        hold.Stop();
        if (pressed is null || dragged || !owner.IsVisible || !owner.IsActive) return;
        IsSorting = true; int index = 0;
        foreach (var button in entries.Keys) Wiggle(button, index++);
        Pick(pressed);
    }
    private void Pick(Button button)
    {
        if (!entries.TryGetValue(button, out picked)) return;
        dragged = true; StopWiggle(button); Panel.SetZIndex(button, 10);
        button.Opacity = .85;
        if (button.Content is Image image)
        {
            translation = new TranslateTransform(); var transforms = new TransformGroup();
            transforms.Children.Add(new ScaleTransform(1.08, 1.08)); transforms.Children.Add(translation);
            image.RenderTransform = transforms;
        }
        if (!button.CaptureMouse()) CancelPress();
    }
    internal bool ConsumeClick()
    {
        if (IsSorting) return true;
        if (!dragged) return false;
        dragged = false; return true;
    }
    internal void ReleaseAt(Point point) => DropOn(TargetAt(point));
    private void DropOn(Button? destination)
    {
        var source = picked;
        AppEntry? other = destination is not null && entries.TryGetValue(destination, out var found) ? found : null;
        CancelPress();
        if (source is not null && other is not null && source != other) swap(source, other);
    }
    private Button? TargetAt(Point point)
    {
        if (!new Rect(new Point(), viewport.RenderSize).Contains(owner.TranslatePoint(point, viewport))) return null;
        foreach (var button in entries.Keys)
            if (button != pressed && new Rect(new Point(), button.RenderSize).Contains(owner.TranslatePoint(point, button))) return button;
        return null;
    }
    private void SetTarget(Button? button)
    {
        if (target == button) return;
        target?.ClearValue(Control.BackgroundProperty); target = button;
        if (target is not null) { var color = Ui.Resource("Accent").Color; color.A = 80; target.Background = new SolidColorBrush(color); }
    }
    internal void CancelPress()
    {
        hold.Stop(); releasing = true;
        try
        {
            SetTarget(null);
            var previous = pressed; pressed = null; picked = null; translation = null;
            if (previous is not null)
            {
                if (previous.IsMouseCaptured) previous.ReleaseMouseCapture();
                previous.Opacity = 1; Panel.SetZIndex(previous, 0);
                StopWiggle(previous);
                if (IsSorting && entries.ContainsKey(previous)) Wiggle(previous, entries.Keys.ToList().IndexOf(previous));
            }
        }
        finally { releasing = false; }
    }
    internal void Stop()
    {
        IsSorting = false; CancelPress(); dragged = false;
        foreach (var button in entries.Keys)
        {
            StopWiggle(button); button.ClearValue(FrameworkElement.CursorProperty);
            ToolTipService.SetIsEnabled(button, true);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, "Kurz klicken zum Starten · Gedrückt halten zum Sortieren");
        }
    }
    private void Wiggle(Button button, int index)
    {
        button.Cursor = Cursors.SizeAll;
        ToolTipService.SetIsEnabled(button, false);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, "Sortiermodus · Auf ein anderes Symbol ziehen zum Platztausch · Esc beendet das Sortieren");
        if (button.Content is not Image image) return;
        image.RenderTransformOrigin = new Point(.5, .5);
        var rotation = new RotateTransform(); image.RenderTransform = rotation;
        if (animate() && SystemParameters.ClientAreaAnimation)
            rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-2, 2, TimeSpan.FromMilliseconds(120 + index % 3 * 15))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(index % 3 * 35) });
    }
    private static void StopWiggle(Button button)
    {
        if (button.Content is not Image image) return;
        if (image.RenderTransform is RotateTransform rotation) rotation.BeginAnimation(RotateTransform.AngleProperty, null);
        image.RenderTransform = Transform.Identity;
    }
    private void BackgroundDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsSorting || picked is not null) return;
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        { if (node is Button button && entries.ContainsKey(button) || node is System.Windows.Controls.Primitives.ScrollBar) return; if (node == owner) break; }
        Stop();
    }
    public void Dispose()
    {
        Stop(); ClearButtons(); hold.Tick -= HoldElapsed; owner.PreviewMouseLeftButtonDown -= BackgroundDown;
    }
}

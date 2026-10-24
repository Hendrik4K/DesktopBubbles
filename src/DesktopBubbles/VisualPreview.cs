using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopBubbles.Core;

namespace DesktopBubbles;

/// <summary>Deterministic native-content captures in an explicitly separate fixture data directory.</summary>
internal static class VisualPreview
{
    public static async Task Run(App app)
    {
        if (app.State.Groups.Count == 0) throw new InvalidOperationException("Für die Vorschau eine Kopie der gewünschten Gruppen bereitstellen.");
        // Fixture names/layout only. The data directory must be a disposable copy.
        var gaming = app.State.Groups[0]; gaming.Name = "Gaming"; gaming.ShowName = false;
        app.State.Groups.Clear(); app.State.Groups.Add(gaming); app.State.Groups.Add(new BubbleGroup { Name = "Arbeit", ShowName = false });
        app.State.Settings.Theme = "Light"; app.State.Settings.Animations = false; app.State.Settings.Columns = 3;
        Ui.ApplyTheme(app.State.Settings); app.Rebuild(); app.ShowSettings();
        var settings = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Single(); settings.RefreshGroups(); settings.RefreshPage();
        await Task.Delay(2400);
        Save(settings, Path.Combine(app.DataDirectory, "host-selected.png"));
        settings.OpenPickerForTest(); await Task.Delay(250);
        Save(settings.Picker!, Path.Combine(app.DataDirectory, "picker-selected.png")); settings.Picker!.Close();
        settings.SelectScope(AppearanceScope.Window); await Task.Delay(150);
        Save(settings, Path.Combine(app.DataDirectory, "window-settings.png"));
        settings.SelectScope(AppearanceScope.App); await Task.Delay(150);
        Save(settings, Path.Combine(app.DataDirectory, "app-settings.png"));
        settings.Close(); app.Open(app.Bubbles[0]); await Task.Delay(400);
        Save(app.CurrentFlyout!, Path.Combine(app.DataDirectory, "folder-selected.png"));
        app.State.Settings.Glass = .45; app.ApplyAppearance();
        Save(app.CurrentFlyout!, Path.Combine(app.DataDirectory, "folder-clear.png"));
        app.State.Settings.Glass = .85; app.ApplyAppearance();
        Save(app.CurrentFlyout!, Path.Combine(app.DataDirectory, "folder-matte.png"));
        gaming.WindowOpacity = 0; app.ApplyAppearance();
        Save(app.CurrentFlyout!, Path.Combine(app.DataDirectory, "folder-transparent.png")); app.CloseFlyout();
    }
    internal static void Save(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}

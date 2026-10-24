using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal static class SmokeTests
{
    public static BubbleState CreateState()
    {
        var work = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        return new BubbleState
        {
            Settings = new BubbleSettings { Theme = "Light" },
            Groups =
            [
                new BubbleGroup { Name = "Werkzeuge", X = work.Right - 350, Y = work.Top + 150, Items =
                [Targets.Create(Path.Combine(Environment.SystemDirectory, "notepad.exe"), "Editor"),
                 Targets.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "Explorer"),
                 Targets.Create(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Windows"),
                 Targets.Create("https://learn.microsoft.com", "Microsoft Learn")] },
                new BubbleGroup { Name = "Meine Projekte", X = work.Right - 220, Y = work.Top + 150 }
            ]
        };
    }
    public static async Task Run(App app, bool appearanceOnly = false)
    {
        var checks = new List<object>();
        void Check(string name, bool pass, string? note = null)
        {
            checks.Add(new { Name = name, Passed = pass, Note = note });
            if (!pass) throw new InvalidOperationException("Smoke: " + name + " " + note);
        }
        await Task.Delay(1200);
        Check("Zwei Desktop-Gruppen erzeugt", app.Bubbles.Count == 2);
        Check("Originalicon geladen", await app.Icons.Get(app.State.Groups[0].Items[0].Target) is not null);
        Check("Gelöschtes Ziel erkannt", !Targets.Exists(Path.Combine(app.DataDirectory, "missing.exe")));
        // Exercise exactly the user's failing path, including Explorer's
        // asynchronous MinimizeAll, rather than moving into a pre-existing gap.
        if (!appearanceOnly)
        {
            app.ShowSettings();
            try
            {
                await app.ShowDesktopAsync(app.State.Groups[0]);
                await Task.Delay(800);
                var located = app.Bubbles[0];
                var bounds = Native.Bounds(located);
                var hit = Native.WindowFromPoint(new Native.Point { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + 38) });
                Check("Bubble finden: sichtbar nach tatsächlichem Desktop-Anzeigen", Native.IsWindowVisible(Native.Handle(located)) && !Native.IsIconic(Native.Handle(located)));
                Check("Bubble finden: Klickfläche in der Bildschirmmitte", hit == Native.Handle(located), $"Bubble=0x{Native.Handle(located):X}, Treffer=0x{hit:X}");
                Check("Eigenständiges Fenster statt unsichtbarem Explorer-Kind", ((long)Native.GetWindowLongPtr(Native.Handle(located), Native.GwlStyle) & Native.WsChild) == 0);
                Check("Bubble ohne Topmost-Stil", ((long)Native.GetWindowLongPtr(Native.Handle(located), Native.GwlExStyle) & 8) == 0);
                Check("Desktop-Zuordnung nach Desktop-Anzeigen erhalten", Native.GetWindow(Native.Handle(located), 4) == Native.GetShellWindow());
                app.ShowSettings(); await Task.Delay(250);
                var coveringWindow = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Single();
                Native.Position(coveringWindow, bounds.X - 200, bounds.Y - 100);
                Native.SetWindowPos(Native.Handle(coveringWindow), 0, 0, 0, 0, 0, Native.NoMove | Native.NoSize | Native.NoActivate);
                await Task.Delay(150);
                var coveredHit = Native.WindowFromPoint(new Native.Point { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + 38) });
                Check("Normales Verwaltungsfenster überdeckt die Desktop-Bubble", coveredHit == Native.Handle(coveringWindow));
                coveringWindow.Close(); await Task.Delay(250);
                var uncoveredHit = Native.WindowFromPoint(new Native.Point { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + 38) });
                Check("Bubble nach Schließen des überdeckenden Fensters erreichbar", uncoveredHit == Native.Handle(located));
            }
            finally
            {
                object? restoreShell = null;
                try { restoreShell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!); ((dynamic)restoreShell!).UndoMinimizeAll(); }
                finally { if (restoreShell is not null) Marshal.FinalReleaseComObject(restoreShell); }
                await Task.Delay(250);
            }
        }
        var desktopPoint = appearanceOnly ? null : FindExposedDesktop();
        if (desktopPoint is { } point)
        {
            var bubble = app.Bubbles[0]; bubble.Group.X = point.X; bubble.Group.Y = point.Y; bubble.RestorePosition();
            await Task.Delay(400);
            var bounds = Native.Bounds(bubble);
            var hit = Native.WindowFromPoint(new Native.Point { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + 38) });
            Check("Desktop-Bubble ist tatsächlich mit der Maus erreichbar", hit == Native.Handle(bubble), $"Bubble=0x{Native.Handle(bubble):X}, Treffer=0x{hit:X}");
        }
        foreach (var b in app.Bubbles)
        {
            long ex = (long)Native.GetWindowLongPtr(Native.Handle(b), Native.GwlExStyle);
            Check("Gruppen ohne Taskbar/Alt-Tab-Stil", (ex & Native.WsExToolWindow) != 0 && (ex & Native.WsExAppWindow) == 0);
            Check("Gruppen ohne Aktivierung", (ex & Native.WsExNoActivate) != 0);
        }
        var position = Native.Bounds(app.Bubbles[0]);
        Native.Position(app.Bubbles[0], position.X + 24, position.Y + 20);
        var moved = Native.Bounds(app.Bubbles[0]);
        Check("Physische Positionierung", Math.Abs(moved.X - position.X - 24) <= 1 && Math.Abs(moved.Y - position.Y - 20) <= 1);
        app.Bubbles[0].RestorePosition();
        app.ShowSettings(); await Task.Delay(400);
        var settings = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Single();
        SaveImage(settings, Path.Combine(app.DataDirectory, "settings-light.png"));
        await Task.Delay(1200);
        Check("Tracking startet beim Öffnen nicht automatisch", settings.Measurement.SampleCount == 0 && !settings.Measurement.IsSampling && !settings.Measurement.IsTracking && (string)settings.Measurement.TrackingButton.Content == "Tracking starten");
        settings.Measurement.TrackingButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(1200);
        Check("Tracking starten sammelt echte Prozessintervalle", settings.Measurement.SampleCount >= 1 && settings.Measurement.IsSampling && (string)settings.Measurement.TrackingButton.Content == "Tracking stoppen");
        settings.Measurement.Toggle(); int samples = settings.Measurement.SampleCount; await Task.Delay(1100);
        Check("Tracking stoppen hält Werte fest und beendet Timer", settings.Measurement.SampleCount == samples && !settings.Measurement.IsSampling && !settings.Measurement.IsTracking);
        settings.Measurement.Toggle();
        Check("Erneuter Tracking-Start beginnt eine neue Messung", settings.Measurement.SampleCount == 0 && settings.Measurement.IsSampling);
        string originalColor = AppearancePreferences.Color(app.State.Settings, app.State.Groups[0]);
        app.RequestSave(); app.Flush();
        settings.ColorButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(250);
        var picker = settings.Picker!;
        SaveImage(settings, Path.Combine(app.DataDirectory, "color-picker-light.png"));
        var hexCorner = picker.HexInput.TranslatePoint(new Point(picker.HexInput.ActualWidth, picker.HexInput.ActualHeight), settings);
        Check("Farbauswahl sitzt im Verwaltungsfenster und verdeckt die anderen Regler", Window.GetWindow(picker) == settings && !settings.TransparencySlider!.IsVisible && !settings.SizeSlider!.IsVisible && !settings.RoundingSlider!.IsVisible && (string)settings.ColorButton.Content == "Anwenden");
        Check("Farbkreis und HEX-Eingabe bleiben innerhalb der Ansicht", picker.Wheel.ActualWidth >= 140 && hexCorner.X < settings.ActualWidth - 15 && hexCorner.Y < settings.ActualHeight - 86);
        picker.HexInput.Text = "#GGGGGG"; settings.ColorButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check("Ungültige HEX-Eingabe deaktiviert Übernahme und erhält den Entwurf", !settings.ColorButton.IsEnabled && settings.Picker is not null && AppearancePreferences.Color(app.State.Settings, app.State.Groups[0]) == originalColor);
        picker.HexInput.Text = "#FFFFFF"; Check("Farbkreis unterstützt Weiß", picker.Wheel.Saturation < .001 && picker.Brightness.Value == 1);
        picker.HexInput.Text = "#FF0000"; picker.Saturation.Value = 0;
        Check("Sättigungsregler und Farbkreis bleiben synchron", picker.HexInput.Text == "#FFFFFF" && picker.Wheel.Saturation == 0);
        picker.Brightness.Value = 0; Check("Helligkeit erlaubt Schwarz", picker.HexInput.Text == "#000000");
        picker.HexInput.Text = "#12A3F0"; app.Flush();
        var savedColorState = StateStore.Read(app.Store.Path);
        Check("Eine Farb-Vorschau verändert weder Modell noch gespeicherte Farbe", AppearancePreferences.Color(app.State.Settings, app.State.Groups[0]) == originalColor && AppearancePreferences.Color(savedColorState.Settings, savedColorState.Groups[0]) == originalColor);
        SaveImage(settings, Path.Combine(app.DataDirectory, "color-editor-draft.png"));
        settings.ColorButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check("Eine freie Farbe gilt gemeinsam für Miniatur und Ordner", app.State.Groups[0].Color == "#12A3F0" && app.State.Groups[0].WindowColor == "#12A3F0");
        Check("Anwenden bringt das normale Layout ohne Regleränderung zurück", settings.Picker is null && settings.TransparencySlider!.IsVisible && settings.SizeSlider!.IsVisible && settings.RoundingSlider!.IsVisible && app.State.Settings.Opacity == .35 && app.State.Settings.MiniatureSize == 76 && (string)settings.ColorButton.Content == "Ändern");
        settings.OpenPickerForTest(); await Task.Delay(100); settings.Picker!.HexInput.Text = "#FF0000";
        var escape = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(settings)!, 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent }; settings.RaiseEvent(escape);
        Check("Esc verwirft nur den Farbentwurf und lässt die Verwaltung geöffnet", settings.Picker is null && settings.IsVisible && app.State.Groups[0].Color == "#12A3F0");
        app.State.Groups[0].Color = null; app.ApplyAppearance();
        Check("Verwaltung verwendet feste dezente Ecken statt eigenem Rundungsregler", app.State.Settings.AppCornerRadius == Ui.AppRadius && !settings.AllowsTransparency);
        var gear = Descendants(settings).OfType<System.Windows.Controls.Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Einstellungen");
        gear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(100);
        Check("Zahnrad zeigt App-Darstellung und allgemeine Checkboxen", settings.Scope == AppearanceScope.App && Descendants(settings).OfType<System.Windows.Controls.CheckBox>().Count() == 5);
        Check("Einstellungen verbergen die Ansichtsreiter", !Descendants(settings).OfType<System.Windows.Controls.Button>().Any(b => b.Content is string text && text is "Miniatur" or "Ordner"));
        var back = Descendants(settings).OfType<System.Windows.Controls.Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Zurück zur Miniaturansicht");
        back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Zurück führt zur Miniatur mit beiden Ansichtsreitern", settings.Scope == AppearanceScope.Bubbles && Descendants(settings).OfType<System.Windows.Controls.Button>().Count(b => b.Content is string text && text is "Miniatur" or "Ordner") == 2);
        gear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        gear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Erneuter Zahnrad-Klick verlässt die Einstellungen", settings.Scope == AppearanceScope.Bubbles);
        gear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var theme = Descendants(settings).OfType<System.Windows.Controls.ComboBox>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Farbmodus der Anwendung");
        theme.SelectedIndex = 1; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Farbmodus neben App-Farbe schaltet die Anwendung dunkel", app.State.Settings.Theme == "Dark" && Ui.Dark);
        string modeAppColor = app.State.Settings.AppColor;
        settings.ModeColorButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        settings.Picker!.HexInput.Text = "#102A43"; app.Flush();
        Check("Modusfarbe zeigt einen Entwurf ohne gespeicherte Änderung", settings.Picker is not null && app.State.Settings.DarkModeColor is null && StateStore.Read(app.Store.Path).Settings.DarkModeColor is null && Ui.Resource("Surface").Color == Ui.Brush("#102A43").Color);
        SaveImage(settings, Path.Combine(app.DataDirectory, "mode-color-draft.png")); settings.CancelColorEdit();
        Check("Abbrechen stellt die dunkle Standardpalette wieder her", Ui.Resource("Surface").Color == Ui.Brush("#252234").Color && app.State.Settings.DarkModeColor is null);
        settings.ModeColorButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        settings.Picker!.HexInput.Text = "invalid";
        var modeApply = Descendants(settings).OfType<System.Windows.Controls.Button>().Single(b => b.Content is string text && text == "Anwenden");
        Check("Ungültige Modusfarbe verhindert Anwenden", !modeApply.IsEnabled);
        settings.Picker.HexInput.Text = "#FFF0D0";
        Check("Modus-Vorschau aktualisiert Editorfläche und Text gemeinsam", ((System.Windows.Media.SolidColorBrush)((System.Windows.Controls.Border)settings.Picker.Content).Background).Color == Ui.Resource("ControlFill").Color && Ui.Resource("Ink").Color == System.Windows.Media.Colors.Black);
        settings.Picker.HexInput.Text = "#16324F"; modeApply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); app.Flush();
        Check("Dunkle Modusfarbe färbt Steuerelemente und bleibt von App-Farbe und Hell getrennt", app.State.Settings.DarkModeColor == "#16324F" && app.State.Settings.LightModeColor is null && app.State.Settings.AppColor == modeAppColor && Ui.Resource("ControlFill").Color != Ui.Brush("#383348").Color && StateStore.Read(app.Store.Path).Settings.DarkModeColor == "#16324F");
        SaveImage(settings, Path.Combine(app.DataDirectory, "mode-color-dark.png"));
        theme = Descendants(settings).OfType<System.Windows.Controls.ComboBox>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Farbmodus der Anwendung");
        theme.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        settings.ModeColorButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        settings.Picker!.HexInput.Text = "#FFE8D6";
        Descendants(settings).OfType<System.Windows.Controls.Button>().Single(b => b.Content is string text && text == "Anwenden").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Helle Modusfarbe wird separat übernommen", app.State.Settings.LightModeColor == "#FFE8D6" && app.State.Settings.DarkModeColor == "#16324F" && Ui.Resource("Surface").Color == Ui.Brush("#FFE8D6").Color);
        SaveImage(settings, Path.Combine(app.DataDirectory, "mode-color-light.png"));
        theme = Descendants(settings).OfType<System.Windows.Controls.ComboBox>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Farbmodus der Anwendung");
        theme.SelectedIndex = 2; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Windows-Standard verwendet das aktive Farbprofil", Ui.Resource("Surface").Color == Ui.Brush(Ui.Dark ? "#16324F" : "#FFE8D6").Color);
        theme = Descendants(settings).OfType<System.Windows.Controls.ComboBox>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Farbmodus der Anwendung");
        theme.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var originalAppOpacity = app.State.Settings.AppOpacity;
        settings.TransparencySlider!.Value = 72;
        Check("Zahnrad-Transparenz verändert nur die Anwendung", Math.Abs(app.State.Settings.AppOpacity - .28) < .001 && app.State.Settings.Opacity == .35 && app.State.Groups[0].Opacity is null);
        Check("Einstellungen ohne Ordner-Vorschau oder Rundungsregler", settings.RoundingSlider is null && !Descendants(settings).OfType<System.Windows.Controls.Border>().Any(b => b.Name == "AppearancePreview"));
        var inspector = Descendants(settings).OfType<System.Windows.Controls.ScrollViewer>().First(s => s.ScrollableHeight > 0);
        inspector.ScrollToEnd(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var actualScrollBar = (System.Windows.Controls.Primitives.ScrollBar)inspector.Template.FindName("PART_VerticalScrollBar", inspector);
        var pill = Descendants(actualScrollBar).OfType<System.Windows.Controls.Border>().Single(b => b.Name == "Pill");
        Check("Schmale Scrollbar bleibt bis zu den Werkzeugen bedienbar", inspector.VerticalOffset > 0 && actualScrollBar.ActualWidth == 10 && pill.ActualWidth <= 4, $"Trefferbreite={actualScrollBar.ActualWidth}, sichtbarer Griff={pill.ActualWidth}");
        inspector.ScrollToHome(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        SaveImage(settings, Path.Combine(app.DataDirectory, "app-settings.png"));
        app.State.Settings.AppOpacity = originalAppOpacity; app.ApplyAppearance();
        gear = Descendants(settings).OfType<System.Windows.Controls.Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Einstellungen");
        var tracking = settings.Measurement.TrackingButton;
        var gearPosition = gear.TranslatePoint(new Point(), settings); var trackingPosition = tracking.TranslatePoint(new Point(), settings);
        Check("Einstellungen stehen unten rechts neben Tracking", gearPosition.X > trackingPosition.X + tracking.ActualWidth && Math.Abs(gearPosition.Y + gear.ActualHeight / 2 - trackingPosition.Y - tracking.ActualHeight / 2) < 1);
        string appColor = app.State.Settings.AppColor;
        app.State.Groups[0].Color = "#12A3F0"; app.State.Groups[1].Color = "#FFAA00"; app.ApplyAppearance();
        settings.LinkColorsCheckBox!.IsChecked = true;
        settings.LinkColorsCheckBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check("Farbkopplung übernimmt die ausgewählte Bubble für alle und hält die App separat", app.State.Settings.LinkBubbleColors && app.State.Groups.All(g => AppearancePreferences.Color(app.State.Settings, g) == "#12A3F0") && app.State.Settings.AppColor == appColor);
        settings.SelectScope(AppearanceScope.Bubbles); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); settings.OpenPickerForTest(); await Task.Delay(100);
        settings.Picker!.HexInput.Text = "#00AA99"; settings.ColorButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check("Gekoppelte Farbauswahl erreicht auch andere Bubbles", app.State.Groups.All(g => AppearancePreferences.Color(app.State.Settings, g) == "#00AA99"));
        settings.SelectScope(AppearanceScope.App); settings.LinkColorsCheckBox!.IsChecked = false;
        settings.LinkColorsCheckBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        settings.SelectScope(AppearanceScope.Bubbles); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); settings.OpenPickerForTest(); await Task.Delay(100);
        settings.Picker!.HexInput.Text = "#FF0000"; settings.ColorButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check("Ohne Kopplung bleibt eine neue Farbe auf die gewählte Bubble begrenzt", AppearancePreferences.Color(app.State.Settings, app.State.Groups[0]) == "#FF0000" && AppearancePreferences.Color(app.State.Settings, app.State.Groups[1]) == "#00AA99");
        settings.SelectScope(AppearanceScope.App); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Descendants(settings).OfType<System.Windows.Controls.Button>().Single(b => b.Content is string text && text == "Farben zurücksetzen").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        settings.SelectScope(AppearanceScope.Bubbles); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Miniatur enthält genau eine Glaswahl zwischen Farbe und Vorschau", settings.GlassSelector is not null && Descendants(settings).OfType<System.Windows.Controls.ComboBox>().Count() == 1);
        settings.GlassSelector!.SelectedIndex = 1; settings.RoundingSlider!.Value = 18;
        Check("Gemeinsame Rundung skaliert Miniatur und Ordner proportional", Math.Abs(app.State.Settings.BubbleCornerRadius - 76 * .18) < .001 && app.State.Settings.CornerRoundness == 18);
        settings.SelectScope(AppearanceScope.Window); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); settings.TransparencySlider!.Value = 37;
        Check("Ordnertransparenz gilt gemeinsam und unabhängig von App und Miniatur", Math.Abs(app.State.Settings.WindowOpacity - .63) < .001 && app.State.Groups.All(g => g.WindowOpacity is null) && app.State.Settings.Opacity == .35 && app.State.Settings.AppOpacity == originalAppOpacity && settings.GlassSelector is null && settings.RoundingSlider is null);
        double smaller = settings.AppearancePreview.Width; settings.SizeSlider!.Value = 432;
        Check("Größenregler verändert die Vorschau direkt im Maßstab 1 zu 3", settings.AppearancePreview.Width > smaller && Math.Abs(settings.AppearancePreview.Width * 3 - 432) < .001 && settings.PreviewFootprint.Width == 432);
        var expectedFootprint = settings.PreviewFootprint;
        settings.PreviewScaleButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Originalvorschau hat echte Ordnermaße ohne Verkleinerung", Math.Abs(settings.AppearancePreview.Width - expectedFootprint.Width) < .001 && Math.Abs(settings.AppearancePreview.Height - expectedFootprint.Height) < .001);
        SaveImage(settings, Path.Combine(app.DataDirectory, "window-preview-actual-size.png"));
        var previewScroll = Descendants(settings).OfType<System.Windows.Controls.ScrollViewer>().Single(s => ReferenceEquals(s.Content, settings.AppearancePreview.Parent));
        previewScroll.ScrollToRightEnd(); previewScroll.ScrollToBottom(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Originalvorschau lässt sich bis zu den verdeckten Rändern scrollen", previewScroll.HorizontalOffset > 0 && previewScroll.VerticalOffset > 0 && settings.PreviewFootprint == expectedFootprint);
        previewScroll.ScrollToLeftEnd(); previewScroll.ScrollToTop();
        settings.PreviewScaleButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Vorschau lässt sich ohne Änderung der echten Ordnergröße zurück auf 1 zu 3 schalten", Math.Abs(settings.AppearancePreview.Width * 3 - expectedFootprint.Width) < .001 && settings.PreviewFootprint == expectedFootprint);
        SaveImage(settings, Path.Combine(app.DataDirectory, "window-settings.png"));
        settings.Close(); app.Open(app.Bubbles[0]); await Task.Delay(200);
        Check("Live-Vorschau entspricht der echten Rasterhöhe und -breite", Math.Abs(app.CurrentFlyout!.ActualWidth - expectedFootprint.Width) < 2 && Math.Abs(app.CurrentFlyout.ActualHeight - expectedFootprint.Height) < 2);
        Check("Window-Größe skaliert das echte Raster und seine Icons", Math.Abs(app.CurrentFlyout!.Width - 432) < 1 && Descendants(app.CurrentFlyout).OfType<System.Windows.Controls.Image>().Any(i => i.Width > 72));
        byte firstOpacity = PixelAlpha(app.CurrentFlyout!, 216, app.CurrentFlyout!.ActualHeight - 8);
        app.Open(app.Bubbles[1]); await Task.Delay(200);
        Check("Größe und Transparenz gelten auch für den anderen Ordner", Math.Abs(app.CurrentFlyout!.Width - 432) < 1 && PixelAlpha(app.CurrentFlyout!, 216, app.CurrentFlyout!.ActualHeight - 8) == firstOpacity);
        app.Open(app.Bubbles[0]); await Task.Delay(200);
        Check("Window rundet auch die äußere Windows-Fläche", HasRoundedRegion(app.CurrentFlyout!));
        Check("Ordner verwendet eine transparente Alpha-Fläche ohne DWM-Hintergrundrechteck", app.CurrentFlyout!.AllowsTransparency && ((long)Native.GetWindowLongPtr(Native.Handle(app.CurrentFlyout), Native.GwlExStyle) & 0x80000) != 0 && Native.DwmGetWindowAttribute(Native.Handle(app.CurrentFlyout), 38, out int backdrop, 4) == 0 && backdrop == 1);
        Check("Ordner-Außenecke ist tatsächlich transparent gerendert", PixelAlpha(app.CurrentFlyout!, 1, 1) == 0);
        app.State.Settings.Glass = .45; app.ApplyAppearance(); byte clearAlpha = PixelAlpha(app.CurrentFlyout!, 216, app.CurrentFlyout!.ActualHeight - 8);
        app.State.Settings.Glass = .85; app.ApplyAppearance(); byte matteAlpha = PixelAlpha(app.CurrentFlyout!, 216, app.CurrentFlyout!.ActualHeight - 8);
        Check("Klar und Matt unterscheiden sich sichtbar im gerenderten Material", matteAlpha - clearAlpha > 30, $"Klar-Alpha={clearAlpha}, Matt-Alpha={matteAlpha}");
        app.State.Settings.WindowOpacity = 0; app.ApplyAppearance();
        Check("100 Prozent Transparenz entfernt Ordnerfläche und Rahmen bei sichtbaren Icons", PixelAlpha(app.CurrentFlyout!, 216, app.CurrentFlyout!.ActualHeight - 8) == 0 && PixelAlpha(app.CurrentFlyout!, 216, app.CurrentFlyout!.ActualHeight - 1) == 0 && Descendants(app.CurrentFlyout!).OfType<System.Windows.Controls.Image>().All(i => i.Opacity == 1 && i.Source is not null));
        app.State.Settings.Glass = .55; app.State.Settings.WindowOpacity = .63; app.ApplyAppearance();
        app.CloseFlyout(); app.ShowSettings(); settings = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Single();
        settings.SelectScope(AppearanceScope.Bubbles); settings.TransparencySlider!.Value = 82;
        Check("Miniatur bietet gemeinsamen Rundungsregler", settings.RoundingSlider is not null && app.State.Settings.CornerRoundness == 18);
        Check("Miniaturtransparenz gilt für alle und lässt den Ordnerwert unverändert", Math.Abs(app.State.Settings.Opacity - .18) < .001 && app.State.Groups.All(g => g.Opacity is null) && Math.Abs(app.State.Settings.WindowOpacity - .63) < .001);
        settings.TransparencySlider!.Value = 100;
        Check("Miniaturtransparenz bis 100 Prozent ohne Änderung am Ordner", app.State.Settings.Opacity == 0 && Math.Abs(app.State.Settings.WindowOpacity - .63) < .001);
        Check("Miniatur-Fläche wird bei 100 Prozent tatsächlich transparent", PixelAlpha(app.Bubbles[0], 52, 6) == 0);
        settings.SizeSlider!.Value = 120; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Miniatur-Größenregler skaliert alle Desktop-Bubbles und ihre Rundung", app.Bubbles.All(b => Math.Abs(b.Width - 148) < 1) && Math.Abs(app.State.Settings.BubbleCornerRadius - 120 * .18) < .001);
        app.State.Groups[0].Opacity = null; app.State.Groups[0].WindowOpacity = null; app.State.Groups[0].Color = null; app.State.Groups[0].WindowColor = null;
        app.State.Settings.Opacity = .35; app.State.Settings.WindowOpacity = .35; app.State.Settings.MiniatureSize = 76;
        app.State.Settings.TileSize = 116; app.State.Settings.CornerRoundness = 14; app.ApplyAppearance(); settings.RefreshPage();
        SaveImage(app.Bubbles[0], Path.Combine(app.DataDirectory, "bubble-light.png"));
        settings.Close(); await Task.Delay(300);
        Check("Verwaltung geschlossen, Desktop-Bubbles bleiben sichtbar", app.Bubbles.All(b => Native.IsWindowVisible(Native.Handle(b))));
        if (desktopPoint is not null)
        {
            var bounds = Native.Bounds(app.Bubbles[0]);
            var hit = Native.WindowFromPoint(new Native.Point { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + 38) });
            Check("Desktop-Bubble nach Schließen der Verwaltung anklickbar", hit == Native.Handle(app.Bubbles[0]));
        }
        app.Toggle(app.Bubbles[0]); await Task.Delay(600);
        Check("Flyout öffnet", app.CurrentFlyout is { IsVisible: true });
        Check("Geöffnetes Raster enthält keinerlei sichtbaren Text", !Descendants(app.CurrentFlyout!).OfType<System.Windows.Controls.TextBlock>().Any());
        Check("Kacheln behalten zugängliche App-Namen", Descendants(app.CurrentFlyout!).OfType<System.Windows.Controls.Button>().All(b => !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(b))));
        Check("Mini-Bubble während geöffnetem Raster ausgeblendet", app.Bubbles[0].Expanded && ((UIElement)app.Bubbles[0].Content).Opacity == 0);
        var anchorBounds = Native.Bounds(app.Bubbles[0]); var popupBounds = Native.Bounds(app.CurrentFlyout!);
        var workArea = Native.WorkArea(anchorBounds.X + anchorBounds.Width / 2, anchorBounds.Y + anchorBounds.Height / 2);
        var expectedPopup = Placement.Flyout(anchorBounds, popupBounds.Width, popupBounds.Height, workArea);
        Check("Raster mittig über Bubble und im Arbeitsbereich", Math.Abs(popupBounds.X - expectedPopup.X) < 2 && Math.Abs(popupBounds.Y - expectedPopup.Y) < 2, $"Anchor={anchorBounds}; Popup={popupBounds}; erwartet={expectedPopup}; Work={workArea}");
        SaveImage(app.CurrentFlyout!, Path.Combine(app.DataDirectory, "flyout-light.png"));
        Check("Flyout ohne Taskbar", ((long)Native.GetWindowLongPtr(Native.Handle(app.CurrentFlyout!), Native.GwlExStyle) & Native.WsExToolWindow) != 0);
        app.ShowSettings(); settings = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().Single();
        Check("Mini-Bubble nach Schließen wieder sichtbar", !app.Bubbles[0].Expanded && ((UIElement)app.Bubbles[0].Content).Opacity == 1);
        app.Open(app.Bubbles[0]); await Task.Delay(200); settings.Activate(); await Task.Delay(250);
        Check("Fokuswechsel schließt Flyout", !app.HasFlyout);
        app.CloseFlyout(); await Task.Delay(100);
        // Warm up framework template and popup caches before checking repeated lifetime cycles.
        for (int i = 0; i < 5; i++) { app.Open(app.Bubbles[0]); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); app.CloseFlyout(); }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        using var process = Process.GetCurrentProcess(); process.Refresh(); int before = process.HandleCount; long memoryBefore = GC.GetTotalMemory(true);
        var timings = new List<double>();
        for (int i = 0; i < 60; i++)
        {
            var watch = Stopwatch.StartNew(); app.Open(app.Bubbles[0]);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); watch.Stop(); timings.Add(watch.Elapsed.TotalMilliseconds);
            app.CloseFlyout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); process.Refresh();
        long memoryAfter = GC.GetTotalMemory(true); int after = process.HandleCount;
        Check("60 Öffnen/Schließen-Zyklen", !app.HasFlyout);
        Check("Keine unbeschränkt steigenden Handles im Kurztest", after - before < 35, $"vorher={before}, nachher={after}");
        Check("Managed-Speicher nach Zyklen", memoryAfter - memoryBefore < 6 * 1024 * 1024, $"Delta={memoryAfter - memoryBefore} Bytes");
        // Resolve a genuine .lnk without rewriting or following it in the configuration.
        var shortcutPath = Path.Combine(app.DataDirectory, "Editor-Test.lnk");
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        object shortcut = ((dynamic)shell).CreateShortcut(shortcutPath);
        try { ((dynamic)shortcut).TargetPath = app.State.Groups[0].Items[0].Target; ((dynamic)shortcut).Save(); }
        finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        var entry = Targets.Create(shortcutPath);
        Check(".lnk-Ziel aufgelöst", LaunchService.Describe(entry).Contains("notepad.exe", StringComparison.OrdinalIgnoreCase));
        Targets.Add(app.State.Groups[0], entry);
        Check("Originalverknüpfung erhalten", File.Exists(shortcutPath));
        var blankLink = Path.Combine(app.DataDirectory, "\u00A0\u00A0.lnk");
        object nameShell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        object nameShortcut = ((dynamic)nameShell).CreateShortcut(blankLink);
        try { ((dynamic)nameShortcut).TargetPath = app.State.Groups[0].Items[0].Target; ((dynamic)nameShortcut).Save(); }
        finally { Marshal.FinalReleaseComObject(nameShortcut); Marshal.FinalReleaseComObject(nameShell); }
        var blankName = await app.Icons.FriendlyName(blankLink);
        Check("App-Name trotz leerem Verknüpfungsnamen ermittelt", !string.IsNullOrWhiteSpace(blankName) && blankName != "Unbenannt", blankName);
        Check("Unbenannte Originalverknüpfung erhalten", File.Exists(blankLink));
        var marker = Path.Combine(app.DataDirectory, "launch-probe.txt");
        var launchLink = Path.Combine(app.DataDirectory, "Launch-Probe.lnk");
        if (File.Exists(marker)) File.Delete(marker);
        object launchShell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        object launchShortcut = ((dynamic)launchShell).CreateShortcut(launchLink);
        try
        {
            ((dynamic)launchShortcut).TargetPath = Environment.ProcessPath!;
            ((dynamic)launchShortcut).Arguments = $"--launch-probe \"{marker}\"";
            ((dynamic)launchShortcut).Save();
        }
        finally { Marshal.FinalReleaseComObject(launchShortcut); Marshal.FinalReleaseComObject(launchShell); }
        var originalLink = SHA256.HashData(File.ReadAllBytes(launchLink));
        LaunchService.Launch(Targets.Create(launchLink));
        var deadline = Stopwatch.StartNew();
        while (!File.Exists(marker) && deadline.Elapsed.TotalSeconds < 8) await Task.Delay(100);
        Check(".lnk startet Ziel mit Originalargumenten", File.Exists(marker) && File.ReadAllText(marker) == "DesktopBubbles launch probe");
        Check("Start verändert .lnk nicht", originalLink.SequenceEqual(SHA256.HashData(File.ReadAllBytes(launchLink)))) ;
        var dragDesktop = Path.Combine(app.DataDirectory, "drag-fixture-desktop"); Directory.CreateDirectory(dragDesktop);
        var dragGroup = new BubbleGroup(); var dragEntry = Targets.Create(launchLink, "Start-Probe"); dragGroup.Items.Add(dragEntry);
        var outbound = new ShortcutDrag(app, dragGroup, dragEntry, dragDesktop);
        Check("Herausziehen verwendet echte unabhängige .lnk-Kopie", outbound.FilePath != launchLink && File.Exists(outbound.FilePath) && ShortcutVault.Hash(outbound.FilePath) == ShortcutVault.Hash(launchLink));
        Check("Windows-FileDrop enthält ausschließlich die Exportkopie", outbound.Data.GetData(DataFormats.FileDrop) is string[] dropPaths && dropPaths.Length == 1 && dropPaths[0] == outbound.FilePath);
        Check("Natives CF_HDROP rendert die Exportkopie ohne Objekt-Deserialisierung", NativeDropPath(outbound.Data) == outbound.FilePath);
        Check("Interner Drag-Token wird als native Bytes übertragen", NativeToken(outbound.Data) == outbound.Token);
        Check("Drag-Abbruch bestätigt keine Entfernung", !await outbound.Confirmed(DragDropEffects.None));
        File.Copy(outbound.FilePath, outbound.DesktopPath);
        Check("Exportbestätigung prüft tatsächliche Desktop-Datei", await outbound.Confirmed(DragDropEffects.Copy));
        File.WriteAllText(outbound.DesktopPath, "changed"); Check("Veränderte Zieldatei bestätigt keinen Export", !outbound.DesktopConfirmed());
        var programDrag = new ShortcutDrag(app, dragGroup, Targets.Create(Path.Combine(Environment.SystemDirectory, "notepad.exe"), "Editor"), dragDesktop);
        Check("Programm wird als .lnk exportiert, nicht als EXE verschoben", programDrag.FilePath.EndsWith(".lnk") && LaunchService.Describe(Targets.Create(programDrag.FilePath)).Contains("notepad.exe", StringComparison.OrdinalIgnoreCase));
        string returnedProgram = programDrag.WriteToDesktop();
        Check("Desktop-Rückgabe erzeugt eine funktionsfähige Programmverknüpfung", File.Exists(returnedProgram) && LaunchService.Describe(Targets.Create(returnedProgram)).Contains("notepad.exe", StringComparison.OrdinalIgnoreCase));
        Check("Wiederholte Rückgabe verwendet dieselbe geprüfte Kopie", programDrag.WriteToDesktop() == returnedProgram);
        string returnedLink = outbound.WriteToDesktop();
        Check("Desktop-Rückgabe überschreibt keine gleichnamige Datei", returnedLink != outbound.DesktopPath && File.ReadAllText(outbound.DesktopPath) == "changed" && ShortcutVault.Hash(returnedLink) == outbound.Hash);
        var returnGroup = new BubbleGroup(); app.State.Groups.Add(returnGroup);
        var returnEntry = Targets.Create("https://example.com", "Website"); returnGroup.Items.Add(returnEntry);
        string returnedWeb = app.ReturnToDesktop(returnGroup, returnEntry, dragDesktop);
        Check("Aus Gruppe entfernen schreibt URL vor dem Entfernen auf den Desktop", File.ReadAllText(returnedWeb).Contains("URL=https://example.com") && !returnGroup.Items.Contains(returnEntry));
        var failedEntry = Targets.Create(launchLink, "Bleibt erhalten"); returnGroup.Items.Add(failedEntry);
        try { app.ReturnToDesktop(returnGroup, failedEntry, returnedWeb); } catch (IOException) { }
        Check("Fehlgeschlagene Desktop-Rückgabe erhält den Gruppeneintrag", returnGroup.Items.Contains(failedEntry) && File.Exists(launchLink));
        app.State.Groups.Remove(returnGroup); app.Changed();
        outbound.Cleanup(); programDrag.Cleanup();
        var missingEntry = Targets.Create(Path.Combine(app.DataDirectory, "missing-shortcut.lnk"), "Defekter Eintrag");
        var deleteGroup = app.State.Groups[0]; deleteGroup.Items.Add(missingEntry); app.Changed(); app.Open(app.Bubbles[0]); await Task.Delay(250);
        var missingButton = Descendants(app.CurrentFlyout!).OfType<System.Windows.Controls.Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == missingEntry.Name);
        var entryMenu = missingButton.ContextMenu!.Items.OfType<System.Windows.Controls.MenuItem>().ToArray();
        Check("Symbolmenü bietet Starten, Zurück zum Desktop und Aus Gruppe löschen", entryMenu.Select(m => (string)m.Header).SequenceEqual(new[] { "Starten", "Zurück zum Desktop", "Aus Gruppe löschen" }));
        entryMenu[2].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check("Defekter Eintrag lässt sich über das echte Symbolmenü dauerhaft löschen", !deleteGroup.Items.Contains(missingEntry) && !StateStore.Read(app.Store.Path).Groups.SelectMany(g => g.Items).Any(e => e.Id == missingEntry.Id) && !Descendants(app.CurrentFlyout!).OfType<System.Windows.Controls.Button>().Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == missingEntry.Name));
        app.CloseFlyout();
        // Native vault workflow on a fixture desktop only; never touch the user's desktop files.
        var fixtureDesktop = Path.Combine(app.DataDirectory, "fixture-desktop"); Directory.CreateDirectory(fixtureDesktop);
        var fixtureData = Path.Combine(app.DataDirectory, "fixture-vault");
        var platform = new WindowsShortcutPlatform(fixtureDesktop);
        var vault = new ShortcutVault(fixtureData, platform);
        var fixtureStore = new StateStore(Path.Combine(fixtureData, "groups.json"));
        var manager = new ShortcutManager(vault, fixtureStore.Save);
        var fixtureState = new BubbleState { Groups = [new BubbleGroup { Name = "Vault-Test" }] };
        var managedSource = Path.Combine(fixtureDesktop, "Launcher.lnk"); File.Copy(launchLink, managedSource);
        Check("Native Copy-Prüfung erhält Ziel, Argumente und Arbeitsverzeichnis", platform.ValidateRelocatedShortcut(launchLink, managedSource));
        string? takeOverWarning = manager.Add(fixtureState, fixtureState.Groups[0], managedSource);
        var managedEntry = fixtureState.Groups[0].Items.Single();
        Check("Native Übernahme entfernt genau das gesicherte Desktop-Original", takeOverWarning is null && !File.Exists(managedSource) && File.Exists(manager.Resolve(managedEntry.Target)), takeOverWarning);
        Check("Gespeicherte .lnk behält alle Bytes", originalLink.SequenceEqual(SHA256.HashData(File.ReadAllBytes(manager.Resolve(managedEntry.Target)))));
        File.Delete(marker);
        LaunchService.Launch(new AppEntry { Target = manager.Resolve(managedEntry.Target) });
        deadline.Restart(); while (!File.Exists(marker) && deadline.Elapsed.TotalSeconds < 8) await Task.Delay(100);
        Check("Übernommene .lnk startet nach Entfernen des Originals", File.Exists(marker));
        manager.Remove(fixtureState, fixtureState.Groups[0], managedEntry);
        Check("Native Wiederherstellung erhält Originalbytes", File.Exists(managedSource) && originalLink.SequenceEqual(SHA256.HashData(File.ReadAllBytes(managedSource)))) ;
        var lockedSource = Path.Combine(fixtureDesktop, "Gesperrt.lnk"); File.Copy(launchLink, lockedSource);
        using (var locked = new FileStream(lockedSource, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var warning = manager.Add(fixtureState, fixtureState.Groups[0], lockedSource);
            Check("Gesperrtes Desktop-Original bleibt bei nativer Übernahme erhalten", warning is not null && File.Exists(lockedSource));
        }
        var modifiedSource = Path.Combine(fixtureDesktop, "Changed.lnk"); File.Copy(launchLink, modifiedSource);
        bool changedRejected = false; try { platform.DeleteOriginalIfUnchanged(modifiedSource, new string('0', 64)); } catch (IOException) { changedRejected = true; }
        Check("Native Löschung lehnt geänderten Hash ab", changedRejected && File.Exists(modifiedSource));
        var webSource = Path.Combine(fixtureDesktop, "Web.url"); File.WriteAllText(webSource, "[InternetShortcut]\r\nURL=https://example.com\r\n");
        manager.Add(fixtureState, fixtureState.Groups[0], webSource);
        Check(".url wird geprüft und übernommen, ohne sie zu öffnen", !File.Exists(webSource));
        var externalSource = Path.Combine(fixtureData, "Extern.lnk"); File.Copy(launchLink, externalSource);
        manager.Add(fixtureState, fixtureState.Groups[0], externalSource);
        var externalTarget = fixtureState.Groups[0].Items.Last().Target;
        bool externalStayed = File.Exists(externalSource); File.Delete(externalSource);
        Check("Link außerhalb des persönlichen Desktops wird unabhängig kopiert", externalStayed && File.Exists(manager.Resolve(externalTarget)) && !manager.CanRestore(externalTarget));
        var bundlePath = Path.Combine(fixtureData, "roundtrip.bubbles"); ConfigurationBundle.Export(bundlePath, fixtureState, vault);
        var bundle = ConfigurationBundle.Read(bundlePath);
        var importedVault = new ShortcutVault(Path.Combine(fixtureData, "import"), platform);
        foreach (var payload in bundle.Payloads) importedVault.ImportPayload(payload.Key, payload.Value);
        Check("Bundle enthält nutzbare .lnk/.url ohne Desktop-Eigentum", bundle.Payloads.Count == 3 && bundle.Payloads.Keys.All(t => File.Exists(importedVault.Resolve(t)) && !importedVault.OwnsDesktopOriginal(t)));
        // Name visibility is a presentation preference; the internal name stays stable.
        var hiddenNameGroup = app.State.Groups[1]; hiddenNameGroup.ShowName = false; app.Rebuild(); await Task.Delay(200);
        var hiddenBubble = app.Bubbles.Single(b => b.Group == hiddenNameGroup);
        var viewbox = (System.Windows.Controls.Viewbox)hiddenBubble.Content;
        var canvas = (System.Windows.Controls.Grid)viewbox.Child;
        var stack = (System.Windows.Controls.StackPanel)canvas.Children[0];
        Check("Bubble-Name ausblendbar, interner Name bleibt erhalten", stack.Children.OfType<System.Windows.Controls.TextBlock>().Single().Visibility == Visibility.Collapsed && hiddenNameGroup.Name == "Meine Projekte");
        SaveImage(hiddenBubble, Path.Combine(app.DataDirectory, "bubble-without-name.png"));
        var apps = await app.Icons.StoreApps(); Check("Installierte Apps gelesen", apps.Count > 0, $"{apps.Count} Apps");
        var storeApp = apps.FirstOrDefault(a => Targets.IsStore(a.Target));
        if (storeApp is not null) Check("Store-Icon abgerufen", await app.Icons.Get(storeApp.Target) is not null);
        app.State.Settings.Theme = "Dark"; Ui.ApplyTheme(app.State.Settings); app.Rebuild(); settings.RefreshPage();
        await Task.Delay(350); settings.Show(); settings.Activate(); await Task.Delay(100);
        SaveImage(settings, Path.Combine(app.DataDirectory, "settings-dark.png"));
        app.Open(app.Bubbles[0]); await Task.Delay(350);
        SaveImage(app.CurrentFlyout!, Path.Combine(app.DataDirectory, "flyout-dark.png"));
        app.CloseFlyout(); settings.Hide(); app.Changed(); app.Flush();
        Check("Persistenz nach UI-Änderungen", StateStore.Read(app.Store.Path).Groups[0].Items.Count == 5);
        var report = await Diagnostics.Measure(app, 10);
        var result = new
        {
            OS = Environment.OSVersion.VersionString, Date = DateTimeOffset.Now, AppearanceOnly = appearanceOnly, Checks = checks,
            Integration = app.Host.Status, EmbeddedGroups = app.Bubbles.Count(b => b.Embedded),
            OpenMedianMs = timings.Order().ElementAt(timings.Count / 2), OpenMaxMs = timings.Max(),
            HandlesBeforeCycles = before, HandlesAfterCycles = after, ManagedDeltaBytes = memoryAfter - memoryBefore,
            ResourceReport = report,
            NotTested = new[] { "Echter Explorer-Neustart", "Win+D mit Benutzerinteraktion", "Mehrere Monitore und gemischte DPI", "Gaming / GPU / Frametimes", "Beliebige Drittanbieter-Programme / UAC / Store-App-Start", "Drag & Drop mit physischer Maus", "Anmeldung / Autostart" }
                .Concat(appearanceOnly ? new[] { "Desktop-Klickfläche unter fremden Fenstern: im Aussehen-Test ausdrücklich übersprungen" } : Array.Empty<string>()).ToArray()
        };
        File.WriteAllText(Path.Combine(app.DataDirectory, "smoke-results.json"), JsonSerializer.Serialize(result, StateStore.JsonOptions));
    }
    private static Native.Point? FindExposedDesktop()
    {
        var host = Native.FindDesktopHost();
        if (host == 0) return null;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var area = screen.WorkingArea;
            for (int y = area.Top + 24; y < area.Bottom - 140; y += 160)
            for (int x = area.Left + 24; x < area.Right - 120; x += 220)
            {
                var hit = Native.WindowFromPoint(new Native.Point { X = x + 52, Y = y + 38 });
                if (hit != host && !Native.IsChild(host, hit)) continue;
                var cls = new System.Text.StringBuilder(128); Native.GetClassName(hit, cls, cls.Capacity);
                if (cls.ToString() is "SysListView32" or "SHELLDLL_DefView" or "Progman" or "WorkerW") return new Native.Point { X = x, Y = y };
            }
        }
        return null;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static bool HasRoundedRegion(Window window) => !RegionContains(window, 0, 0) && RegionContains(window, window.ActualWidth / 2, window.ActualHeight / 2);
    private static byte PixelAlpha(Window window, double x, double y)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect((int)x, (int)y, 1, 1), pixel, 4, 0); return pixel[3];
    }
    private static bool RegionContains(Window window, double x, double y)
    {
        nint region = Native.CreateRoundRectRgn(0, 0, 1, 1, 0, 0);
        if (region == 0) throw new InvalidOperationException("Test region allocation failed");
        try
        {
            if (Native.GetWindowRgn(Native.Handle(window), region) == 0) throw new InvalidOperationException("Window has no native clip region");
            double scale = Native.GetDpiForWindow(Native.Handle(window)) / 96d;
            return Native.PtInRegion(region, (int)Math.Round(x * scale), (int)Math.Round(y * scale));
        }
        finally { Native.DeleteObject(region); }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(nint drop, uint index, System.Text.StringBuilder? path, uint count);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
    private static string NativeDropPath(DataObject data)
    {
        var format = new System.Runtime.InteropServices.ComTypes.FORMATETC { cfFormat = 15, dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL };
        ((System.Runtime.InteropServices.ComTypes.IDataObject)data).GetData(ref format, out var medium);
        try { var path = new System.Text.StringBuilder(32768); DragQueryFile(medium.unionmember, 0, path, (uint)path.Capacity); return path.ToString(); }
        finally { ReleaseStgMedium(ref medium); }
    }
    private static string NativeToken(DataObject data)
    {
        var format = new System.Runtime.InteropServices.ComTypes.FORMATETC { cfFormat = unchecked((short)DataFormats.GetDataFormat(ShortcutDrag.Format).Id), dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL };
        ((System.Runtime.InteropServices.ComTypes.IDataObject)data).GetData(ref format, out var medium);
        try { var pointer = GlobalLock(medium.unionmember); try { var bytes = new byte[32]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return System.Text.Encoding.UTF8.GetString(bytes); } finally { GlobalUnlock(medium.unionmember); } }
        finally { ReleaseStgMedium(ref medium); }
    }
    private static void SaveImage(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}

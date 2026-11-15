using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopBubbles.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DesktopBubbles;

public partial class App : System.Windows.Application
{
    internal BubbleState State { get; private set; } = new();
    internal ShellIcons Icons { get; } = new();
    internal DesktopHost Host { get; } = new();
    internal StateStore Store { get; private set; } = null!;
    internal ShortcutVault Vault { get; private set; } = null!;
    internal ShortcutManager Shortcuts { get; private set; } = null!;
    private readonly WindowsShortcutPlatform shortcutPlatform = new();
    internal bool Portable { get; private set; }
    internal string DataDirectory { get; private set; } = "";
    internal IReadOnlyList<BubbleWindow> Bubbles => bubbles;
    private readonly List<BubbleWindow> bubbles = [];
    private readonly HashSet<Guid> resolvingNames = [];
    private FlyoutWindow? flyout;
    private SettingsWindow? settings;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private DispatcherTimer saveTimer = null!, recoveryTimer = null!;
    private HwndSource? listener;
    private Mutex? mutex;
    private bool ownsMutex, exiting, dirty, saveWarning;
    // Taken-over desktop shortcuts return to the desktop while the app is not running.
    private bool parkOnExit, sessionEnding;
    private int recoveryAttempts;
    private DateTime lastClose;
    private Guid lastGroup;
    internal bool HasFlyout => flyout is not null;

    // Hooked from the combo styles in App.xaml: every drop-down list glides open and closed.
    private void ComboLoaded(object sender, RoutedEventArgs e) { if (sender is System.Windows.Controls.ComboBox box) SettingsMotion.SmoothDropDown(box); }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            // Used only by the automated .lnk/ShellExecute integration test.
            if (e.Args.Length == 2 && e.Args[0] == "--launch-probe")
            { File.WriteAllText(e.Args[1], "DesktopBubbles launch probe"); Shutdown(); return; }
            bool smoke = e.Args.Contains("--smoke-test");
            bool visual = e.Args.Contains("--design-preview");
            Portable = e.Args.Contains("--portable") || File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));
            int dataArg = Array.IndexOf(e.Args, "--data-dir");
            string requestedDirectory = dataArg >= 0 && dataArg + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataArg + 1])
                : Path.Combine(Portable ? AppContext.BaseDirectory : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Portable ? "data" : "DesktopBubbles");
            if (smoke && dataArg < 0) throw new InvalidOperationException("--smoke-test benötigt ein separates --data-dir.");
            if (visual && dataArg < 0) throw new InvalidOperationException("--design-preview benötigt ein separates --data-dir.");
            var location = DataLocation.Select(requestedDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopBubbles"));
            if ((smoke || visual) && location.Redirected) throw new IOException("Der ausdrücklich gewählte Testdatenordner ist nicht beschreibbar.");
            DataDirectory = location.DirectoryPath;
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..16];
            mutex = new Mutex(true, @"Local\DesktopBubbles-" + hash, out ownsMutex);
            uint showMessage = Native.RegisterWindowMessage("DesktopBubbles.ShowSettings");
            if (!ownsMutex)
            {
                var existing = Native.FindWindow(null, "DesktopBubbles.Message." + hash);
                if (existing != 0) Native.SendNotifyMessage(existing, showMessage, 0, 0);
                Shutdown(); return;
            }
            location.Prepare(); Log.DirectoryPath = DataDirectory;
            if (location.Redirected) Log.Write("Benutzerdaten statt geschütztem Ordner: " + location.RequestedDirectory);
            Store = new StateStore(Path.Combine(DataDirectory, "groups.json"));
            State = Store.Load(); Loc.Use(State.Settings.Language); Ui.ApplyTheme(State.Settings);
            Vault = new ShortcutVault(DataDirectory, shortcutPlatform);
            Shortcuts = new ShortcutManager(Vault, state => { Store.Save(state); dirty = false; });
            var recoveryNotices = Shortcuts.Recover(State);
            foreach (var notice in recoveryNotices) Log.Write(notice);
            // Test and diagnostic runs never touch the real desktop.
            parkOnExit = !smoke && !visual && !e.Args.Contains("--measure-idle");
            if (parkOnExit) foreach (var notice in Shortcuts.Unpark(State)) Log.Write(notice);
            if (parkOnExit)
            {
                // After unparking: whatever is still a duplicate on the desktop came from an update.
                Duplicates = new DesktopDuplicates(this, shortcutPlatform.PersonalDesktop);
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => Duplicates?.Start());
            }
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            saveTimer.Tick += (_, _) => { saveTimer.Stop(); Flush(); };
            recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            recoveryTimer.Tick += (_, _) =>
            {
                recoveryTimer.Stop();
                if (exiting) return;
                if (Native.GetShellWindow() == 0 && recoveryAttempts++ < 3) { recoveryTimer.Start(); return; }
                recoveryAttempts = 0; Rebuild();
            };
            var parameters = new HwndSourceParameters("DesktopBubbles.Message." + hash) { WindowStyle = unchecked((int)0x80000000), Width = 0, Height = 0 };
            listener = new HwndSource(parameters);
            uint taskbarMessage = Native.RegisterWindowMessage("TaskbarCreated");
            listener.AddHook((nint h, int m, nint w, nint l, ref bool handled) =>
            {
                if (m == taskbarMessage) { Log.Write("TaskbarCreated: recovering desktop windows."); ScheduleRecovery(); }
                if (m == showMessage) ShowSettings();
                if (m == 0x7E) ScheduleRecovery(); // WM_DISPLAYCHANGE
                if (m == 0x11) Flush(); // WM_QUERYENDSESSION
                return 0;
            });
            SystemEvents.UserPreferenceChanged += PreferencesChanged;
            SystemEvents.DisplaySettingsChanged += DisplaysChanged;
            SessionEnding += (_, _) => { sessionEnding = true; Flush(); };
            DispatcherUnhandledException += (_, args) => { Log.Write(args.Exception.ToString()); args.Handled = true; Ui.Error(args.Exception); };
            SetupTray();
            if (smoke)
            {
                State = SmokeTests.CreateState(); Loc.Use(State.Settings.Language); Ui.ApplyTheme(State.Settings); Rebuild();
                _ = RunSmoke(e.Args.Contains("--appearance-only"));
            }
            else
            {
                Rebuild();
                _ = ResolveNames();
                if (!e.Args.Contains("--background") || State.Groups.Count == 0) ShowSettings();
                if (visual) _ = RunVisualPreview();
                if (Store.RecoveryNotice is not null) MessageBox.Show(Store.RecoveryNotice, Loc.T("DesktopBubbles · Wiederherstellung", "DesktopBubbles · Recovery"));
                if (recoveryNotices.Count > 0) MessageBox.Show(string.Join("\n", recoveryNotices.Distinct()), Loc.T("DesktopBubbles · Verknüpfungen wiederhergestellt", "DesktopBubbles · Shortcuts restored"));
                if (e.Args.Contains("--measure-idle")) _ = MeasureAndExit();
            }
            Log.Write($"Started {Environment.OSVersion.Version}, .NET {Environment.Version}, portable={Portable}, data={DataDirectory}");
        }
        catch (Exception ex) { Ui.Error(ex); Shutdown(1); }
    }

    private async Task RunSmoke(bool appearanceOnly)
    {
        try { await SmokeTests.Run(this, appearanceOnly); Quit(); }
        catch (Exception ex) { Log.Write("SMOKE FAILED " + ex); File.WriteAllText(Path.Combine(DataDirectory, "smoke-failure.txt"), ex.ToString()); Shutdown(2); }
    }
    private async Task RunVisualPreview()
    {
        try { await VisualPreview.Run(this); Quit(); }
        catch (Exception ex) { Log.Write(ex.ToString()); File.WriteAllText(Path.Combine(DataDirectory, "preview-failure.txt"), ex.ToString()); Shutdown(4); }
    }
    private async Task MeasureAndExit()
    {
        try { await Task.Delay(2000); await Diagnostics.Measure(this, 15); Quit(); }
        catch (Exception ex) { Log.Write(ex.ToString()); Shutdown(3); }
    }
    private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => { Ui.ApplyTheme(State.Settings); ScheduleRecovery(); settings?.RefreshPage(); });
    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(ScheduleRecovery);
    internal void ScheduleRecovery()
    { if (exiting || recoveryTimer is null) return; recoveryTimer.Stop(); recoveryTimer.Start(); }

    private void SetupTray()
    {
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/Assets/DesktopBubbles.ico")).Stream;
        using var brandIcon = new System.Drawing.Icon(iconStream);
        trayIcon = new System.Drawing.Icon(brandIcon, Forms.SystemInformation.SmallIconSize);
        var assembly = Assembly.GetExecutingAssembly();
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3) ?? Loc.T("unbekannt", "unknown");
        tray = new Forms.NotifyIcon { Text = "DesktopBubbles · " + version, Icon = trayIcon, Visible = true };
        BuildTrayMenu(version);
        // A single left click opens the app; the right button keeps the menu.
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ShowSettings); };
    }
    private string version = "";
    // Built again when the language changes.
    private void BuildTrayMenu(string version)
    {
        if (tray is null) return;
        this.version = version;
        var old = tray.ContextMenuStrip;
        var menu = TrayMenu.Create();
        var head = new VersionItem(version) { Font = menu.Font }; menu.Items.Add(head);
        // The version line checks for updates and must keep the menu open while it does.
        bool keepOpen = false;
        menu.ItemClicked += (_, e) => keepOpen = e.ClickedItem == head;
        menu.Closing += (_, e) => { if (keepOpen && e.CloseReason == Forms.ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; keepOpen = false; };
        head.Click += async (_, _) => await head.Check(version);
        menu.Opening += (_, _) => head.Reset();
        menu.Items.Add(TrayMenu.Spacer());
        menu.Items.Add(TrayMenu.Item(Loc.T("Einstellungen öffnen", "Open settings"), (_, _) => Dispatcher.Invoke(ShowSettings)));
        menu.Items.Add(TrayMenu.Item(Loc.T("Zum Repository", "Open repository"), (_, _) => Dispatcher.Invoke(OpenRepository)));
        menu.Items.Add(TrayMenu.Item(Loc.T("Desktop-Verbindung erneuern", "Reconnect desktop"), (_, _) => Dispatcher.Invoke(Reconnect)));
        menu.Items.Add(TrayMenu.Spacer());
        menu.Items.Add(TrayMenu.Exit(Loc.T("Beenden", "Quit"), (_, _) => Dispatcher.Invoke(Quit)));
        tray.ContextMenuStrip = menu;
        old?.Dispose();
    }
    // Switches the whole app between German and English, without a restart: the tray menu, the
    // miniatures' tooltips and menus, and the open management window (with a page hand-over).
    internal void SetLanguage(string language)
    {
        if (State.Settings.Language == language) return;
        State.Settings.Language = language; Loc.Use(language); RequestSave();
        BuildTrayMenu(version);
        CloseFlyout();
        foreach (var bubble in bubbles) bubble.RefreshTexts();
        settings?.Retranslate();
    }
    private void OpenRepository()
    {
        try { Process.Start(new ProcessStartInfo("https://github.com/Hendrik4K/DesktopBubbles") { UseShellExecute = true }); }
        catch (Exception ex) { Ui.Error(ex); }
    }

    internal void Rebuild()
    {
        CloseFlyout();
        foreach (var window in bubbles.ToArray()) { try { window.Finish(); } catch (InvalidOperationException) { } }
        bubbles.Clear();
        foreach (var group in State.Groups)
        {
            var window = new BubbleWindow(this, group); bubbles.Add(window); window.Show();
        }
        settings?.UpdateStatus();
    }
    // "Neu verbinden": the miniatures fade out, are rebuilt and fade back in instead of blinking.
    internal async void Reconnect()
    {
        if (!State.Settings.Animations || !SystemParameters.ClientAreaAnimation) { Rebuild(); return; }
        CloseFlyout();
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
        foreach (var window in bubbles) window.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        await Task.Delay(260);
        if (exiting) return;
        Rebuild();
        foreach (var window in bubbles)
            window.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease, FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop });
    }
    internal void Changed(bool rebuild = false)
    {
        RequestSave();
        if (rebuild) Rebuild();
        else foreach (var window in bubbles.ToArray()) _ = window.LoadIcons();
        settings?.RefreshGroups();
        _ = ResolveNames();
    }
    private async Task ResolveNames()
    {
        var entries = State.Groups.SelectMany(g => g.Items).Where(Targets.NeedsFriendlyName).Where(e => resolvingNames.Add(e.Id)).ToList();
        bool changed = false;
        try
        {
            foreach (var entry in entries)
            {
                var name = await Icons.FriendlyName(Resolve(entry.Target));
                if (exiting || !State.Groups.Any(g => g.Items.Contains(entry)) || string.IsNullOrWhiteSpace(name)) continue;
                entry.Name = StateStore.CleanName(name); changed = true;
            }
            if (changed && !exiting) { Changed(); if (flyout is not null) await flyout.Populate(); }
        }
        finally { foreach (var entry in entries) resolvingNames.Remove(entry.Id); }
    }
    internal void RequestSave() { dirty = true; saveTimer.Stop(); saveTimer.Start(); }
    internal void Flush()
    {
        if (!dirty || Store is null) return;
        try { Store.Save(State); dirty = false; saveWarning = false; }
        catch (Exception ex)
        {
            Log.Write("SAVE FAILED " + ex);
            if (!saveWarning && !exiting) { saveWarning = true; Ui.Error(new IOException(Loc.T("Die Änderungen konnten nicht gespeichert werden. Bitte den Datenordner prüfen.\n", "The changes could not be saved. Please check the data folder.\n") + ex.Message)); }
        }
    }
    internal void ApplySettings(BubbleSettings value)
    {
        State.Settings = value; StateStore.Validate(State); Ui.ApplyTheme(value); Changed(true);
        settings?.RefreshPage();
    }
    internal void ApplyAppearance()
    {
        StateStore.Validate(State); Ui.ApplyTheme(State.Settings); RequestSave();
        foreach (var bubble in bubbles) bubble.RefreshAppearance();
        if (flyout is { } current && (current.Liquid != State.Settings.LiquidGlass || current.Frosted != (!State.Settings.LiquidGlass && State.Settings.FrostedFolders)))
        {
            var anchor = bubbles.FirstOrDefault(b => b.Group.Id == current.Group.Id);
            if (anchor is not null) Open(anchor); else CloseFlyout();
        }
        else flyout?.RefreshAppearance();
        settings?.RefreshMaterial();
    }
    internal void ShowSettings()
    {
        CloseFlyout();
        if (settings is null)
        {
            settings = new SettingsWindow(this); settings.Closed += (_, _) => settings = null;
            settings.Show();
        }
        if (settings.WindowState == WindowState.Minimized) settings.WindowState = WindowState.Normal;
        settings.Activate();
    }
    internal async void ShowDesktop(BubbleGroup? group = null)
    {
        try { await ShowDesktopAsync(group); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal async Task ShowDesktopAsync(BubbleGroup? group = null)
    {
        CloseFlyout(); Flush(); settings?.Close();
        try { LaunchService.ShowDesktop(); }
        catch (Exception ex) { Ui.Error(ex); return; }
        // Explorer applies MinimizeAll asynchronously. Restore our toolwindows
        // only after that operation; never place them in the topmost band.
        await Task.Delay(300);
        if (exiting) return;
        foreach (var bubble in bubbles)
        {
            Native.ShowWindow(Native.Handle(bubble), 4);
            Native.SetWindowPos(Native.Handle(bubble), 0, 0, 0, 0, 0, Native.NoMove | Native.NoSize | Native.NoActivate | 0x40);
        }
        var selected = bubbles.FirstOrDefault(b => b.Group.Id == group?.Id);
        if (selected is not null)
        {
            var area = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            selected.Group.X = area.Left + area.Width / 2 - 52;
            selected.Group.Y = area.Top + area.Height / 2 - 58;
            selected.RestorePosition(); RequestSave(); Flush();
        }
        if (selected is not null && State.Settings.Animations && SystemParameters.ClientAreaAnimation)
            selected.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, .45, TimeSpan.FromMilliseconds(350))
                { AutoReverse = true, RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(2), FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop });
    }
    internal void Toggle(BubbleWindow window)
    {
        // A click on the miniature while its folder is still opening changes nothing: the opening plays out.
        if (flyout?.Group.Id == window.Group.Id) { if (!flyout.Opening) CloseFlyout(); return; }
        if (lastGroup == window.Group.Id && DateTime.UtcNow - lastClose < TimeSpan.FromMilliseconds(240)) return;
        Open(window);
    }
    internal void Open(BubbleWindow window)
    {
        CloseFlyout();
        flyout = new FlyoutWindow(this, window);
        var current = flyout;
        current.Closed += (_, _) => { window.SetExpanded(false); lastClose = DateTime.UtcNow; lastGroup = current.Group.Id; if (flyout == current) flyout = null; };
        // Seed on the anchor's monitor before first layout; WPF then adopts its DPI.
        var a = Native.Bounds(window); Native.Position(current, a.X, a.Y);
        try { if (!current.HidesAnchor) window.SetExpanded(true); current.Show(); current.Activate(); }
        catch { window.SetExpanded(false); flyout = null; current.Close(); throw; }
    }
    internal void CloseFlyout() { var current = flyout; flyout = null; current?.CloseNow(); }
    internal FlyoutWindow? CurrentFlyout => flyout;
    // Saves the auto-close choice and re-arms an open folder; nothing else on screen changes.
    internal void ApplyAutoClose() { StateStore.Validate(State); RequestSave(); flyout?.ArmAutoClose(); }
    internal bool Exiting => exiting;
    private ShortcutDrag? activeDrag;
    private DateTime dragQuietUntil;
    // During a drag-out and shortly after, links on the desktop are there on purpose.
    internal bool DragQuiet => activeDrag is not null || DateTime.UtcNow < dragQuietUntil;
    internal DesktopDuplicates? Duplicates { get; private set; }
    internal void ReportSharedDuplicates(IReadOnlyList<string> names)
    {
        Log.Write("Shared desktop duplicates: " + string.Join(", ", names));
        tray?.ShowBalloonTip(8000, "DesktopBubbles",
            Loc.T($"{string.Join(", ", names)} liegt erneut auf dem öffentlichen Desktop, obwohl es schon in einer Bubble ist. Entfernen: Einstellungen → Daten & Werkzeuge.", $"{string.Join(", ", names)} is back on the public desktop although it is already in a bubble. Remove it: Settings → Data & tools."),
            Forms.ToolTipIcon.Info);
    }
    // The shared desktop needs administrator rights; Windows asks once, for exactly these files.
    internal void RemoveSharedDuplicates()
    {
        Duplicates?.Scan();
        var found = Duplicates?.Shared ?? [];
        if (found.Count == 0) { MessageBox.Show(Loc.T("Auf dem öffentlichen Desktop liegen keine Verknüpfungen, die schon in einer Bubble sind.", "There are no shortcuts on the public desktop that are already in a bubble."), "DesktopBubbles"); return; }
        var list = string.Join("\n", found.Select(f => "• " + Path.GetFileNameWithoutExtension(f.Path) + "  (Bubble " + f.Bubble + ")"));
        if (MessageBox.Show(Loc.T("Diese Verknüpfungen liegen auf dem öffentlichen Desktop und schon in einer Bubble:\n\n", "These shortcuts are on the public desktop and already in a bubble:\n\n") + list +
            Loc.T("\n\nVom öffentlichen Desktop entfernen? Windows fragt dafür nach Administratorrechten. Programme und Bubble-Einträge bleiben unverändert.", "\n\nRemove them from the public desktop? Windows asks for administrator rights. Programs and bubble entries stay unchanged."),
            Loc.T("Doppelte Verknüpfungen", "Duplicate shortcuts"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string paths = string.Join(",", found.Select(f => "'" + f.Path.Replace("'", "''") + "'"));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"Remove-Item -LiteralPath {paths} -Force\"")
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            process?.WaitForExit(30000);
        }
        catch (System.ComponentModel.Win32Exception) { return; } // Administrator prompt declined.
        Duplicates?.Scan();
        int left = Duplicates?.Shared.Count ?? 0;
        Log.Write($"Shared desktop duplicates removed: {found.Count - left} of {found.Count}");
        if (left > 0) MessageBox.Show(Loc.T($"{left} Verknüpfung(en) konnten nicht entfernt werden.", $"{left} shortcut(s) could not be removed."), "DesktopBubbles", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    // Effect shown while dragging over a bubble or folder of the given group.
    internal DragDropEffects DropEffect(IDataObject data, BubbleGroup group)
        => IsInternalDrag(data) ? activeDrag?.Group == group ? DragDropEffects.None : DragDropEffects.Move
            : data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    internal bool IsInternalDrag(IDataObject data)
    {
        if (activeDrag is null) return false;
        try
        {
            if (!data.GetDataPresent(ShortcutDrag.Format, false)) return false;
            return data.GetData(ShortcutDrag.Format, false) is MemoryStream { Length: 32 } stream && Encoding.UTF8.GetString(stream.ToArray()) == activeDrag.Token;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or NotSupportedException) { return false; }
    }
    internal void ReceiveDrop(BubbleGroup group, IDataObject data, bool rebuild = false, int? index = null)
    {
        if (IsInternalDrag(data) && activeDrag is { } drag)
        {
            // Within one folder only the wiggle mode reorders; a plain drag changes nothing there.
            if (drag.Group == group) { drag.Internal = true; return; }
            try
            {
                Shortcuts.Transfer(State, drag.Group, group, drag.Entry, index);
                drag.Internal = true; Changed();
            }
            catch (Exception ex) { Ui.Error(ex); }
        }
        else if (data.GetData(DataFormats.FileDrop) is string[] files) AddTargets(group, files, rebuild);
    }
    internal async Task DragEntry(FrameworkElement source, BubbleGroup group, AppEntry entry)
    {
        try
        {
            var drag = new ShortcutDrag(this, group, entry); activeDrag = drag; DragDropEffects result;
            Log.Write("Drag out started: " + entry.Name);
            try { result = DragDrop.DoDragDrop(source, drag.Data, DragDropEffects.Copy | DragDropEffects.Move); }
            finally { activeDrag = null; dragQuietUntil = DateTime.UtcNow.AddSeconds(5); }
            Log.Write($"Drag out ended: effect={result}, internal={drag.Internal}");
            if (drag.Internal || result == DragDropEffects.None) { drag.Cleanup(); return; }
            bool confirmed = await drag.Confirmed(result);
            Log.Write($"Drag out confirmed={confirmed}");
            if (Shortcuts.CompleteExport(State, group, entry, confirmed)) Changed();
            else if (result != DragDropEffects.None) Log.Write("Drop not confirmed: kept bubble entry and staging copy.");
            if (confirmed) drag.Cleanup();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void CreateGroup()
    {
        if (State.Groups.Count >= 100) { Ui.Error(new InvalidOperationException(Loc.T("Maximal 100 Gruppen sind möglich.", "At most 100 groups are possible."))); return; }
        string? name = Ui.Ask(settings, Loc.T("Neue Gruppe", "New group"), Loc.T("Wie soll deine Gruppe heißen?", "What should your group be called?"), Loc.T("Meine Apps", "My apps"));
        if (name is null) return;
        var work = Forms.Screen.PrimaryScreen!.WorkingArea;
        var group = new BubbleGroup { Name = StateStore.CleanName(name), X = work.Right - 150 - State.Groups.Count % 5 * 112, Y = work.Y + 80 + State.Groups.Count / 5 * 120 };
        State.Groups.Add(group); Changed(true);
    }
    internal void Rename(BubbleGroup group)
    { CloseFlyout(); var name = Ui.Ask(settings, Loc.T("Gruppe umbenennen", "Rename group"), Loc.T("Gruppenname", "Group name"), group.Name); if (name is not null) { group.Name = StateStore.CleanName(name); Changed(true); } }
    internal void Delete(BubbleGroup group)
    {
        CloseFlyout();
        if (MessageBox.Show(Loc.T($"Gruppe „{group.Name}“ entfernen?\nÜbernommene Desktop-Verknüpfungen werden zurückgelegt, sofern keine andere Gruppe sie verwendet.", $"Remove group “{group.Name}”?\nAdopted desktop shortcuts are put back unless another group uses them."), Loc.T("Gruppe löschen", "Delete group"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { Shortcuts.RemoveGroup(State, group); Changed(true); } catch (Exception ex) { Ui.Error(ex); }
    }
    internal void ToggleName(BubbleGroup group) { group.ShowName = !group.ShowName; Changed(true); }
    internal string Resolve(string target) => Shortcuts.Resolve(target);
    internal string EntryLocation(AppEntry entry)
    {
        if (!ManagedTargets.IsManaged(entry.Target)) return entry.Target;
        string source = Vault.OriginalSource(entry.Target) ?? "";
        string location = Loc.T("In der Bubble gespeichert", "Saved in the bubble") + (string.IsNullOrEmpty(source) ? "" : Loc.T("\nUrsprünglich: ", "\nOriginally: ") + source);
        if (!string.IsNullOrEmpty(source) && File.Exists(source)) location += Loc.T("\nDas Original ist zusätzlich vorhanden.", "\nThe original exists as well.");
        return location;
    }
    internal void Launch(AppEntry entry) => LaunchService.Launch(new AppEntry { Name = entry.Name, Target = Resolve(entry.Target) });
    internal bool CanTakeOver(AppEntry entry)
    {
        string source = Vault.OriginalSource(entry.Target) ?? entry.Target;
        return shortcutPlatform.IsPersonalDesktopShortcut(source) && File.Exists(source);
    }
    internal void TakeOver(BubbleGroup group, AppEntry entry)
    {
        try
        {
            string source = Vault.OriginalSource(entry.Target) ?? entry.Target;
            var warning = Shortcuts.Add(State, group, source, true);
            if (warning is not null) MessageBox.Show(warning, "DesktopBubbles");
            Changed();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void RestoreShortcut(AppEntry entry)
    {
        try { var path = Shortcuts.Restore(entry.Target); if (path is not null) MessageBox.Show(Loc.T("Auf dem Desktop wiederhergestellt:\n", "Restored on the desktop:\n") + path, "DesktopBubbles"); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void RestoreAllShortcuts()
    {
        try { var paths = Vault.RestoreAll(); MessageBox.Show(Loc.T($"{paths.Count} Verknüpfung(en) liegen wieder auf dem Desktop.", $"{paths.Count} shortcut(s) are back on the desktop."), "DesktopBubbles"); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void RemoveEntry(BubbleGroup group, AppEntry entry)
    {
        try { ReturnToDesktop(group, entry); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void DeleteEntry(BubbleGroup group, AppEntry entry)
    {
        try { Shortcuts.RemoveFromGroup(State, group, entry); Changed(); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void SwapEntries(BubbleGroup group, AppEntry first, AppEntry second)
    {
        Shortcuts.Swap(State, group, first, second); Changed();
    }
    internal string ReturnToDesktop(BubbleGroup group, AppEntry entry, string? desktopOverride = null)
    {
        if (!State.Groups.Contains(group) || !group.Items.Contains(entry)) throw new InvalidOperationException(Loc.T("Der Eintrag gehört nicht mehr zu dieser Gruppe.", "The entry no longer belongs to this group."));
        var export = new ShortcutDrag(this, group, entry, desktopOverride);
        try
        {
            string destination = export.WriteToDesktop();
            if (!Shortcuts.CompleteExport(State, group, entry, true)) throw new IOException(Loc.T("Der Eintrag konnte nicht aus der Gruppe entfernt werden.", "The entry could not be removed from the group."));
            Changed();
            return destination;
        }
        finally { export.Cleanup(); }
    }
    internal void SetIcon(BubbleGroup group)
    {
        CloseFlyout();
        var dialog = new OpenFileDialog { Title = Loc.T("Gruppensymbol wählen", "Choose group icon"), Filter = Loc.T("Symbole und Bilder|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.lnk|Alle Dateien|*.*", "Icons and images|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.lnk|All files|*.*") };
        if (dialog.ShowDialog() == true) { group.CustomIcon = dialog.FileName; Changed(true); }
    }
    internal void AddTargets(BubbleGroup group, IEnumerable<string> targets, bool rebuild = true)
    {
        try
        {
            var warnings = new List<string>();
            foreach (var target in targets) { var warning = Shortcuts.Add(State, group, target); if (warning is not null) warnings.Add(warning); }
            if (warnings.Count > 0) MessageBox.Show(string.Join("\n", warnings.Distinct()), Loc.T("DesktopBubbles · Original beibehalten", "DesktopBubbles · Original kept"));
        }
        catch (Exception ex) { Ui.Error(ex); }
        finally { Changed(rebuild); }
    }
    internal void AddFiles(BubbleGroup group, bool rebuild = true)
    {
        if (rebuild) CloseFlyout();
        var dialog = new OpenFileDialog { Title = Loc.T("Dateien oder Verknüpfungen hinzufügen", "Add files or shortcuts"), Multiselect = true, Filter = Loc.T("Alle Dateien|*.*|Apps und Verknüpfungen|*.lnk;*.exe;*.url", "All files|*.*|Apps and shortcuts|*.lnk;*.exe;*.url") };
        if (dialog.ShowDialog() == true) AddTargets(group, dialog.FileNames, rebuild);
    }
    internal void AddFolder(BubbleGroup group, bool rebuild = true)
    {
        if (rebuild) CloseFlyout();
        var dialog = new OpenFolderDialog { Title = Loc.T("Ordner hinzufügen", "Add folder"), Multiselect = true };
        if (dialog.ShowDialog() == true) AddTargets(group, dialog.FolderNames, rebuild);
    }
    internal void AddWeb(BubbleGroup group, Window? owner = null, bool rebuild = true)
    {
        var url = Ui.Ask(owner ?? settings, Loc.T("Webadresse hinzufügen", "Add web address"), Loc.T("https-Adresse", "https address"), "https://");
        if (url is null) return;
        if (!Targets.IsWeb(url)) { Ui.Error(new ArgumentException(Loc.T("Bitte eine vollständige http- oder https-Adresse eingeben.", "Please enter a complete http or https address."))); return; }
        AddTargets(group, [url], rebuild);
    }
    internal void PickApp(BubbleGroup group, bool rebuild = true)
    {
        if (rebuild) CloseFlyout();
        var picker = new AppPickerWindow(this); if (settings is { IsVisible: true }) picker.Owner = settings;
        if (picker.ShowDialog() == true && picker.Selected is { } entry) { Targets.Add(group, entry); Changed(rebuild); }
    }
    internal void Export()
    {
        var dialog = new SaveFileDialog { Title = Loc.T("Gruppen und Verknüpfungen sichern", "Back up groups and shortcuts"), Filter = Loc.T("DesktopBubbles-Sicherung|*.bubbles", "DesktopBubbles backup|*.bubbles"), FileName = Loc.T("DesktopBubbles-Sicherung.bubbles", "DesktopBubbles-Backup.bubbles") };
        if (dialog.ShowDialog() != true) return;
        try { ConfigurationBundle.Export(dialog.FileName, State, Vault); } catch (Exception ex) { Ui.Error(ex); }
    }
    internal void Import()
    {
        var dialog = new OpenFileDialog { Title = Loc.T("Gruppen importieren", "Import groups"), Filter = Loc.T("DesktopBubbles-Sicherung oder ältere Konfiguration|*.bubbles;*.json", "DesktopBubbles backup or older configuration|*.bubbles;*.json") };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bundle = Path.GetExtension(dialog.FileName).Equals(".bubbles", StringComparison.OrdinalIgnoreCase) ? ConfigurationBundle.Read(dialog.FileName) : null;
            var imported = bundle?.State ?? StateStore.Read(dialog.FileName);
            if (bundle is null && imported.Groups.SelectMany(g => g.Items).Any(e => ManagedTargets.IsManaged(e.Target)))
                throw new InvalidDataException(Loc.T("Eine JSON-Datei enthält die gespeicherten Verknüpfungen nicht. Bitte eine .bubbles-Sicherung importieren.", "A JSON file does not contain the saved shortcuts. Please import a .bubbles backup."));
            if (MessageBox.Show(Loc.T($"{imported.Groups.Count} Gruppen importieren und die aktuellen Gruppen ersetzen?\nNur Konfigurationen aus vertrauenswürdiger Quelle importieren: Einträge können Programme starten.\nDie bisherige Konfiguration wird gesichert.", $"Import {imported.Groups.Count} groups and replace the current groups?\nOnly import configurations from a trusted source: entries can start programs.\nThe current configuration is backed up."), Loc.T("Konfiguration importieren", "Import configuration"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Store.Save(State);
            File.Copy(Store.Path, Path.Combine(DataDirectory, "before-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff") + ".json"));
            if (bundle is not null) foreach (var payload in bundle.Payloads) Vault.ImportPayload(payload.Key, payload.Value);
            var incoming = imported.Groups.SelectMany(g => g.Items).Select(e => e.Target).ToHashSet();
            foreach (var target in State.Groups.SelectMany(g => g.Items).Select(e => e.Target).Distinct().Where(t => !incoming.Contains(t))) Shortcuts.Restore(target);
            Store.Save(imported);
            State = imported; Ui.ApplyTheme(State.Settings); Changed(true); Flush(); settings?.RefreshPage();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void Quit()
    {
        Flush();
        if (dirty && MessageBox.Show(Loc.T("Änderungen konnten nicht gespeichert werden. Trotzdem beenden?", "Changes could not be saved. Quit anyway?"), "DesktopBubbles", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        exiting = true; Shutdown();
    }
    // Lays every taken-over desktop shortcut back onto the desktop and, while Explorer runs,
    // moves each icon to its remembered spot. A spot taken by another icon, or none known,
    // leaves the placement to Explorer, which fills the desktop from the top left.
    private void ParkShortcuts()
    {
        parkOnExit = false;
        try
        {
            var parked = Shortcuts.Park(State, Log.Write);
            Log.Write($"Desktop shortcuts parked: {parked.Count}");
            if (sessionEnding) return; // Explorer is ending too; the remembered spots stay for next time.
            var wanted = parked.Where(p => p.X is not null && p.Y is not null).ToList();
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (wanted.Count > 0 && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(150); // Explorer picks up new desktop files asynchronously.
                var (placed, settled) = DesktopIcons.Place(wanted.Select(p => (p.Path, p.X!.Value, p.Y!.Value)).ToList());
                foreach (var item in wanted.Where(p => placed.Contains(p.Path))) Shortcuts.ConfirmPlacement(item.Target, item.X!.Value, item.Y!.Value);
                wanted.RemoveAll(p => settled.Contains(p.Path));
            }
        }
        catch (Exception ex) { Log.Write("Desktop shortcuts could not be parked completely: " + ex); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        exiting = true; Flush(); saveTimer?.Stop(); recoveryTimer?.Stop();
        Duplicates?.Dispose(); Duplicates = null; // Parked links must not be collected again.
        if (parkOnExit) ParkShortcuts();
        SystemEvents.UserPreferenceChanged -= PreferencesChanged; SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        CloseFlyout(); foreach (var window in bubbles.ToArray()) { try { window.Finish(); } catch (InvalidOperationException) { } }
        tray?.Dispose(); trayIcon?.Dispose(); Icons.Dispose(); listener?.Dispose();
        if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose();
        base.OnExit(e);
    }
}

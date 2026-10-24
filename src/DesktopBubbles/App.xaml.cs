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
    private int recoveryAttempts;
    private DateTime lastClose;
    private Guid lastGroup;
    internal bool HasFlyout => flyout is not null;

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
            DataDirectory = dataArg >= 0 && dataArg + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataArg + 1])
                : Path.Combine(Portable ? AppContext.BaseDirectory : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Portable ? "data" : "DesktopBubbles");
            if (smoke && dataArg < 0) throw new InvalidOperationException("--smoke-test benötigt ein separates --data-dir.");
            if (visual && dataArg < 0) throw new InvalidOperationException("--design-preview benötigt ein separates --data-dir.");
            Directory.CreateDirectory(DataDirectory); Log.DirectoryPath = DataDirectory;
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..16];
            mutex = new Mutex(true, @"Local\DesktopBubbles-" + hash, out ownsMutex);
            uint showMessage = Native.RegisterWindowMessage("DesktopBubbles.ShowSettings");
            if (!ownsMutex)
            {
                var existing = Native.FindWindow(null, "DesktopBubbles.Message." + hash);
                if (existing != 0) Native.SendNotifyMessage(existing, showMessage, 0, 0);
                Shutdown(); return;
            }
            Store = new StateStore(Path.Combine(DataDirectory, "groups.json"));
            State = Store.Load(); Ui.ApplyTheme(State.Settings);
            Vault = new ShortcutVault(DataDirectory, shortcutPlatform);
            Shortcuts = new ShortcutManager(Vault, state => { Store.Save(state); dirty = false; });
            var recoveryNotices = Shortcuts.Recover(State);
            foreach (var notice in recoveryNotices) Log.Write(notice);
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
            SessionEnding += (_, _) => Flush();
            DispatcherUnhandledException += (_, args) => { Log.Write(args.Exception.ToString()); args.Handled = true; Ui.Error(args.Exception); };
            SetupTray();
            if (smoke)
            {
                State = SmokeTests.CreateState(); Ui.ApplyTheme(State.Settings); Rebuild();
                _ = RunSmoke(e.Args.Contains("--appearance-only"));
            }
            else
            {
                Rebuild();
                _ = ResolveNames();
                if (!e.Args.Contains("--background") || State.Groups.Count == 0) ShowSettings();
                if (visual) _ = RunVisualPreview();
                if (Store.RecoveryNotice is not null) MessageBox.Show(Store.RecoveryNotice, "DesktopBubbles · Wiederherstellung");
                if (recoveryNotices.Count > 0) MessageBox.Show(string.Join("\n", recoveryNotices.Distinct()), "DesktopBubbles · Verknüpfungen wiederhergestellt");
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
            ?? assembly.GetName().Version?.ToString(3) ?? "unbekannt";
        tray = new Forms.NotifyIcon { Text = "DesktopBubbles · " + version, Icon = trayIcon, Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Version " + version) { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Zum Repository", null, (_, _) => Dispatcher.Invoke(OpenRepository));
        menu.Items.Add("Desktop-Verbindung erneuern", null, (_, _) => Dispatcher.Invoke(Rebuild));
        menu.Items.Add("Beenden", null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
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
            if (!saveWarning && !exiting) { saveWarning = true; Ui.Error(new IOException("Die Änderungen konnten nicht gespeichert werden. Bitte den Datenordner prüfen.\n" + ex.Message)); }
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
        flyout?.RefreshAppearance(); settings?.RefreshMaterial();
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
        if (flyout?.Group.Id == window.Group.Id) { CloseFlyout(); return; }
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
        try { window.SetExpanded(true); current.Show(); current.Activate(); }
        catch { window.SetExpanded(false); flyout = null; current.Close(); throw; }
    }
    internal void CloseFlyout() { var current = flyout; flyout = null; current?.Close(); }
    internal FlyoutWindow? CurrentFlyout => flyout;
    private ShortcutDrag? activeDrag;
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
            try { Shortcuts.Transfer(State, drag.Group, group, drag.Entry, index); drag.Internal = true; Changed(); }
            catch (Exception ex) { Ui.Error(ex); }
        }
        else if (data.GetData(DataFormats.FileDrop) is string[] files) AddTargets(group, files, rebuild);
    }
    internal async Task DragEntry(FrameworkElement source, BubbleGroup group, AppEntry entry)
    {
        try
        {
            var drag = new ShortcutDrag(this, group, entry); activeDrag = drag; DragDropEffects result;
            try { result = DragDrop.DoDragDrop(source, drag.Data, DragDropEffects.Copy | DragDropEffects.Move); }
            finally { activeDrag = null; }
            if (drag.Internal || result == DragDropEffects.None) { drag.Cleanup(); return; }
            bool confirmed = await drag.Confirmed(result);
            if (Shortcuts.CompleteExport(State, group, entry, confirmed)) Changed();
            else if (result != DragDropEffects.None) Log.Write("Drop not confirmed: kept bubble entry and staging copy.");
            if (confirmed) drag.Cleanup();
        }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void CreateGroup()
    {
        if (State.Groups.Count >= 100) { Ui.Error(new InvalidOperationException("Maximal 100 Gruppen sind möglich.")); return; }
        string? name = Ui.Ask(settings, "Neue Gruppe", "Wie soll deine Gruppe heißen?", "Meine Apps");
        if (name is null) return;
        var work = Forms.Screen.PrimaryScreen!.WorkingArea;
        var group = new BubbleGroup { Name = StateStore.CleanName(name), X = work.Right - 150 - State.Groups.Count % 5 * 112, Y = work.Y + 80 + State.Groups.Count / 5 * 120 };
        State.Groups.Add(group); Changed(true);
    }
    internal void Rename(BubbleGroup group)
    { CloseFlyout(); var name = Ui.Ask(settings, "Gruppe umbenennen", "Gruppenname", group.Name); if (name is not null) { group.Name = StateStore.CleanName(name); Changed(true); } }
    internal void Delete(BubbleGroup group)
    {
        CloseFlyout();
        if (MessageBox.Show($"Gruppe „{group.Name}“ entfernen?\nÜbernommene Desktop-Verknüpfungen werden zurückgelegt, sofern keine andere Gruppe sie verwendet.", "Gruppe löschen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { Shortcuts.RemoveGroup(State, group); Changed(true); } catch (Exception ex) { Ui.Error(ex); }
    }
    internal void ToggleName(BubbleGroup group) { group.ShowName = !group.ShowName; Changed(true); }
    internal string Resolve(string target) => Shortcuts.Resolve(target);
    internal string EntryLocation(AppEntry entry)
    {
        if (!ManagedTargets.IsManaged(entry.Target)) return entry.Target;
        string source = Vault.OriginalSource(entry.Target) ?? "";
        string location = "In der Bubble gespeichert" + (string.IsNullOrEmpty(source) ? "" : "\nUrsprünglich: " + source);
        if (!string.IsNullOrEmpty(source) && File.Exists(source)) location += "\nDas Original ist zusätzlich vorhanden.";
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
        try { var path = Shortcuts.Restore(entry.Target); if (path is not null) MessageBox.Show("Auf dem Desktop wiederhergestellt:\n" + path, "DesktopBubbles"); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void RestoreAllShortcuts()
    {
        try { var paths = Vault.RestoreAll(); MessageBox.Show($"{paths.Count} Verknüpfung(en) liegen wieder auf dem Desktop.", "DesktopBubbles"); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal void RemoveEntry(BubbleGroup group, AppEntry entry)
    {
        try { ReturnToDesktop(group, entry); }
        catch (Exception ex) { Ui.Error(ex); }
    }
    internal string ReturnToDesktop(BubbleGroup group, AppEntry entry, string? desktopOverride = null)
    {
        if (!State.Groups.Contains(group) || !group.Items.Contains(entry)) throw new InvalidOperationException("Der Eintrag gehört nicht mehr zu dieser Gruppe.");
        var export = new ShortcutDrag(this, group, entry, desktopOverride);
        try
        {
            string destination = export.WriteToDesktop();
            if (!Shortcuts.CompleteExport(State, group, entry, true)) throw new IOException("Der Eintrag konnte nicht aus der Gruppe entfernt werden.");
            Changed();
            return destination;
        }
        finally { export.Cleanup(); }
    }
    internal void SetIcon(BubbleGroup group)
    {
        CloseFlyout();
        var dialog = new OpenFileDialog { Title = "Gruppensymbol wählen", Filter = "Symbole und Bilder|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.lnk|Alle Dateien|*.*" };
        if (dialog.ShowDialog() == true) { group.CustomIcon = dialog.FileName; Changed(true); }
    }
    internal void AddTargets(BubbleGroup group, IEnumerable<string> targets, bool rebuild = true)
    {
        try
        {
            var warnings = new List<string>();
            foreach (var target in targets) { var warning = Shortcuts.Add(State, group, target); if (warning is not null) warnings.Add(warning); }
            if (warnings.Count > 0) MessageBox.Show(string.Join("\n", warnings.Distinct()), "DesktopBubbles · Original beibehalten");
        }
        catch (Exception ex) { Ui.Error(ex); }
        finally { Changed(rebuild); }
    }
    internal void AddFiles(BubbleGroup group, bool rebuild = true)
    {
        if (rebuild) CloseFlyout();
        var dialog = new OpenFileDialog { Title = "Dateien oder Verknüpfungen hinzufügen", Multiselect = true, Filter = "Alle Dateien|*.*|Apps und Verknüpfungen|*.lnk;*.exe;*.url" };
        if (dialog.ShowDialog() == true) AddTargets(group, dialog.FileNames, rebuild);
    }
    internal void AddFolder(BubbleGroup group, bool rebuild = true)
    {
        if (rebuild) CloseFlyout();
        var dialog = new OpenFolderDialog { Title = "Ordner hinzufügen", Multiselect = true };
        if (dialog.ShowDialog() == true) AddTargets(group, dialog.FolderNames, rebuild);
    }
    internal void AddWeb(BubbleGroup group, Window? owner = null, bool rebuild = true)
    {
        var url = Ui.Ask(owner ?? settings, "Webadresse hinzufügen", "https-Adresse", "https://");
        if (url is null) return;
        if (!Targets.IsWeb(url)) { Ui.Error(new ArgumentException("Bitte eine vollständige http- oder https-Adresse eingeben.")); return; }
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
        var dialog = new SaveFileDialog { Title = "Gruppen und Verknüpfungen sichern", Filter = "DesktopBubbles-Sicherung|*.bubbles", FileName = "DesktopBubbles-Sicherung.bubbles" };
        if (dialog.ShowDialog() != true) return;
        try { ConfigurationBundle.Export(dialog.FileName, State, Vault); } catch (Exception ex) { Ui.Error(ex); }
    }
    internal void Import()
    {
        var dialog = new OpenFileDialog { Title = "Gruppen importieren", Filter = "DesktopBubbles-Sicherung oder ältere Konfiguration|*.bubbles;*.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bundle = Path.GetExtension(dialog.FileName).Equals(".bubbles", StringComparison.OrdinalIgnoreCase) ? ConfigurationBundle.Read(dialog.FileName) : null;
            var imported = bundle?.State ?? StateStore.Read(dialog.FileName);
            if (bundle is null && imported.Groups.SelectMany(g => g.Items).Any(e => ManagedTargets.IsManaged(e.Target)))
                throw new InvalidDataException("Eine JSON-Datei enthält die gespeicherten Verknüpfungen nicht. Bitte eine .bubbles-Sicherung importieren.");
            if (MessageBox.Show($"{imported.Groups.Count} Gruppen importieren und die aktuellen Gruppen ersetzen?\nNur Konfigurationen aus vertrauenswürdiger Quelle importieren: Einträge können Programme starten.\nDie bisherige Konfiguration wird gesichert.", "Konfiguration importieren", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
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
        if (dirty && MessageBox.Show("Änderungen konnten nicht gespeichert werden. Trotzdem beenden?", "DesktopBubbles", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        exiting = true; Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        exiting = true; Flush(); saveTimer?.Stop(); recoveryTimer?.Stop();
        SystemEvents.UserPreferenceChanged -= PreferencesChanged; SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        CloseFlyout(); foreach (var window in bubbles.ToArray()) { try { window.Finish(); } catch (InvalidOperationException) { } }
        tray?.Dispose(); trayIcon?.Dispose(); Icons.Dispose(); listener?.Dispose();
        if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose();
        base.OnExit(e);
    }
}

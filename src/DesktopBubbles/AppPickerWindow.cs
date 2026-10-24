using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal sealed class AppPickerWindow : Window
{
    public AppEntry? Selected { get; private set; }
    public AppPickerWindow(App app)
    {
        Title = "Installierte App hinzufügen"; Width = 500; Height = 600; MinWidth = 360; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Ui.Resource("Surface");
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var title = Ui.Text("Apps auf diesem PC", 24, weight: FontWeights.SemiBold); title.Margin = new Thickness(0, 0, 0, 16); DockPanel.SetDock(title, Dock.Top); root.Children.Add(title);
        var search = new TextBox { Margin = new Thickness(0, 0, 0, 12), ToolTip = "App-Namen suchen" }; DockPanel.SetDock(search, Dock.Top); root.Children.Add(search);
        var status = Ui.Text("Installierte Apps werden gelesen …", 12, "Muted"); status.Margin = new Thickness(0, 8, 0, 8); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var list = new ListBox { DisplayMemberPath = "Name", Background = Ui.Resource("ControlFill"), Foreground = Ui.Resource("Ink"), BorderThickness = new Thickness(0) };
        root.Children.Add(list);
        void Choose() { if (list.SelectedItem is AppEntry item) { Selected = item; DialogResult = true; } }
        buttons.Children.Add(Ui.Button("Abbrechen", () => DialogResult = false)); buttons.Children.Add(Ui.Button("Hinzufügen", Choose));
        list.MouseDoubleClick += (_, _) => Choose(); list.KeyDown += (_, e) => { if (e.Key == Key.Enter) Choose(); };
        List<AppEntry> apps = [];
        search.TextChanged += (_, _) => list.ItemsSource = apps.Where(a => a.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToList();
        Loaded += async (_, _) =>
        {
            try { apps = await app.Icons.StoreApps(); list.ItemsSource = apps; status.Text = $"{apps.Count} installierte Apps · einschließlich Store-Apps"; search.Focus(); }
            catch (Exception ex) { status.Text = "Apps konnten nicht gelesen werden. .lnk-Dateien können weiterhin hinzugefügt werden."; Log.Write(ex.ToString()); }
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}

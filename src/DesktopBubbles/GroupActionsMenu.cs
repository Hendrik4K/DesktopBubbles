using System.Windows.Controls;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal static class GroupActionsMenu
{
    internal static ContextMenu Create(App app, BubbleGroup group, bool settings = false)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Ui.Menu(Loc.T("Umbenennen …", "Rename …"), () => app.Rename(group)));
        var name = Ui.Menu(Loc.T("Name unter Bubble anzeigen", "Show name under bubble"), () => app.ToggleName(group));
        name.IsCheckable = true; name.IsChecked = group.ShowName; menu.Items.Add(name);
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu(Loc.T("Auf dem Desktop finden", "Find on desktop"), () => app.ShowDesktop(group)));
        var add = new MenuItem { Header = Loc.T("Hinzufügen", "Add") };
        add.Items.Add(Ui.Menu(Loc.T("Dateien / Verknüpfungen …", "Files / shortcuts …"), () => app.AddFiles(group)));
        add.Items.Add(Ui.Menu(Loc.T("Installierte App …", "Installed app …"), () => app.PickApp(group)));
        add.Items.Add(Ui.Menu(Loc.T("Ordner …", "Folder …"), () => app.AddFolder(group)));
        add.Items.Add(Ui.Menu(Loc.T("Webadresse …", "Web address …"), () => app.AddWeb(group)));
        menu.Items.Add(add);
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu(Loc.T("Bubble entfernen …", "Remove bubble …"), () => app.Delete(group)));
        if (settings) { menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu(Loc.T("Einstellungen", "Settings"), app.ShowSettings)); }
        return menu;
    }
}

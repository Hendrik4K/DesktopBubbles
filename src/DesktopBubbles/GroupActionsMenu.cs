using System.Windows.Controls;
using DesktopBubbles.Core;

namespace DesktopBubbles;

internal static class GroupActionsMenu
{
    internal static ContextMenu Create(App app, BubbleGroup group, bool settings = false)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Ui.Menu("Umbenennen …", () => app.Rename(group)));
        var name = Ui.Menu("Name unter Bubble anzeigen", () => app.ToggleName(group));
        name.IsCheckable = true; name.IsChecked = group.ShowName; menu.Items.Add(name);
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("Auf dem Desktop finden", () => app.ShowDesktop(group)));
        var add = new MenuItem { Header = "Hinzufügen" };
        add.Items.Add(Ui.Menu("Dateien / Verknüpfungen …", () => app.AddFiles(group)));
        add.Items.Add(Ui.Menu("Installierte App …", () => app.PickApp(group)));
        add.Items.Add(Ui.Menu("Ordner …", () => app.AddFolder(group)));
        add.Items.Add(Ui.Menu("Webadresse …", () => app.AddWeb(group)));
        menu.Items.Add(add);
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("Bubble entfernen …", () => app.Delete(group)));
        if (settings) { menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("Einstellungen", app.ShowSettings)); }
        return menu;
    }
}

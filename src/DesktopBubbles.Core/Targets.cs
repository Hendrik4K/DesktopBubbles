namespace DesktopBubbles.Core;

public static class Targets
{
    public const string AppsPrefix = "shell:AppsFolder\\";
    public static bool IsStore(string target) => target.StartsWith(AppsPrefix, StringComparison.OrdinalIgnoreCase)
        && target.Length > AppsPrefix.Length && !target.Contains('"') && !target.Contains('\n');

    public static bool IsWeb(string target) => Uri.TryCreate(target, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || uri.Scheme == "http");

    public static bool IsAllowed(string target) => !string.IsNullOrWhiteSpace(target) && target.Length <= 32767
        && !target.Any(char.IsControl) && (ManagedTargets.IsManaged(target) || IsStore(target) || IsWeb(target) || Path.IsPathFullyQualified(target));

    public static bool Exists(string target) => IsStore(target) || IsWeb(target) || File.Exists(target) || Directory.Exists(target);

    public static bool NeedsFriendlyName(AppEntry entry) => string.IsNullOrWhiteSpace(entry.Name)
        || (entry.Name == "Unbenannt" && (ManagedTargets.IsManaged(entry.Target) || string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(entry.Target))));

    public static AppEntry Create(string target, string? name = null)
    {
        target = target.Trim().Trim('"');
        if (!IsAllowed(target)) throw new ArgumentException("Bitte eine Datei, einen Ordner, eine https-Adresse oder eine Store-App wählen.");
        return new AppEntry { Target = target, Name = string.IsNullOrWhiteSpace(name)
            ? (IsWeb(target) ? new Uri(target).Host : Path.GetFileNameWithoutExtension(target.TrimEnd(Path.DirectorySeparatorChar)))
            : name.Trim() };
    }

    public static bool Add(BubbleGroup group, AppEntry entry)
    {
        if (group.Items.Count >= 300) throw new InvalidOperationException("Eine Gruppe kann höchstens 300 Einträge enthalten.");
        if (group.Items.Any(e => string.Equals(e.Target, entry.Target, StringComparison.OrdinalIgnoreCase))) return false;
        group.Items.Add(entry);
        return true;
    }

    public static void Move(BubbleGroup group, Guid id, int destination)
    {
        int source = group.Items.FindIndex(x => x.Id == id);
        if (source < 0 || group.Items.Count < 2) return;
        var entry = group.Items[source];
        group.Items.RemoveAt(source);
        group.Items.Insert(Math.Clamp(destination, 0, group.Items.Count), entry);
    }
}

using DesktopBubbles.Core;

namespace DesktopBubbles.Application;

public interface IShortcutPlatform
{
    bool IsPersonalDesktopShortcut(string path);
    bool ValidateRelocatedShortcut(string source, string copy);
    void DeleteOriginalIfUnchanged(string path, string sha256);
    string PersonalDesktop { get; }
}

public sealed record PreparedShortcut(string Target, bool RemoveOriginal);
public interface IShortcutVault
{
    string Resolve(string target);
    PreparedShortcut Prepare(string source, bool move);
    void Complete(PreparedShortcut prepared);
    string? Restore(string target);
    bool OwnsDesktopOriginal(string target);
    void ConfirmExport(string target);
    string? FindBySource(string source);
    IReadOnlyList<string> Recover(ISet<string> referenced);
}

/// <summary>Use cases commit state before removing originals. The vault keeps recovery copies.</summary>
public sealed class ShortcutManager(IShortcutVault vault, Action<BubbleState> persist)
{
    public string Resolve(string target) => vault.Resolve(target);
    public bool CanRestore(string target) => vault.OwnsDesktopOriginal(target);

    public string? Add(BubbleState state, BubbleGroup group, string source, bool forceTakeover = false)
    {
        if (!state.Groups.Contains(group)) throw new InvalidOperationException("Unbekannte Gruppe.");
        var entry = Targets.Create(source);
        source = entry.Target;
        var existing = vault.FindBySource(source);
        var old = group.Items.FirstOrDefault(e => string.Equals(e.Target, source, StringComparison.OrdinalIgnoreCase)
            || (existing is not null && e.Target == existing));
        if (old is not null && !forceTakeover) return null;
        if (old is null && group.Items.Count >= 300) throw new InvalidOperationException("Maximal 300 Einträge pro Gruppe.");
        // Prepare a verified durable copy. Unsupported targets stay external.
        var prepared = vault.Prepare(source, state.Settings.MoveDesktopShortcuts || forceTakeover);
        entry.Target = prepared.Target;
        // Existing entries in other groups must not retain a path we are about to remove.
        var aliases = state.Groups.SelectMany(g => g.Items).Where(e => string.Equals(e.Target, source, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var alias in aliases) alias.Target = entry.Target;
        if (old is null) group.Items.Add(entry);
        try { persist(state); }
        catch
        {
            if (old is null) group.Items.Remove(entry);
            foreach (var alias in aliases) alias.Target = source;
            throw;
        }
        // Any failure here leaves both the usable vault copy and the original.
        try { vault.Complete(prepared); return null; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { return "In der Bubble gespeichert; das Desktop-Original bleibt erhalten: " + ex.Message; }
    }

    public void Remove(BubbleState state, BubbleGroup group, AppEntry entry)
    {
        if (!group.Items.Contains(entry)) return;
        if (!state.Groups.Where(g => g != group).SelectMany(g => g.Items).Any(e => e.Target == entry.Target)
            && !group.Items.Any(e => e != entry && e.Target == entry.Target)) vault.Restore(entry.Target);
        int index = group.Items.IndexOf(entry); group.Items.Remove(entry);
        try { persist(state); } catch { group.Items.Insert(index, entry); throw; }
    }

    /// <summary>Remove only the group reference; never resolve, restore or delete the target or recovery files.</summary>
    public void RemoveFromGroup(BubbleState state, BubbleGroup group, AppEntry entry)
    {
        if (!state.Groups.Contains(group)) throw new InvalidOperationException("Unbekannte Gruppe.");
        int index = group.Items.IndexOf(entry); if (index < 0) return;
        group.Items.RemoveAt(index);
        try { persist(state); } catch { group.Items.Insert(index, entry); throw; }
    }

    public void RemoveGroup(BubbleState state, BubbleGroup group)
    {
        int index = state.Groups.IndexOf(group); if (index < 0) return;
        foreach (var target in group.Items.Select(e => e.Target).Distinct())
            if (!state.Groups.Where(g => g != group).SelectMany(g => g.Items).Any(e => e.Target == target)) vault.Restore(target);
        state.Groups.RemoveAt(index);
        try { persist(state); } catch { state.Groups.Insert(index, group); throw; }
    }

    public string? Restore(string target) => vault.Restore(target);
    public void Transfer(BubbleState state, BubbleGroup source, BubbleGroup destination, AppEntry entry, int? index = null)
    {
        if (!state.Groups.Contains(source) || !state.Groups.Contains(destination) || !source.Items.Contains(entry)) throw new InvalidOperationException("Der gezogene Eintrag ist nicht mehr vorhanden.");
        var beforeSource = source.Items.ToList(); var beforeDestination = destination.Items.ToList();
        if (source == destination) Targets.Move(source, entry.Id, index ?? source.Items.Count - 1);
        else
        {
            if (!destination.Items.Any(e => string.Equals(e.Target, entry.Target, StringComparison.OrdinalIgnoreCase)))
            {
                if (destination.Items.Count >= 300) throw new InvalidOperationException("Die Ziel-Bubble ist voll.");
                destination.Items.Insert(Math.Clamp(index ?? destination.Items.Count, 0, destination.Items.Count), entry);
            }
            source.Items.Remove(entry);
        }
        try { persist(state); }
        catch { source.Items = beforeSource; if (source != destination) destination.Items = beforeDestination; throw; }
    }
    public bool CompleteExport(BubbleState state, BubbleGroup group, AppEntry entry, bool destinationConfirmed)
    {
        if (!destinationConfirmed || !state.Groups.Contains(group) || !group.Items.Contains(entry)) return false;
        int index = group.Items.IndexOf(entry); group.Items.RemoveAt(index);
        try { persist(state); } catch { group.Items.Insert(index, entry); throw; }
        if (!state.Groups.SelectMany(g => g.Items).Any(e => e.Target == entry.Target)) vault.ConfirmExport(entry.Target);
        return true;
    }
    public IReadOnlyList<string> Recover(BubbleState state) => vault.Recover(state.Groups.SelectMany(g => g.Items).Select(e => e.Target).ToHashSet(StringComparer.Ordinal));
}

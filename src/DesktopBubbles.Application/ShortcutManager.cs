using DesktopBubbles.Core;

namespace DesktopBubbles.Application;

public interface IShortcutPlatform
{
    bool IsPersonalDesktopShortcut(string path);
    bool ValidateRelocatedShortcut(string source, string copy);
    void DeleteOriginalIfUnchanged(string path, string sha256);
    string PersonalDesktop { get; }
    // Optional: the icon's spot in the desktop view, in view coordinates. Null when unknown.
    (int X, int Y)? DesktopIconPosition(string path) => null;
}

public sealed record PreparedShortcut(string Target, bool RemoveOriginal);
// A desktop shortcut laid back onto the desktop while the app is not running.
public sealed record ParkedShortcut(string Target, string Path, int? X, int? Y);
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
    ParkedShortcut? Park(string target);
    void ConfirmPlacement(string target, int x, int y);
    string? Unpark(string target);
    // The replacement takes over the old copy's desktop spot; the old one never returns to the desktop.
    void Supersede(string old, string replacement);
    // A desktop file the app itself placed there on request (e.g. "Zurück zum Desktop").
    bool PlacedByRequest(string path);
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
    public void Swap(BubbleState state, BubbleGroup group, AppEntry first, AppEntry second)
    {
        int a = group.Items.IndexOf(first), b = group.Items.IndexOf(second);
        if (!state.Groups.Contains(group) || a < 0 || b < 0) throw new InvalidOperationException("Die Einträge gehören nicht mehr zu dieser Gruppe.");
        if (a == b) return;
        (group.Items[a], group.Items[b]) = (group.Items[b], group.Items[a]);
        try { persist(state); }
        catch { (group.Items[a], group.Items[b]) = (group.Items[b], group.Items[a]); throw; }
    }
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

    /// <summary>On exit: lay every taken-over desktop shortcut back onto the desktop. Groups keep their entries.</summary>
    public IReadOnlyList<ParkedShortcut> Park(BubbleState state, Action<string>? notice = null)
    {
        var parked = new List<ParkedShortcut>();
        foreach (var target in Managed(state))
        {
            try { if (vault.Park(target) is { } item) parked.Add(item); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { notice?.Invoke("Verknüpfung bleibt in der Bubble: " + target + " · " + ex.Message); }
        }
        return parked;
    }
    public void ConfirmPlacement(string target, int x, int y) => vault.ConfirmPlacement(target, x, y);

    /// <summary>On start: take the parked desktop shortcuts back. Changed or missing ones are reported, never deleted.</summary>
    public IReadOnlyList<string> Unpark(BubbleState state)
    {
        var notices = new List<string>();
        foreach (var target in Managed(state))
        {
            try { if (vault.Unpark(target) is { } notice) notices.Add(notice); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { notices.Add("Desktop-Verknüpfung bleibt zusätzlich auf dem Desktop: " + target + " · " + ex.Message); }
        }
        return notices;
    }
    /// <summary>
    /// A program update put a shortcut back on the personal desktop although the same program
    /// already lives in a bubble. The fresh link replaces the bubble's copy (keeping names and
    /// places) and leaves the desktop through the verified takeover. Returns false when the
    /// file is not one the app may take over.
    /// </summary>
    public bool Absorb(BubbleState state, string source, IReadOnlyList<AppEntry> entries)
    {
        if (entries.Count == 0 || vault.PlacedByRequest(source)) return false;
        var prepared = vault.Prepare(source, true);
        if (!prepared.RemoveOriginal) return false;
        var before = entries.Select(e => (Entry: e, e.Target)).ToList();
        foreach (var entry in entries) entry.Target = prepared.Target;
        try { persist(state); }
        catch { foreach (var (entry, target) in before) entry.Target = target; throw; }
        var referenced = state.Groups.SelectMany(g => g.Items).Select(e => e.Target).ToHashSet(StringComparer.Ordinal);
        foreach (var old in before.Select(b => b.Target).Distinct())
            if (old != prepared.Target && !referenced.Contains(old) && ManagedTargets.IsManaged(old)) vault.Supersede(old, prepared.Target);
        vault.Complete(prepared);
        return true;
    }
    private static IEnumerable<string> Managed(BubbleState state)
        => state.Groups.SelectMany(g => g.Items).Select(e => e.Target).Where(ManagedTargets.IsManaged).Distinct(StringComparer.Ordinal);
}

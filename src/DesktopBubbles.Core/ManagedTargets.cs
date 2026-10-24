namespace DesktopBubbles.Core;

/// <summary>Only opaque IDs are accepted, never user-controlled relative paths.</summary>
public static class ManagedTargets
{
    public const string Prefix = "bubble-link:";
    public static bool TryFileName(string target, out string name)
    {
        name = target.StartsWith(Prefix, StringComparison.Ordinal) ? target[Prefix.Length..] : "";
        return name.Length == 36 && Guid.TryParseExact(name[..32], "N", out _)
            && (name.EndsWith(".lnk", StringComparison.Ordinal) || name.EndsWith(".url", StringComparison.Ordinal));
    }
    public static bool IsManaged(string target) => TryFileName(target, out _);
}

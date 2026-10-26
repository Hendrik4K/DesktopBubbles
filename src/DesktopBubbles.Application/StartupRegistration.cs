namespace DesktopBubbles.Application;

/// <summary>Identifies one Windows startup registration by executable and data location.</summary>
public static class StartupRegistration
{
    public static string Command(string executable, bool portable, string dataDirectory) =>
        $"{QuotePath(executable)} --background --data-dir {QuotePath(dataDirectory)}" + (portable ? " --portable" : "");

    // An older installation's entry must not check the current installation's checkbox.
    // Compare our own generated commands conservatively; do not execute or import them.
    public static bool Matches(string? command, string executable, bool portable, string dataDirectory)
    {
        if (command is null) return false;
        string candidate = command.Trim(), program = QuotePath(executable), data = QuotePath(dataDirectory);
        const string options = " --background --data-dir ";
        string suffix = portable ? " --portable" : "";
        if (candidate.Length != program.Length + options.Length + data.Length + suffix.Length) return false;
        // Paths are case insensitive on Windows; the app's command-line switches are not.
        return candidate.AsSpan(0, program.Length).Equals(program, StringComparison.OrdinalIgnoreCase)
            && candidate.AsSpan(program.Length, options.Length).SequenceEqual(options)
            && candidate.AsSpan(program.Length + options.Length, data.Length).Equals(data, StringComparison.OrdinalIgnoreCase)
            && candidate.AsSpan(program.Length + options.Length + data.Length).SequenceEqual(suffix);
    }

    private static string QuotePath(string value)
    {
        if (!Path.IsPathFullyQualified(value) || value.Any(c => c == '"' || char.IsControl(c)))
            throw new ArgumentException("Der Autostart benötigt einen vollständigen gültigen Pfad.");
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        // Windows command-line parsing requires doubling backslashes before a closing quote.
        int trailing = path.Reverse().TakeWhile(c => c == '\\').Count();
        return "\"" + path + new string('\\', trailing) + "\"";
    }
}

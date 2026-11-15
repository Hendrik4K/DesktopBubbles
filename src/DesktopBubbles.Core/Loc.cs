using System.Globalization;

namespace DesktopBubbles.Core;

/// <summary>
/// The app speaks German or English. Every visible text carries both versions right where it is
/// used (<c>Loc.T("Deutsch", "English")</c>), so the two can never drift apart. Without a stored
/// choice the Windows display language decides: German stays German, everything else is English.
/// </summary>
public static class Loc
{
    public static bool English { get; private set; }
    public static string T(string german, string english) => English ? english : german;
    public static void Use(string? language) => English = language switch
    {
        "en" => true,
        "de" => false,
        _ => !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase),
    };
}

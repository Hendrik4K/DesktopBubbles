using DesktopBubbles.Application;

internal static class StartupRegistrationTests
{
    public static void Run(string root, Action<string, Action> test, Action<bool> assert, Action<Action> reject)
    {
        string executable = Path.Combine(root, "Installed App", "DesktopBubbles.exe"), data = Path.Combine(root, "User Data");
        test("Autostart bindet Programmdatei und Datenordner ausdrücklich", () =>
        {
            string command = StartupRegistration.Command(executable, true, data);
            assert(command == $"\"{executable}\" --background --data-dir \"{data}\" --portable" && StartupRegistration.Matches(command, executable, true, data));
        });
        test("Autostart einer älteren Ausgabe gilt nicht für die aktuelle", () =>
            assert(!StartupRegistration.Matches(StartupRegistration.Command(executable, true, data), Path.Combine(root, "Preview", "DesktopBubbles.exe"), true, data)));
        test("Autostart mit anderem Speicherstand wird nicht als aktiv angezeigt", () =>
            assert(!StartupRegistration.Matches(StartupRegistration.Command(executable, true, data), executable, true, Path.Combine(root, "Preview Data"))));
        test("Fehlender Autostart und abweichender portabler Modus sind inaktiv", () =>
            assert(!StartupRegistration.Matches(null, executable, true, data) && !StartupRegistration.Matches(StartupRegistration.Command(executable, false, data), executable, true, data)));
        test("Autostart erkennt Windows-Pfadschreibweise und abschließenden Trenner", () =>
            assert(StartupRegistration.Matches("  " + StartupRegistration.Command(executable.ToUpperInvariant(), true, data.ToUpperInvariant()) + "  ", executable, true, data + Path.DirectorySeparatorChar)));
        test("Autostart mit von der App nicht erkannten Optionen gilt nicht als aktiv", () =>
            assert(!StartupRegistration.Matches(StartupRegistration.Command(executable, true, data).ToUpperInvariant(), executable, true, data)));
        test("Autostart lehnt relative Pfade und eingeschleuste Argumente ab", () =>
        {
            reject(() => StartupRegistration.Command("DesktopBubbles.exe", true, data));
            reject(() => StartupRegistration.Command(executable, true, data + "\" --other"));
        });
        test("Windows-Laufwerkswurzel bleibt als Datenordner korrekt gequotet", () =>
        {
            if (!OperatingSystem.IsWindows()) return;
            string drive = Path.GetPathRoot(root)!;
            assert(StartupRegistration.Command(executable, true, drive).Contains("--data-dir \"" + drive + "\\\" --portable"));
        });
    }
}

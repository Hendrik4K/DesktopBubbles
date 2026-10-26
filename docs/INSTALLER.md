# Installer und sichere UI-Vorschau

Verfügbar ab Version 1.7.0. Die Release-Seite enthält Setup, portable ZIP und Quellcode. Die installationsfreie UI-Vorschau lässt sich zusätzlich aus dem Quellcode bauen.

## UI ausprobieren, ohne neu zu installieren

`DesktopBubbles-Setup-<Version>-Preview.exe` ist eine **eigene Vorschau-Ausgabe**. Beim Kompilieren entfallen das Programmpaket, die Dateiinstallation, Verknüpfungen, Programmstart und die Deinstallationsregistrierung. Du kannst Einführung, MIT-Lizenz, Optionen, Datenhinweise und Zusammenfassung ansehen. Der letzte Knopf heißt **Vorschau beenden**. Auch ein unbeaufsichtigter Start wird vor der Installation abgewiesen. Die Vorschau darf neben der laufenden App geöffnet werden.

Wie andere selbstentpackende Programme verwendet auch die Vorschau temporäre Setup-Dateien. Sie installiert DesktopBubbles nicht und verändert weder Gruppen noch Einstellungen, Autostart oder Startmenü-Verknüpfungen. Das geprüfte Setup enthält keine Signatur; ein Windows-Hinweis auf einen unbekannten Herausgeber wird durch die Vorschau nicht umgangen.

## Echte Installation und Updates

Der echte Installer installiert nur für das aktuelle Benutzerkonto nach `%LOCALAPPDATA%\Programs\DesktopBubbles`. Er benötigt keine Administratorrechte. Dieser feste Ort entspricht der bisher lokal verwendeten Installation. Eine vorhandene portable Installation **an diesem Ort** verwendet weiter ihren Unterordner `data`; der Installer ersetzt diese Daten nicht. Das mitgelieferte `portable.flag` sorgt dafür, dass Startmenü, manuelles Öffnen und die App denselben Datenort verwenden.

Vor einem Update muss DesktopBubbles über **Tray → Beenden** geschlossen werden. Eine laufende Zielinstallation blockiert das Update; Setup erzwingt kein Beenden. Vor dem Kopieren der Programmdateien sichert der Installer einen vorhandenen Datenordner vollständig nach `%LOCALAPPDATA%\DesktopBubbles\InstallerBackups\<Zeitpunkt>`. Dateien werden mit SHA-256 geprüft und die Quelle erneut verglichen. Fehlende Leserechte, Dateisystem-Links oder geänderte Daten brechen den Vorgang ab. Der Original-Datenordner wird dabei nicht verschoben, geleert oder mit einer älteren Konfiguration überschrieben.

Angelegt werden ein Startmenüeintrag **DesktopBubbles**, ein regulärer Deinstallationseintrag und auf Wunsch eine Desktop-Verknüpfung. **An Start anheften** bleibt eine Windows-Benutzeraktion. Setup übernimmt keine anderen Pins und aktiviert keinen Autostart. Ein schon vorhandener Autostart auf den festen Programm-/Datenort bleibt gültig.

Andere portable Ordner und ältere umgeleitete Speicherprofile werden **nicht automatisch zusammengeführt**. Vor einem Umstieg von einem anderen Ort die alte App exportieren und den tatsächlichen Datenordner vollständig sichern; anschließend den gewünschten Stand ausdrücklich importieren. Unterschiedliche Datenstände allein nach Datum zusammenzuführen wäre unsicher.

Die Deinstallation entfernt die vom Setup installierten Programmdateien und Verknüpfungen. Persönliche Daten unter `data` und die externen Installer-Sicherungen bleiben erhalten. Ein eigener Autostarteintrag wird nur entfernt, wenn er exakt auf diese Programmdatei samt ihrem Datenordner zeigt. Ein Autostart einer anderen portablen Ausgabe bleibt erhalten.

## Bauen

1. Einen frischen selbständigen portablen Build erstellen, der noch keinen `data`-Ordner enthält.
2. `tools/Get-InnoSetup.ps1` ausführen. Es stellt den offiziellen **Inno Setup 6.7.3**-Compiler im portablen Modus im Projekt-Werkzeugordner bereit und prüft Paket- und Compiler-SHA-256 sowie beim Download die Herausgebersignatur. Der Paket-Digest ist `9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732` (Herausgeber Pyrsys B.V.).
3. `tools/Build-Installer.ps1 -Payload <frischer Buildordner> -Compiler <ISCC.exe>` ausführen.

Erzeugt werden das echte Setup, die separate Vorschau, SHA-256-Dateien und `build.json` unter `artifacts/installer`. `tools/installer/DesktopBubbles.iss` ist die gemeinsame UI- und Installationsquelle. Der Build weist Datenordner, Gruppen-Dateien, Backups, Verknüpfungen, Logs und Dateisystem-Links im Payload zurück.

Inno Setup stammt von Jordan Russell und Martijn Laan; seine Hinweise bleiben im Setup erhalten. [Hersteller und Lizenz](https://jrsoftware.org/isinfo.php), [Quellcode/Lizenztext](https://github.com/jrsoftware/issrc/blob/main/license.txt). Compiler und Entwicklerwerkzeuge werden nicht als Teil der DesktopBubbles-App mitgeliefert.

## Isoliert testen

Beim Bauen `-FixtureRoot <Projekt>\artifacts\<neuer Testordner>` angeben und anschließend `tools/Test-Installer.ps1` starten. Die zusätzliche **Fixture.exe** hat bereits beim Kompilieren andere Ziel-, Sicherungs- und Verknüpfungsordner sowie keine echte Deinstallationsregistrierung. Sie darf nicht veröffentlicht werden. Es handelt sich um denselben Installationscode mit anderen festen Konstanten; der echte Installer besitzt keinen nachträglich aktivierbaren Testmodus.

Geprüft werden Vorschau-Sperre, Erstinstallation, Programm- und Verknüpfungsziel, ein laufendes Ziel, ein Update mit Gruppen/Verknüpfungen/Journal, vollständige Sicherung, Abbruch bei gesperrter Datendatei und Datenerhalt nach Deinstallation. Eine Unicode-Pfad-Fixture prüft die Großschreibung für die Mutex-Erkennung. Die echte Installation wird dabei nur gelesen; ihr Programm, ihre Gruppen und ihr Autostart werden nicht durch die Tests aktualisiert.

Der CI-Workflow baut und prüft den Installer auf einem frischen Windows-Runner. Windows-Suche und Start-Anheften wurden zusätzlich am Entwicklungs-PC manuell bestätigt. Eine breite Prüfung auf unterschiedlichen Windows-11-Geräten, SmartScreen-Konfigurationen und bestehender portabler Daten an anderen Orten ist damit nicht vollständig abgedeckt.

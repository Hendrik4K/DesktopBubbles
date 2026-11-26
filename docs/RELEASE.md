# DesktopBubbles veröffentlichen

Windows-11-x64-Ausgabe als Benutzer-Installer und portable ZIP, Projektcode unter MIT. Das Paket enthält die .NET-8-Desktop-Laufzeit und deren Lizenzhinweise. App und Installer sind nicht codesigniert; es gibt keinen automatischen Updater. Die Desktop-Anbindung bleibt experimentell.

## Versionsnummer

Ab dem nächsten Release ist die Version das Release-Datum in der Form Jahr.Monat.Tag (YY.MM.DD): 26.12.01 ist der 1. Dezember 2026. Das Jahr steht vorn, damit Update-Prüfung, GitHub und Installer neuer immer als größer erkennen. Ein zweiter Release am selben Tag bekommt eine vierte Zahl (26.12.01.2). `tools/Bump-Version.ps1` setzt die Version auf heute: `<Version>` im Projekt in schlichter Form (26.12.1), weil .NET keine führenden Nullen annimmt, `<InformationalVersion>` mit Nullen (26.12.01) für App, Installer, Dateinamen und Tag (v26.12.01). Den Versions-Badge `docs/badges/version.svg` von Hand mitziehen.

## Paket und Prüfung

`Build.ps1 -OutputName DesktopBubbles-win-x64-v<Version>` kompiliert die Lösung, führt die Konsolensuite aus und erzeugt portable Ausgabe, ZIP, Source-ZIP und SHA-256-Prüfsummen. `tools/Get-InnoSetup.ps1` stellt den geprüften Compiler bereit. `tools/Build-Installer.ps1 -Payload <Buildordner> -FixtureRoot <neuer Projekt-Testordner>` baut Setup, Vorschau und die nicht zu veröffentlichende Test-Fixture. `tools/Test-Installer.ps1` prüft Installation, Update, Sicherung und Deinstallation. Native Prüfungen laufen separat auf dem interaktiven Windows-Desktop über Test-Windows.ps1. Ergebnisse und Grenzen: [TESTS.md](TESTS.md).

Vor dem Upload ZIP-Inhalte auf data, Benutzerverknüpfungen, Logs, SDK-Caches und Zugangsdaten prüfen. Die portable Paketierung schließt data aus; der Release wird aus einem frischen Ausgabeordner gebaut. Das veröffentlichte Source-ZIP aus dem getaggten Commit erzeugen. Nur echtes Setup, portable ZIP, Source-ZIP und deren SHA-256-Dateien hochladen; keine Fixture oder Testverzeichnisse. GitHub-Release-Assets nach dem Upload anhand von Größe und SHA-256 prüfen und CI-Erfolg abwarten. Erst danach den vorbereiteten Release veröffentlichen.

## Upgrade und Entfernen

Alte App im Tray beenden. Den vollständigen data-Ordner in die neue portable Ausgabe kopieren oder eine .bubbles-Sicherung verwenden. Schema 1 bis 5 wird beim Speichern zu Schema 6 migriert. Gruppen, Ziele und interne Verknüpfungen bleiben erhalten. Transparenzwerte werden global; je Ansicht wird der bisher wirksame Wert der ersten Gruppe übernommen. Alte Ausgaben dürfen denselben migrierten Datenordner nicht öffnen. [Bedienung](../README.md).

Der Installer erhält vorhandene Daten am festen Benutzer-Programmort und legt vorher eine geprüfte Sicherung an. Andere portable Datenorte zuerst ausdrücklich sichern und übernehmen; keine automatische Zusammenführung. Vor endgültigem Entfernen gewünschte Verknüpfungen auf Desktop zurücklegen und im Tray beenden. Der Deinstaller erhält Daten und Sicherungen; ein genau auf diese Installation zeigender Autostarteintrag wird entfernt. [Installer-Details](INSTALLER.md).

## Weitere Plattformarbeit

- Physische Explorer-Bedienung, mehrere Monitore, gemischte DPI, virtuelle Desktops, hohe Kontraste und weitere Windows-Versionen testen.
- Für eine spätere Microsoft-Store-Ausgabe Paketformat, Signierung, Updatepfad und Rückgabe von Verknüpfungen bei Deinstallation gesondert prüfen. Dieser GitHub-Release ist keine Store-Einreichung.
- Eingebettete Runtime regelmäßig aktualisieren; SDK- und Runtime-Version stehen im Prüfbericht und im Runtimeconfig des Pakets.
- Konfiguration, Sicherungen und Logs können persönliche Pfade und Programmnamen enthalten. Supportberichte vor öffentlichem Upload prüfen. Die App lädt nichts automatisch hoch.

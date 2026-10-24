# DesktopBubbles 1.5.2 veröffentlichen

Portable Windows-11-x64-Ausgabe, Projektcode unter MIT. Das Paket enthält die .NET-8-Desktop-Laufzeit und deren Lizenzhinweise. Es gibt keinen Installer, keine Codesignatur und keinen automatischen Updater. Die Desktop-Anbindung bleibt experimentell.

## Paket und Prüfung

`Build.ps1 -OutputName DesktopBubbles-win-x64-v1.5.2` kompiliert die Lösung, führt die Konsolensuite aus und erzeugt portable Ausgabe, ZIP, Source-ZIP und SHA-256-Prüfsummen. Native Prüfungen laufen anschließend separat auf dem interaktiven Windows-Desktop über Test-Windows.ps1. Ergebnisse und Grenzen: [TESTS.md](TESTS.md).

Vor dem Upload ZIP-Inhalte auf data, Benutzerverknüpfungen, Logs, SDK-Caches und Zugangsdaten prüfen. Die portable Paketierung schließt data aus; der Release wird aus einem frischen Ausgabeordner gebaut. GitHub-Release-Assets nach dem Upload anhand von Größe und SHA-256 prüfen. Erst danach den vorbereiteten Release veröffentlichen.

## Upgrade und Entfernen

Alte App im Tray beenden. Den vollständigen data-Ordner in die neue portable Ausgabe kopieren oder eine .bubbles-Sicherung verwenden. Schema 1 bis 5 wird beim Speichern zu Schema 6 migriert. Gruppen, Ziele und interne Verknüpfungen bleiben erhalten. Transparenzwerte werden global; je Ansicht wird der bisher wirksame Wert der ersten Gruppe übernommen. Alte Ausgaben dürfen denselben migrierten Datenordner nicht öffnen. [Bedienung](../README.md).

Vor Entfernen der App Verknüpfungen auf Desktop zurücklegen, bei Bedarf Datenordner sichern, Autostart ausschalten und im Tray beenden. Ein künftiger Deinstaller muss die Rückgabe vor Entfernen gespeicherter Links anbieten.

## Weitere Plattformarbeit

- Physische Explorer-Bedienung, mehrere Monitore, gemischte DPI, virtuelle Desktops, hohe Kontraste und weitere Windows-Versionen testen.
- Für eine spätere Microsoft-Store-Ausgabe Paketformat, Signierung, Updatepfad und Rückgabe von Verknüpfungen bei Deinstallation gesondert prüfen. Dieser GitHub-Release ist keine Store-Einreichung.
- Eingebettete Runtime regelmäßig aktualisieren; SDK- und Runtime-Version stehen im Prüfbericht und im Runtimeconfig des Pakets.
- Konfiguration, Sicherungen und Logs können persönliche Pfade und Programmnamen enthalten. Supportberichte vor öffentlichem Upload prüfen. Die App lädt nichts automatisch hoch.

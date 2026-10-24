# Öffentlichen Windows-11-Release vorbereiten

1.4.1 ist eine nicht signierte portable Vorschau für Windows 11 x64. Der lokale Prüfstand umfasst 97 Kern- und 92 native Windows-Prüfungen. Veröffentlichungsstatus und Downloads werden im GitHub-Repository geführt; diese Datei beschreibt Paketierung und offene Plattformprüfungen.

`Build.ps1 -OutputName DesktopBubbles-win-x64-v1.4.1` erzeugt EXE-Ordner, ZIP und SHA-256. Das Release-ZIP schließt `data` vollständig aus. Das Source-ZIP enthält Code, Tests, Dokumentation und Design-QA ohne SDK oder Testdaten. Native Prüfungen laufen separat auf einem interaktiven Windows-Desktop. Prüfstand: [TESTS.md](TESTS.md).

## Upgrade und Entfernen

Die laufende alte Ausgabe im Tray beenden. Den vollständigen `data`-Ordner in die neue portable Ausgabe kopieren oder eine `.bubbles`-Sicherung verwenden. Ein lokal vorbereiteter neuer Ordner kann bereits eine Kopie des bisherigen Datenstands enthalten. Der erste Start liest Schema 1/2/3/4 und migriert beim Speichern auf Schema 5. Keine alte Ausgabe auf denselben migrierten Ordner ansetzen. Die Datenkopie selbst übernimmt keine Desktop-Dateien. Einträge, beide Transparenzen und Ordnergröße bleiben erhalten. Farbe wird gemeinsam: explizite Miniaturfarbe gewinnt, sonst alte Ordnerfarbe. Die bisherige Ordner-Rundung wird proportional in eine gemeinsame Rundung für Miniatur/Ordner umgerechnet. Die Verwaltung bleibt fest gerundet. 100 % Window-Transparenz setzt null Materialstärke und keinen DWM-Backdrop voraus. Details: [ARCHITECTURE.md](ARCHITECTURE.md).

Vor dem Löschen **Zahnrad → Desktop-Verknüpfungen wiederherstellen** ausführen und bei Bedarf den Datenordner sichern. Dann Autostart abschalten, im Tray beenden, portablen Ordner entfernen. Ein künftiger Deinstaller muss die Rückgabe vor Entfernen der gespeicherten Kopien anbieten. Bloßes Löschen des Datenordners kann die einzige verbliebene Kopie übernommener Links entfernen.

## Offene Freigabepunkte

- Manuelle Windows-/Explorer-/DPI-/Mehrmonitor-/Barrierefreiheitstests, echte Mausbedienung, OneDrive und spezielle Launcher abschließen. Unterstützte Systeme danach festlegen.
- Installer/MSIX, Codesignatur, sichere Updateverifikation und Rückgabe bei Deinstallation entscheiden/testen. Derzeit kein Installer oder Updater.
- Microsofts Release-Metadaten am 24.10.2026 geprüft: SDK 8.0.426 und Runtime 8.0.32 sind dort die neuesten .NET-8-Versionen. .NET 8 erreicht laut Metadaten am 10.11.2026 sein Supportende; die nächste Wartung muss die Runtime aktualisieren. Einen vollständig reproduzierbaren Build gibt es noch nicht.
- Eigener Projektcode steht unter MIT (LICENSE im Projekt und portablen Paket). Das Buildskript kopiert die Lizenz-/Drittanbieterhinweise der tatsächlich eingebundenen Runtime-Pakete nach `licenses`. Künftig ergänzte Komponenten benötigen eigene Prüfung und Hinweise.
- Produktname und eigene Gestaltung vor kommerzieller Veröffentlichung prüfen. Keine Apple-Assets oder Apple-Herkunft behaupten. Die Glasfläche verwendet eigenen Code und Windows-Funktionen.
- Datenverarbeitung/Support dokumentieren: Konfiguration, Export und Logs können lokale Pfade und Programmnamen enthalten. Keine automatische Telemetrie oder Uploads vorhanden.
- Prozessabbrüche und Datenträger-/Sync-Fehler zusätzlich in einer Wegwerf-Umgebung prüfen. Bisherige Recovery-Fehlerprüfungen sind überwiegend simuliert; keine verteilte Cloud-Transaktion.

Dies ist eine technische Arbeitsliste, keine allgemeine rechtliche oder technische Release-Freigabe.

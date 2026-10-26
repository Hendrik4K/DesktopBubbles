# Autostart und unterschiedliche lokale Ausgaben

Eine portable Ausgabe verwendet standardmäßig ihren eigenen Unterordner `data`. Unterschiedliche Ausgabeordner können deshalb absichtlich verschiedene Gruppen, Farben und Einstellungen enthalten. Ein neuer Build aktualisiert weder eine andere Installation noch deren Autostart von allein.

## Fehler vor 1.6.1

Die Checkbox „Mit Windows starten“ prüfte lediglich, ob irgendein DesktopBubbles-Eintrag im Windows-Autostart existiert. Dadurch sah sie auch in einer neueren Vorschau eingeschaltet aus, obwohl Windows weiterhin eine ältere Programmdatei und deren anderen Datenordner öffnen würde. Das konnte wie ein zurückgesetzter Speicherstand aussehen.

Ab Version 1.6.1 wird der Eintrag mit der aktuellen Programmdatei, dem vollständigen aktiven Datenordner und dem portablen Modus verglichen. Ein abweichender Eintrag lässt die Checkbox ausgeschaltet; ihr Tooltip erklärt, wie die aktuelle Ausgabe übernommen wird. Das Öffnen der Einstellungen ändert keine Registrierung. Erst ausdrückliches Aktivieren schreibt diese Ausgabe samt ihrem Datenordner in den Autostart.

## Sicher umsteigen

1. In der Ausgabe mit den neuesten Gruppen über **Einstellungen → Datenordner öffnen** den tatsächlich aktiven Speicher ermitteln.
2. Alle DesktopBubbles-Ausgaben im Tray beenden und den vollständigen aktiven Datenordner sichern. Ein Export ersetzt nicht die vollständige Rückgabehistorie.
3. Die neue Programmversion an einem festen Ort ablegen und den gesicherten Datenordner übernehmen oder ausdrücklich mit `--data-dir` weiterverwenden. Nicht nur `groups.json` kopieren: Interne Verknüpfungen und Journale gehören dazu.
4. Die neue Ausgabe starten und die Gruppen prüfen. Dort bei Bedarf **Mit Windows starten** aktivieren; damit verwenden manuelles Öffnen und Windows-Start denselben gewünschten Speicher.

Unterschiedliche Datenordner werden nicht automatisch nach Änderungsdatum zusammengeführt. Bei mehreren Ständen zunächst Kopien sichern und anhand von Gruppen, Inhalten und Einstellungen den gewünschten Stand bestimmen. Alte vollständige Sicherungen erst entfernen, wenn der richtige Zustand bestätigt ist.

## Prüfung

Die Kernprüfungen verwenden ausschließlich konstruierte Befehlszeilen und Testpfade. Sie ändern weder den echten Windows-Autostart noch Benutzerdateien. Geprüft werden Programm-/Datenpfadwechsel, portabler Modus, fehlende Einträge, Windows-Pfadschreibweise, unzulässige Argumente und eine Laufwerkswurzel. Native Fensterprüfungen lesen die Registrierung nur. Ein realer Neustart beziehungsweise eine echte Benutzeranmeldung bleibt eine zusätzliche Prüfung.

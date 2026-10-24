# Sichere Verknüpfungsverwaltung

## Nur aus der Gruppe löschen · ab 1.5.1

Das Symbolmenü enthält Starten, Zurück zum Desktop und **Aus Gruppe löschen**. Die letzte Aktion entfernt ausschließlich die Referenz in dieser Gruppe. Sie löst das Ziel nicht auf, prüft keine Datei und ruft weder Wiederherstellung noch Exportbestätigung auf. Damit lassen sich auch fehlende Ziele, fehlende interne Kopien und defekte Metadaten entfernen. Dateien, Programme, Sicherungskopien und Referenzen anderer Gruppen bleiben erhalten. Bestehende Rückgabehistorie bleibt ebenfalls erhalten; eine noch nutzbare Sicherung kann daher später über das zusätzliche Rücklege-Werkzeug wiederhergestellt werden.

Die Zustandsänderung wird sofort gespeichert. Scheitert das Speichern, wird der Eintrag an derselben Listenposition wieder eingesetzt. Zurück zum Desktop bleibt der getrennte Ablauf mit geprüfter Dateirückgabe. Die folgenden Abschnitte beschreiben diese Datei- und Wiederherstellungsabläufe.

## Verhalten und Speicher

`.lnk` und `.url` erhalten eine eigene bytegetreue Kopie. Bei Links direkt auf dem **persönlichen** Windows-Desktop ist standardmäßig zusätzlich „Original entfernen“ eingeschaltet. Unter **Zahnrad → Desktop-Originale nach Übernahme entfernen** lässt sich der Kopiermodus wählen. Links von anderen Orten, einschließlich des gemeinsamen Desktops, werden ausschließlich kopiert. Programme, Ordner und sonstige Dateien bleiben externe Referenzen.

Der persönliche Desktop kommt aus dem Windows-Known-Folder-Pfad, einschließlich bestehender Umleitung etwa nach OneDrive. Unterverzeichnisse zählen nicht als direkte Desktop-Verknüpfungen. Gemeinsame Desktop-Dateien werden nicht entfernt; kein automatischer UAC-/Administratorablauf. Alte Einträge bleiben beim Upgrade unverändert. Für persönliche Desktop-Links gibt es **Rechtsklick → Desktop-Original in Bubble übernehmen**.

```text
data/groups.json                         Schema 5
data/groups.json.bak                     vorheriger Stand
data/shortcuts/<GUID>.lnk                 bytegetreue Kopie, alternativ .url
data/shortcuts/<GUID>.lnk.json            Wiederherstellungsjournal
```

Gruppeneinträge speichern `bubble-link:<GUID>.lnk`. Nur GUID-Dateinamen mit `.lnk` oder `.url` sind erlaubt; keine frei bestimmbaren relativen Pfade. `ShortcutVault.Resolve` verwendet den aktuellen Datenordner. Das lokale Journal enthält Quelle, SHA-256, Übernahmewunsch, Phase und Rückgabeort.

Nutzdaten bleiben nach Entfernen eines Eintrags erhalten, damit alte Backups und **Alle Desktop-Verknüpfungen wiederherstellen** nutzbar bleiben. Es gibt keine automatische Speicherbereinigung. Vor dem Löschen eines portablen Ordners zurückholen oder den vollständigen Datenordner sichern.

## Übernahmetransaktion

1. Vorhandene identische Übernahme anhand Quelle und Hash wiederverwenden. Doppelte Drops in dieselbe Gruppe erzeugen keinen zweiten Eintrag.
2. Neue GUID-Datei bytegetreu schreiben, `Flush(true)` und Hash prüfen. Maximal 1 MiB pro Link. Reparse-Dateien werden nicht übernommen.
3. `.lnk` in beiden Verzeichnissen per Shell lesen: vollständig aufgelöster Zielpfad, Argumente und Arbeitsverzeichnis müssen übereinstimmen. `.url` muss eine absolute URL enthalten. Nichts ausführen. Unklare Sonderlinks ablehnen und Original behalten.
4. Journalphase `Prepared` dauerhaft schreiben.
5. Gruppeneintrag und andere Referenzen auf dieselbe Quelle aktualisieren; die Konfiguration sofort atomar speichern. Bei Commit-Fehler die Änderungen im Arbeitsspeicher zurücknehmen; das Original bleibt erhalten.
6. Nur bei persönlichem Desktop und Übernahmewunsch: Quelle mit Lese-/Löschrecht öffnen, Schreiben und weitere Löschzugriffe ausschließen, Hash auf diesem Handle prüfen und genau dieses Handle per `SetFileInformationByHandle(FileDispositionInfo)` zur Löschung markieren. Geänderte oder gesperrte Originale bleiben erhalten; die Bubble benutzt ihre Kopie.
7. Journal auf `Owned` oder `Copied` setzen. Auch bei Fehler an dieser Stelle ist die nutzbare Kopie bereits gespeichert.

`.lnk` wird nicht als vereinfachter EXE-Verweis neu erzeugt. Advertised-/Sonderlinks oder unklare relative Auflösungen können die Prüfung nicht bestehen. Dann die installierte App oder Programmdatei hinzufügen.

## Abbruch und Rückgabe

| Abbruchstelle | Zustand |
| --- | --- |
| Vor Konfigurations-Commit | Original bleibt; eine vorbereitete Reserve darf im Speicher bleiben. |
| Nach Commit, vor Entfernen | Gruppe verwendet Kopie; Recovery lässt das vorhandene Original stehen und meldet den Abbruch. |
| Nach Entfernen, vor Journalabschluss | Gruppe verwendet Kopie; Recovery erkennt fehlendes Original. Rückgabe bleibt möglich. |
| Kopie fehlt/Hash falsch | Betroffener Eintrag wird gemeldet; kein Desktop-Original wird automatisch gelöscht. Datenordner/Backup prüfen. |

Recovery führt keine verspätete Desktop-Löschung aus. Nach einem Abbruch darf ein Original zusätzlich vorhanden sein. Anschließend kann der Benutzer erneut ausdrücklich übernehmen.

Beim Entfernen der letzten Gruppenreferenz wird ein übernommener persönlicher Desktop-Link zuerst zurückgelegt, dann der Eintrag dauerhaft entfernt. Andere Gruppen halten die gemeinsame Kopie aktiv. Bei Speicherfehler bleibt der Eintrag nutzbar; der Link kann bereits zusätzlich zurückgelegt sein. Kopierte Links von anderen Orten werden nicht automatisch auf den persönlichen Desktop gelegt.

Rückgabe verwendet eine geflushte temporäre Datei und Umbenennung ohne Überschreiben. Gleicher Hash am Ziel wird wiederverwendet. Anderer Inhalt erhält einen Namen wie `Launcher (Bubble 1).lnk`. Die Sicherung bleibt erhalten. Rechtsklick kann zusätzlich zurückholen; **Zahnrad → Desktop-Verknüpfungen wiederherstellen** bezieht erhaltene, weiterhin rückgabepflichtige Journale aus entfernten Gruppen ein.

Nach einer nachträglichen Desktop-Umleitung wird eine alte Quelle konservativ nicht mehr als aktueller eigener Desktop anerkannt. Nutzdaten bleiben verfügbar, benötigen dann aber eine neue Zuordnung. Das Journal garantiert keine verteilte Transaktion über Cloud-Sync, mehrere Rechner oder beliebige Strom-/Datenträgerausfälle.

## Herausziehen und zwischen Bubbles verschieben · 1.3.0

Beim Ziehen erzeugt ShortcutDrag `data/drag-out/<GUID>/<Anzeigename>.lnk` beziehungsweise `.url`. Ein bestehender Link wird bytegetreu kopiert und seine Auflösung geprüft. Programme, Dateien und Ordner erhalten eine echte neue `.lnk`; Webadressen eine `.url`, Store-Ziele eine Explorer-Verknüpfung mit AppUserModelID. CF_HDROP bietet ausschließlich diese Staging-Datei an, niemals das Programm oder die interne Vault-Datei. Copy/Move sind erlaubt; Link ist ausgeschlossen, damit kein Link auf eine spätere temporäre Datei entsteht.

Eine private, nur während dieses Drags aktive Kennung unterscheidet interne Transfers. Diese werden ohne Desktop-Rückgabe in einem Konfigurations-Commit verschoben beziehungsweise umsortiert. Bei Speicherfehler wird der Zustand zurückgenommen; identische Ziele in der Zielgruppe werden zusammengeführt. Windows erhält die Kennung als UTF-8-Stream, ohne unsichere Objekt-Deserialisierung.

Ein erfolgreicher DoDragDrop-Rückgabewert allein entfernt keinen Eintrag. Externe Ziele müssen die [Shell-Übergabeformate](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard) bestätigen (Logical Performed DropEffect, Performed DropEffect oder Paste Succeeded). Fehlt eine solche Bestätigung, prüft die App für höchstens zwei Sekunden den vorher reservierten Dateinamen auf dem persönlichen Desktop gegen den vollständigen Hash. Vorhandene Namen werden nicht als Ziel gewählt.

Erst nach Bestätigung wird der Bubble-Eintrag gespeichert entfernt. Bei letzter verwalteter Referenz setzt das Journal Exported und beendet den Rückgabeauftrag; die interne Nutzdatei bleibt für alte Sicherungen erhalten. Andere Gruppen behalten ihre Referenz. Bei fehlender Bestätigung bleiben Eintrag, Nutzdaten und Staging erhalten: ein Ziel ohne Shell-Bestätigung kann bereits eine Kopie erhalten haben, die App behauptet dann keinen erfolgreichen Umzug. Bei Abbruch sowie bestätigten/internen Transfers wird nur die selbst erzeugte Staging-Datei und ihr leerer GUID-Ordner aufgeräumt.

Automatisiert geprüft sind Datei-/Commit-Verhalten und native CF_HDROP-/Stream-Formate. Physisches Ziehen mit der Maus zu Desktop/Explorer, Cloud-Zielen und Sonderverknüpfungen bleibt manuell zu prüfen. Insbesondere ist die Shell-Bestätigung ein Protokollvertrag mit dem Zielprogramm, keine nachträgliche Inhaltsprüfung beliebiger Zielordner.

## Sicherung und Import

Seit 1.4.1 enthält das Symbolmenü nur **Starten** und **Aus Gruppe entfernen**. Letzteres erstellt auch für direkt referenzierte Programme, Dateien, Ordner, Store-Ziele und Webadressen eine unabhängige Desktop-Verknüpfung. Bestehende Links werden bytegetreu kopiert und ihre Auflösung geprüft. Eine temporäre Datei auf dem Zielvolume wird geflusht und per SHA-256 geprüft, dann ohne Überschreiben umbenannt und am endgültigen Ort nochmals geprüft. Erst danach entfernt `CompleteExport` den Eintrag mit sofortigem Konfigurations-Commit. Bei Namenskonflikten wird ein Suffix vergeben; eine identische geprüfte Kopie kann wiederverwendet werden. Bei Schreibfehler oder Commitfehler bleibt der Gruppeneintrag erhalten; eine bereits geschriebene Desktop-Kopie bleibt ebenfalls erhalten. Andere Gruppen und interne Reserven bleiben nutzbar.

`.bubbles` ist ein ZIP mit `groups.json`, `sha256.json` und den referenzierten gespeicherten Links. Keine Programme, privaten Journal-Quelldaten, unreferenzierten Reserven oder eigenen Symbolbilder. Externe Ziele bleiben externe Pfade. Für eine vollständige Sicherung einschließlich Rückgabehistorie den ganzen Datenordner bei beendeter App kopieren.

Import liest begrenzte Streams und extrahiert keine ZIP-Pfade. Traversal, doppelte Namen, falsche Hashes, fehlende Nutzdaten, Links über 1 MiB und Archive über 128 MiB entpackter Gesamtdaten werden abgelehnt. Vor dem Ersetzen wird der bisherige Zustand zusätzlich gesichert. Nicht mehr referenzierte eigene Desktop-Links werden zurückgelegt.

Fremde Sicherungen erhalten keine lokalen Quelldatei-Journale und damit keine Rechte zur Desktop-Entfernung oder automatischen Rückgabe. Ein bestehender eigener identischer Vault-Eintrag behält seine lokale Historie. Hashes prüfen Integrität, keine Vertrauenswürdigkeit: nur bekannte Sicherungen importieren. Ältere JSON mit externen Zielen bleiben importierbar; für verwaltete Ziele ist das vollständige `.bubbles`-Archiv nötig.

# Prüfstand

## Version 1.7.0 · 27.10.2026

**20 isolierte Installer-Prüfungen bestanden.** [Rohbericht](results/installer-v1.7.0.json), [Bedienung und Grenzen](INSTALLER.md). Geprüft: installationsfreie Vorschau auch bei unbeaufsichtigtem Start, Erstinstallation, Startmenüziel und Portable-Datenort, Sperre bei laufender Ziel-App auf einem Unicode-Pfad sowie bei gesperrter Programmdatei ohne passenden Datenordner-Mutex, Update mit bytegleichem Erhalt von Gruppen/Links/Journal, geprüfte externe Sicherung, Abbruch bei gesperrter Datendatei sowie Datenerhalt nach Deinstallation. Die echte Nutzerinstallation wurde nur gelesen; Programmdatei, Gruppen und Autostart blieben unverändert. Der echte Installer wurde nicht auf die Nutzerinstallation angewendet. UI mit eigener Fensteraufnahme in dunklem Windows-Design geprüft. Installer-Build und isolierte Prüfungen werden zusätzlich im Windows-CI-Workflow ausgeführt.

### App und Milchglas

Build ohne Warnungen/Fehler, **135 Kernprüfungen und 142 native Fenster-/Integrationsprüfungen bestanden**. [Core](results/core-tests-v1.7.0.txt), [Windows](results/windows-smoke-v1.7.0.json). Native Prüfung mit `AppearanceOnly=true`; die bestehende Nutzerinstallation und deren Registrierung bleiben außerhalb der Fixture.

Neue Fälle prüfen ältere Konfigurationen ohne Milchglasfelder, feste Tönung unabhängig vom früheren Deckkraftwert, unveränderte normale Transparenz, Umschalten und Persistenz. Der native Test bedient den Milchglas-Knopf links vom Maßstab und prüft Aktivfarbe, ausgeblendete Transparenzzeile und Wiederherstellung, unabhängige Tönung, tatsächlich verfügbare Hintergrundübernahme, ausdrücklich abgeschalteten DWM-Systemhintergrund, Alpha-Fensterstil, Blur einer synthetischen Schwarzweißkante, transparente Glaspixel an allen vier Ecken, starke/Null-Rundung, Größenänderung und Wiederherstellung nach simulierter verlorener Kontur. Zwölf zusätzliche Öffnen/Schließen-Zyklen prüfen begrenzte Handle-Zahl. Eine Aufnahme erfolgt pro Öffnen; keine laufenden Aufnahmen oder Speicherung von Desktopbildern durch die App. Gespeicherte Milchglas-Testbilder verwenden synthetische Hintergrundpixel. Reale gemischte DPI-/Mehrmonitor-Hardware bleibt manuell zu prüfen. [Bedienung und Grenzen](FROSTED-FOLDERS.md).

## Version 1.6.1 · 26.10.2026

Finaler Build ohne Warnungen/Fehler, **130 Kernprüfungen und 124 native Fenster-/Integrationsprüfungen bestanden**. [Core](results/core-tests-v1.6.1.txt), [Windows](results/windows-smoke-v1.6.1.json). Der native Lauf verwendet `AppearanceOnly=true`; Desktop-Klickflächen-/MinimizeAll-Prüfungen und eine echte Windows-Anmeldung gehören nicht zu diesem Lauf. Der vorhandene Nutzer-Autostart wird bei automatischen Tests nicht verändert.

Die acht neuen Kernfälle prüfen den tatsächlich gebundenen Programm- und Datenpfad, abweichende Ausgaben, portablen Modus, fehlende Einträge, Windows-Pfadschreibweise, Groß-/Kleinschreibung der Optionen, Argumentvalidierung und Quoting einer Laufwerkswurzel. Daten bleiben pro Ausgabe getrennt; die Korrektur ändert die Anzeige und die ausdrückliche Autostart-Auswahl. [Übernahme bestehender Gruppen](STARTUP.md).

## Lokale Korrektur 1.6.1-local.1 · 26.10.2026

Build ohne Warnungen/Fehler, **130 Kernprüfungen und 124 native Fenster-/Integrationsprüfungen bestanden**. [Core](results/core-tests-startup-local.txt), [Windows](results/windows-smoke-startup-local.json). Der native Lauf verwendet `AppearanceOnly=true`; die realen Autostart-Einträge werden dabei ausschließlich gelesen.

Acht neue Kernfälle prüfen die Bindung des Windows-Startbefehls an Programmdatei, aktiven Datenordner und portablen Modus, abweichende Ausgaben, fehlende Einträge, Windows-Pfadschreibweise, Groß-/Kleinschreibung der tatsächlich erkannten Optionen, unzulässige Argumente und korrektes Quoting einer Laufwerkswurzel. Die Anzeige übernimmt keinen fremden Eintrag von allein. [Autostart und unterschiedliche lokale Ausgaben](STARTUP.md).

Zusätzlich wurde eine gesicherte lokale Datensammlung samt internen Verknüpfungen in die feste Installation übernommen und bei zwei vollständigen App-Starts mit den Autostart-Argumenten auf erhaltene Gruppeninhalte und Einstellungen geprüft. Das ist kein echter Windows-Neustart und keine vollständige Prüfung des Anmeldeablaufs. Persönliche Daten, Programm-/Datensicherungen und Reparaturberichte verbleiben außerhalb der Quellpakete im ignorierten artifacts-Ordner. Dieser lokale Vorabstand war noch nicht veröffentlicht.

## Version 1.6.0 · 25.10.2026

Finaler Build ohne Warnungen/Fehler, **122 Kernprüfungen und 124 native Fenster-/Integrationsprüfungen bestanden**. [Core](results/core-tests-v1.6.0.txt), [Windows](results/windows-smoke-v1.6.0.json). Der native Lauf verwendet `AppearanceOnly=true`: Die neun Desktop-Klickflächen-/MinimizeAll-Prüfungen sind ausdrücklich ausgelassen; für 1.5.2 ist weiter unten der damalige vollständige Plattformlauf dokumentiert.

Die neuen Prüfungen decken den Halte-Timer, Sortiermodus, Wackelanimation, Zielkoordinaten, exakten Platztausch, gespeicherte Reihenfolge und Abbruch/Esc ab. Fehlende Ziele lassen sich ebenfalls sortieren. Bei Schreibfehlern bleibt die alte Reihenfolge erhalten. Eingaben werden im eigenen WPF-Fenster simuliert; physische Mausbedienung und die übrigen Plattformgrenzen unten sind damit nicht vollständig abgenommen. Die finale Ausgabe verwendet dasselbe Schema 6. Die bereitgestellte animierte Vorher-nachher-Demo zeigt die Bedienidee; sie ersetzt keinen Plattformtest.

## Lokale Vorschau 1.6.0-preview.1 · Gedrückthalten und Platztausch

Build ohne Warnungen/Fehler, **122 Kernprüfungen und 124 native Fenster-/Integrationsprüfungen bestanden**. Dieser Lauf verwendet ausdrücklich `AppearanceOnly=true` und überspringt die neun Desktop-Klickflächen-/MinimizeAll-Prüfungen. [Core](results/core-tests-swap-preview.txt), [Windows](results/windows-smoke-swap-preview.json).

Neue Kernfälle prüfen den exakten Tausch zweier von neun Einträgen, dauerhaft gespeicherte Reihenfolge, Rücknahme bei Speicherfehler, fehlende Ziele ohne Datei-/Journaländerung, Tausch auf sich selbst und inzwischen entfernte Ziele. Native Fälle prüfen den 500-ms-Timer, kurzes Klicken ohne Sortiermodus, langes Halten ohne Start, Wackelanimation unter Beachtung der Bewegungseinstellungen, sofortiges Greifen im Sortiermodus, Ablegen anhand der Symbolkoordinaten, Speichern und aktualisierte Ansicht, Abbruch außerhalb des Ordners und Esc ohne Schließen des Fensters. Die Eingabeereignisse werden im eigenen WPF-Fenster simuliert; die physische Mausbedienung sowie gemischte DPI und mehrere Monitore bleiben manuell zu prüfen.

Bedienung und Implementierung: [REORDERING.md](REORDERING.md). Diese Vorschau war noch nicht veröffentlicht und änderte die installierte stabile Version nicht. Persönliche Vorschau-Daten und deren Kopierbericht liegen ausschließlich im ignorierten artifacts-Ordner.

## Patch 1.5.2 · 24.10.2026

Build ohne Warnungen/Fehler, **117 Kernprüfungen und 124 vollständige native Windows-Prüfungen bestanden**. [Core](results/core-tests-v1.5.2.txt), [Windows](results/windows-smoke-v1.5.2.json).

Neue Fälle: tatsächliche schreibgeschützte Konfiguration, Migration ohne Quelländerung, Erhalt interner Verknüpfungen/Journale, dauerhafte Löschung nach Umleitung und Neustart, keine erneute Übernahme alter Quelldaten, getrennte Ausgangsordner, beschädigte Hauptdatei mit/ohne Backup und stale read-only groups.json.tmp. Deckkraft wird als Alpha statt Helligkeit geprüft, einschließlich Entwurf ohne Persistenz, Abbrechen, getrenntem Übernehmen und Neustart. Eine separate Ansichtsprüfung bestand vorher 115 Fälle; der abschließende vollständige Lauf ist der oben verlinkte Bericht.

Zusätzlich wurde die echte, normal nicht beschreibbare Program-Files-Datensammlung des Nutzers mit 7 Gruppen und 31 Einträgen in einen beschreibbaren Benutzerprofil-Pfad kopiert, anhand der aktuellen lokalen Export-Sicherung abgeglichen und dort erfolgreich gespeichert. Quelle und Sicherung blieben erhalten. Persönliche Daten und dieser lokale Migrationsbericht werden nicht veröffentlicht. Plattformgrenzen unten bleiben bestehen.

## Patch 1.5.1 · 24.10.2026

Release-Build ohne Warnungen/Fehler, **108 Kernprüfungen und 118 vollständige native Windows-Prüfungen bestanden** (AppearanceOnly=false). [Core-Bericht](results/core-tests-v1.5.1.txt), [Windows-Bericht](results/windows-smoke-v1.5.1.json).

Neue Regressionen prüfen fehlende externe Ziele, fehlende interne Kopien, kaputte Journale, unveränderte Dateien/Sicherungen und andere Gruppen sowie Rollback an derselben Listenposition bei Speicherfehler. Der native Test ruft Aus Gruppe löschen über das tatsächliche Symbol-Kontextmenü für ein fehlendes Ziel auf und prüft unmittelbar Oberfläche und gespeicherte Konfiguration. Die übrigen vollständigen Desktop-, Rückgabe- und UI-Prüfungen bestanden ebenfalls. Die unten dokumentierten manuellen Plattformgrenzen bleiben bestehen.

## Release 1.5.0 · 24.10.2026

Release-Build ohne Warnungen oder Fehler. **104 Kernprüfungen und 116 native Windows-Prüfungen bestanden**, vollständiger Modus (AppearanceOnly=false). Windows 10.0.26300.0, x64, .NET 8.0.32, SDK 8.0.426. Berichte: [Core](results/core-tests-v1.5.0.txt) und [Windows](results/windows-smoke-v1.5.0.json).

Der vollständige Lauf prüft auch Bubble finden, die tatsächliche Desktop-Klickfläche, Überdecken durch ein normales Fenster und Erreichbarkeit nach dessen Schließen. Ein vorheriger Lauf traf die Bubble einer parallel laufenden alten Vorschau. Deren gespeicherte Daten wurden separat gesichert, die alte Instanz beendet und der unveränderte Release-Build erneut vollständig geprüft; dieser Lauf bestand.

Weitere Schwerpunkte: Schema-6-Migration, gemeinsame Größen/Transparenzen, Farbkopplung, Modusfarben, eingebettete Farbentwürfe mit Anwenden/Abbrechen, 1:1-Vorschau, Verknüpfungsrückgabe ohne Überschreiben, Shell-Datenübergabe und Ressourcen-/Handle-Zyklen. Das Rauchglas-Icon ist eingebettet; das aus der EXE geladene 32×32-Symbol wurde zuvor pixelgenau mit dem ICO verglichen.

Physisches Explorer-Drag-&-Drop, echte Win+D-Bedienung, Explorer-Neustart, Mehrmonitor-/DPI-Mischbetrieb, Store-App-/UAC-Fälle und Anmeldung/Autostart bleiben manuell beziehungsweise auf weiteren Systemen zu prüfen. Die Screenshots rendern den eigenen WPF-Inhalt, nicht die vollständige Desktop-Komposition.

## Historische Entwicklungsprüfungen

**Aktuelle lokale Modusfarben:** Release-Build ohne Warnungen/Fehler, 104 Kernprüfungen und 107 native Prüfungen im Modus AppearanceOnly bestanden. Geprüft sind getrennte Speicherung, Standards bei fehlenden/ungültigen Feldern, Schwarz-/Weiß-Kontrast auf 216 eigenen Farbpaletten, Vorschau ohne Persistenz, Abbrechen, ungültige HEX-Eingabe, eigene Hell-/Dunkel-Steuerelemente, Windows-Standard-Profilwahl und vorhandene Farbreset-/Kopplungsabläufe. Rohbericht: artifacts/mode-colors-native-20261024-172458/smoke-results.json. Die Desktop-Klickflächenprüfung bleibt in diesem Modus ausdrücklich ausgelassen; die folgenden Prüfstände sind historisch.

**Neuester lokaler Farbeditor:** 101 Kernprüfungen und 96 native Ansichts-/Integrationsprüfungen mit AppearanceOnly bestanden. Die vollständige Desktop-Klickflächenprüfung erreichte am Prüfpunkt ein anderes Fenster und bleibt auf freiem Desktop offen. [Prüfumfang](LOCAL-PREVIEW.md#ergänzung-eingebetteter-farbeditor), Rohbericht windows-smoke-inline-color.json. Der folgende vollständige 101er-Lauf stammt vom vorherigen Stand ohne eingebetteten Farbeditor.

**Lokale Vorschau 1.5.0-preview.1, noch nicht veröffentlicht:** 101 Kernprüfungen und 101 native Windows-Prüfungen im abschließenden Durchlauf bestanden. Gemeinsame Einstellungen, Farbauswahl mit und ohne Kopplung, Miniaturgröße und scrollbar erreichbare 1:1-Vorschau geprüft. [Details und Grenzen](LOCAL-PREVIEW.md). Der folgende Stand beschreibt den veröffentlichten Release 1.4.1.

Stand: **24.10.2026**, Version **1.4.1**, Windows **10.0.26300.0**, x64, .NET **8.0.32**, lokal eingerichtetes SDK **8.0.426**. Ein Buildname wie „26H2“ wird nicht aus der Buildnummer abgeleitet. Getestete Version und noch offene Fälle bleiben getrennt.

## Automatisiert ausgeführt

Neu in 1.4.1: geprüfte Programm- und Web-Verknüpfung zurück auf den Fixture-Desktop schreiben, identische Kopie wiederverwenden, Namenskollision ohne Überschreiben behandeln und bei Schreibfehler den Gruppeneintrag erhalten. 97 Kern- und 92 native Prüfungen bestanden. Ein erster Lauf scheiterte an der Erreichbarkeitsprüfung nach dem Schließen eines überdeckenden Fensters; der vollständige Wiederholungslauf bestand. Physische Explorer-Bedienung bleibt offen.

- Release-Kompilierung der gesamten Projektmappe, ohne Warnungen und Fehler.
- **97 Konsolenprüfungen bestanden**: Modell-/Persistenzprüfungen einschließlich Schema-1/2/3/4-Migration zu 5, RGB/HSV und Farbnormalisierung, zeitgewichteter CPU-Serie/Pausen, Gruppen-Transfer und bestätigtem Export mit Commit-Rollback; außerdem Übernahme mit Commit vor Entfernung, Speicherfehler, gesperrte/veränderte Quellen, Kopiermodus, gemeinsame Referenzen, explizite Übernahme alter Einträge, unabhängige Kopien, letzte Referenz und Gruppenlöschung, Rückgabekonflikte, simulierte Abbrüche, fehlende Kopie, beschädigtes Journal, Bundle-Roundtrip, Import-Eigentumsrechte, Archiv-Traversal/Duplikate/Hashfehler, versteckte Namen und Bildschirmgeometrie.
- **92 native Windows-Prüfungen bestanden**: Fenster-/Shell-Prüfungen und echte bytegetreue `.lnk`-Übernahme, native Löschung genau der geprüften Quelldatei, Start der verwalteten Kopie nach Entfernen der Quelle, Rückgabe, gesperrtes Original, abweichender Hash, `.url` ohne Start, externe unabhängige Kopie und Bundle-Import ohne Desktop-Eigentum. Dateioperationen an selbst erzeugten Testdateien im Fixture-Desktop; keine Benutzerverknüpfung übernommen oder gelöscht.
- Neu in 1.4.0: gemeinsame Farbe mit festgelegter Migration, proportionale Rundung, Rastergeometrie und unabhängige Transparenzen bis null Materialstärke. Nativ: Tracking zunächst aus, Start/Stop über den echten Button, eingefrorene Werte und Neustart ohne alte Intervalle. Miniatur enthält genau eine Glaswahl, Ordner keine doppelte Farbe/Glas-/Rundung. Größenregler verändert die Vorschau direkt; deren Breite/Höhe stimmen innerhalb 2 DIP mit dem echten geöffneten Raster überein. Miniatur bei 100 % tatsächlich mit transparentem Hintergrund gerendert.
- Historisch in 1.3.1: null Window-Deckkraft wird nicht mehr auf einen Mindestwert angehoben. Nativ: App ohne Vorschau/Rundungsregler, Bubbles ohne Rundungsregler; korrektes Scrollen und tatsächliche Griff-/Trefferbreite. Layered-Style und DWM-Hintergrund NONE am echten Ordner-HWND, Alpha null an der Außenecke sowie bei 100 % an Fläche/Rahmen bei weiter sichtbaren Icon-Images. Gerenderter Klar-/Matt-Unterschied über 30 Alpha-Stufen bei identischer gewählter Materialstärke.
- Historisch in 1.3.0: Migration erhält bisherige Fensterfarbe/-transparenz; unabhängige Darstellung und Größen-/Rundungsgrenzen im Datei-Roundtrip. Historisch in 1.3.0: Zahnrad öffnete die Checkbox-Seite, App-/Bubble-/Window-Transparenz und Window-Farbe bleiben getrennt; Größenregler ändert echtes Raster und Icons. Der damalige Regionstest allein erfasste das zusätzliche DWM-Rechteck nicht; deshalb die stärkeren 1.3.1-Tests. Der App-Rundungsregler ist inzwischen entfernt.
- Historisch in 1.2.0: Live-Timer sammelt echte Intervalle, Pause stoppt ihn; Farbkreis mit gültigem/ungültigem HEX, Weiß/Schwarz, Übernehmen/Abbrechen und vollständig sichtbarem Fertig; Raster ohne sichtbare TextBlocks, mit zugänglichen Icon-Namen. Eigenständiger Export-Link und direkte EXE als neue `.lnk`, Staging-Datei im FileDrop, natives COM-CF_HDROP und 32-Byte-Nonce-Stream ohne BinaryFormatter; Abbruch ohne Bestätigung, tatsächliche Fixture-Desktop-Datei per Hash bestätigt, veränderter Inhalt abgelehnt.
- Neu: unsichtbarer Miniaturinhalt bei geöffnetem Raster und Wiederherstellung beim Schließen geprüft; tatsächliche native Rasterposition gegen Anker/Arbeitsbereich geprüft; ausgeblendete Beschriftung bei erhaltenem internen Namen geprüft. Ein Fehler durch nachlaufende WPF-Größenberechnung wurde mit erneuter Positionierung nach `SizeChanged` behoben.
- Nativer Windows-Selbsttest mit zwei Gruppen, echten Shell-Symbolen, physischer Fensterpositionierung und Toolwindow-/Noactivate-Styles.
- Historisch: Child-Einbettung in 1.0/1.0.1 per SetParent geprüft. Ab 1.0.2 durch eigenständige Desktop-Toolfenster mit Desktop-Owner ersetzt.
- Neu: tatsächliche Klickfläche einer Bubble auf einem nicht verdeckten Desktopbereich mit `WindowFromPoint` geprüft. Nach dem vollständigen Schließen der Verwaltung bleibt das Bubble-HWND sichtbar und an derselben Stelle mit der Maus erreichbar; der Bubble-Schalter öffnet das Raster ohne Verwaltungsfenster.
- Neu: eine echte `.lnk` mit ausschließlich geschützten Leerzeichen im Dateinamen erhält den App-Namen aus dem Ziel; die Verknüpfung bleibt erhalten. Die automatische Reparatur wurde zusätzlich mit einer separaten Kopie der vorhandenen sieben Launcher-Einträge überprüft, ohne diese Programme zu starten.
- Hell-/Dunkelansicht und Raster als tatsächliche WPF-Inhalte gerendert und visuell geprüft. Die PNGs erfassen den eigenen WPF-Inhalt, **nicht** den vom DWM zusammengesetzten Hintergrund; sie belegen daher nicht alleine die sichtbare Unschärfe.
- 60 Öffnen-/Schließen-Zyklen nach Framework-Aufwärmung; begrenzte Handle-Differenz und Managed-Speicher-Differenz geprüft. Das ist ein Kurztest und kein Nachweis langfristiger Leckfreiheit.
- `.lnk` über Windows Script Host gelesen und mit Originalargumenten über ShellExecute an eine eigene Testinstanz gestartet; Byte-Hash der Verknüpfung danach unverändert. 210 installierte Apps aus der Shell aufgezählt und Store-Icon abgerufen. Fokuswechsel zu den Einstellungen schließt das App-Raster.
- UI-Zustand gespeichert und erneut eingelesen; lokale Ressourcenmessungen durchgeführt.

Die Rohberichte liegen unter `docs/results`. Der Windows-Selbsttest erzeugt selbst `smoke-results.json`, Diagnose-JSON und Ansichts-PNGs. Ein fehlgeschlagener Selbsttest schreibt `smoke-failure.txt` und einen Fehler-Exitcode. Die Kernprüfungen sind bewusst ein ausführbares Projekt: `dotnet test` ist hier nicht der Testeinstieg, `Build.ps1` beziehungsweise das Test-EXE führt sie aus.

## Manuell noch auszuführen

| Fall | Schritte und erwartetes Ergebnis | Status |
| --- | --- | --- |
| Echte Desktopinteraktion | Gruppe klicken; Miniatur verschwindet, Raster darüber, App starten; Esc/Klick außerhalb bringt Miniatur zurück | Offen für physische Eingabe; Fensterzustände automatisiert |
| Drag & Drop | Hinein, zwischen Gruppen und auf Desktop/Explorer herausziehen; Abbruch testen; Ziel-Link starten; keine EXE oder Vault-Datei verschoben; ohne Bestätigung bleibt Eintrag erhalten | Anwendungsabläufe, native Dateiarbeit und COM-Datenformate automatisiert; physische Explorer-Mausbedienung offen |
| Reihenfolge | Kacheln im Raster ziehen; schließen, erneut öffnen und Anwendung neu starten; Reihenfolge bleibt | Offen |
| Klick außerhalb / Esc | Auf freien Desktop und fremdes Fenster klicken; Esc im Raster drücken; keine hängenden Flyouts | Offen für physische Eingabe; Fokuswechsel wird automatisiert geprüft |
| Vordergrund / Vollbild | Fremde Fenster über Gruppen ziehen; Spiel im Vollbild und randlosen Modus verwenden; kein Topmost-Overlay und kein Fokusdiebstahl | Offen |
| Win+D / Taskleisten-Desktopknopf | Im eingebetteten Modus Gruppen sichtbar, keine Taskleisten-/Alt+Tab-Einträge; separat Rückfallmodus dokumentiert prüfen | Offen |
| Explorer-Neustart | Konfiguration sichern; Windows-Explorer im Task-Manager neu starten; Gruppen und Tray wieder erreichbar, keine beschädigte Konfiguration | Offen; Benutzer-Explorer wurde nicht absichtlich neu gestartet |
| Ab-/Anmelden | Gruppen positionieren, Windows abmelden; nach Autostart Name, Inhalte und Position wieder da | Offen |
| Monitore | Links/rechts/oberhalb, negative Koordinaten, 1080p/4K; Monitor trennen und verbinden; Gruppen bleiben erreichbar | Geometrie automatisiert; echte Hardware offen |
| Gemischte DPI | 100/125/150/200 %, Gruppen und Raster zwischen Monitoren bewegen; scharfe Darstellung und korrekte Trefferflächen | Offen |
| Skalierung live ändern | Windows-Skalierung ändern; Gruppen werden neu aufgebaut und bleiben erreichbar | Offen |
| Store / Netzwerk / UAC | Verschiedene Store-Apps, Netzwerkpfade, spezielle `.lnk`, UAC-Programme und gelöschte Ziele starten | Fehlendes Ziel und Test-.lnk automatisiert; breiter Anwendungstest offen |
| Designs / Barrierefreiheit | Windows-App-Modus wechseln; hohen Kontrast, abgeschaltete Transparenz und reduzierte Bewegung prüfen | Hell/Dunkel gerendert; Systemwechsel und Hilfstechnologien offen |
| Autostart | Schalter aktivieren, Ab-/Anmelden, Hintergrundstart prüfen, Schalter deaktivieren und Run-Eintrag entfernt bestätigen | Offen; Benutzer-Autostart wurde nicht im Test verändert |
| Import beschädigt / fremde Pfade | Fehlerhafte JSON/Bundle und fehlende Ziele importieren; keine Pfadextraktion oder fremde Desktop-Eigentumsrechte | Grenzen und Dateiabläufe automatisiert; Dialogablauf offen |
| Cloud-Desktop / spezielle Links | OneDrive-Desktop, umgeleitete Known Folders, Reparse-/Advertised-/relative Links; unklaren Umzug ablehnen und Original behalten | Offen; konservative Adapterregeln vorhanden |
| Prozess-/Stromausfall | Wegwerf-Datenordner: Prozess an Commit-Grenzen beenden; Kopie nutzbar, Original niemals nachträglich automatisch löschen | Abbruchzustände simuliert; harte Prozess-/Datenträgerausfälle offen |
| Langzeit / Gaming | 2 Stunden Leerlauf und 500 Interaktionen, Handles/RAM verfolgen, Spielbenchmarks mit/ohne App vergleichen | Offen |
| Bubble finden / Zum Desktop | Shell.MinimizeAll, einmalige Wartezeit, Wiederherstellen ohne Aktivierung, gewählte Bubble in Bildschirmmitte | In 1.0.2 automatisiert ausgeführt; normale Überdeckung ebenfalls geprüft |

## Wiederholen

`Build.ps1` führt die Konsolenprüfungen aus. `Test-Windows.ps1` führt den nativen Selbsttest im eigenen Datenordner durch. Für Desktop-Zuordnung und Shell-Fensteroperationen muss der Prozess im normalen interaktiven Benutzerdesktop laufen; eine Entwicklungs-Sandbox kann diese Operationen blockieren. Administratorrechte sollen dafür nicht verwendet werden. Der CI-Workflow ist vorbereitet, aber noch nicht auf GitHub ausgeführt. Rohbericht: `docs/results/windows-smoke-v1.4.0.json`; Konsolensuite: `docs/results/core-tests-v1.4.0.txt`. Ressourcen: `docs/results/resources-v1.4.0-after-interactions.json`.

## Designvergleich

Ausgewählte Referenzen und finale native Eigeninhalts-Aufnahmen sind im [Design-QA-Bericht](../design-qa.md) gegenübergestellt. Verwaltung 660×576 DIP, Farbauswahl 292×360 DIP, Launcher-Raster abhängig vom gespeicherten Größenwert (Standard 396×396 DIP). Die sieben realen Launcher wurden dafür ausschließlich aus einer separaten Datenkopie gelesen, ohne Programmstart oder Desktop-Übernahme. WPF-RenderTargetBitmap bestätigt Inhalt/Layout/Alpha; physische DWM-Komposition und die genannten manuellen Fälle sind damit nicht abgenommen.

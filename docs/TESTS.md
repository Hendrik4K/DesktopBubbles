# Prüfstand

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

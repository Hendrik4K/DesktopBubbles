# Entwicklungsprotokoll zu 1.5.0

Dieses Dokument hält die lokalen Vorschauen und Prüfstände vor der Veröffentlichung von 1.5.0 fest. Die unten genannten Ausgabeordner und Formulierungen wie „noch nicht veröffentlicht“ beschreiben den damaligen Entwicklungsstand. Aktuelle Downloads und Bedienung stehen in der README, der Release-Prüfstand in TESTS.md.

## Darstellung und Bedienung

- Das bereitgestellte Rauchglas-Logo mit neun Kacheln ersetzt die zuvor ausprobierten Frostglas- und Kristallglas-Varianten. Das unveränderte PNG ist die Quelle für das sichtbare Fensterlogo; das daraus konvertierte ICO enthält 16/20/24/32/40/48/64/128/256 px für EXE, Fenster/Taskleiste und Tray. Beide Dateien sind in der Anwendung eingebettet. Herkunft und Aktualisierung stehen unter src/DesktopBubbles/Assets/README.md.
- Die Miniaturen in der linken Gruppenliste stehen an einer gemeinsamen linken Kante. Eine feste Symbolspalte reserviert den restlichen Platz für linksbündige, bei Bedarf umgebrochene Namen.
- Unter Aussehen stehen die Überschrift und die gleich breiten Reiter Miniatur und Ordner mittig. Beide Reiter füllen zusammen die Breite des Inhaltsbereichs und treffen sich unter der Mitte der Überschrift; sie wachsen bei breiterem Fenster mit. Das Zahnrad steht unten rechts neben dem Tracking-Knopf und enthält App-Darstellung und allgemeine Einstellungen. Bei schmalem Fenster blendet die Tracking-Leiste die Verlaufsgrafik aus, damit Messwerte und beide Knöpfe Platz behalten.
- In den Einstellungen entfallen die Reiter Miniatur und Ordner. Ein Zurück-Pfeil links neben der Überschrift oder ein erneuter Zahnrad-Klick führt zur Miniaturansicht. Neben App-Farbe steht der Farbmodus (Hell, Dunkel oder Windows-Standard). Unter Allgemein stehen die Farbkopplung und der rote Knopf Farben zurücksetzen nebeneinander; darunter folgen Windows-Start und weitere Optionen.
- Rechts neben dem Farbmodus öffnet eine quadratische Farbkachel den eingebetteten Editor für die **Modusfarbe**. Hell und Dunkel speichern je eine eigene Grundfarbe für Steuerelemente und Glasflächen; App-Farbe bleibt die separate Tönung beziehungsweise Akzentfarbe. Bei Windows-Standard wird das aktuell aktive Profil bearbeitet. Der Editor ersetzt den Moduswähler vorübergehend durch Anwenden; Abbrechen, Esc und Verlassen verwerfen den Entwurf. Farben zurücksetzen setzt auch beide Modusfarben zurück. Das betrifft ausschließlich DesktopBubbles, nicht die Windows-Systemfarben.
- Neben App-Farbe lässt sich **Bubble-Farben für alle übernehmen** einschalten. Die Farbe der ausgewählten Bubble wird dabei zur gemeinsamen Farbe. Danach ändert die Farbauswahl jeder Bubble alle Miniaturen und Ordner. Die App-Farbe bleibt unabhängig. Ausschalten behält zunächst die gemeinsame Farbe; neue Farbänderungen betreffen wieder nur die ausgewählte Bubble.
- **Farben zurücksetzen** setzt App- und alle Bubble-Farben auf den Standard zurück. Transparenz, Größen, Rundung und die Kopplungswahl bleiben erhalten.
- Miniaturtransparenz gilt für alle Miniaturen, Ordnertransparenz für alle Ordner. Die beiden Werte bleiben unabhängig und reichen bis 100 Prozent Transparenz.
- Miniaturgröße: 48–144 px Kantenlänge bei 100 Prozent Windows-Skalierung. Alle Miniaturen ändern sich gemeinsam. Die Beschriftung behält ihre Schriftgröße. Ordnergröße und Rundung bleiben ebenfalls gemeinsame Einstellungen.
- Der Vorschauknopf schaltet zwischen 1:3 und 1:1 um. 1:1 verändert keine echten Ordnermaße und entspricht der aktuellen Windows-Skalierung. Bei Platzmangel ist die Vorschau scrollbar.
- Gruppenmenüs beginnen mit Umbenennen und Name unter Bubble anzeigen. Auf dem Desktop finden bleibt erhalten. Seltene Ergänzungen liegen unter Hinzufügen. Öffnen, eigene Gruppensymbole, Mini-Icons verwenden und die bisherigen Rücksetz-Einträge entfallen. Miniaturen zeigen ihren Inhalt.
- Das Symbolmenü enthält Starten und **Zurück zum Desktop**. Die geprüfte Rückgabe aus 1.4.1 bleibt erhalten.
- Öffnen beim Darüberfahren ist entfernt, einschließlich des Timers. Zum Desktop entfällt in den Einstellungen.
- Das Tray-Menü enthält die tatsächlich installierte Versionsnummer sowie genau drei Aktionen: Zum Repository, Desktop-Verbindung erneuern und Beenden. Zum Repository öffnet Hendrik4K/DesktopBubbles im Standardbrowser. Ein Doppelklick auf das Tray-Symbol öffnet weiterhin die Verwaltung. Die Versionsanzeige stammt aus dem Build und benötigt keine Online-Abfrage.
- **Ändern** öffnet jetzt einen eingebetteten Farbeditor über den unteren Reglern. Neben dem Farbfeld steht währenddessen **Anwenden**. Farbkreis, Sättigungsregler, Helligkeit und HEX-Eingabe bearbeiten einen Entwurf. Das Farbfeld und die Miniaturvorschau beziehungsweise App-Fläche zeigen ihn live; Modell und gespeicherte Farben bleiben bis Anwenden unverändert. Anwenden übernimmt die Farbe und stellt die normalen Regler wieder her. Abbrechen, Esc, Reiter-/Gruppenwechsel oder Schließen verwerfen den Entwurf. Die Farbkopplung greift erst beim Übernehmen.

## Daten & Werkzeuge

| Aktion | Wirkung |
| --- | --- |
| Sicherung exportieren | Speichert Gruppen, Einstellungen und die referenzierten internen Verknüpfungen als .bubbles-Datei. Installierte Programme werden nicht mitkopiert. |
| Sicherung importieren | Ersetzt den Zustand nach Rückfrage durch die Sicherung; der bisherige Zustand wird zusätzlich gesichert. |
| Verknüpfungen auf Desktop zurücklegen | Legt noch rückgabepflichtige gesicherte Desktop-Verknüpfungen zusätzlich zurück. Die Gruppen behalten ihre Einträge. Sinnvoll vor dem Entfernen der App. |
| Programmordner öffnen | Öffnet den Ordner mit DesktopBubbles.exe. Bei portabler Verwendung enthält dessen Unterordner data die Einstellungen und internen Links. |
| Desktop-Bubbles neu verbinden | Baut die Desktop-Fenster neu auf, beispielsweise nach einem Explorer-Neustart. Gruppeninhalte und gespeicherte Positionen bleiben erhalten. |
| Diagnosebericht speichern | Misst den eigenen Prozess zehn Sekunden lang und schreibt einen lokalen Bericht. Kein automatischer Upload. |

Der Rücklege-Knopf ist eine zusätzliche Wiederherstellung. **Zurück zum Desktop** im Symbolmenü entfernt dagegen den einzelnen Eintrag erst nach erfolgreicher Desktop-Kopie aus seiner Gruppe.

Für eine vollständige Sicherung einschließlich Rückgabehistorie den gesamten Datenordner bei beendeter App kopieren. Exportdateien enthalten nicht sämtliche externen Ziele, Programme oder historische Reserven.

## Datenmodell und Migration

Schema 6 ergänzt LinkBubbleColors und MiniatureSize. Transparenz-Overrides je Gruppe werden nicht mehr verwendet. Beim einmaligen Laden älterer Konfigurationen wird pro Ansicht der bisher wirksame Wert der ersten Bubble übernommen; fehlende Overrides verwenden den bisherigen globalen Standard. Die Gruppenreihenfolge bestimmt die Auswahl, nicht der gerade geöffnete Einstellungsreiter. Danach gelten beide Werte global. Neue Miniaturen sind zunächst 76 px groß, die Farbkopplung startet ausgeschaltet.

LightModeColor und DarkModeColor sind ergänzende optionale HEX-Felder. Fehlende, leere oder ungültige Werte verwenden die bisherigen Paletten unverändert. Eigene Farben erzeugen über ThemePalette eine gemeinsame Steuerelementpalette und einen Glasgrundton. Die Textfarbe wird anhand des höheren Schwarz-/Weiß-Kontrasts gewählt; glasbedingte Durchmischung mit dem Desktop kann den tatsächlichen Kontrast verändern. Windows-Hochkontrast behält seine Systemfarben.

CustomIcon und HoverOpen bleiben als alte JSON-Felder lesbar, werden jedoch nicht mehr dargestellt beziehungsweise ausgeführt. Ordner und Miniatur verwenden weiterhin dieselbe Farbe pro Gruppe, sofern die globale Kopplung aus ist.

Eine mit Schema 6 gespeicherte Konfiguration darf nicht mit Version 1.4.1 geöffnet werden. Die lokale Vorschau deshalb mit einem eigenen Datenordner verwenden und den stabilen Stand aufbewahren.

## Lokal bauen

Aktuelle Vorschau mit Rauchglas-Logo: artifacts/DesktopBubbles-local-preview-smoked-glass mit geprüfter Datenkopie der Kristallglas-Vorschau (52 Dateien per Hash verglichen). Das gelieferte PNG ist unverändert; das ICO enthält neun transparente Auflösungen. Build ohne Warnungen/Fehler und 104 Kernprüfungen bestanden. Das aus der EXE geladene 32×32-Symbol stimmt pixelgenau mit dem neuen ICO überein. Start mit eingebettetem Tray-Symbol und die native Fensteraufnahme unter artifacts/smoked-glass-visual-qa/host-selected.png wurden geprüft. Keine Veröffentlichung.

Aktuelle Vorschau mit Kristallglas-Logo: artifacts/DesktopBubbles-local-preview-crystal mit geprüfter Datenkopie der vorherigen Branding-Vorschau (52 Dateien per Hash verglichen). Das gelieferte PNG ist bytegleich übernommen; das ICO enthält neun transparente Auflösungen. Build ohne Warnungen/Fehler und 104 Kernprüfungen bestanden. Das aus der EXE geladene 32×32-Symbol stimmt pixelgenau mit dem neuen ICO überein. Start mit eingebettetem Tray-Symbol und die native Fensteraufnahme unter artifacts/crystal-visual-qa/host-selected.png wurden geprüft. Keine Veröffentlichung.

Aktuelle Vorschau mit Frostglas-Branding: artifacts/DesktopBubbles-local-preview-branding mit geprüfter Datenkopie der Modusfarben-Vorschau. Build ohne Warnungen/Fehler und 104 Kernprüfungen bestanden. Das aus der fertigen EXE geladene 32×32-Symbol stimmt pixelgenau mit der gelieferten ICO-Auflösung überein; Start mit eingebettetem Tray-Symbol und WPF-Fensterlogo im isolierten Vorschaumodus geprüft. Aufnahme: artifacts/branding-visual-qa/host-selected.png.

Aktuelle Vorschau mit Modusfarben: artifacts/DesktopBubbles-local-preview-mode-colors mit geprüfter Kopie der Daten aus der Reiter-Vorschau (52 JSON-/Verknüpfungs-/Sicherungsdateien per Hash verglichen). Release-Build ohne Warnungen/Fehler, 104 Kernprüfungen und 107 native Ansichts-/Integrationsprüfungen mit AppearanceOnly bestanden. Rohbericht: artifacts/mode-colors-native-20261024-172458/smoke-results.json. Geprüfte native Aufnahmen: docs/screenshots/local-preview-mode-dark.png, local-preview-mode-light.png und local-preview-mode-editor.png. Es wurde nichts veröffentlicht.

Aktuelle Vorschau mit breiten Reitern: artifacts/DesktopBubbles-local-preview-tabs mit geprüfter Datenkopie der Einstellungs-Vorschau. Build und 101 Kernprüfungen bestanden; die native WPF-Aufnahme unter artifacts/tabs-visual-qa/host-selected.png wurde visuell geprüft.

Aktuelle Einstellungs-Vorschau: artifacts/DesktopBubbles-local-preview-settings mit geprüfter Datenkopie der Layout-Vorschau. 101 Kernprüfungen und 100 native Prüfungen im Modus AppearanceOnly bestanden, einschließlich beider Rückwege, Farbmoduswechsel und bestehender Farbkopplung/Rücksetzung. Die eigene WPF-Aufnahme der neuen Einstellungen wurde geprüft. Rohbericht: artifacts/settings-native-20261024-170609/smoke-results.json.

Die aktuelle Layout-Vorschau liegt unter artifacts/DesktopBubbles-local-preview-layout. Sie enthält eine geprüfte Datenkopie der vorherigen Sidebar-Vorschau. Der Layout-Build und 101 Kernprüfungen sowie 96 native Prüfungen im Modus AppearanceOnly bestanden. Die Position des Zahnrads neben Tracking wurde über die echte Navigation geprüft und die native Ansicht kontrolliert. Rohbericht: artifacts/layout-native-20261024-165555/smoke-results.json.

```powershell
.\Build.ps1 -NoZip -OutputName DesktopBubbles-local-preview
.\Test-Windows.ps1 -Executable .\artifacts\DesktopBubbles-local-preview\DesktopBubbles.exe
# Ansichtsprüfung ohne Desktop-Klickflächen-/MinimizeAll-Tests:
.\Test-Windows.ps1 -Executable .\artifacts\DesktopBubbles-local-preview-color\DesktopBubbles.exe -AppearanceOnly
```

Der native Selbsttest verwendet eigene Testgruppen und einen isolierten Fixture-Desktop. Er übernimmt keine Benutzerverknüpfungen und startet keine fremden Programme.

## Prüfstand · 24.10.2026

Release-Build ohne Warnungen und Fehler, 101 Kernprüfungen und 101 native Windows-Prüfungen im abschließenden Lauf bestanden. Geprüft sind unter anderem Kopplung an/aus über die echte Checkbox und Farbauswahl, unveränderte App-Farbe, beide gemeinsamen Transparenzen, Miniaturgröße an allen Desktop-Fenstern, Ordnergröße und Transparenz an der zweiten Gruppe, Originalmaße der Vorschau sowie Scrollen bis zu beiden verdeckten Rändern. Kernprüfungen decken zusätzlich die einmalige Schema-5-Migration, Grenzen der Miniaturgröße, Speicherung und Farbreset ohne Änderung anderer Darstellungswerte ab.

Ein früher Testablauf griff vor dem nächsten WPF-Layoutdurchlauf auf neu erzeugte Steuerelemente zu. Diese Testschritte warten jetzt auf das Layout; der abschließende vollständige Lauf bestand. Physische Explorer-Drag-&-Drop- und Mehrmonitor-/DPI-Prüfungen bleiben weiterhin offen.

Die Bilder unter docs/screenshots/local-preview-* sind Aufnahmen des eigenen WPF-Inhalts und zeigen nicht den vom Desktop zusammengesetzten Hintergrund. Rohberichte: docs/results/windows-smoke-local-preview.json und core-tests-local-preview.txt.

## Ergänzung: eingebetteter Farbeditor

Lokale Ausgabe: artifacts/DesktopBubbles-local-preview-color. Die vorherige Vorschau war während des Builds geöffnet, deshalb wurde diese Ausgabe separat gebaut und mit einer geprüften Kopie ihrer aktuellen Daten vorbereitet. Die bestehende Datensammlung wurde nicht ersetzt. Der Editor benötigt kein zusätzliches HWND; ColorPickerPanel ist ein WPF-Steuerelement im Detailbereich. Verdeckte Regler sind nicht bedienbar. Entwürfe liegen nur im Steuerelement und werden nicht automatisch gespeichert.

101 Kernprüfungen sowie 96 native Ansichts-/Integrationsprüfungen im Modus AppearanceOnly bestanden. Dieser Modus ist im Rohbericht ausdrücklich markiert; er überspringt Desktop-Klickflächenprüfungen, behält aber die übrigen UI-, Datei-, Shell- und Ressourcenprüfungen. Im vollständigen Lauf erreichte WindowFromPoint am Bildschirmmittelpunkt ein anderes Fenster als die Test-Bubble. Dieser Fehler wurde nicht als bestanden gewertet; der vollständige Desktop-Lauf bleibt auf freiem Desktop zu wiederholen.

Zusätzlich geprüft: eingebettete Farbauswahl statt zusätzlichem Fenster, ausgeblendete normale Regler, erreichbare HEX-Eingabe, ungültige Eingabe ohne Übernahme, synchroner Sättigungsregler, unverändertes Modell und gespeicherter Zustand während der Vorschau, Rückkehr zum unveränderten Reglerlayout nach Anwenden, Esc ohne Schließen der Verwaltung und bestehende Farbkopplung. Rohbericht: docs/results/windows-smoke-inline-color.json. Aufnahme: docs/screenshots/local-preview-color-editor.png.

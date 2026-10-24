# Design-QA · DesktopBubbles 1.4.1

1.4.1 behält die folgende Darstellung bei. Das Symbolmenü wurde auf Starten und Aus Gruppe entfernen reduziert; Letzteres gibt eine geprüfte Verknüpfung auf den Desktop zurück. Der vollständige native Wiederholungslauf bestand 92 Prüfungen. Die Bildaufnahmen unten stammen aus 1.4.0 und zeigen die unveränderten Ansichten.

**Result: passed — angefragte Aufteilung, gekoppelte Darstellung, Live-Geometrie und manuelles Tracking im geprüften WPF-Inhalt.** 24.10.2026, native Windows/WPF-App. Kein Browser-Viewport; echte Desktop-/DPI-/Explorer-Eingabeabnahme bleibt zusätzlich offen.

## Finale Aufteilung

Benennung: Miniatur = geschlossene Desktop-Bubble, Ordner = geöffnetes App-Raster. Miniatur enthält Farbe, einmal Glas zwischen Farbe und Vorschau, darunter eigene Transparenz und gemeinsame proportionale Rundung. Ordner enthält seine unabhängige Transparenz, Größe und die tatsächlich mitwachsende Vorschau bei Maßstab 1:3. Das Zahnrad enthält weder Gruppen-Vorschau noch Glas-Duplikat. Tracking startet ausschließlich manuell.

| Finale Aufnahme | Prüfung |
| --- | --- |
| [Miniatur](docs/screenshots/v1.4.0-selected-host.png) | 660×576 DIP; Farbe → kompakte Glaswahl → Vorschau, darunter Transparenz/Rundung. Tracking aus mit Startbutton und ohne angeblich aktuelle Messwerte. |
| [Ordner](docs/screenshots/v1.4.0-selected-window.png) | 660×576 DIP; separate Transparenz und Größe; mitwachsende Vorschau mit 309×307-DIP-Abmessungen und Maßstab 1:3. Keine zweite Farbe/Glas-/Rundung. |
| [Einstellungen](docs/screenshots/v1.4.0-selected-app.png) | App-Farbe/Transparenz, allgemeine Optionen, keine Gruppen-Vorschau und keine Glaswahl. |
| [Offener Ordner](docs/screenshots/v1.4.0-selected-folder.png) | Sieben reale Launcher aus separater Kopie; Icon-Reihenfolge erhalten, proportionale Kontur. |
| [Transparente Fläche](docs/screenshots/v1.4.0-folder-transparent.png) | Null Materialstärke blendet Fläche/Rahmen aus, Icons bleiben. |

WPF-Eigeninhalts-Aufnahmen mit 96-DPI-RenderTargetBitmap, keine physische Desktopaufnahme. Die Fixture liest sieben Launcher, startet sie nicht und verschiebt keine Benutzerdateien. Gaming/Arbeit sind nur Fixture-Namen. App-Farbe und individuelle Transparenzen kommen aus der separaten Kopie. Die tatsächlich verwendete Gruppe bleibt unverändert.

## Verifikation

- [97 Kernprüfungen](docs/results/core-tests-v1.4.0.txt), [87 native Windows-Prüfungen](docs/results/windows-smoke-v1.4.0.json), Build ohne Warnungen/Fehler.
- Schaltfläche vor Start: Tracking starten, Timer aus, Samples 0. Nach Klick echte Intervalle und Tracking stoppen. Stop friert Werte ein; erneuter Start beginnt mit Samples 0.
- Genau eine Glaswahl in Miniatur; Ordner ohne duplizierte Einstellungen. Gemeinsame Farbe wird auf beide Ansichten angewendet.
- Miniatur und Ordner behalten getrennte Transparenzen bis 100 %. Null Alpha im gerenderten Miniatur-Hintergrund sowie Ordnerfläche/-rahmen; Icons bleiben sichtbar.
- Größenregler verändert die Preview-Breite sofort. Preview-Maße entsprechen dem nativen Fenster im Vier-Icon-Test innerhalb 2 DIP. Echte Größe 432×302 DIP, Darstellung im Maßstab 1:3.
- Gemeinsame Rundung ist prozentual auf die kürzere Kante bezogen. Die Verwaltung behält feste dezente Ecken.

Kein offener Layoutfehler in den angefragten Bereichen festgestellt. P3/manuell offen: sehr helle freie Akzentfarben und Textkontrast, Hilfstechnologien, große Schrift, echte DPI-/Monitorwechsel. Lange Ordner können im verfügbaren Bildschirmarbeitsbereich zusätzlich begrenzt werden; Preview-Maße sind nominell bei 100 % Skalierung. Kein öffentlicher Release und keine vollständige Barrierefreiheitsfreigabe.

Nach Test-/Vorschau-Build nur Dokumentation und Paketierung geändert. Historische Berichte folgen.

---

# Archiv: Design-QA · DesktopBubbles 1.3.1

**Result: passed — für die angefragten Änderungen an Ordnerfläche, Transparenz, Reglern und Scrollbar im geprüften eigenen Inhalt.** 24.10.2026, native Windows/WPF-Anwendung. Kein Browser-Viewport. Physische Desktop-Komposition, Hochkontrast, gemischte DPI und Explorer-Mauseingabe brauchen weiterhin manuelle Abnahme.

## Korrektur nach Benutzerfeedback

Die 1.3.0-Tests einer nativen Fensterregion waren als Nachweis für den gesamten sichtbaren Hintergrund unzureichend. Der Benutzer zeigte ein zusätzliches eckiges DWM-Rechteck hinter dem runden WPF-Inhalt. Geöffnete Ordner und Farbwahl verwenden jetzt eine transparente Alpha-Oberfläche ohne DWM-Systembackdrop. Die eigene Fläche und ihre Lichtkante werden gemeinsam begrenzt. Die Verwaltung verwendet feste dezente DWM-Rundungen; Mini-Bubbles haben feste 20 DIP. Nur die offenen Ordner behalten einen Rundungsregler.

## Finale Bilder

Aufnahmen aus der final getesteten EXE mit separater Datenkopie, ohne Launcher-Start oder Desktop-Dateiänderung. Host 660×576 DIP, Farbwahl 292×360 DIP. Die gespeicherte Benutzergröße ergibt in dieser Fixture ein 344×344-DIP-Raster mit Radius 52. App-Farbe Gelb, sieben Launcher und Reihenfolge wurden aus der aktuellen Kopie übernommen.

| Aufnahme | Beobachtung |
| --- | --- |
| [Einstellungen](docs/screenshots/v1.3.1-selected-app.png) | App-Farbe/Transparenz bleiben; keine Ordner-Vorschau und kein Rundungsregler. Schmale Scrollbar, Allgemein/Werkzeuge scrollbar. |
| [Window](docs/screenshots/v1.3.1-selected-window.png) | Eigene Vorschau, Transparenz bis 100 %, Größe und einzige anpassbare Eckenrundung. |
| [Klar](docs/screenshots/v1.3.1-folder-clear.png) / [Matt](docs/screenshots/v1.3.1-folder-matte.png) | Material unterscheidet sich in Dichte und Tönung. Klar hat schwächere farbigere Fläche, Matt dichtere neutralere Fläche. Kein echter Desktop-Blur im Ordner. |
| [100 % transparent](docs/screenshots/v1.3.1-folder-transparent.png) | Nur Icons bleiben sichtbar; sämtliche freien Pixel, Fläche und Rahmen sind transparent. Ein schwarzer Bildhintergrund im Betrachter ist keine gezeichnete Ordnerfläche. |
| [Farbwahl](docs/screenshots/v1.3.1-selected-picker.png) | Kreis, Helligkeit, HEX/Fertig innerhalb des eigenen Fensters. |

WPF-RenderTargetBitmap bestätigt eigene Pixel/Alpha. Die Kombination aus echtem WS_EX_LAYERED, DWM_BACKDROP=NONE und null Alpha an Ecken/Fläche/Rahmen ist gezielt stärker als der frühere reine Regionstest. Eine physische Desktopaufnahme wird damit weiterhin nicht behauptet.

## Nachweise und verbleibende Grenzen

- [93 Kernprüfungen](docs/results/core-tests-v1.3.1.txt), [80 native Windows-Prüfungen](docs/results/windows-smoke-v1.3.1.json), Build ohne Warnungen/Fehler.
- Tatsächlicher ScrollViewer: 10 DIP Trefferbreite, 4 DIP sichtbarer Griff. ScrollToEnd erreicht die Werkzeuge; keine Pfeile oder Track-Fläche.
- Klar/Matt bei identischer eingestellter Materialstärke: gemessene Hintergrund-Alpha 77 gegenüber 160 (8 Bit). Bei 100 % Transparenz null, Icon-Images weiter mit Opacity 1.
- Fenster-HWND des Ordners meldet WS_EX_LAYERED und DWM_SYSTEMBACKDROP_TYPE=NONE. Die Verwaltung bleibt im nativen DWM-Modus.
- Bei 100 % sind freie transparente Pixel auch eingabedurchlässig. Drops auf Icons oder die Mini-Bubble verwenden; leere transparente Ordner lassen sich mit Esc schließen.
- Manuell offen: tatsächlicher Host-Rand auf weiteren Windows-Konfigurationen, Mehrmonitor/DPI/hoher Kontrast, sehr helle benutzerdefinierte Akzente und Hilfstechnologien. Dies ist keine vollständige Barrierefreiheits-/Releasefreigabe.

Nach finalem Build/Windows-Test und Screenshotlauf nur Dokumentation/Paketierung geändert. Historische Berichte folgen.

---

# Archiv: Design-QA · DesktopBubbles 1.3.0

**Result: passed — im geprüften eigenen WPF-Inhalt und den nativen Fenster-/Reglerabläufen.** 24.10.2026. Native Windows-Anwendung; kein Browser-Viewport. Physische DWM-Glaswirkung, gemischte DPI und echte Explorer-Mauseingabe bleiben manuell abzunehmen.

## Nutzerwunsch und Umsetzung

Weiterentwicklung der gewählten Variante 3/Farbkreis 1 anhand des Feedbacks zu 1.2.0: Zahnrad statt App-Texttab, Bubbles für Mini-Ordner, Window für das geöffnete Raster; getrennte Farbe/Transparenz und Größe, weichere Ecken und Rundungsregler. Die bisherigen …-Optionen sind im scrollbaren Zahnrad-Bereich als Checkboxen beziehungsweise Werkzeugbuttons zugänglich. Kein …-Knopf mehr in der Titelleiste.

| Finale Aufnahme | Größe und Prüfung |
| --- | --- |
| [Bubbles](docs/screenshots/v1.3.0-selected-host.png) | 660×576 DIP, Zahnrad/Bubbles/Window, eine Farbprobe, Transparenz und Rundung. Bestehende 84-%-Transparenz aus einer separaten Kopie korrekt übernommen. |
| [Window](docs/screenshots/v1.3.0-selected-window.png) | 660×576 DIP, eigene Farbe/Transparenz, Größen- und Rundungsregler. Glas am unteren Scrollrand zugänglich. |
| [Zahnrad](docs/screenshots/v1.3.0-selected-app.png) | 660×576 DIP, eigene App-Darstellung; Allgemein und Daten & Werkzeuge darunter scrollbar. Fünf native Checkboxen bestätigt. |
| [Ordner](docs/screenshots/v1.3.0-selected-folder.png) | 396×396 DIP, sieben Launcher ohne sichtbare Texte, Radius 40 DIP statt 26. |
| [Farbwahl](docs/screenshots/v1.3.0-selected-picker.png) | 292×360 DIP, Farbkreis, Helligkeit, HEX und vollständig sichtbares Fertig. |

Eigene WPF-Aufnahmen aus der final getesteten EXE, RenderTargetBitmap bei 96 DPI. Sie erfassen Inhalt und Alpha, nicht den physisch komponierten DWM-Hintergrund. Vorschau nutzt nur kopierte Daten, Fixture-Namen Gaming/Arbeit, keinen Start eines Launchers und keine Desktop-Übernahme. Die echte Konfiguration bleibt unverändert.

## Behobene Punkte

- P2: Nur der WPF-Rand war rund, während Windows außen seine eigene weniger gerundete Fläche zeichnete. Jetzt sind Inhaltsclip und native Fensterregion abgestimmt. GetWindowRgn/PtInRegion bestätigt ausgeschnittene Außenecken sowie veränderte Kontur beim App-Rundungsregler.
- P2: Der erste native Test zeigte, dass WindowChrome im Vollglasmodus die Region zurücksetzt. GlassFrameThickness=0 trennt Resize-/Konturverwaltung von der separat gesetzten DWM-Hintergrunderweiterung. Der abschließende native Test besteht.
- P2: Bisher änderte ein gemeinsamer Wert Mini-Bubble und geöffnetes Raster gleichzeitig. Schema 4 übernimmt alte Werte einmalig und speichert unabhängige Window-Werte. UI-Tests bestätigen getrennte Farbe/Transparenz sowie echtes Skalieren von Raster und Icons.
- Testkorrektur: Direkt nach Ersetzen einer WPF-Seite war deren Farbwähler-Anker noch nicht an die PresentationSource angeschlossen. Der Test wartet auf den Layout-Dispatcher wie ein realer Klick nach dem Rendern; kein verzögernder Produktionstimer ergänzt.

Keine offenen P0/P1/P2-Abweichungen im geprüften eigenen Inhaltslayout. P3: Auf der kompakten Grundhöhe werden Allgemein/Werkzeuge und der untere Glas-Regler über Scrollen erreicht. Tabs bleiben währenddessen sichtbar. Öffentliche Freigabe, physische Glaswirkung, sehr große Schrift und echte DPI-/Monitorwechsel sind damit nicht bestätigt.

## Nachweise

[92 Kernprüfungen](docs/results/core-tests-v1.3.0.txt), [74 native Windows-Prüfungen](docs/results/windows-smoke-v1.3.0.json), [Ressourcenbericht](docs/results/resources-v1.3.0-after-interactions.json). Build ohne Warnungen oder Fehler. Nach dem abschließenden EXE-/Vorschautest nur Dokumentation/Pakete bearbeitet. Historischer Vergleich zur ursprünglichen Bildreferenz folgt.

---

# Archiv: Design-QA · DesktopBubbles 1.2.0

**Result: passed — für den geprüften eigenen WPF-Inhalt und die automatisierten Interaktionen.** Datum: 24.10.2026. Dies ist eine native WPF-Anwendung. Browserprüfung ist nicht anwendbar. Physische Windows-Eingabe, tatsächliche DWM-Hintergrundkomposition und weitere DPI-/Monitor-Kombinationen bleiben offene manuelle Prüfungen, siehe [TESTS](docs/TESTS.md).

## Quelle und Prüfaufbau

Benutzerauswahl: Hauptlayout Variante 3 plus kompakte Farbauswahl Variante 1. Referenzen: [Hauptlayout](docs/design/selected-main-layout.png), [Farbkreis](docs/design/selected-color-wheel.png). Der zweite Referenzentwurf ist eine 1488×1056-Desktop-Szene; das darin gezeichnete Verwaltungsfenster ist ungefähr 830×720 Pixel groß. Normalisiert mit etwa 125,6 % ergibt sich die native Basis 660×576 DIP. Kein Nachbau des Hintergrundbildes oder externer Produktlogos als neue Assets; Launcher-Icons kommen aus der vorhandenen Windows-Shell.

Finale native Aufnahmen aus der getesteten Release-EXE, 96-DPI-RenderTargetBitmap, eigene WPF-Inhalte einschließlich Alpha:

| Ansicht / Viewport | Referenz und Prüfergebnis |
| --- | --- |
| [Verwaltung 660×576](docs/screenshots/v1.2.0-selected-host.png) | Schmale Gruppenleiste, große Aussehen-Überschrift, App/Bubbles-Umschaltung, eine Farbprobe mit Ändern, Transparenz, Glas und kompakte Messleiste vollständig sichtbar. |
| [Farbkreis 292×360](docs/screenshots/v1.2.0-selected-picker.png) | HS-Kreis, Helligkeit, HEX und Fertig passen ohne Abschneiden. Kreisposition stimmt mathematisch zur tatsächlichen Farbe; Referenzmarker war nicht farbmetrisch konsistent. |
| [Geöffnetes Raster 396×396](docs/screenshots/v1.2.0-selected-folder.png) | Sieben echte Launcher in drei Spalten, Symbole bis 72 DIP, keine sichtbaren Namen/Überschrift/Anzahl/Plus/Hinweistexte. |
| [Dunkle Verwaltung](docs/screenshots/v1.2.0-settings-dark.png) | Inhalt und Bedienelemente vorhanden; Systemkontrast/physischer Hintergrund separat abzunehmen. |

Die Vorschau verwendet ausschließlich eine Kopie der bisherigen sieben Launcher. Gaming/Arbeit sind Fixture-Namen; die Benutzerkonfiguration wird dafür nicht verändert. Kein Launcher wurde geöffnet. Die WPF-Aufnahmen zeigen keine DWM-Komposition, keinen realen Desktop-Hintergrund und keinen Betriebssystem-Fensterschatten. Eine flächigere Darstellung auf weißem Bildhintergrund ist deshalb kein Nachweis fehlender Desktop-Transparenz. Das Material enthält tatsächlich Alpha; sichtbare Blur-/Reflexionswirkung muss am Desktop geprüft werden.

## Iterationen und behobene Abweichungen

| Priorität | Beobachtung | Korrektur / Nachweis |
| --- | --- | --- |
| P2 | Erste Aufnahme hatte schwarze Beschriftung auf violettem aktivem Tab/Fertig. | Globalen TextBlock-Foreground entfernt, weiße Button-Farbe wird geerbt; finale Aufnahmen geprüft. |
| P2 | Native Standard-ComboBox war eckig gegenüber den übrigen Flächen. | Eigenes abgerundetes Template, Popup-Breite an Element gebunden; finale Verwaltung geprüft. |
| P2 | Erste 324×324-Folder-Version mit 56-DIP-Icons war gegenüber der normalisierten Referenz zu klein. | Standard TileSize 116, Raster 396×396 und Icons bis 72, Shell-Bitmap bis 128 Pixel. Frische Schema-2-Fixture migriert; finale Aufnahme geprüft. |
| P2 | Farbdialog musste Fertig und HEX gleichzeitig innerhalb seiner Grenzen halten. | 292×360-DIP-Layout, nativer Bounds-Test und visuelle Aufnahme bestätigen vollständigen Inhalt. |

Keine verbleibenden P0/P1/P2-Abweichungen im geprüften App-Inhaltslayout festgestellt. Das ist keine vollständige Windows-Kompatibilitäts- oder Barrierefreiheitsfreigabe.

## Bewusste Anpassungen / offene Punkte

- P3: Native Toolfenster-Farbauswahl ohne dekorative Pfeilspitze; Position wird am Arbeitsbereich begrenzt. Vereinfachte Titelleiste mit Einstellungen und Schließen statt gezeichneter Min-/Max-Symbole.
- P3: Eigene Glasverläufe und Windows-Acrylic statt einer Nachbildung physikalischer Lichtbrechung. Tatsächliche Systemtransparenz und hoher Kontrast müssen physisch geprüft werden.
- Benchmark enthält funktionale reale Werte und Verlauf, keine dekorativen Referenzwerte und keine erfundene Genauigkeitsquote. Ein Startbild nach zwei Sekunden enthält erwartungsgemäß wenig Verlauf.
- Physisches Drag & Drop zu Desktop/Explorer, Tooltip-/Tastaturabläufe, gemischte DPI, große Schrift und echte Bildschirmränder stehen in der manuellen Abnahme. Native Datenformate, Speichersicherheit, zugängliche Namen und geometrische Platzierung sind automatisiert geprüft.

## Validierung

Release-Build ohne Warnungen/Fehler; [89 Kernprüfungen](docs/results/core-tests-v1.2.0.txt), [65 native Windows-Prüfungen](docs/results/windows-smoke-v1.2.0.json), [Ressourcenbericht](docs/results/resources-v1.2.0-after-interactions.json). Der abschließende Screenshotlauf verwendet dieselbe getestete EXE; danach wurden nur Dokumentation und Paketierung geändert.

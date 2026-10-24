# Architektur · 1.4.0

DesktopBubbles ist eine lokale Windows-Anwendung. Desktop-Bubbles, geöffnetes App-Raster und Verwaltung sind getrennte Fenster. Das Schließen der Verwaltung lässt die Anwendung im Tray weiterlaufen. Die Dateiverwaltung liegt hinter eigenen Anwendungsports.

## Projekte

```text
DesktopBubbles.Core
        ↑
DesktopBubbles.Application
        ↑
DesktopBubbles.Infrastructure
        ↑
DesktopBubbles (WPF + Windows-Adapter + Composition Root)
```

| Projekt | Verantwortung |
| --- | --- |
| Core | Gruppen, Einstellungen, Zieltypen, portable Link-IDs, Bildschirmgeometrie, RGB/HSV-Farben und zeitgewichtete CPU-Serie; keine WPF-/COM-/Persistenzabhängigkeit |
| Application | ShortcutManager: Übernahme, letzte Referenz entfernen, Gruppe entfernen, Transfer zwischen Gruppen, bestätigter Export, Recovery; Ports IShortcutVault und IShortcutPlatform; Commit-Reihenfolge |
| Infrastructure | StateStore: JSON, Migration, atomare Speicherung und Backup; ShortcutVault: Kopien und Wiederherstellungsjournal; ConfigurationBundle: begrenzter ZIP-Leser |
| DesktopBubbles | Fenster, Styles, Lebensdauer, Windows-Shell/DWM/COM und Composition Root. WindowsShortcutPlatform implementiert den Windows-Port. |
| Tests | Konsolenprüfungen gegen Modell, Anwendungsabläufe und echtes Dateisystem mit austauschbarem Windows-Port |

App.OnStartup verbindet die Implementierungen. Views dürfen keine Desktop-Originale löschen. Einfache Namens-, Positions- und Anzeigeänderungen laufen weiterhin über App.Changed; die Oberfläche ist noch kein vollständiges MVVM-System. Targets.Exists ist eine explizite Verfügbarkeitsprüfung, keine laufende Überwachung.

## Fenster und Desktop

Das Miniraster ist immer 3×3, mit höchstens neun Original-Icons. ShowName betrifft nur die geschlossene Beschriftung; interner Name, Verwaltung und Tooltip bleiben erhalten. Das geöffnete Raster enthält seit 1.2.0 keine sichtbaren Texte oder Plus-Schaltflächen. Standard: drei Spalten, TileSize 116 DIP, Icons bis 72 DIP; sieben Einträge ergeben 396×396 DIP. Beim Öffnen wird der WPF-Inhalt der Mini-Bubble transparent und für Eingaben inaktiv. HWND und physische Ankerkoordinaten bleiben erhalten. Placement.Flyout zentriert das Raster über dem Anker und begrenzt es auf den Monitorarbeitsbereich. SizeChanged positioniert nach endgültigem WPF-Layout erneut. Jede Close-/Rebuild-/Fehlerroute stellt die Mini-Bubble wieder her.

SettingsWindow ist eine native 660×576-DIP-Verwaltung mit Gruppenleiste und Zahnrad-/Miniatur-/Ordner-Inspector. Farbe, Glas und Rundung liegen ausschließlich unter Miniatur, die separate Ordnerseite enthält Transparenz, Größe und Live-Vorschau. ColorPickerWindow (292×360 DIP) erzeugt den HS-Kreis einmal als Frozen Bitmap, ergänzt Helligkeit und HEX. Farbe wird erst mit Fertig übernommen. Globaler App-Tint und gruppenbezogener gemeinsamer Ordner-Tint werden getrennt gespeichert; beide Gruppenansichten behalten eigene Opacity. Eigene Alpha-Verläufe werden beim Ändern aktualisiert; es gibt keine kontinuierliche Shader-/Render-Schleife.

Seit 1.0.2 sind Bubbles eigenständige transparente WPF-Top-Level-Fenster. GetShellWindow und GWLP_HWNDPARENT ordnen den Desktop als Besitzer zu. Toolwindow-/Noactivate-Styles verhindern Taskleisten-Eintrag und Aktivierung. Kein Topmost: normale Anwendungen können sie überdecken. Die frühere Child-Einbettung in Progman/WorkerW war beim Benutzer unsichtbar und wird nicht mehr zur Darstellung verwendet.

Bubble finden ruft Shell.Application.MinimizeAll auf, wartet einmalig auf Explorer, stellt eigene Fenster ohne Aktivierung wieder her und setzt die gewählte Bubble in die Hauptmonitormitte. Zum Desktop ändert keine Positionen. Der Selbsttest stellt fremde Fenster anschließend mit UndoMinimizeAll wieder her.

Ein eigenes unsichtbares HWND empfängt TaskbarCreated und WM_DISPLAYCHANGE. Zerstörte Bubble-Fenster melden ebenfalls Wiederherstellung an. Ein einmaliger Timer bündelt Ereignisse und wartet begrenzt bei fehlender Shell. Ein Mutex pro Datenpfad verhindert konkurrierende Instanzen. Win+D, virtuelle Desktops, Explorer-Neustart und gemischte DPI bleiben manuell abzunehmen; die Desktop-Zuordnung bleibt experimentell.

## Daten und Anwendungsabläufe

Schema 5 ergänzt CornerRoundness (0–50 %) und vereinheitlicht Miniatur-/Ordnerfarbe. Color/BubbleColor sind kanonisch, WindowColor bleibt als gespiegeltes Kompatibilitätsfeld erhalten. Schemas 1–4 werden akzeptiert; explizite Miniaturfarbe hat Vorrang, sonst wird die alte Ordnerfarbe übernommen. Miniatur-/Ordner-Opacity bleiben getrennt und erlauben 0–.95. Alte Ordner-Rundung wird in einen Größenanteil umgerechnet. App-Rundung bleibt fest 8 DIP.

AppearanceGeometry.Radius berechnet den gemeinsamen Anteil der kürzeren Kante. BubbleCornerRadius/WindowCornerRadius sind abgeleitete Kompatibilitätswerte; die echte Ordnerkontur berechnet ihren Radius aus ActualWidth/ActualHeight. AppearanceGeometry.Folder liefert nominelle Breite (Spalten × TileSize + 48) und Höhe (Zeilen × TileSize + 46, maximal 640 DIP). Ein leerer Ordner behält zwei Zeilen. Die Live-Vorschau verwendet diese Geometrie und echte Icon-Positionen bei festem Maßstab 1:3. Monitorbegrenzung/DPI können das tatsächliche Fenster zusätzlich einschränken.

Schema 4 führte zuvor getrennte WindowColor/WindowOpacity ein. Die Farbe ist in Schema 5 wieder gemeinsam; die Transparenzen bleiben unabhängig.

Schema 3 ergänzte AppColor, BubbleColor, AppOpacity und optionale Color/Opacity je Gruppe. Schemas 1/2 werden gelesen; bisherige Standardwerte (System, vier Spalten, TileSize 92 und Opacity .78) erhalten die neuen Vorgaben (Light, drei, 116, .35). Abweichende Individualwerte bleiben erhalten. Schema 2 führte ShowName, MoveDesktopShortcuts und bubble-link:<GUID>.lnk/.url ein. Vorhandene externe Ziele bleiben bis zur ausdrücklichen Übernahme externe Referenzen. Unbekannte Versionen werden abgelehnt. Alte Programmstände dürfen denselben migrierten Datenordner nicht öffnen. Farben werden normalisiert; Material-Opacity ist begrenzt, damit Bubbles durchscheinend bleiben. Hoher Kontrast ist eine bewusste Ausnahme.

Normale UI-Änderungen verwenden einen einmaligen 500-ms-Speichertimer. Übernahme und Entfernen speichern dagegen sofort in der erforderlichen Reihenfolge. StateStore schreibt maximal 8 MiB in eine temporäre Datei, verwendet Flush(true), atomaren Ersatz und .bak. Beschädigte Dateien bleiben als .damaged-* erhalten. Details zu Transaktionen, Rückgabe, Import und Grenzen: [SHORTCUTS.md](SHORTCUTS.md).

Die gespeicherte .lnk wird bytegetreu per ShellExecute gestartet; Argumente und Arbeitsverzeichnis werden nicht in eigene Befehlszeilen umgeschrieben. Hinzufügen und Import starten nichts. Store-Einträge behalten ihre AppUserModelID. Opaque verwaltete IDs erlauben einen Umzug des Datenordners; externe Programme bleiben installationsabhängig.

ShortcutDrag kapselt den Windows-OLE-Austausch: CF_HDROP enthält ausschließlich eine erzeugte Staging-Verknüpfung; interne Transfers verwenden einen kurzlebigen UTF-8-Nonce als Stream. BinaryFormatter-Deserialisierung ist deaktiviert. ShortcutManager.Transfer verschiebt nach erfolgreichem Commit ohne Desktop-Rückgabe. CompleteExport entfernt erst nach bestätigter externer Übergabe; bei letzter Referenz beendet ConfirmExport die ursprüngliche Rückgabepflicht. Die Nutzdaten bleiben für alte Sicherungen erhalten. Details: [SHORTCUTS.md](SHORTCUTS.md).

## Glas, Icons und Ressourcen

Eigene statische Glasverläufe, Lichtkanten und Windows-Acrylic bilden die Optik. Keine Apple-Code-/Grafikdateien und keine externe Glasbibliothek. Das ist eine eigene glasartige Gestaltung, keine Implementierung von Apples Liquid Glass.

Geschlossene Bubbles verwenden keinen Blur. Die Verwaltung verwendet dokumentiertes [DWMWA_SYSTEMBACKDROP_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) mit transientem Backdrop und DWM-Standardrundung. WindowChrome übernimmt ihre Größenänderung, die eigene Inhaltskontur ist fest 8 DIP. Windows entscheidet über den Effekt; Fallback, hoher Kontrast und reduzierte Bewegung werden berücksichtigt.

Seit 1.3.1 verwenden Raster und Farbwahl AllowsTransparency=true, WindowStyle=None und einen transparenten Fensterhintergrund. Dort wird kein WindowChrome installiert und DWM_SYSTEMBACKDROP_TYPE explizit auf NONE (1) gesetzt. DwmExtendFrameIntoClientArea erhält Nullränder. So existiert kein vom WPF-Clip unabhängiges Hintergrundrechteck mehr. Eigene Fläche, Lichtkante und Symbole werden in derselben Alpha-Oberfläche gezeichnet. Inhaltsclip und native Region folgen Radius und DPI. Bei 100 % Window-Transparenz sind sämtliche Material-Gradienten und die Lichtkante null; Icon-Opacity bleibt eins. Transparente freie Pixel sind auch für Windows-Eingaben transparent, daher Drops auf Symbole beziehungsweise Mini-Bubble.

Die bisherige 1.3.0-Abnahme prüfte eine native Fensterregion, erfasste aber den vom Benutzer anschließend gezeigten rechteckigen DWM-Hintergrund nicht. Die neue Prüfung kontrolliert zusätzlich WS_EX_LAYERED, DWM_BACKDROP=NONE und gerenderte Alpha-Pixel an Ecke, Fläche und Rand. Eine eigene Aufnahme ist weiterhin keine vollständige physische Desktopaufnahme.

Material und Glas: Klar verwendet eine farbigere, schwächere Materialschicht; Matt eine neutrale, dichtere. Alle Lichtkanten skalieren mit der Materialstärke; bei null gibt es auch keinen additiven Rest-Highlight. Das ist eigene Materialtönung, kein Desktop-Blur der geöffneten Ordner. Keine Bildschirmaufnahmen, nachgebildeten Hintergrundfotos oder permanenten Shader. Der Hintergrund der Verwaltung bleibt systemverwaltet. Es gibt kein kontinuierliches Rendering; Animationen enden nach höchstens 450 ms.

Quellen: [WPF AllowsTransparency](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency), [WPF-Technologieregionen](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/technology-regions-overview), [Windows-Rundungen und deren Grenzen](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners), [SetWindowRgn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowrgn). Windows übernimmt erfolgreich zugewiesene Regionen; nur nicht zugewiesene Regionen und Testkopien werden selbst freigegeben. Echte Hardware-/DPI-Abnahme bleibt nötig.

Ein einzelner STA-Hintergrundthread verarbeitet Shell-Icons und App-Namen. Frozen Bitmaps liegen in einem auf 384 Einträge begrenzten Cache. HICON/HBITMAP und COM-Referenzen werden freigegeben. Die installierte App-Liste wird nur auf Anforderung gelesen. Dateiübernahmen sind begrenzt, derzeit jedoch synchron; größere Stapel und langsame Cloud-/Netzwerkpfade können die Oberfläche verzögern. Eine spätere asynchrone Verarbeitung benötigt eine serialisierte Commit-Warteschlange.

LiveMeasurementBar bleibt standardmäßig inaktiv: kein Mess-Timer und kein Process-Wrapper für Tracking vor Benutzerstart. Tracking starten erzeugt eine neue CpuSeries und Baseline; nachfolgende Sekundenticks berechnen CPU-Durchschnitt, RAM und Verlauf. Stoppen nimmt den letzten Teilabschnitt auf, stoppt den Timer und hält die Anzeige fest. Erneuter Start setzt die Serie zurück. Bei minimierter/ausgeblendeter Verwaltung pausiert der Timer ohne Zeit-/CPU-Anteil; Schließen gibt ihn und den Process-Wrapper frei. Keine automatische Wiederaufnahme beim Erzeugen einer neuen Verwaltung.

CpuSeries verwendet zeitgewichteten Mittelwert und Standardabweichung mit konstantem Aggregatspeicher, maximal 90 Verlaufspunkte. RAM ist ein aktueller Working Set, kein Durchschnitt. [Messmethodik](PERFORMANCE.md).

Keine Telemetrie, Benutzerkonten oder eigener Netzwerkdienst. Gestartete Links und Anwendungen können Netzwerkzugriffe auslösen. Logs und Diagnosewerte bleiben im Datenordner. Prüfstand: [TESTS.md](TESTS.md), Messungen: [PERFORMANCE.md](PERFORMANCE.md), Erweiterungsregeln: [DEVELOPMENT.md](DEVELOPMENT.md).

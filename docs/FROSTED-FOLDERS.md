# Milchglas nur für geöffnete Ordner

Verfügbar ab Version 1.7.0.

## Bedienung

Unter **Aussehen → Ordner** sitzt **Milchglas** direkt links neben dem Vorschau-Maßstab. Grün bedeutet eingeschaltet; ausgeschaltet verwendet der Knopf dieselbe neutrale Fläche wie der Maßstab. Die Option gilt für alle geöffneten Ordner. Miniaturen und App behalten ihre eigene Darstellung. Größe und proportionale Eckenrundung bleiben wie bisher gemeinsam eingestellt.

Im Milchglasmodus ignoriert die Ordnerfläche die Miniaturfarbe und den gemeinsamen Klar-/Matt-Stil. Stattdessen erhält sie eine feste neutrale helle beziehungsweise dunkle Tönung, passend zum aktiven Hell-/Dunkelmodus. Die gesamte Transparenzzeile wird ausgeblendet. Die Materialdeckkraft beträgt 55 Prozent. Der Hintergrund wird **beim Öffnen einmal weichgezeichnet übernommen**. Bewegungen unter dem geöffneten Ordner erscheinen erst beim nächsten Öffnen im Glashintergrund; die Symbole bleiben scharf.

Die normale Ordnertransparenz wird dabei nicht überschrieben. Beim Ausschalten erscheint der Regler mit seinem bisherigen Wert wieder. Der Ordner verwendet wieder die gemeinsame Farbe und den Klar-/Matt-Stil aus Miniatur sowie seine gespeicherte normale Transparenz. Änderungen an den gemeinsamen Farben wirken wieder, sobald Milchglas ausgeschaltet ist.

Ohne Milchglas bleiben bis **100 % Transparenz** möglich. Die Vorschau in den Einstellungen zeigt Tönung und Geometrie, keinen zweiten unabhängigen Desktop-Blur innerhalb des App-Fensters.

## Eine gemeinsame gerundete Fläche

Die vorherige Vorschau verwendete den Windows-Systemhintergrund `DWMSBT_TRANSIENTWINDOW`. Auf dem getesteten Windows-Build zeichnet dieser trotz korrekt gesetzter Fensterregion ein Rechteck hinter die gerundete WPF-Oberfläche. Deshalb werden nun **alle Ordner als per-pixel-transparente Fenster** gezeichnet und ihr DWM-Systemhintergrund ausdrücklich abgeschaltet. Ein nativer Regions-Test allein konnte diesen Darstellungsfehler nicht nachweisen.

Beim Öffnen bleibt die eigene Oberfläche zunächst durchsichtig. Nach dem WPF-Rendern und `DwmFlush` wird nur der physische Ordnerbereich mit 36 Pixel Zugabe übernommen. `BitBlt` mit `SRCCOPY | CAPTUREBLT` berücksichtigt auch andere transparente Desktopfenster. WPF weichzeichnet die Aufnahme mit einem Gaussian-Blur von 24 Pixeln und schneidet den Überstand anschließend weg. Aufnahme und neutrale Tönung bilden einen einzigen Hintergrundpinsel der gerundeten Ordnerfläche. Rahmen und Symbole verwenden dieselbe äußere Alpha-Kontur; es existiert kein zweiter Glaslayer außerhalb der Rundung.

Die kleine Desktopaufnahme bleibt ausschließlich im Arbeitsspeicher. Die App speichert oder überträgt sie nicht. Es gibt keine Aufnahme-Abfrageschleife und keine neue Abhängigkeit. Beim Schließen wird die Referenz freigegeben. Bei Größenänderungen des geöffneten Ordners wird die vorhandene Aufnahme angepasst; neu erfasst wird erst beim nächsten Öffnen. Hochkontrast verwendet Systemfarben. Wenn Windows-Transparenz deaktiviert ist, die Bildschirmübernahme fehlschlägt oder die Aufnahme mehr als vier Millionen Pixel umfassen würde, bleibt eine neutrale gerundete Materialfläche. Negative Monitorkoordinaten und die physische Fenstergröße werden berücksichtigt. Echte gemischte DPI-/Mehrmonitor-Hardware bleibt zusätzlich manuell zu prüfen. Die App-Verwaltung behält ihren bisherigen Fensterstil.

`FrostedFolders` und das frühere `FrostedFolderOpacity` sind zusätzliche optionale Einstellungen im bestehenden Schema 6. Fehlen sie, bleibt Milchglas aus und der bisherige Stil unverändert. Der alte Milchglas-Deckkraftwert wird für Kompatibilität weiter eingelesen und erhalten, beeinflusst das feste Material aber nicht mehr. Insbesondere kann ein früherer Nullwert den aktivierten Effekt nicht unsichtbar machen. Farbrücksetzung und Farbkopplung ändern diese Werte nicht.

## Prüfung

Kernfälle prüfen Altdateien ohne neue Felder, unveränderte normale Transparenz, Umschalten, Speichern/Neuladen, feste Materialdeckkraft und Farbreset. Native Fensterprüfungen bedienen den echten Knopf und prüfen ausgeblendete Transparenzzeile, wiederhergestellten Regler, tatsächliche Hintergrundübernahme, deaktivierten DWM-Systemhintergrund, Alpha-Fensterstil, Blur einer synthetischen Schwarzweißkante, **transparente Glaspixel an allen vier Ecken**, starke/Null-Rundung, Größenänderung, Wiederherstellung nach simulierter verlorener Region, Moduswechsel und wiederholtes Öffnen/Schließen. Gespeicherte Milchglas-Testbilder verwenden einen synthetischen Hintergrund, keine Desktopaufnahme des Nutzers.

Offizielle Grundlagen: [BitBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt), [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush), [WPF BlurEffect](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.blureffect).

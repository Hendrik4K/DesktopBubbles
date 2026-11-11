# DesktopBubbles-Branding

Das aktuelle Symbol mit schillernden Regenbogen-Seifenblasen wurde am 11.11.2026 vom Projektinhaber Hendrik4K als PNG für die Verwendung als App-Icon und Logo bereitgestellt. Es ersetzt das bisherige Rauchglas-Symbol mit neun Kacheln.

- DesktopBubbles.ico: aus dem PNG konvertierte Datei für EXE, Fenster/Taskleiste und Tray; 16/20/24/32/40/48/64/128/256 px, jeweils mit Alphakanal.
- DesktopBubbles.png: unveränderte bereitgestellte PNG-Datei für das Logo im Fenster.

Beide Dateien sind eingebettete WPF-Ressourcen. Die Anwendung benötigt die ursprünglichen externen Bildpfade nicht. Das sichtbare Fensterlogo wird zur Laufzeit passend dekodiert; die Quelldatei bleibt unverändert.

Neue Branding-Dateien über tools/Import-Branding.ps1 importieren. PngPath ist erforderlich; IconPath ist optional. Ohne ICO-Vorlage werden neun quadratische Größen mit proportional eingepasstem Motiv erzeugt. Das PNG bleibt unverändert; es erfolgen keine gestalterischen Änderungen. Der bisherige Generator des geometrischen Vier-Kacheln-Symbols wurde entfernt.

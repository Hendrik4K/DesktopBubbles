# Ressourcen und Messmethodik

DesktopBubbles verwendet keine dauerhafte Render-Schleife und kein regelmäßiges Hardware-/Shell-Polling. Seit 1.4.0 startet die Ressourcenmessung ausschließlich durch Tracking starten. Erst danach läuft ein Sekundentimer für den eigenen Prozess bei sichtbarer, nicht minimierter Verwaltung. Stoppen schaltet ihn ab, erneuter Start beginnt mit leerer Serie. Beim Schließen wird er freigegeben. Weitere Timer speichern nach Änderungen, bündeln Wiederherstellungsereignisse oder laufen nur während der Hover-Interaktion. Der Icon-STA wartet blockierend auf Arbeit.

Die nachfolgenden Werte sind lokale Momentaufnahmen und keine Zusage für andere PCs oder Spiele. Die abschließenden Rohberichte stehen unter `docs/results`.

## Version 1.4.1 nach Interaktionen · 24.10.2026

92 native Prüfungen und 60 Öffnungszyklen; Verwaltung und Raster danach geschlossen, Tracking aus. Zehnsekündige lokale Prozessmessung, Windows 10.0.26300.0, .NET 8.0.32, 16 logische Prozessoren.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU alle Prozessoren / ein Prozessor | 0,0293 % / 0,4680 % |
| Working Set / Private Bytes | 224,32 / 126,93 MiB |
| Managed Heap | 10,68 MiB |
| Handles vor/nach Leerlaufmessung | 1082 / 1082 |
| Handles vor/nach 60 Öffnungszyklen | 964 / 964 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 47,09 / 60,67 ms |

Diese Momentaufnahme dient der Einordnung dieses Testlaufs, nicht als Vergleichsmessung zwischen Versionen.
## Version 1.4.0 nach Interaktionen · 24.10.2026

Finale portable EXE nach 87 nativen Prüfungen und 60 Öffnungszyklen. Verwaltung geschlossen, Raster geschlossen; Tracking aus. 10,004 Sekunden, Windows 10.0.26300.0, .NET 8.0.32, 16 logische Prozessoren.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU alle Prozessoren / ein Prozessor | 0,0195 % / 0,3124 % |
| Working Set / Private Bytes | 207,50 / 107,55 MiB |
| Managed Heap | 10,13 MiB |
| Handles vor/nach Leerlaufmessung | 1047 / 1047 |
| Handles vor/nach 60 Öffnungszyklen | 929 / 929 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 45,39 / 70,12 ms |
| GPU / Gaming | Nicht gemessen |

Rohdaten: `resources-v1.4.0-after-interactions.json`, `windows-smoke-v1.4.0.json`. Kurztest, keine Zusage für andere PCs oder langfristige Leckfreiheit. Der separat gestartete Diagnosebericht ist unabhängig vom freiwilligen Tracking. Historische Werte folgen.

## Version 1.3.1 nach Interaktionen · 24.10.2026

Finale portable EXE nach 80 nativen Prüfungen und 60 Öffnungszyklen. Eigene Alpha-Ordnerfläche statt DWM-Backdrop. Verwaltung ausgeblendet, Raster geschlossen. 10,011 Sekunden, Windows 10.0.26300.0, .NET 8.0.32, 16 logische Prozessoren.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU alle Prozessoren / ein Prozessor | 0,0195 % / 0,3122 % |
| Working Set / Private Bytes | 225,10 / 120,40 MiB |
| Managed Heap | 10,25 MiB |
| Handles vor/nach Leerlaufmessung | 1076 / 1076 |
| Handles vor/nach 60 Öffnungszyklen | 956 / 956 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 40,83 / 59,53 ms |
| GPU / Gaming | Nicht gemessen |

Rohdaten: `resources-v1.3.1-after-interactions.json`, `windows-smoke-v1.3.1.json`. Öffnen ist hier teurer als in einigen früheren Kurzläufen; weder die Einblendanimation noch die physische Displaylatenz sind darin vollständig enthalten. Kein Langzeit-Lecknachweis. Die eigene Alpha-Darstellung zeichnet ereignisgesteuert und hat keinen permanenten Blur-/Screenshot-/Render-Timer. Historische Werte folgen.

## Version 1.3.0 nach Interaktionen · 24.10.2026

Finale portable EXE nach 74 nativen Prüfungen und 60 Öffnungszyklen. Verwaltung ausgeblendet, Raster geschlossen. 10,014 Sekunden, Windows 10.0.26300.0, .NET 8.0.32, 16 logische Prozessoren.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU alle Prozessoren / ein Prozessor | 0,0195 % / 0,3121 % |
| Working Set / Private Bytes | 218,34 / 117,10 MiB |
| Managed Heap | 7,12 MiB |
| Handles vor/nach Leerlaufmessung | 1083 / 1083 |
| Handles vor/nach 60 Öffnungszyklen | 963 / 963 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 22,73 / 35,40 ms |
| GPU / Gaming | Nicht gemessen |

Rohdaten: `resources-v1.3.0-after-interactions.json`, `windows-smoke-v1.3.0.json`. Kurztest, keine Langzeit- oder physische Displaylatenz-Zusage. Die folgenden Abschnitte dokumentieren ältere Messungen und die Methodik.

## Version 1.2.0 nach Interaktionen · 24.10.2026

Finale portable EXE, 65 native Prüfungen einschließlich Farbauswahl, Live-Leiste, Datei- und COM-Übergaben; zwei Desktop-Bubbles und 60 Öffnungszyklen. Verwaltung ausgeblendet, Raster geschlossen. Messfenster 10,011 Sekunden, Windows 10.0.26300.0, .NET 8.0.32, 16 logische Prozessoren.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU bezogen auf alle Prozessoren / einen Prozessor | 0,0195 % / 0,3122 % |
| Working Set / Private Bytes | 216,37 / 112,97 MiB |
| Managed Heap | 9,42 MiB |
| Handles vor/nach Leerlaufmessung | 1054 / 1054 |
| Handles vor/nach 60 Öffnungszyklen | 933 / 934 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 27,14 / 46,42 ms |
| GPU und Gaming-Frametimes | Nicht gemessen |

Rohdaten: `resources-v1.2.0-after-interactions.json`, `windows-smoke-v1.2.0.json`. Das ist ein aufgewärmter Kurztest, kein Kaltstart- oder Langzeit-Lecknachweis. Die nachfolgenden älteren Messungen bleiben historische Werte.

## Live-Leiste und Aussagekraft

Die Leiste zeigt nach ausdrücklichem Start ausschließlich den DesktopBubbles-Prozess. Stoppen hält die letzten Werte fest; erneuter Start verwirft alte Intervalle. Vor dem Start werden weder RAM noch CPU als aktuelle Messwerte angezeigt. CPU-Intervalle entstehen aus zusätzlicher `Process.TotalProcessorTime` geteilt durch monotone verstrichene Zeit und Zahl logischer Prozessoren. Angezeigt wird der nach Intervalldauer gewichtete Mittelwert der aktiven Messung, daneben aktueller Working Set in MiB. Die aktive Dauer schließt Pausen und ausgeblendete/minimierte Verwaltung aus; nach Wiederaufnahme beginnt eine neue Baseline. Die Kurve zeigt höchstens 90 letzte CPU-Intervalle mit automatischer Skalierung, keine feste 100-%-Achse.

„Schwankung ±… Prozentpunkte“ ist die zeitgewichtete Standardabweichung der beobachteten Intervalle. Sie ist weder Messfehler noch Konfidenzintervall oder Prozent-Genauigkeit. Mehr Laufzeit erfasst mehr Situationen; Genauigkeit steigt nicht zwangsläufig monoton. Scheduler, Timerauflösung, JIT/GC, App-Interaktion und die kleine Anzeige-Last beeinflussen Werte. Keine GPU- oder Gesamtsystemmessung. Die Anzeige unter zwei Sekunden warnt vor kurzer Messung.

## Version 1.1.0 nach Interaktionen · 24.10.2026

Abschließende portable EXE, zwei eigenständige Desktop-Bubbles, 60 Öffnungszyklen und native Speicher-/Launch-/Rückgabeprüfungen. Verwaltung ausgeblendet, Raster geschlossen. Messfenster 10,014 Sekunden, Windows 10.0.26300.0, .NET 8.0.32, 16 logische Prozessoren.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU bezogen auf alle Prozessoren / einen Prozessor | 0,0195 % / 0,3121 % |
| Working Set / Private Bytes | 223,54 / 152,90 MiB |
| Managed Heap | 6,01 MiB |
| Handles vor/nach Leerlaufmessung | 1074 / 1074 |
| Handles vor/nach 60 Öffnungszyklen | 955 / 956 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 28,41 / 35,42 ms |
| GPU und Gaming-Frametimes | Nicht gemessen |

Rohdaten: `resources-v1.1.0-after-interactions.json` und `windows-smoke-v1.1.0.json`. Der größere Windows-Test lädt auch die neue Speicherverwaltung. Ein zusätzlicher Handle im Kurztest ist kein Langzeit-Lecknachweis. Die folgenden älteren Messungen bleiben als historische Vergleichswerte erhalten; sie sind keine neuen Kaltstartwerte für 1.1.0.

## Portable Ausgabe nach Interaktionen, 23.10.2026

Windows 10.0.26300.0, x64, .NET 8.0.32, 16 logische Prozessoren. Zwei eingebettete Gruppen; nach 60 Öffnen-/Schließen-Zyklen, App-Liste und visuellen Prüfungen. Einstellungsfenster ausgeblendet, App-Raster geschlossen. Messfenster 10,012 Sekunden.

| Messgröße | Ergebnis |
| --- | ---: |
| CPU bezogen auf alle 16 logischen Prozessoren | 0,0293 % |
| CPU bezogen auf einen logischen Prozessor | 0,4682 % |
| Working Set | 210,5 MiB |
| Private Bytes | 114,3 MiB |
| Managed Heap, Momentaufnahme | 5,31 MiB |
| Handles vor/nach der Leerlaufmessung | 1047 / 1047 |
| Handles vor/nach 60 Öffnungszyklen | 911 / 919 |
| Öffnen bis Dispatcher-Idle, Median / Maximum | 24,20 / 41,61 ms |
| GPU, Gaming-Frametimes | Nicht gemessen |

Die WPF-/CLR-/Grafiklaufzeit dominiert den Working Set. Etwa 210 MiB nach dem umfangreichen Test sind **kein** extrem niedriger RAM-Wert. Im Zyklentest kamen acht Handles hinzu; frühere Läufe zeigten keine Differenz. Der Test begrenzt die kurzfristige Differenz, beweist aber keine langfristige Leckfreiheit. Der Icon-Cache ist begrenzt; Working Set und Managed Heap sind unterschiedliche Kennzahlen und dürfen nicht gleichgesetzt werden. Es wird kein künstliches Working-Set-Trimmen verwendet.

## Kaltstart ohne Einstellungsfenster

Dieselbe portable EXE, dieselben zwei gespeicherten Gruppen, kein geöffnetes Raster und kein erzeugtes Einstellungsfenster. Zwei Sekunden nach Start beginnt ein 15,014 Sekunden langes Messfenster.

| Messgröße | Ergebnis |
| --- | ---: |
| Zusätzliche Prozess-CPU-Zeit im Messfenster | 0 ms, innerhalb der Messauflösung |
| CPU bezogen auf alle Prozessoren / einen Prozessor | 0 % / 0 % |
| Working Set | 143,2 MiB |
| Private Bytes | 72,7 MiB |
| Managed Heap, Momentaufnahme | 3,92 MiB |
| Handles vorher / nachher | 867 / 867 |

Das Ergebnis zeigt einen ruhigen kurzen Leerlauf, keine garantierte dauerhafte 0-%-Auslastung. Die noch vorhandenen Laufzeit- und Grafik-Ressourcen gehören zum WPF-Grundbedarf. GPU und Frametimes wurden auch bei diesem Lauf nicht gemessen.

Die Öffnungszeit endet bei Dispatcher-Idle; sie misst weder eine physische Displaylatenz noch die vollständige Dauer der Einblendanimation. Die Tests enthalten explizite GCs für einen vergleichbaren Lebensdauer-Kurztest. Im normalen Leerlauf erzwingt die Anwendung keine GC-Zyklen.

## Selbst messen

1. Gewünschte Gruppen anlegen, Verwaltungsfenster öffnen.
2. Alle App-Raster schließen, **Zahnrad → Daten & Werkzeuge → Diagnosebericht speichern** drücken und zehn Sekunden keine weiteren Aktionen in DesktopBubbles ausführen. Bei sichtbarer Verwaltung enthält der Bericht auch die Last der Live-Leiste; für Vergleichbarkeit pausieren oder einen Hintergrundlauf verwenden.
3. Diagnose-JSON im angezeigten Datenordner prüfen.
4. Für einen Kaltstart bestehende Testdaten mit `--background --measure-idle --data-dir "..."` verwenden: zwei Sekunden Wartezeit, 15 Sekunden Messfenster, danach reguläres Beenden. Bei leerem Datenbestand öffnet die App die Verwaltung; für einen reinen Hintergrundlauf daher Gruppen vorher anlegen.

Der Diagnosebericht berechnet CPU aus der Differenz von `Process.TotalProcessorTime` und gemessener Wanduhrzeit, auf einen Kern und alle logischen Kerne normiert. RAM, Prozess-Handles sowie GDI-/USER-Objekte werden am Ende abgefragt. Im Hintergrund läuft kein Ressourcenmonitor. Kurzintervalle unterliegen Scheduler-, GC- und Timerauflösungseinflüssen; mehrere längere Messungen sind für belastbare Vergleiche nötig.

GPU-Verbrauch extern über Task-Manager beziehungsweise Windows Performance Recorder prüfen. Für Gaming wiederholbare Szenen mit identischen Einstellungen verwenden und mindestens Frametimes, 1%-Lows und 0,1%-Lows vergleichen; sowohl exklusive als auch randlose Darstellung testen. Hierzu wurden noch keine Messwerte erhoben. Auch ein CPU-Leerlauf nahe null beweist keine unveränderten Spiele-Frametimes.

## Optimierungsoptionen

- Verwaltung nach der Einrichtung schließen; dadurch endet auch die Live-Messung.
- Bei Bedarf die klare Tönung verwenden. Reduzierte Bewegung und hoher Kontrast werden berücksichtigt. Hintergrundweichzeichnung ist seit 1.3.0 eine Checkbox unter Zahnrad; Animationsdauer bleibt ohne UI-Regler.
- Gruppen und Icon-Cache bleiben klein; keine Apps oder Netzwerkdateien zum Zweck einer laufenden Statusprüfung abfragen.
- Für Probleme mit neuer Explorer-Struktur den Fenstermodus verwenden und die Einschränkung bei Win+D beachten.

Falls der gemessene WPF-Grundbedarf für den gewünschten Einsatz zu hoch ist, wäre ein späterer Wechsel der geschlossenen Kacheln auf reine Win32-/Direct2D-Fenster eine mögliche weitere Optimierung. Diese Ausgabe bleibt bewusst bei der tatsächlich implementierten und getesteten WPF-Technik.

# Weiterentwickeln

Zuerst [ARCHITECTURE.md](ARCHITECTURE.md), [SHORTCUTS.md](SHORTCUTS.md) und [TESTS.md](TESTS.md) lesen. `DesktopBubbles.sln` in Visual Studio öffnen oder `Build.ps1 -InstallSdk` verwenden. Zum Schutz einer benutzten Ausgabe `-OutputName DesktopBubbles-win-x64-dev` angeben.

Neue Arbeit in getrenntem `--data-dir` testen. Der Benutzerdesktop ist keine Testfixture. Der native Selbsttest konstruiert seinen eigenen Fixture-Desktop unter dem Testdatenordner. Für Modell-/Use-Case-Änderungen die Konsolensuite ausführen; bei Fenster-/COM-/Shell-/Adapteränderungen zusätzlich `Test-Windows.ps1 -Executable <neue EXE>` auf einem interaktiven Windows-Desktop. `dotnet test` startet die Konsolensuite nicht.

## Erweiterungsregeln

- Neue Zieltypen im Core validieren und im Windows-Adapter auflösen/starten. Keine Befehlszeilen aus Benutzertext zusammensetzen. Hinzufügen/Import führen nichts aus.
- Dateiabläufe über `ShortcutManager` und Ports. Eine View darf keine Desktop-Originale löschen. Erst eigene Kopie und persistierter Verweis, dann Inhaltsprüfung und Entfernung.
- Unbekannte Schema-Versionen nicht still akzeptieren. Migrationen mit Fixtures prüfen, Backups erhalten. Neue Assettypen benötigen begrenzte Resolver und eine Export-/Importpolitik.
- Zugängliche Namen erhalten, auch ohne sichtbare Beschriftungen. Hohen Kontrast und reduzierte Bewegung beachten. Jede Open-/Close-/Rebuild-Route stellt die Miniatur wieder her. DIP und physische Pixel unterscheiden.
- Keine neue Explorer-Child-Abhängigkeit als Standarddarstellung und kein Topmost als Sichtbarkeits-Workaround. `IsWindowVisible` oder `WindowFromPoint` allein beweisen keine physische Sichtbarkeit.
- Konfigurationsänderungen laufen aktuell seriell im UI-Thread. Neue Hintergrundjobs dürfen `BubbleState` nicht neben einem Commit verändern. Asynchrone Stapel brauchen einen serialisierten Anwendungsablauf.
- COM-Objekte und native Handles in `finally`/`using` freigeben. Keine permanenten Render-/Polling-Timer; die sichtbare Live-Leiste darf im Sekundentakt messen, muss bei Pause/Verbergen stoppen und beim Schließen freigegeben werden. Cache-Grenzen erhalten.
- Fehlerfälle prüfen: Commit scheitert, Quelle verändert/gesperrt, Recovery vor/nach Entfernung, Namenskonflikt, letzte gemeinsame Referenz, beschädigtes Archiv. Keine Benutzerprogramme, Autostart-Einstellungen oder echten Explorer-Neustarts im automatischen Test verändern.

## Entscheidungen

| Entscheidung | Grund / Folge |
| --- | --- |
| WPF/Win32 | Windows-Shell-Integration ohne Browser-Runtime; messbarer WPF-Grundbedarf bleibt. |
| Anwendungsports | Dateitransaktionen ohne Benutzerdesktop prüfbar; von weiteren Oberflächen wiederverwendbar. |
| Vollständige `.lnk` | Argumente, Arbeitsverzeichnis und Linkdaten bleiben erhalten; unklare Links werden abgelehnt. |
| Opaque IDs | Umzug des Datenordners; kein frei wählbarer Extraktionspfad. |
| Nur persönlicher Desktop als Löschzone | Keine systemweiten Änderungen/UAC-Anforderung; gemeinsame Originale bleiben sichtbar. |
| Nutzdaten aufbewahren | Alte Backups und Rückgabe bleiben nutzbar; Bereinigung wird später ein eigener referenzgeprüfter Anwendungsfall. |
| Eigene Glasoptik und Acrylic | Keine Apple-Assets oder fremde Glasbibliothek; noch keine physikalische Brechung. |

Weitere Schritte: Rückgabeübersicht mit Vorschau, asynchrone Stapel, geprüfte Installation/Deinstallation und zusätzliche Hardwaretests. Sie gehören zur Vorbereitung eines öffentlichen Releases.

## Oberfläche und Visual-QA · 1.2.0

ColorValue und CpuSeries sind reine Core-Logik. ColorPickerWindow, LiveMeasurementBar und ShortcutDrag sind native Adapter. Transfer/CompleteExport bleiben im ShortcutManager; niemals aufgrund von DoDragDrop allein ein Original oder einen Gruppeneintrag löschen. CF_HDROP muss auf eine unabhängige Staging-Verknüpfung zeigen, interne Drag-Kennungen bleiben Streams. Unsafe BinaryFormatter ist deaktiviert.

Für eigene WPF-Aufnahmen `DesktopBubbles.exe --design-preview --data-dir "<separate Kopie>"` verwenden. Dieser Modus verändert Namen und Layout seiner Fixture; niemals den echten Datenordner angeben. Die Aufnahmen erfassen eigenen WPF-Inhalt und Alpha, nicht den DWM-komponierten Desktop. Gewählte Referenzen: `docs/design`; abschließender Vergleich: [design-qa.md](../design-qa.md). Zusätzlich zu einer Layoutprüfung bleibt echte Windows-Eingabe-/Kompositions-QA nötig.

## Darstellung 1.3.0

AppearanceScope unterscheidet App/Zahnrad, Mini-Bubbles und geöffnete Window-Raster. Neue Felder stehen in Schema 4. Migration muss alte gemeinsame Farbe/Opacity einmalig kopieren und darf sie nachher nicht bei jeder Validierung erneut koppeln. Größen- und Rundungswerte begrenzen und nicht endliche Werte abfangen. Bei native Rundung nicht nur Border.CornerRadius prüfen: Region mit GetWindowRgn auslesen und Punkte an der Außenecke prüfen. GDI-Regionen unterliegen dem SetWindowRgn-Eigentumsvertrag. Der WPF-Vollglasmodus darf die Region nicht wieder löschen. Die physische DWM-Darstellung bleibt eine zusätzliche manuelle Prüfung.

## Korrektur 1.3.1: Rechteck hinter Rundung

GetWindowRgn allein hat den DWM-Hintergrund in 1.3.0 nicht erfasst. Geöffnete Ordner und Farbwahl müssen jetzt als Layered Window mit AllowsTransparency=true erstellt werden, bevor ein HWND existiert. Kein DWM-Systembackdrop für diese Fenster. Native Styles/Attribute plus Alpha-Pixel prüfen; nicht allein Border.CornerRadius oder die Region. Bei null Materialstärke müssen auch additive Reflexe und der Rim verschwinden. Klar/Matt sind Materialdichten, keine Desktopaufnahme/Unschärfe. App-/Mini-Bubble-Rundung ist fest. ScrollViewer kann die systemweite Scrollbar-Breite explizit binden; die Ressourcen dafür müssen zur schmalen Vorlage passen. Die echte Griffbreite am PART_VerticalScrollBar testen, nicht irgendeinen versteckten ScrollBar-Nachfahren.

## Darstellung und Tracking 1.4.0

Schema 5 vereinheitlicht Farbe und proportionale Rundung, erhält jedoch beide Opacity-Werte. Kompatibilitätsfelder dürfen beim Validieren nicht die neue kanonische Farbe zurücküberschreiben. Rundung anhand der kürzeren tatsächlichen Kante berechnen. AppearanceGeometry.Folder/Radius liefern die gemeinsame Basis für Preview und Fenster. Live-Vorschau bei Regleränderung nur geometrisch aktualisieren; Shell-Icons nicht erneut laden. Maßstab 1:3 und genaue nominelle Abmessungen anzeigen. Monitorbegrenzung bleibt separat.

LiveMeasurementBar: Standard ist aus. Process-Wrapper erst bei Start erzeugen, neue Serie/Baseline pro Sitzung, beim Stop letzten aktiven Teilabschnitt erfassen und Werte einfrieren. Timer bei Stop/Verbergen/Minimieren stoppen; beim Schließen freigeben. Keine Altwerte beim Neustart einrechnen. Native Tests müssen den Startbutton bedienen und vor dem Start null Samples bestätigen.

# Änderungen

## Unveröffentlicht

- Neue Einstellung **Bubble automatisch schließen** unter „Bubble-Farben für alle übernehmen“, mit Sekundenwahl rechts daneben (1–120 s, Standard 5 s, standardmäßig aus). Ein geöffneter Ordner schließt sich nach dieser Zeit ohne Maus auf ihm; mit der Maus darauf läuft keine Zeit ab. Während Sortieren, Ziehen, offener Menüs oder Dialoge schließt nichts.

- Nach dem Verlegen des Desktop-Ordners (etwa nach `D:\Desktop`) kommen übernommene Verknüpfungen beim Beenden und über „Zurück zum Desktop“ auf den heutigen Desktop. Bisher blieben sie in diesem Fall in der Bubble.
- Herausziehen aus Ordnern funktioniert wieder: Symbol anklicken, ohne Haltepause auf den Desktop ziehen und loslassen; die Verknüpfung landet dort und verlässt die Bubble. Die Zieh-Logik bekam zuvor keine Mausbewegungen, weil der gedrückte Button sie selbst abfing.
- Im Sortiermodus tauschen zwei Symbole ihre Plätze mit einer weichen Federbewegung, das gegriffene gleitet von dort, wo es losgelassen wurde.
- Direktes Ziehen ohne Wackelmodus tauscht innerhalb desselben Ordners keine Plätze mehr; dort zeigt der Mauszeiger „nicht möglich“. Verschieben in eine andere Bubble bleibt möglich.

## 1.8.0 · 12.11.2026

- Neuer Glasstil **Liquid** für Miniaturen und Ordner: Die Glaskante bricht den echten Hintergrund wie eine Linse, mit leichter Farbaufspaltung, Glanzkante von oben links und ruhiger Mitte. Wählbar unter Miniaturen → Glas; hat Vorrang vor Milchglas. Bestehende Konfigurationen starten mit Liquid aus.
- Regler **Liquid-Stärke** direkt unter Transparenz in Miniaturen und Ordner, gemeinsamer Wert, Standard 60 %.
- Miniaturen nehmen ihren Hintergrund einmal pro Platzierung auf und warten, solange ein anderes Fenster darüber liegt. Beim Ziehen wird das Glas live an der aktuellen Stelle neu berechnet (höchstens 30-mal pro Sekunde, nur während des Ziehens).
- Die Miniatur wird für Hintergrundaufnahmen per Windows-Aufnahmeausschluss unsichtbar, bleibt aber auf dem Bildschirm sichtbar: kein Blinken und keine Miniatur im Ordnerglas. In den Desktop eingebettete Miniaturen blenden sich dafür weiterhin kurz aus.
- Ordner öffnen und schließen mit Federanimation: Glas und Symbole wachsen aus der Miniatur, die Symbole fliegen aus ihrem Platz im 3×3-Raster an ihre Position und verformen sich dabei leicht; beim Schließen schrumpft alles vollständig in die Miniatur zurück. Bei deaktivierten Animationen öffnen und schließen Ordner wie bisher sofort.
- Der Look der Miniatur blendet beim Öffnen weich in den des Ordners über (auch in Milchglas) und beim Schließen zurück. Miniatur, Animation und Ordner wechseln im selben Frame und pixelgenau; Hintergrundaufnahmen warten, solange eine Animation läuft. Die Beschriftung der Miniatur blendet weich aus und ein.
- Native Prüfungen warten auf das Ende der Animationen und prüfen zusätzlich, dass zurückfaltende Ordner und Übergangsfenster vollständig schließen und die Symbole exakt auf der Miniatur starten und landen.
- Während des Öffnens und Schließens bricht das Liquid-Glas Frame für Frame den echten Hintergrund unter der aktuellen Form; der Bewegungsweg wird dafür einmal aufgenommen. Der Transparenzregler wirkt auch auf Liquid: Standard zeigt die reine Linse, transparenter blendet sie aus, weniger transparent legt eine milchige Schicht darüber.
- **Desktop-Verknüpfungen kehren zurück, solange DesktopBubbles nicht läuft:** Beim Beenden über das Tray werden übernommene Verknüpfungen vom persönlichen Desktop geprüft zurückgelegt und an ihre gemerkte Position gesetzt; ist der Platz belegt oder unbekannt, ordnet Windows sie selbst ein. Beim nächsten Start werden sie per Hash-geprüfter Löschung wieder übernommen, ihre aktuelle Position wird gemerkt. Veränderte Verknüpfungen bleiben auf dem Desktop, gelöschte bleiben in der Bubble. Beim Abmelden und Herunterfahren werden sie ohne Positionierung zurückgelegt. Nach einem Absturz oder erzwungenen Beenden bleiben sie wie bisher in der Bubble. Test- und Diagnoseläufe fassen den Desktop nicht an.

## 1.7.1 · 11.11.2026

- Neues App-Symbol mit schillernden Regenbogen-Seifenblasen für EXE, Fenster, Taskleiste, Tray und Installer. Neun ICO-Auflösungen von 16 bis 256 Pixeln erhalten den transparenten Hintergrund; das bereitgestellte PNG bleibt unverändert.
- README zeigt das neue Logo. Bedienung, Datenformat und Speicherorte bleiben unverändert.

## 1.7.0 · 27.10.2026

- Windows-Installer mit Startmenüeintrag, optionaler Desktop-Verknüpfung und Deinstallation unter dem Benutzerkonto. Updates sichern vorhandene Daten geprüft; Deinstallation erhält persönliche Daten und Sicherungen. Eine separat baubare UI-Vorschau enthält kein App-Paket und führt keine Installation aus. Details: docs/INSTALLER.md.
- Installer-Prüfungen laufen zusätzlich in GitHub Actions. Eine laufende Ziel-App oder gesperrte Programmdatei blockiert das Update. Der Installer erzwingt kein Beenden und aktiviert keinen Autostart.

- Eigene Option **Milchglas für Ordner** im Reiter Ordner, unabhängig von Miniaturfarbe und gemeinsamem Klar-/Matt-Stil.
- Kompakter Milchglas-Knopf links neben dem Vorschau-Maßstab, aktiv grün. Im Milchglasmodus entfällt die Transparenzzeile; die Tönung ist fest eingestellt. Ausschalten stellt den normalen Stil und den Transparenzregler mit seinem bisherigen Wert wieder her.
- Der rechteckige Windows-Acrylic-Hintergrund entfällt. Milchglas, Tönung und Rand teilen eine einzige gerundete Alpha-Oberfläche. Der Hintergrund wird beim Öffnen einmal im Arbeitsspeicher übernommen und weichgezeichnet; Änderungen dahinter erscheinen beim nächsten Öffnen. Keine laufenden Aufnahmen und keine Speicherung der Aufnahme durch die App.
- Rundung und alle vier transparenten Glasecken folgen der gemeinsamen Geometrie, auch bei Größenänderungen. Bei deaktivierter Windows-Transparenz oder nicht verfügbarer Aufnahme bleibt eine gerundete neutrale Materialfläche.
- Ohne Milchglas bleiben 100 Prozent Transparenz möglich. Normale und Milchglas-Ordner verwenden dieselbe gerundete Alpha-Fläche.
- Größe und Rundung bleiben gemeinsame Werte. Bestehende Konfigurationen starten mit deaktiviertem Milchglas; Schema bleibt 6. Details: docs/FROSTED-FOLDERS.md.

## 1.6.1 · 26.10.2026

- Die Autostart-Checkbox prüft jetzt die tatsächliche Programmdatei, den Datenordner und den portablen Modus. Ein vorhandener Eintrag einer anderen Ausgabe wird nicht mehr fälschlich als aktiver Autostart dieser Ausgabe angezeigt.
- Ein Hinweis an der Checkbox erklärt einen abweichenden vorhandenen Eintrag. Erst ausdrückliches Aktivieren übernimmt die aktuell verwendete Ausgabe und deren Gruppen in den Windows-Start.
- Die Befehlszeilenerzeugung behandelt auch Datenordner an einer Laufwerkswurzel korrekt. Keine automatische Zusammenführung unterschiedlicher Datensammlungen und keine stillschweigende Autostart-Aktivierung.

## 1.6.0 · 25.10.2026

- Ein Symbol im geöffneten Ordner etwa eine halbe Sekunde gedrückt halten aktiviert den Sortiermodus. Symbole wackeln dezent; bei deaktivierten Animationen bleiben sie ruhig.
- Ein gegriffenes Symbol auf einem anderen ablegen tauscht genau diese beiden Plätze. Die Reihenfolge wird gespeichert und in der Miniatur aktualisiert. Auch fehlende Ziele lassen sich sortieren; ein Speicherfehler stellt die bisherige Reihenfolge wieder her.
- Der Sortiermodus bleibt für weitere Tauschaktionen aktiv. Esc, eine freie Stelle im Ordner oder das Schließen beendet ihn. Kurzes Klicken startet weiterhin, direktes Ziehen ohne Haltepause verwendet die bestehende Drag-&-Drop-Funktion.
- Bedienung und technische Grenzen: docs/REORDERING.md.
- Neue Vorher-nachher-Demo im README und ein direkter Einstieg in Download und Bedienung.

## 1.5.2 · 24.10.2026

- Fehlende Schreibrechte im Datenordner werden beim Start erkannt. Gruppen, Einstellungen, Verknüpfungen und Rückgabehistorie werden geprüft in einen beschreibbaren Benutzerprofil-Pfad übernommen. Die Quelle bleibt unverändert; spätere Starts verwenden weiter die neuere Kopie. Unterschiedliche Ausgangsordner bleiben getrennt.
- Eindeutige temporäre Dateinamen verhindern, dass eine alte blockierte groups.json.tmp neue Speicherung verhindert. Datenordner öffnen zeigt den aktiven Speicher.
- Deckkraft im Modusfarbeditor macht Steuerelement-Hintergründe transparent, unabhängig von Helligkeit. Hell und Dunkel speichern getrennte Werte; Vorschau, Anwenden und Abbrechen bleiben erhalten.

## 1.5.1 · 24.10.2026

- Symbolmenü um **Aus Gruppe löschen** ergänzt. Entfernt nur den Gruppeneintrag, auch wenn Ziel, interne Kopie oder Metadaten fehlen beziehungsweise defekt sind.
- Die neue Aktion legt nichts auf den Desktop zurück und löscht keine Programme, Verknüpfungsdateien oder Sicherungskopien. Andere Gruppen bleiben unverändert. Bei Speicherfehler wird der Eintrag an seiner bisherigen Position wieder eingesetzt.
- **Starten** und **Zurück zum Desktop** bleiben eigene Aktionen.

## 1.5.0 · 24.10.2026

- Optionale Farbkopplung für alle Miniaturen und Ordner, separater Farbreset.
- Getrennte globale Transparenzen und neuer gemeinsamer Miniatur-Größenregler.
- Ordner-Vorschau schaltet zwischen 1:3 und 1:1 um.
- Navigation und Kontextmenüs vereinfacht; Zurück zum Desktop deutlicher benannt.
- Gruppenminiaturen links ausgerichtet; Aussehen und Reiter zentriert, Einstellungszahnrad neben dem Ressourcen-Tracking unten rechts.
- Einstellungen mit Zurück-Pfeil und Zahnrad zum Verlassen, ohne Ansichtsreiter. Farbmodus neben App-Farbe; gemeinsame Bubble-Farben und roter Farbreset unter Allgemein.
- Miniatur und Ordner teilen sich die gesamte Breite des Inhaltsbereichs in zwei gleich breite, mitwachsende Reiter.
- Separate frei wählbare Hell-/Dunkel-Modusfarben über eine Farbkachel neben dem Farbmodus. Steuerelemente und Glasgrundflächen verwenden die Palette; Entwürfe bleiben bis Anwenden ungespeichert. Der Farbreset umfasst beide Modusfarben.
- Neues bereitgestelltes Rauchglas-Branding mit neun Kacheln für EXE, Fenster/Taskleiste, Tray und Logo im App-Fenster. Das PNG ersetzt die zuvor ausprobierten Frostglas- und Kristallglas-Varianten; das daraus erzeugte ICO enthält neun Auflösungen. ICO und PNG sind eingebettet.
- Hover-Öffnen entfernt, Datenwerkzeuge mit klaren Beschreibungen.
- Tray-Menü auf installierte Version und drei Aktionen reduziert: Repository, Desktop-Verbindung erneuern und Beenden.
- Farbauswahl in die Ansicht integriert: überdeckt vorübergehend die unteren Regler, zeigt einen Entwurf live und übernimmt erst mit Anwenden. Abbrechen oder Esc stellt den bisherigen Zustand wieder her.
- Schema 6 und eigene lokale Vorschau; Details: docs/LOCAL-PREVIEW.md.

## 1.4.1 · 24.10.2026

- Symbolmenü auf **Starten** und **Aus Gruppe entfernen** reduziert.
- Entfernen legt zuerst eine geprüfte, unabhängige `.lnk` oder `.url` auf dem persönlichen Desktop ab. Das funktioniert auch für direkt hinzugefügte Programme, Dateien, Ordner und Webadressen.
- Vorhandene Dateien bleiben erhalten; unterschiedliche Inhalte erhalten einen neuen Dateinamen. Bei Schreibfehler bleibt der Gruppeneintrag erhalten.
- Drag & Drop nach außen und zwischen Gruppen bleibt verfügbar. Programme und interne Sicherungen werden nicht verschoben.
- 97 Kernprüfungen und 92 native Windows-Prüfungen bestanden. Physische Explorer-Drag-&-Drop-Bedienung und weitere Hardwarekonfigurationen bleiben manuell zu prüfen.

## 1.4.0 · 24.10.2026

- Gemeinsame Farbe, Klar/Matt-Materialwahl und proportionale Rundung für Miniatur und geöffneten Ordner.
- Getrennte Transparenz bis 100 %, Größenregler mit maßstäblicher Live-Vorschau.
- Ressourcen-Tracking startet erst durch Benutzeraktion; CPU-Durchschnitt, RAM, Verlauf und aktive Messdauer.
- Portable Ausgabe mit enthaltenem .NET-Desktop-Runtime und ausgeschlossenen Benutzerdaten.

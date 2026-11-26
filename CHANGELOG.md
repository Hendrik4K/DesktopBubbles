# Änderungen

## 1.9.0 · 26.11.2026

- Bildschirmaufnahmen (OBS, Snipping Tool): Miniaturen blinken beim Öffnen eines Ordners, im Leerlauf und beim Ziehen nicht mehr kurz weg. Die Glasprobe holt den Hintergrund jetzt direkt vom Desktop, statt die Miniatur dafür vor Aufnahmen zu verstecken.
- Farbe: Farbkreis mit angesetzter Pille fester Breite; „Ändern“ und „Anwenden“ blenden nur den Text über, der Farbwähler blendet weich mit Weichzeichner ein und aus.
- Glas-Auswahl klappt als zusammenhängendes Menü nach unten aus, Beschriftung bleibt stehen.
- Ordnervorschau: runder 1:3/1:1-Knopf mit dauerhaft angesetztem „Milchglas“; Wechsel 1:3 ↔ 1:1 mit weicher Größenänderung und Überblendung.
- „Neu“ gestrichelt auf grauem Grund; Seitenwechsel Aussehen ↔ Einstellungen mit Gaußschem Weichzeichner; Zurückpfeil direkt am Titel, schwillt beim Überfahren an.
- Erstes Öffnen groß genug für die volle 1:3-Ordnervorschau; eigene Fenstergröße bleibt bis zur nächsten Version.
- Weiches Mausrad-Scrollen, größere Beschriftungen für Farbe und Glas, sanftere Wellen in der Tray-Versionszeile.
- Seitenwechsel (Aussehen ↔ Einstellungen und Miniatur ↔ Ordner) in zwei Takten: alte Seite verschwimmt ganz, dann wird die neue scharf; beide Titel stehen auf gleicher Höhe.
- Trennstrich zwischen Bubble-Liste und Seite läuft bis unten durch und ist zugleich die Scrollbar beider Seiten; die Liste blendet über „Neu“ weich aus.
- Verbrauchsmessung als Tacho mit Ausklapppfeil: Play/Stopp, geglättete Kurve über die letzten 20 Sekunden, die nach links wandert und weich ausblendet; der Tachozeiger folgt der CPU-Last.
- Zahnrad wächst beim Überfahren, dreht beim Öffnen der Einstellungen 45° nach links und leuchtet gelb.
- Ordner-Symbole schwellen beim Überfahren sanft an statt eines Hover-Hintergrunds; beim Sortieren bleibt das gegriffene Symbol vergrößert und nur das Tauschziel leuchtet weiß auf.
- Messleiste: Werte und Play/Stopp klappen links vom Tacho aus, der Tacho wandert mit nach rechts; weicher Hover am Tacho; Zahlen mit fester Breite, nichts verrutscht mehr beim Ticken.
- Zahnrad als Vektorform mit Luft: wird beim Drehen und Vergrößern nicht mehr abgeschnitten.
- Randblende beim Scrollen wächst mit dem Abstand zum Ende: oben und unten kein Springen mehr auf volle Deckung.
- Scrollbalken auf dem Trennstrich morpht beim Wechsel zwischen Bubble-Liste und Seite weich auf Länge und Platz des anderen Bereichs.
- Farbwähler: die ganze Pille wird beim Anwenden grün (auch um den Farbkreis); das Fenster wächst so weit, dass der Farbwähler ganz zu sehen ist, und gleitet danach zurück.
- 1:1-Vorschau erweitert das Fenster nach rechts und unten, bis der Ordner ganz sichtbar ist, und gleitet bei 1:3 genau an Platz und Größe von vorher zurück; Fenstergrößen ändern sich in einem Zug pro Bild statt ruckelnd.
- Vorschauwechsel (1:3 ↔ 1:1, andere Bubble) mit derselben zweistufigen Weichzeichner-Blende wie der Seitenwechsel.
- Messleiste: Tacho gleitet nach links weg, dann zieht der Pfeil die Messung auf; beim Einklappen umgekehrt. Tacho und Pfeil ohne Hover-Hintergrund, stattdessen weiß und kräftiger.
- Ordner: Das Öffnen läuft ungestört zu Ende; ein Klick auf die Miniatur währenddessen wird ignoriert, ein Fokuswechsel schließt erst danach. Schließt die App den Ordner mitten im Öffnen, faltet er sich ohne Aufblitzen aus dem aktuellen Zustand zurück. Ein Klick auf eine leere Stelle im geöffneten Ordner klappt ihn zu.
- 1:3 ↔ 1:1 blendet das alte Bild ganz aus und das neue mittig ein, während das Fenster gleichzeitig zur neuen Größe gleitet; von 1:1 direkt zur Miniatur gleitet es ebenfalls zurück.
- Beim Wechsel in den Ordner-Tab wächst das Fenster nur, wenn die 1:3-Vorschau nicht ganz hineinpasst, und nur so weit wie nötig.
- Scrollbalken-Morph als weiche S-Kurve, die der Scrollposition live folgt: Scrollen geht sofort, auch mitten im Morph.
- Verwaltung öffnet sich wieder dort, wo sie geschlossen wurde (sofern die Stelle noch auf einem Bildschirm liegt).
- Desktop-Bubbles neu verbinden blendet die Miniaturen weich aus und wieder ein statt kurz zu flackern.
- Bubble-Wechsel in der Verwaltung: alte Vorschau blendet ganz aus, erst dann die neue ein; die Auswahl-Pille gleitet über die ganze Übergabe. Im Ordner-Tab wächst das Fenster für mehrzeilige Ordner, wird dabei aber nie kleiner.
- 1:3/1:1 lässt einen laufenden Wechsel nie abbrechen (Doppelklick zählt einmal); die Zahl dreht sich um ihre senkrechte Achse, zu 1:1 rechts herum, zurück links herum.
- Seitenwechsel (Zahnrad, Miniatur/Ordner, Farbwähler) wie der Bubble-Wechsel: erst ganz ausblenden, dann ganz einblenden, ohne Weichzeichner. Das Zahnrad dreht dabei 135° nach links und beim Zurück 135° nach rechts.
- Hover auf Anwenden, Ändern, Milchglas, 1:3/1:1 und den Einträgen von Glas und Farbmodus: der Text wird etwas größer und kräftiger statt eines weißen Hintergrunds.
- Ausklappmenüs öffnen auf ihre echte Höhe mit klassischer Kurve (langsam, schneller, sanft bremsen).
- Farbwähler: großzügige Greifzonen für Sättigung, Helligkeit und Deckkraft; HEX-Feld zeigt die Farbe selbst; rechts Pipette (Farbe vom Bildschirm, mit Live-Vorschau, Esc bricht ab) und X statt Abbrechen; überschüssige Fläche blendet unten weich aus.
- Einstellungen: Modusfarbe als runder Kreis links neben dem Farbmodus; Farbmodus-Überschrift so groß wie App-Farbe.
- Miniatur ↔ Ordner blendet jetzt wirklich nacheinander aus und ein (die neue Seite sprang vorher sofort hinein, weil sie beim Start der Blende noch nicht im Fenster hing).
- Farbwähler öffnen und schließen: die echten Regler blenden ganz aus, dann der Farbwähler ein (und zurück), ohne flackerndes Schnappschuss-Bild.
- Das X im Farbwähler wird beim Überfahren weich rot, kräftiger und etwas größer.
- Blenden zwischen Seiten verzerren nichts mehr: das Bild der alten Seite wurde gestreckt, wenn ihr Inhalt nicht bis unten reichte (auch das frühere Aufpoppen der Ordnervorschau).
- Ausklappmenüs zeigen wieder alle Einträge; sie öffnen sich mit klassischer Kurve über einer mitlaufenden Maske, ohne dass darunter etwas verrutscht.
- 1:3 ↔ 1:1 in drei ruhigen Schritten: altes Bild ganz ausblenden, Fenster gleitet, neues Bild einblenden.
- Einstellungen: Allgemein steht zuerst (Mit Windows starten oben, Bubble automatisch schließen vorletzt, Bubble-Farben für alle zuletzt), darunter App-Farbe und Farbmodus, dann Transparenz. Farbmodus ist eine Pille wie App-Farbe: Kreis links, die Auswahl klappt darunter in Pillenbreite aus.
- Trennstrich endet auf Höhe der Unterkante von Neu; die gestrichelte Linie um Neu wandert langsam im Kreis.
- Miniatur ↔ Ordner blendet sichtbar aus und ein: das Bild der alten Seite wird sofort angeheftet, die Uhr startet erst mit dem ersten gezeichneten Bild nach dem Seitenaufbau (vorher lief die Blende beim langsamen Aufbau ins Leere).
- Ausklappmenüs (Glas, Farbmodus) fahren weich auf und wieder ein; Klicks während des Aufklappens werden verworfen. Vorher wurde die Animation nie angehängt, weil WPF Loaded nur bei Elementen mit eigenem Handler auslöst.
- Einstellungen: App-Farbe und Farbmodus wieder zuerst, dann Allgemein, dann Transparenz und Werkzeuge.
- **Deutsch und Englisch:** Zwei runde Flaggen rechts neben „Einstellungen“ schalten die ganze App ohne Neustart um: Verwaltung (mit Seitenblende), Tray-Menü, Bubble-Tooltips und -Menüs, Ordner-Menüs, Farbwähler, Dialoge und Fehlermeldungen. Die Wahl wird gespeichert; ohne Wahl gilt die Windows-Anzeigesprache.
- Seitenblenden ohne Ruck nach unten: Das Bild der alten Seite lag um ihren Abstand zur Überschrift zu tief, sodass beim Start der Blende alles kurz nach unten sprang (Miniatur ↔ Ordner, ebenso beim Zahnrad). Es wird jetzt so aufgenommen, wie es auf dem Bildschirm steht, und genau auf die Seite zugeschnitten; ein Pixeltest vergleicht es mit dem echten Fenster.
- Sprachwahl als Schalter wie in Pi Relay statt zwei Flaggen: eine Pille mit rundem Flaggenknopf, der die Zielsprache zeigt (im deutschen UI britische Flagge und „English“). Klick, kurzes Ziehen oder die Pfeiltasten schalten um; der Knopf gleitet erst hinüber, dann wechselt die App.
- Ausklappmenüs: das Feld bleibt unten eckig, bis die Liste ganz eingefahren ist, erst dann rundet es sich wieder (vorher sprangen die Ecken beim Einklappen sofort rund).
- Farbmodus klappt wie Glas als ein Stück aus der Pille aus (unten eckig, bündig, gleich breit) und steht weiter rechts, etwa ab der Seitenmitte.
- Einstellungen: „Farben zurücksetzen“ ist genau so breit wie der Sekunden-Regler darüber. Der Transparenz-Regler läuft als durchgehende Linie über die ganze Breite; die Prozentzahl erscheint nur beim Verschieben rechts neben „Transparenz“ und blendet nach dem Loslassen wieder aus, ohne dass sich etwas verschiebt.

## 1.8.2 · 15.11.2026

- **Von Updates neu angelegte Verknüpfungen werden eingesammelt:** Legt ein Programm-Update eine Desktop-Verknüpfung an, deren Programm (Ziel und Parameter, bzw. URL) schon in einer Bubble liegt, übernimmt DesktopBubbles sie automatisch. Die frische Verknüpfung ersetzt die Kopie in der Bubble; Name des Eintrags und gemerkter Desktop-Platz bleiben. Beim Start wird nachgeholt, was während der Pause angelegt wurde. Unberührt bleiben Verknüpfungen ohne passenden Eintrag, auf Wunsch zurückgelegte oder herausgezogene und alles im Kopiermodus. Abschaltbar unter „Von Updates neu angelegte Verknüpfungen einsammeln“.
- **Tray:** Ein einfacher Linksklick öffnet DesktopBubbles. Das Rechtsklick-Menü trägt das App-Design: durchgehender heller Verlauf ohne graue Leiste, oben Bubble-Logo mit Version (Klick sucht nach einem neueren Release und bietet es an), „Einstellungen öffnen“, rotes „Beenden“ mit zitternden Buchstaben.
- **Verwaltung, weicher und ruhiger:** Miniatur/Ordner als ein Schiebeschalter mit gleitendem Akzent. Gruppenliste mit weich ein- und ausblendendem Hover, gleitendem Auswahlbalken, weichen Rändern oben und unten und Scrollleisten nur unter der Maus; lange Gruppennamen werden verkleinert statt mitten im Wort umbrochen. Die Vorschau blendet beim Gruppenwechsel über.
- Regler greifen auf der ganzen Zeile vom Titel bis zum Wert; der Punkt gleitet zum Klick und folgt beim Ziehen weich der Maus.
- Keine blauen Fokusrahmen mehr; das Zahnrad wird nicht mehr abgeschnitten. Knöpfe hellen beim Darüberfahren weich auf, statt ihre Farbe zu ersetzen (weiße Schrift auf farbigen Knöpfen verschwand vorher).
- Die Sekundenwahl für „Bubble automatisch schließen“ verschiebt die Seite nicht mehr. 1:1 öffnet das Fenster nach unten, bis die Vorschau ganz sichtbar ist, 1:3 klappt es zurück.
- Ressourcen-Tracking ohne Statuszeile, mit größerem Verlauf und größeren Werten.
- Duplikate auf dem öffentlichen Desktop werden per Tray-Hinweis gemeldet und lassen sich über Einstellungen → „Doppelte Verknüpfungen vom öffentlichen Desktop entfernen …“ nach Bestätigung mit Administratorrechten entfernen.

## 1.8.1 · 12.11.2026

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

# Änderungen

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

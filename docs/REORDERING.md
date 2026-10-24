# Symbole durch Gedrückthalten sortieren

Verfügbar ab Version 1.6.0. Bestehende Gruppen und das Datenschema 6 bleiben erhalten.

## Bedienung

1. Eine Desktop-Bubble öffnen.
2. Ein Symbol mit der linken Maustaste ungefähr 500 ms gedrückt halten. Der Ordner wechselt in den Sortiermodus; die anderen Symbole wackeln dezent. Das gehaltene Symbol wird leicht angehoben und folgt dem Mauszeiger.
3. Auf einem anderen Symbol loslassen. Das Ziel wird während des Ziehens hervorgehoben. Genau diese zwei Einträge tauschen ihre Plätze; alle übrigen bleiben stehen.
4. Für weitere Tauschaktionen direkt ein anderes Symbol greifen. Esc oder ein Klick auf eine freie Stelle im Ordner beendet das Sortieren. Ein weiteres Esc schließt den Ordner wie bisher.

Loslassen auf einer freien Stelle oder außerhalb des Ordners bricht den aktuellen Griff ab. Das Öffnen eines Kontextmenüs bricht ebenfalls den Griff ab. Beim Schließen oder Deaktivieren des Ordners enden Sortiermodus und Animationen.

Ein kurzer Klick startet weiterhin die gewählte Anwendung. Direktes Ziehen ohne vorheriges Gedrückthalten bleibt die bestehende Drag-&-Drop-Bedienung, etwa zum Desktop oder in eine andere Gruppe. Im Sortiermodus startet ein normaler Klick keine Anwendung. Die vorhandenen Kontextmenü-Aktionen bleiben verfügbar.

## Implementierung

`FolderReorder` verwaltet den Halte-Timer, den Sortiermodus, die Mausaufnahme und die Zielmarkierung des geöffneten `FlyoutWindow`. Der Positionsvergleich verwendet die festen Schaltflächen im Scrollbereich und WPF-Koordinaten; nur deren Bilder werden gedreht beziehungsweise verschoben. Außerhalb des sichtbaren Scrollbereichs wird kein Ziel akzeptiert.

Ein Tausch wird erst beim Loslassen gespeichert. `ShortcutManager.Swap` validiert beide Einträge, tauscht ihre Listenpositionen und speichert den Zustand. Wenn Speichern fehlschlägt, wird die alte Reihenfolge wiederhergestellt. Der Vorgang löst keine Ziele auf und verschiebt, erstellt oder löscht keine Verknüpfungsdateien. Danach werden Ordner und Desktop-Miniatur aktualisiert. Dasselbe Tauschverhalten gilt beim bisherigen direkten Drag & Drop auf ein anderes Symbol innerhalb derselben Gruppe.

Animationen laufen ausschließlich im geöffneten Sortiermodus und beachten sowohl die App-Einstellung als auch die Windows-Einstellung für Animationen. Beim Beenden und beim Neuaufbau der Symbolansicht werden die Animationsuhren entfernt. Es gibt keine dauerhafte Rendering-Schleife und keine Änderung des Datenschemas.

Für ein Upgrade die alte App im Tray beenden und den vollständigen aktiven Datenordner sichern und übernehmen; siehe README. Nur eine Version gleichzeitig verwenden, damit keine doppelten Desktop-Bubbles erscheinen. Wer vorher die lokale Vorschau 1.6.0-preview.1 verwendet hat: Deren eigener Datenordner enthält möglicherweise neuere Änderungen als die frühere Installation. Diese Änderungen werden nicht automatisch zwischen Installationen zusammengeführt.

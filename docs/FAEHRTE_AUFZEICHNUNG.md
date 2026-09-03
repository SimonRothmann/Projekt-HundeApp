# Fährte: Aufzeichnung im Vollbild

Nächstes Feature. Vier Wünsche vom Platz — zwei davon sind kleiner als
gedacht, weil die Grundlage schon steht.

Stand der Untersuchung: 2026-09-03, Commit `82d9fc6`.

---

## Was schon da ist

Vor dem Bauen geprüft, damit nichts doppelt entsteht:

| Wunsch | Stand heute |
|---|---|
| Karte in Laufrichtung statt fest nach Norden | **Existiert bereits.** `TrackMap` kennt drei Modi und einen Kompass-Knopf zum Durchschalten. |
| Knöpfe für Gegenstand / Leckerlipot / andere | **Datenmodell und Auswahl existieren.** `GpsMarkerType` = Gegenstand, Leckerlipot, Verleitung, Sonstiges. |
| Start- **und** Endzeit des Legens | **Endzeit wird bereits berechnet**, nur nicht angezeigt. |
| Vollbild beim Aufzeichnen | Fehlt vollständig. |

Das verschiebt den Schwerpunkt: Es geht überwiegend nicht darum, Funktionen
zu bauen, sondern sie **erreichbar** zu machen.

---

## 1. Karte in Laufrichtung

`TrackMap` hat drei Ausrichtungen, umschaltbar über den Kompass-Knopf:

- `north-arrow` — Norden oben, Richtungspfeil an der eigenen Position
- `heading` — **Karte dreht sich mit der Laufrichtung** (Navi-Modus)
- `north` — Norden oben, ohne Pfeil

Der Standard beim Aufzeichnen ist `north-arrow`. Der gewünschte Modus ist
also da, nur nicht voreingestellt — und der Kompass-Knopf offenbar nicht
auffindbar genug.

**Vorschlag:** Beim Aufzeichnen `heading` zum Standard machen. Die drei Modi
bleiben, die Wahl wird aber pro Nutzer gemerkt (passt zu den geplanten
`UserPreferences`, siehe [Verbände, Sprachen, Module](VERBAENDE_SPRACHEN_MODULE.md)).

Zu beachten: Die Drehung stützt sich auf die aus GPS abgeleitete
Bewegungsrichtung, geglättet. Beim Fährtelegen geht man langsam — bei sehr
geringer Geschwindigkeit wird die Richtung unruhig. Deshalb: unterhalb einer
Mindestgeschwindigkeit die letzte stabile Richtung halten, statt zu zittern.
Das ist der eigentliche Knackpunkt, nicht die Drehung selbst.

**Nachtrag (2026-09-03):** Beim Start zeigte die Karte ganz Deutschland und
sprang erst nach etlichen Sekunden auf den Standort. Ursache war eine
Vermischung zweier Dinge: Die Karte wartete auf den ersten Punkt, der die
Genauigkeitsprüfung besteht - bei kaltem GPS werden aber genau die ersten
Messungen verworfen (gefordert sind 8 m, gelockert wird erst nach 15 s).
Der Filter gehört zur AUFZEICHNUNG; wohin die Karte schaut, ist eine andere
Frage. Sie holt sich jetzt beim Öffnen eine grobe Position
(`enableHighAccuracy: false`, Cache bis 60 s) und zentriert sofort; der erste
echte Punkt rastet danach genau ein.

### Kartenhintergrund: Straße, Luftbild, dunkel (2026-09-03)

Rückmeldung war "Leaflet sieht nicht modern aus". Das Aussehen bestimmt aber
nicht die Bibliothek, sondern die Kachelquelle - der OSM-Standardstil ist
für Kartenzeichner gemacht und zeigt jede Drogerie und jedes
Bekleidungsgeschäft.

Geprüft wurde an der echten Fährtenkarte, nicht an Beschreibungen:

| Quelle | Ergebnis |
|---|---|
| OSM Standard | Funktioniert, aber überfrachtet und grellweiß unter dunkler App |
| CARTO (Positron/Dark/Voyager) | Sieht deutlich moderner aus - liefert aber **"API KEY REQUIRED"** quer über der Karte. Achtung: mit **HTTP 200**, der Statuscode verrät es nicht, nur das Bild |
| Esri World Imagery | Funktioniert ohne Schlüssel, bis in hohe Zoomstufen. Für Fährten fachlich das Beste: Man sieht den Schlag, nicht Ladenlokale |
| Esri Dark Gray Canvas | Fällt aus - meldet in den nötigen Zoomstufen "Map data not yet available" |

Umgesetzt: Umschalter Straße/Luftbild auf der Karte, Auswahl im
localStorage gemerkt (Anzeigevorliebe des Geräts, nicht des Kontos), und
ein dunkler Modus für die Straßenkarte.

Der dunkle Modus entsteht per Filter aus derselben, schon geladenen Kachel
(`invert` + `hue-rotate` + `saturate(0.45)`) statt aus einer zweiten Quelle:
kein Schlüssel, keine zusätzlichen Abrufe. Das `saturate` ist dabei nicht
Geschmack - ohne es macht die Farbtondrehung aus OSMs bunten Ladensymbolen
grelles Magenta.

**Zwei Fallen, beide beim Ansehen gefunden, nicht beim Messen:**

1. Der Filter lag zuerst als seitenweite Regel `.leaflet-tile-pane` vor. Auf
   einer Seite liegen aber mehrere Karten - das Vollbild und die Karten der
   bisherigen Fährten. Die Regel der einen invertierte das Luftbild der
   anderen, das dann aussah wie ein Negativ. Beide Stilregeln sind jetzt
   über eine Kennung je Karte begrenzt.
2. Jede Karte las die Auswahl nur beim Einhängen. Wer im Vollbild wechselte
   und es schloss, sah darunter weiter die alte Ebene. Die Karten stimmen
   sich jetzt über ein Ereignis ab.

---

### Gibt es einen Standard für drehbare Karten? (geprüft 2026-09-03)

Ja - und wir nutzen ihn bewusst NICHT. Die Prüfung, damit die Frage nicht
wiederkehrt:

| Bibliothek | Stand | Größe (gzip) | Drehung |
|---|---|---|---|
| Leaflet 1.9.4 (heute) | Mai 2023 | **42 KB** | kennt keine |
| MapLibre GL 6.7.0 | September 2026 | **283 KB** | `setBearing`, nativ |
| leaflet-rotate 0.2.8 | Juli 2023 | ~20 KB | Plugin, seit 3 Jahren still |

MapLibre GL ist der Standard: WebGL-Kamera, Drehung ist erste Klasse,
Zeigerkoordinaten und Ziehen stimmen dabei. Es kostet aber **rund 240 KB
gzip mehr** - und zwar genau auf dem Bildschirm, der auf dem Hundeplatz bei
schlechtem Netz geöffnet wird. Dazu käme CSP-Anpassung (`worker-src blob:`,
Kachelhost in `connect-src`).

Dagegen steht: Die bekannten Schwächen der CSS-Drehung treffen HIER nicht
zu. Sie betreffen Ziehen und Klicken auf der gedrehten Karte - beim
Aufzeichnen folgt die Karte aber jede Sekunde der eigenen Position und
überschreibt jedes Verschieben ohnehin, und die historische Karte dreht
gar nicht. Was blieb, war eine Bedienung, die beim Ziehen schräg läuft;
deshalb ist Ziehen im Drehmodus jetzt abgeschaltet, mit dem Kompass kommt
man auf "Nord oben".

### Gegenprobe: MapLibre lokal eingebaut (2026-09-03)

Nicht bei der Schätzung belassen, sondern eingebaut und gemessen. Ergebnis:

**Was zutrifft**

| Vermutung | Gemessen |
|---|---|
| Deutlich größer | **+255 KB gzip** (976 KB roh, eigener Chunk). Alle Chunks zusammen: 478 → 734 KB gzip |
| CSP muss angepasst werden | Bestätigt: Kacheln laufen über `connect-src` statt `img-src` - jede einzelne meldete einen Verstoß. Zusätzlich `worker-src blob:`, der Worker wird per `URL.createObjectURL` aus einem Blob erzeugt |
| Kachelquelle nutzbar | Ja - OSM liefert `access-control-allow-origin: *`, MapLibre kann die Kacheln per fetch holen |

**Was zusätzlich auftrat und vorher niemand auf dem Zettel hatte**

MapLibre lief in diesem Aufbau überhaupt nicht an. Es leitet die Adresse
seines Web Workers aus `import.meta.url` ab:

```js
function Gi(){ let e = import.meta.url; if(!/^https?:/.test(e)) return ``; … }
```

Unter dem Bundler dieses Projekts ist das keine `http`-Adresse. MapLibre
liefert dann einen leeren Pfad, der Worker startet nie - und die Karte
bleibt leer, **ohne dass ein Fehler ausgelöst wird**: Weder `load` noch
`error` feuert, das Modul lädt in 25 ms, das Map-Objekt entsteht, und dann
passiert nichts mehr. Der dokumentierte Ausweg `setWorkerUrl` samt
Auslieferung der Worker-Dateien aus `public/` hat es in mehreren Anläufen
nicht behoben.

**Schluss**

MapLibre ist die technisch richtige Antwort auf "drehbare Karte" - aber in
diesem Aufbau kein Austausch, sondern ein eigenes Vorhaben: 255 KB mehr auf
dem Bildschirm mit dem schlechtesten Netz, zwei CSP-Änderungen, und eine
Bundler-Integration, die erst zum Laufen gebracht werden muss.

Sollte die Karte einmal echte Interaktion im gedrehten Zustand brauchen -
oder Vektorkacheln, Neigung, Beschriftungen, die sich mitdrehen -, lohnt
sich dieser Aufwand. Für das Fährtelegen, wo die Karte ohnehin der eigenen
Position folgt, nicht.

---

## 2. Vollbild beim Aufzeichnen

Heute steckt die Karte mit `h-64` (256 px) in einer Karte auf der Hundeseite,
zwischen Kopfzeile, Zielen und Trainingsformular. Auf einem 375-px-Handy
bleibt davon wenig — und darunter drängeln sich Eingabefelder und Knöpfe.

**Vorschlag:** „Aufzeichnen" öffnet eine eigene Vollbildansicht.

- Karte über die volle Höhe, alles andere darüber gelegt.
- Große, mit dem Daumen erreichbare Knöpfe im unteren Drittel — dort, wo die
  Hand beim einhändigen Halten ohnehin ist. Die andere Hand hält die Leine.
- Deutlich über der Mindestgröße für Touch (das Projekt nutzt dafür bereits
  `coarse:min-h-11`).
- Verlassen nur bewusst: Ein versehentliches Zurück darf die laufende
  Aufzeichnung nicht abbrechen.

Der Bildschirm wird wachgehalten (`useWakeLock`). Wichtig dabei: Das System
nimmt die Sperre von sich aus zurück, sobald der Tab in den Hintergrund
gerät - etwa bei einem Anruf. Der Haken fordert sie deshalb beim
Sichtbarwerden erneut an, sonst schläft das Display mitten in der
Aufzeichnung ein.

**Beim Bauen aufgefallen:** Das Vollbild muss per Portal am `body` hängen,
nicht an seiner Stelle im Baum. Der Inhaltsbereich der App-Hülle trägt
`relative z-10` und bildet einen eigenen Stapelkontext - ein `z-50` darin
bleibt trotzdem unter der Kopfzeile (`z-30`) und der unteren Navigation
(`z-40`), weil beide Geschwister des Inhaltsbereichs sind und gegen dessen
`z-10` verglichen werden. Ohne Portal lag das Vollbild zwischen den beiden,
und der Abschluss-Knopf war verdeckt.

---

## 3. Marker direkt setzen

Heute: ein Auswahlfeld für den Markertyp, daneben ein Knopf „Punkt setzen".
Das sind zwei Bedienschritte und ein kleines Ziel — mit Hund an der Leine,
im Stehen, auf dem Acker.

**Vorschlag:** je ein großer Knopf pro Markertyp, direkt auf der
Vollbildansicht. Ein Tipp = ein Marker an der aktuellen Position.

Die vier Typen gibt es schon, sie brauchen nur je einen Knopf mit Symbol.
Das Textfeld für eine Beschriftung entfällt im Vollbild — nachträgliches
Benennen ist in der Übersicht möglich, unterwegs stört es.

---

## 4. Start- und Endzeit

Die Übersicht zeigt heute `Gelegt 14:32 · Dauer 12:40`. Die Endzeit wird in
`trackTimes()` bereits berechnet und dann zugunsten der Dauer verworfen.

**Vorschlag:** `Gelegt 14:32–14:45 · 12:40`. Eine Zeile Anzeige, keine
Datenbankänderung.

Warum das fachlich zählt: Für das Fährtenalter ist der Zeitpunkt maßgeblich,
an dem das Legen **endet** — von da an altert der letzte Abschnitt. Bei einer
Fährte über 20 Minuten ist der Unterschied zwischen Anfang und Ende
erheblich, und das Alter ist die wichtigste Größe für die Schwierigkeit.

Eine Einschränkung, die man kennen sollte: Beide Zeiten leiten sich aus dem
ersten und letzten automatischen Trackpunkt ab. Wer die Aufzeichnung nach dem
Legen weiterlaufen lässt, verschiebt damit die Endzeit. Solange
„Aufzeichnung beenden" und „Legen beendet" dasselbe sind, stimmt es — das
Vollbild sollte deshalb einen klaren Abschluss haben, keinen beiläufigen.

---

## Reihenfolge

1. **Endzeit anzeigen** — eine Zeile, sofort erledigt.
2. **Vollbildansicht** mit großer Karte und Marker-Knöpfen — das Hauptstück.
3. **`heading` als Standard** samt Richtungsglättung bei langsamem Gehen —
   sinnvollerweise im Vollbild, wo die Karte groß genug ist, dass die
   Drehung überhaupt etwas bringt.

Punkt 1 lässt sich vorab ausliefern, 2 und 3 gehören zusammen.

---

## Entschieden (2026-09-03, Auftraggeber)

1. **Bildschirm wird wachgehalten** während der Aufzeichnung. Zuverlässigkeit
   vor Akku - eine abgebrochene Fährte ist verloren, ein leerer Akku nicht.
   Umsetzung über die Wake-Lock-API, mit Freigabe beim Verlassen der Ansicht.
2. **Vollbild gilt für beides** - Legen und Suchen. Auch wenn beim Suchen
   der Hund führt: Wer doch aufs Display schaut, soll dasselbe sehen, und
   zwei getrennte Ansichten wären zwei Stellen zum Pflegen.
3. Die Ausrichtung wird **pro Nutzer gemerkt** (`UserPreferences`).

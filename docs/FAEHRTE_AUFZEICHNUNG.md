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

Sollte die Karte einmal echte Interaktion im gedrehten Zustand brauchen -
oder Vektorkacheln, Neigung, Beschriftungen, die sich mitdrehen -, ist
MapLibre die richtige Antwort. Für das Fährtelegen ist es 240 KB für nichts.

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

# Mantrailing (Paket M1, Fassung 0.46)

Mantrailing ist eine Sportart wie jede andere im Katalog (Ordnungen DVG MT-I bis MT-III und SV-MT 1 bis 3,
Quellen und Prüfprotokoll in docs/PO_VERIFIKATION.md) - und nutzt die GPS-Aufzeichnung der Fährte:
**Trail legen** ist die Laufspur der Versteckperson, **Trail suchen** der Lauf des Hundes.

## Art der Aufzeichnung

`GpsTrack.Kind` (`Faehrte` = 0, `Mantrailing` = 1, als Text gespeichert, Vorgabe `Faehrte`) entscheidet über die
Auswertung und die Begriffe. Bestehende Fährten sind unverändert. Im Frontend heißt das Feld `kind`
(`lib/mantrailing.ts`: `istMantrailing`, `ART_MANTRAILING`).

## Toleranz (eine Quelle je Seite)

| Art | „auf der Spur“ | grün | gelb | rot |
|---|---|---|---|---|
| Fährte | ≤ 3 m | ≤ 3 m | ≤ 6 m | darüber |
| Mantrailing | ≤ 10 m | ≤ 10 m | ≤ 25 m | darüber |

Warum weiter: Der Hund arbeitet an einer 5 bis 10 m langen Leine (SV) und sucht den Geruch auch neben der Spur der
Versteckperson; gemessen wird die Linie des Hundeführers. Backend: `GpsTrackEvaluator.FaehrteToleranz` und
`MantrailingToleranz` (`ToleranzFuer(kind)`); Frontend: `FAEHRTE_GRENZEN` und `MANTRAILING_GRENZEN`
(`spurGrenzen`). Beim Ändern beide Seiten anfassen. Die Ablauflinie auf der Karte, die Legende, die Auswertung im
Tagebuch und das Bild zum Teilen folgen der Art. Gegenstände zählt die Auswertung beim Mantrailing nicht (0).

## Angaben (Migration `AddMantrailing`)

| Angabe | Wo | Warum dort |
|---|---|---|
| Trailalter | `GpsTrack.AgeMinutes` (Minuten; die Oberfläche zeigt und erfasst **Stunden**) | gehört zur Spur; eine Spalte für Fährte und Trail |
| Doppelblind, Negativ-Trail | `GpsTrack.DoubleBlind`, `NegativeTrail` (bool) | stehen fest, sobald die Versteckperson losgeht, und gelten für jeden Suchversuch |
| Geruchsartikel | `GpsTrack.ScentArticle` (Freitext, höchstens 100 Zeichen, `Textlaengen.Geruchsartikel`) | wie oben |
| Person gefunden | `GpsWalkRun.PersonFound` (bool?) | jeder Versuch geht anders aus |
| Anzeigeverhalten | `GpsWalkRun.Indication` (`Barking`, `SittingInFront`, `StandingInFront`, `LyingInFront`, `ReturnIndication`, `Other`; als Text) | wie oben |

**Negativ-Trail:** Es bleibt bei einem Feld. Die Frage heißt dann „Negativ korrekt angezeigt?“ (`PersonFound` = der Hund
hat korrekt angezeigt, dass niemand da ist), ein Anzeigeverhalten gibt es nicht. Das Anzeigeverhalten wird auch
ohne Fund geleert (`UpdateTrailRunAsync`).

Eingabe: Doppelblind, Negativ-Trail und Geruchsartikel vor dem Legen; nach dem Suchen im Ergebnis-Fenster Fund,
Anzeigeverhalten und Trailalter (vorbelegt mit der Zeit zwischen Ende des Legens und Start des Laufs). Nachträglich
über „Ergebnis eintragen/ändern“ im Tagebuch. Endpunkte: `PUT /api/gps-tracks/{id}/trail` und
`PUT /api/gps-tracks/{id}/walk-runs/{runId}/trail`; beide nur für Mantrailing-Spuren.

**Rechte:** Die Angaben zum Trail (`PUT .../trail`) ändert nur, wer das Training bearbeiten darf (Besitzer:innen;
betreuende Trainer:innen nur eigene Einträge, `TrainingsRechte`). Das Ergebnis eines Laufs (`PUT .../walk-runs/{runId}/trail`)
darf jede Person mit Zugriff auf den Hund eintragen, wie den Kommentar eines Ablaufs. Das Ergebnis-Fenster speichert
zuerst das Ergebnis und dann das Alter; lehnt der Server das Alter mit 404 ab (Trainer:in am Eintrag der Familie),
bleibt es beim Ergebnis und das Fenster schließt ohne Fehler. Das Trailalter wird beim Anlegen wie beim Ändern auf
0 bis 168 Stunden geprüft.

## Doppelblind

Bei einem doppelblinden Trail sieht das Team die gelegte Linie während der Suche nicht auf der Karte und bekommt keine
Vibration an Abbiegungen - beides verriete den Verlauf. Nach dem Suchen zeigt das Ergebnis die Spur.

## Weitere Festlegungen

- **Anbieten:** Der Trail wird nur angeboten, wenn Mantrailing in den Sportarten des Hundes **ausdrücklich** gewählt
  ist (`spurArten`); bei leerer Auswahl bleibt es bei der Fährte. Wählt ein Hund beides, wechselt das Fenster
  „Fährte | Trail“ im Fenster zum Legen. Nur Trail: der Knopf heißt „Trail legen“.
- **Frist zum Suchen:** Fährte 24 Stunden nach dem Ende des Legens, Trail 48 (SV-MT 3 liegt ca. 24 Stunden).
- **Erinnerung „Fährte noch nicht abgelaufen“:** gilt nur für Fährten; ein Trail wird nach Stunden gesucht, nicht nach
  der Frist von 3 bis 24 Stunden. `MANTRAILING` steht aber in der Liste der Sportarten mit Aufzeichnung
  (`FAEHRTEN_SPORTARTEN` und `Erinnerungsregeln.FaehrtenSportCodes`).
- **Statistik:** Der Fährten-Verlauf am Hund zählt nur Fährten, kein Mantrailing (andere Grenzen).
- **Prüfungsreife** und Trainingsplan hängen nicht an der Fährte und gelten für Mantrailing wie für jede Sportart.
- **Datenschutz:** Die Laufspur der Versteckperson sind Standortdaten einer weiteren Person; Hinweis in der
  Datenschutzerklärung (Abschnitt 3) und im Verarbeitungsverzeichnis 2.3. Export und Löschung laufen mit der Spur
  (`AccountDataService`: `Spurart`, `Doppelblind`, `NegativTrail`, `Geruchsartikel`, `PersonGefunden`,
  `Anzeigeverhalten`).

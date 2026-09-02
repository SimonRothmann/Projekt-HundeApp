# Verfassung des Hundes

Status: **umgesetzt** (2026-09-02).

## Warum

Eine Bewertung allein sagt wenig. „Drei Sterne" heißt etwas anderes, wenn der
Hund müde war, als wenn er motiviert bei der Sache war. Mit einem Tipp beim
Eintragen bekommt die Statistik eine zweite Achse — und kann Zusammenhänge
zeigen, die im Alltag untergehen, weil niemand seine Trainingstage zusammenzählt.

## Was erfasst wird

Fünf Werte, am **Trainingstag** und nicht an der einzelnen Übung: `Motivated`,
`Settled`, `Distracted`, `Tired`, `Stressed` (motiviert, ausgeglichen, abgelenkt,
müde, gestresst).

Vier davon kamen aus der Anforderung. **`Settled` ist ergänzt**: Ohne eine Mitte
hätte ein ganz normaler, unauffälliger Trainingstag keine ehrliche Antwort, und
die Auswertung bekäme lauter falsche „motiviert".

**Optional, ohne Vorauswahl.** Ein Pflichtfeld mehr würde die Hürde beim
Eintragen wieder anheben, die zuletzt mühsam gesenkt wurde. Erneutes Antippen
hebt die Auswahl auf; nachträglich ändern geht über die Trainingskarte
(„Ändern" neben Ort und Uhrzeit).

Beim Zusammenfassen mehrerer Einträge desselben Tages gewinnt die **erste**
Angabe — ein zweiter Eintrag soll die Einschätzung nicht stillschweigend
überschreiben.

## Was die Auswertung zeigt

`GET /api/stats/dogs/{dogId}/condition`, dargestellt unter „Übungen &
Schwerpunkte" auf der Statistikseite.

**Bewertung nach Verfassung** — Ø Bewertung und Erfolgsquote je Verfassung. Das
ahnt man, aber man hat es nicht in Zahlen.

**Nach Trainingstagen am Stück** — dieselben Zahlen, gruppiert danach, wie viele
Tage *unmittelbar davor* schon trainiert wurde: nach einer Pause, zweiter Tag in
Folge, dritter Tag oder später. Dazu der Anteil „müde oder gestresst".

Gezählt werden **zusammenhängende Vortage**, nicht „Trainings der letzten drei
Tage". Genau danach fragt man sich im Alltag: „der dritte Tag in Folge, kein
Wunder." Bei zwei ist Schluss — mehr Stufen würden die Gruppen so klein machen,
dass der Schnitt nichts mehr aussagt.

Steht ein Einbruch tatsächlich in den Zahlen, fasst ihn ein Satz zusammen
(„Dritter Tag oder später fällt die Bewertung im Schnitt um 0,8 ★ niedriger aus
als nach einer Pause"). Der Satz erscheint nur ab einem halben Stern Abstand und
drei Einheiten in der Gruppe — ein Hinweis auf einen Einbruch, den es nicht
gibt, wäre schlimmer als gar keiner.

## Zwei Fallstricke

**Einheiten ohne Angabe bleiben außen vor**, wenn Anteile gerechnet werden.
Sonst sähe ein Hund umso ausgeglichener aus, je seltener jemand etwas eingetragen
hat. Die Abdeckung steht deshalb darunter: „Verfassung bei 12 von 30 Trainings
angegeben."

**Die API überträgt Enums numerisch** (wie `ExerciseDifficulty`, `GoalStatus`):
`Motivated` ist die **0**. In JavaScript ist 0 falsch — Prüfungen auf „ist eine
Verfassung gesetzt" müssen `!= null` lauten, nicht `if (condition)`. Die
Zuordnung Zahl → Beschriftung steht an genau einer Stelle:
`components/dogs/condition-picker.tsx`.

## Offen

- **Verfassung des Hundeführers.** Fachlich mindestens so wichtig, aber ein
  zweiter Tipp beim Eintragen — erst sinnvoll, wenn der erste angenommen wird.
- **Zusammenhang mit dem Wetter.** Temperatur steht schon an der Einheit; „bei
  über 25 °C dreimal so oft müde" wäre die naheliegende nächste Auswertung.

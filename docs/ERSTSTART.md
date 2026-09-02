# Geführter Erststart

Status: **umgesetzt** (2026-09-02).

## Warum

Ein leeres Dashboard ist die härteste Hürde der App. Man sieht, dass etwas
fehlt, aber nicht, was als Erstes zu tun wäre — und wer nicht weiterkommt,
kommt nicht wieder.

## Der Weg

**Zuerst der Hund.** Alles Weitere hängt daran: Ziele, Trainings, die Zuordnung
zu einer Gruppe.

**Danach gabelt es sich**, und zwar sichtbar nebeneinander:

| Selbst loslegen | Über den Verein |
|---|---|
| Ziel setzen | Verein beitreten |
| Erstes Training eintragen | Trainingsgruppe beitreten |

**Einer der beiden Wege genügt.** Sie sind Alternativen, keine Reihenfolge —
deshalb steht „oder" dazwischen und nicht „danach". Der Erststart gilt als
erledigt, sobald jemand einen Hund hat **und** entweder ein Training eingetragen
hat **oder** in einer Trainingsgruppe ist.

Beide zu verlangen wäre falsch: Wer über den Verein kommt, trägt sein erstes
Training oft erst nach dem ersten Gruppentraining ein — und wer allein
trainiert, wird nie einem Verein beitreten.

## Trainer:innen gehören dazu

Wer einem Verein als Trainer:in zugewiesen wird, bekommt **keine
Mitgliedschaftszeile** — `ClubTrainer` ist eine eigene Tabelle
(`ClubService.AssignTrainerAsync`). Dasselbe gilt für Gruppen: Wer eine leitet,
steht in `Group.TrainerId` oder `GroupTrainers`, nicht in `GroupMembers`.

Der erste Wurf schaute nur auf Mitgliedschaften — und forderte eine
Vereinstrainerin auf, dem Verein beizutreten, den sie leitet, und einer
Gruppe, die sie führt. Beide Fragen gehen jetzt über
`ClubAccessQueries.BelongsToAnyClubAsync` / `BelongsToAnyGroupAsync`, die
Trainerrollen mitzählen.

Dieselbe Falle steckte in der Dashboard-Karte „Tritt einem Verein bei": Sie
las `/api/clubs/my-memberships` und hielt Trainer:innen für vereinslos. Sie
richtet sich jetzt nach demselben Erststart-Status.

## Zwei Zustände, die leicht verwechselt werden

**Angefragt ≠ offen.** Eine gestellte Beitrittsanfrage wartet auf die Freigabe
durch den Verein. Der Nutzer hat getan, was er tun konnte. Der Schritt zeigt
deshalb „Anfrage gestellt – warte auf Freigabe" statt weiter als offene Aufgabe
dazustehen.

**Weggeklickt ≠ erledigt.** Wer sich erst umsehen will, blendet den Erststart
aus. Das wird **am Nutzer** gespeichert (`users.onboarding_dismissed_at`), nicht
im Browser: Wer den Hinweis auf dem Telefon wegklickt, will ihn auch am Rechner
nicht wiedersehen.

## API

| Endpunkt | |
|---|---|
| `GET /api/onboarding/status` | Woran der Erststart steht |
| `POST /api/onboarding/dismiss` | Wegklicken |

Ein einzelner Aufruf statt fünf: Das Dashboard müsste sonst Hunde, Ziele,
Trainings, Vereins- und Gruppenmitgliedschaften einzeln abfragen, nur um zu
wissen, was als Nächstes dran ist.

`firstDogId` trägt die Verweise der beiden Folgeschritte — der **älteste** Hund,
damit der Erststart nicht auf einen zeigt, den jemand später nebenbei angelegt
hat.

## Zusammenspiel mit dem Dashboard

Das Dashboard hatte schon eine eigene Karte „Tritt einem Verein bei". Solange
der Erststart läuft, spricht er den Vereinsbeitritt bereits an — die zweite
Karte bleibt dann zurück. Zwei Kacheln mit derselben Botschaft wären Lärm.

Fällt der Statusabruf aus, rendert der Erststart einfach nicht. Er ist eine
Hilfe, kein Kernstück; eine Fehlermeldung über etwas, das der Nutzer gar nicht
angefordert hat, wäre schlimmer als sein Fehlen.

## Offen

- **Gruppen, die ein gelöschter Nutzer geleitet hat**, bleiben stehen. Was mit
  ihnen passieren soll, ist eine Entscheidung, keine Aufräumarbeit — deshalb
  fasst `ClubService.PurgeUserAsync` sie bewusst nicht an.
- **Ein zweiter Hund** startet den Erststart nicht neu — richtig so, aber wer
  Jahre später einen Welpen bekommt, bekäme die Anleitung vielleicht gern noch
  einmal. Bisher nur über das Ausblenden-Rücknehmen möglich, das es nicht gibt.
- **Der Sachkunde-Trainer** wäre ein dritter Weg für Einsteiger ohne Hund
  (siehe `docs/SACHKUNDE.md`) — bewusst nicht aufgenommen, damit die Gabelung
  zweiteilig und lesbar bleibt.

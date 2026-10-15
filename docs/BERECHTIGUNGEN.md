# Berechtigungen

Wer was sehen und ändern darf, und wo das im Code entschieden wird. Gilt, seit
ein zweiter, fremder Verein die App nutzt: Die Vereine müssen voneinander
getrennt sein.

## Grundsatz

**Niemand wird ohne eigene Zustimmung Teil eines Vereins, einer Gruppe oder
Mitbesitzer:in eines Hundes.** Wer einlädt, sieht bis zur Zusage nur die
Adresse, die er selbst eingetippt hat - keinen Namen, nicht die Adresse des
Kontos. Eine Einladung zählt nirgends als Mitgliedschaft; erst das Annehmen
macht daraus eine (oder der eigene Antrag der Person, dann wollen beide Seiten
es).

| Was | Status der Einladung | Antwort der Person |
|---|---|---|
| Verein | `ClubMembershipStatus.Invited` (+ `InvitedEmail`) | `ClubService.RespondToInvitationAsync` |
| Gruppe (Mitglied) | `GroupMemberStatus.Invited` | `GroupService.RespondToInvitationAsync` |
| Gruppe (Trainer:in) | `GroupTrainerStatus.Invited` | `GroupService.RespondToTrainerInvitationAsync` |
| Mitbesitz eines Hundes | `DogOwnerStatus.Invited` | `DogService.RespondToInvitationAsync` |

Ausnahme ist allein der Betreiber (Admin): Er darf einen Verein direkt mit
Mitgliedern und Trainer:innen anlegen (`AddMemberAsync`/`AssignTrainerAsync`
mit `isAdmin`).

## Wer ist wer

- **Betreiber (Admin)**: betreibt die App, sieht Kennzahlen, legt Vereine an.
  Kleine Kompromisse bei den Rechten sind hier okay, weil es nur eine Person ist.
- **Vereinstrainer:in** (`ClubTrainer`) mit zwei Rollen:
  - *Verwaltung* (`ClubRole.Verwaltung`): Stammdaten ändern, Trainer:innen
    berufen und abberufen, Mitglieder zur Trainer:in machen.
  - *Training* (`ClubRole.Training`): Mitglieder und Anfragen sehen, einladen,
    Anfragen entscheiden, Gruppen verwalten, Termine planen.
  - Beide sehen alle Gruppen ihres Vereins. Trainer:in wird nur, wer
    **freigegebenes Mitglied dieses Vereins** ist.
- **Gruppentrainer:in**: leitet eine Gruppe (`Group.TrainerId`) oder betreut sie
  mit (`GroupTrainer`, erst nach Annehmen).
- **Mitglied**: freigegebenes Vereinsmitglied (`ClubMembershipStatus.Approved`)
  bzw. aktives Gruppenmitglied (`GroupMemberStatus.Active`). Offene Anfragen und
  Einladungen sind **keine** Mitgliedschaft.
- **Betreuung eines Hundes** (`TrainerAssignment`): Eine Gruppentrainer:in trägt
  sich als Betreuer:in der Hunde eines aktiven Gruppenmitglieds ein und sieht
  dann Trainings, Ziele und Fährten dieser Hunde und darf Rückmeldung geben. Sie
  endet mit dem Austritt aus Gruppe oder Verein.
- **Besitzer:in / Mitbesitzer:in** (`DogOwner`): sieht und ändert alles am Hund.

## Wer sieht was

| | Sieht | Sieht nicht |
|---|---|---|
| Vereinstrainer:in | Mitglieder (Name, E-Mail), offene Anfragen, offene Einladungen (nur die eingegebene Adresse), Gruppen und Termine **des eigenen Vereins** | alles von anderen Vereinen; Tagebuch der Mitglieder (nur über eine Betreuung) |
| Gruppentrainer:in | die eigene Gruppe, deren aktive Mitglieder, die Hunde, die sie betreut | andere Gruppen, offene Einladungen mit Namen |
| Mitglied | die eigenen Hunde, Gruppen und Vereine, offene Einladungen an sich selbst (mit Namen des Vereins) | andere Mitglieder |
| Eingeladene Person | nur den Vereins-/Gruppen-/Hundenamen und wer einlädt | - |

## Wo die Prüfungen liegen

Die Entscheidung steht **im Service**, nie nur am Controller: Dieselbe Aktion
hängt oft an mehreren Routen (Verein, Admin), und eine Regel an der Route geht
beim Hinzufügen der zweiten verloren.

- `ClubAccessQueries` (`Dogity.Application/Abstractions`):
  - `IsClubTrainerAsync` - Trainer:in dieses Vereins (beide Rollen)
  - `CanManageClubAsync` - Rolle Verwaltung
  - `GetManageableGroupAsync` - die Gruppe, wenn man sie verwalten darf
    (Leitung, Mit-Betreuung, Vereinstrainer:in des Vereins der Gruppe)
  - `GetVisibleClubIdsAsync` - Sichtbarkeit von Vereinsübungen (nur Trainer:innen
    und **aktive** Gruppenmitglieder)
  - `BelongsToAnyClubAsync`, `BelongsToAnyGroupAsync`, `IsAnyTrainerAsync` -
    Zugehörigkeit (Erststart, Perspektive)
- `DogAccessQueries.HasDogAccessAsync`: Zugriff auf einen Hund hat, wer Besitzer:in
  ist (`DogOwners`) oder ihn über eine `TrainerAssignment` betreut. Training, Ziele
  und Fährten fragen darüber.
- Verweigert wird verdeckt: "nicht gefunden" (404) statt "darfst du nicht", damit
  niemand erfährt, dass es einen Verein, eine Gruppe oder einen Hund gibt. Das
  gilt für die Vereins-, Hunde-, Training- und Zieldienste. Ausnahme sind ältere
  Dienste (Gruppenmitglieder-Hunde, Betreuung eines Hundes über die Gruppe,
  Terminplanung, Übungsbibliothek): Sie antworten mit einem fachlichen Fehler
  ("Keine Trainer-Berechtigung ..."), geben dabei nichts heraus und ändern
  nichts. `VereinsgrenzenTests` prüft beide Formen.

## Bewusst offen: erneutes Einladen

Nach einer Ablehnung darf der Verein dieselbe Adresse erneut einladen. Es gibt
keine Sperrfrist: Die Person wird dadurch nie Mitglied (sie muss jedes Mal selbst
zusagen), erhält aber jedes Mal eine Benachrichtigung. Wird das zum Lästigen,
wäre eine Frist pro Verein und Person der nächste Schritt.

## Wächter

`backend/tests/Dogity.Application.Tests/Community/VereinsgrenzenTests.cs` baut
zwei Vereine mit je einer Verwaltung auf und prüft, dass die Verwaltung von
Verein B von Verein A, dessen Gruppe, Mitgliedern, Hunden und Tagebüchern
nichts sieht und nichts ändert - und niemanden ohne Zustimmung in Verein B holt.
`ClubEinladungTests.cs` deckt den Einladungsablauf ab. Wer eine neue Abfrage oder
Aktion rund um Vereine, Gruppen oder Hunde einführt, ergänzt sie dort.

Für neue Einladungswege gilt dasselbe wie oben: eingegebene Adresse speichern
(nicht den Namen preisgeben), Antwort der Person abwarten, Status `Invited` an
**keiner** Stelle als Mitgliedschaft zählen, Adresse mit der Antwort löschen und
die Daten im Kontoexport und beim Löschen des Kontos berücksichtigen
(`AccountDataService`).

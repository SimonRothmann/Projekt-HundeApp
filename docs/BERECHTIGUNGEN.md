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
- **Rollen im Verein** (`ClubTrainer`, eine Zeile je Verein und Person mit einer
  *Menge* von Rollen, `ClubRole` als Bitmenge): Die Rollen sind frei
  kombinierbar (etwa Training + Vorstand), die Rechte addieren sich, Verwaltung
  schließt alles ein. Die Tabelle heißt weiter `club_trainers`, auch wenn
  Mitgliederverwaltung und Vorstand keine Trainer:innen sind.
  - *Training* (1): leitet Gruppen, plant deren Termine, pflegt den
    Vereinskatalog - **nur die eigenen Gruppen** (Leitung oder Mit-Betreuung).
  - *Verwaltung* (2): alles - Stammdaten ändern, alle Rollen vergeben und
    entziehen, dazu die Rechte aller anderen Rollen.
  - *Ausbildungswart:in* (4): übergeordnete:r Trainer:in - alle Gruppen und
    Termine des Vereins, Vereinskatalog, Trainer:innen (nur die Rolle Training)
    berufen und abberufen und ihnen Gruppen zuordnen. Vergibt **keine** anderen
    Rollen.
  - *Mitgliederverwaltung* (8): Beitrittsanfragen, Einladungen, Einladungslink,
    Mitglieder hinzufügen und entfernen, Mitgliederliste - **ohne** Trainerrechte
    (keine Gruppen, kein Katalog, keine Termine) und **keine Trainer:in**
    (`IsAnyTrainerAsync` bleibt `false`, keine Identity-Rolle TRAINER).
  - *Vorstand* (16): nur lesen (Zahlen und Mitgliederliste mit Namen und
    Gruppenzuordnung). Die Vereinsübersicht dafür kommt mit Paket V2; bis dahin
    gibt die Rolle noch keine Rechte.
  - *Wer Rollen vergibt:* Verwaltung und Admin alle. Eine Ausbildungswart:in darf
    nur das Bit Training setzen und löschen - eine Änderung, die sich in einem
    anderen Bit unterscheidet, wird abgewiesen (also auch kein Abberufen
    einer Person, die zusätzlich z. B. Vorstand ist). Die letzte Verwaltung
    kann sich weder entziehen noch abberufen werden (auch nicht durch den Admin).
    Eine leere Rollenmenge entfernt die Zeile. Rollen bekommt nur, wer
    **freigegebenes Mitglied dieses Vereins** ist (der Admin darf davon abweichen).
  - *Nachfolge:* Fällt die letzte Verwaltung eines Vereins durch Kontolöschung
    (`AccountDataService.PurgeAsync`) oder Aufräumen verwaister Daten
    (`CommunityOrphanCleanup`) weg, wird die dienstälteste verbleibende Person
    **mit einer Trainer-Rolle** (früheste Zuweisung, bei Gleichstand nach Id) zur
    Verwaltung und benachrichtigt (`ClubNachfolge`); Mitgliederverwaltung und
    Vorstand behält sie. Bleibt eine zweite Verwaltung, rückt niemand nach; gibt
    es keine Person mit Trainer-Rolle mehr, bleibt der Verein ohne Verwaltung,
    bis ein Admin jemanden einsetzt.
  - *Neue Rolle:* Die Person bekommt eine Benachrichtigung (Training und
    Ausbildungswart:in: „Du bist jetzt Trainer bei ...“, sonst „Du hast im Verein
    ... eine neue Rolle bekommen.“); wer sich selbst Rollen gibt, bekommt keine.
  - *Letzte Verwaltung:* Sie kann weder gehen noch sich herabstufen noch den
    Verein verlassen oder entfernt werden (auch nicht durch den Admin).
  - *Migration der Altwerte:* Die Umrechnung der alten Textwerte in Bitmengen
    (`Training` -> 1, `Verwaltung` -> 2) ist gegen echtes Postgres von Hand
    geprüft; die Tests laufen ohne Postgres und prüfen nur die festen Bitwerte.
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
| Mitgliederverwaltung, Verwaltung | Mitglieder (Name, E-Mail), offene Anfragen, offene Einladungen (nur die eingegebene Adresse) **des eigenen Vereins**, den Einladungslink | alles von anderen Vereinen; Tagebuch der Mitglieder (nur über eine Betreuung) |
| Ausbildungswart:in, Verwaltung | alle Gruppen und Termine **des eigenen Vereins** (mit Mitgliedern, Anmeldungen), die Liste der Rollen im Verein | die Mitgliederliste und Anfragen des Vereins (nur mit Mitgliederverwaltung bzw. Verwaltung); alles von anderen Vereinen |
| Training | die eigenen Gruppen (Leitung, Mit-Betreuung), deren Termine, Anfragen und Anmeldungen, den Vereinskatalog; die Gruppen des Vereins in der Vereinsliste (wie jedes Mitglied) | fremde Gruppen und deren Termine, Mitgliederliste, Vereinsanfragen |
| Vorstand | Gruppen des Vereins in der Vereinsliste (wie jedes Mitglied) | noch nichts darüber hinaus (Paket V2) |
| Gruppentrainer:in | die eigene Gruppe, deren aktive Mitglieder, die Hunde, die sie betreut | andere Gruppen, offene Einladungen mit Namen |
| Mitglied | die eigenen Hunde, Gruppen und Vereine, offene Einladungen an sich selbst (mit Namen des Vereins) | andere Mitglieder |
| Eingeladene Person | nur den Vereins-/Gruppen-/Hundenamen und wer einlädt | - |
| Besitzer:in / Mitbesitzer:in eines Hundes | Trainingspausen mit Grund (verletzt, krank, läufig), Zeitraum und Notiz; trägt sie ein, ändert und löscht sie | - |
| Besitzer:in / Mitbesitzer:in eines Hundes | Gesundheitseinträge (Impfungen, Entwurmung, Parasitenschutz, Tierarzttermine) und den Gesundheitsstand am Hund; trägt sie ein, ändert und löscht sie; bekommt die Erinnerungen dazu | - |
| Betreuende Trainer:in (`TrainerAssignment`) | Trainingspausen nur als Zeitraum („pausiert bis …“); die Pausenliste nur mit laufenden und kommenden Pausen, im Plan zusätzlich die Zeiträume vergangener Pausen im Planfenster | Grund und Notiz (Gesundheitsangaben gehören der Familie); kann keine Pause eintragen. Der Server baut dafür ein eigenes DTO ohne diese Felder (`DogPauseQueries.ToDto`). |
| Besitzer:in / Mitbesitzer:in eines Hundes | Trainings, Bewertungen, Notizen und Fährten des Hundes: ändert und löscht alles, auch Einträge der Betreuenden | - |
| Betreuende Trainer:in (`TrainerAssignment`), zu Trainings | alle Trainings, Bewertungen, Notizen und Fährten des betreuten Hundes; gibt auf allen Feedback und Trainer-Bewertung | ändert und löscht nur Einträge, die sie selbst eingetragen hat (Training löschen, Datum, Tagesnotiz, Ort/Zeit/Verfassung, Übungsbewertung und -notiz, Fährte löschen); bei Einträgen der Familie antwortet der Server mit 404. Abläufe auf Fährten sind davon bewusst ausgenommen. |
| Betreuende Trainer:in, zu Gesundheitseinträgen | nichts: 404 auf `/api/dogs/{id}/gesundheit`, am Hund kein Gesundheitsstand (docs/GESUNDHEIT.md) | alles |
| Trainer:in ohne Betreuung, Fremde | nichts (404) | alles |

## Wo die Prüfungen liegen

Die Entscheidung steht **im Service**, nie nur am Controller: Dieselbe Aktion
hängt oft an mehreren Routen (Verein, Admin), und eine Regel an der Route geht
beim Hinzufügen der zweiten verloren.

- `ClubAccessQueries` (`Dogity.Application/Abstractions`):
  - Eine Definition je Recht, aus Rollenmengen (`ClubRollen`):
    `IsClubTrainerAsync` (Training, Ausbildungswart:in, Verwaltung: Katalog,
    Gruppen anlegen, Bibliothek), `CanManageAllGroupsAsync` (Ausbildungswart:in,
    Verwaltung), `CanManageMembersAsync` (Mitgliederverwaltung, Verwaltung),
    `CanAppointTrainersAsync` (Ausbildungswart:in, Verwaltung),
    `CanViewClubOverviewAsync` (Vorstand, Verwaltung - erst mit V2 genutzt),
    `CanManageClubAsync` (Verwaltung). `MitRolle` filtert Abfragen über viele
    Zeilen nach derselben Menge. Services vergleichen keine Rollen selbst.
  - `GetManageableGroupAsync` - die Gruppe, wenn man sie verwalten darf
    (Leitung, Mit-Betreuung, oder alle Gruppen des Vereins der Gruppe)
  - `GetVisibleClubIdsAsync` - Sichtbarkeit von Vereinsübungen (nur Personen mit
    Trainer-Rolle und **aktive** Gruppenmitglieder; Mitgliederverwaltung und
    Vorstand allein sehen den Katalog des Vereins nicht)
  - `BelongsToAnyClubAsync`, `BelongsToAnyGroupAsync`, `IsAnyTrainerAsync` -
    Zugehörigkeit (Erststart, Perspektive)
- `DogAccessQueries`: die einzige Stelle der Hundezugriffsregel. Zwei Stufen,
  beide nur für Hunde, die es noch gibt (zu einem weich gelöschten Hund hat
  niemand Zugriff, es gibt 404 statt einer leeren Liste):
  - **Besitzer:in** - aktive Zeile in `DogOwners`, auch Mitbesitzer:innen
    (`IstBesitzerAsync`, einbettbar `EigeneHundeIds`; `BesitzerIds` fragt
    umgekehrt nach den Besitzer:innen eines Hundes).
  - **Zugriff** - Besitzer:in ODER betreuende Trainer:in über eine
    `TrainerAssignment` (`HasDogAccessAsync`, einbettbar `ZugaenglicheHundeIds`).
    Training, Ziele und Fährten fragen darüber - das gilt für das **Lesen**.
  Wer einen Hund in einer Abfrage über viele Zeilen prüft, bettet die
  `...Ids`-Abfrage ein (`.Where(s => db.ZugaenglicheHundeIds(userId).Contains(s.DogId))`)
  und schreibt für die Prüfung keinen eigenen Ausdruck auf `DogOwners`/`TrainerAssignments`.
  Bewusst anders und deshalb nicht darüber: "Hauptbesitzer:in" (`Role == Owner`,
  Mitbesitzer:innen einladen/entfernen) und die Frage "ist diese Person die
  zugewiesene Trainer:in" (Feedback, Bewertung, Trainerübersicht).
- `TrainingsRechte` (`Dogity.Application/Training`): **Ändern und Löschen** von
  Trainings samt Übungen, Bewertungen, Notizen, Ort/Zeit und Fährten. Besitzer:innen
  dürfen alles am Hund, betreuende Trainer:innen nur Einträge, die sie selbst
  eingetragen haben (`TrainingSession.UserId`); Feedback und Trainer-Bewertung
  auf Einträgen der Familie bleiben ihnen. Der Server liefert die Auskunft als
  `darfBearbeiten` an Trainings und Protokolleinträgen der Plan-Ziele, die
  Oberfläche blendet danach die Bearbeiten-Knöpfe aus. Die Tageseinheit wird
  getrennt geführt (`EinheitDesTagesAsync`): ein Eintrag der Trainer:in landet
  nie in der Einheit der Familie, sondern in einer eigenen am selben Tag; bei
  mehreren Einheiten gilt die älteste zuerst. Das Tagebuch zeigt beide
  weiterhin auf einer Tageskarte. Abläufe auf Fährten (walk runs) sind bewusst
  nicht eingeschränkt. `TrainerRechteTests` prüft die Regel.
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
`VereinsrollenTests.cs` prüft je Rolle, was sie darf (Matrix Rolle x Recht), die
Kombinationen, die Grenzen der Ausbildungswart:in und die letzte Verwaltung.
`ClubEinladungTests.cs` deckt den Einladungsablauf ab. Wer eine neue Abfrage oder
Aktion rund um Vereine, Gruppen oder Hunde einführt, ergänzt sie dort.

Für neue Einladungswege gilt dasselbe wie oben: eingegebene Adresse speichern
(nicht den Namen preisgeben), Antwort der Person abwarten, Status `Invited` an
**keiner** Stelle als Mitgliedschaft zählen, Adresse mit der Antwort löschen und
die Daten im Kontoexport und beim Löschen des Kontos berücksichtigen
(`AccountDataService`).

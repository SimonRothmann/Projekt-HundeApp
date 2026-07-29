# Vereins-Trainingsbibliothek (Gruppentraining)

Ersetzt das frühere Modell aus `GROUP_TRAINING_PLANS.md` (geseedete Welpen-/
Junghunde-Vorlagen zum „in Gruppe kopieren"). Entscheidung im Grilling-Interview
2026-07-29: nicht der Seeder liefert Gruppeninhalte, sondern die **Trainer eines
Vereins** bauen sie gemeinsam auf.

## Idee

Jeder **Verein** hat eine geteilte Trainingsbibliothek:

- **Baustein** (`GroupTrainingExercise`) = wiederverwendbare Übung (Titel, Fokus,
  Dauer, Ablauf-Beschreibung). Optional **Prüfungs-Tags** {BH, IBGH1, IBGH2,
  IBGH3} = „bereitet auf diese Prüfung(en) vor" (reine Labels, keine harte
  Katalog-Kopplung – gemischte Gruppen bilden sich so ab).
- **Einheit** (`GroupTrainingUnit`) = geordnete Mischung aus Bausteinen
  (`GroupTrainingUnitItem` referenziert einen Baustein + `SortOrder`). Eine
  wiederverwendbare Vorlage, nicht an Gruppe/Datum gebunden.

**Kategorien / Progression**: Welpen → Junghunde → **Basis** (Grundausbildung,
führt Richtung BH/IBGH). `GroupTrainingCategory` = Puppy / YoungDog / Basis.

## Sichtbarkeit & Rechte

- Alles ist **verein-weit geteilt**: jede:r **ClubTrainer** des Vereins sieht +
  nutzt Bausteine und Einheiten.
- **Voll geteilt**: jede:r Vereinstrainer:in darf anlegen, bearbeiten, löschen
  (Ersteller:in nur zur Info). Eigene Varianten via **Einheit duplizieren**
  („als Kopie anpassen"), statt das geteilte Original umzuschreiben.
- Trainer ohne Verein / reine Einzeltrainer:innen sehen das Feature nicht.
- Bei mehreren Vereinen wählt das Frontend den Verein-Kontext (die eigenen
  Vereine kommen aus `GET /api/groups/my-clubs`).

## Kein Seed, aber ein Starthilfe-Katalog

Der `GroupTrainingSeeder` wurde entfernt – nichts wird automatisch angelegt.
Als Starthilfe kann ein Trainer den fachlichen Best-Practice-Starterkatalog
(`GroupTrainingStarterCatalog`) **per Klick** in seinen Verein übernehmen
(`POST clubs/{clubId}/import-starter`, idempotent auf Titel-Ebene). Danach sind
die Inhalte ganz normale, frei editier-/löschbare Vereins-Bausteine/-Einheiten.

Der Katalog deckt Welpen/Junghunde/Basis ab, enthält Alltagstraining und
Hinterhandarbeit in allen Stufen; Junghunde-/Basis-Einheiten beginnen jeweils
mit einer anderen Leinenführigkeits-/Freifolge-Übung. Der `SportCatalogSeeder`
(individuelle Prüfungs-/Sportübungen) bleibt davon unberührt.

## Datenmodell

- `GroupTrainingExercise`: `ClubId`, `Category`, `Title`, `Focus?`,
  `DurationMinutes?`, `Description?`, `ExamTargets` ([Flags] als int),
  `CreatedByUserId?`.
- `GroupTrainingUnit`: `ClubId`, `Category`, `Title`, `Description?`,
  `CreatedByUserId?`, `Items[]`.
- `GroupTrainingUnitItem`: `GroupTrainingUnitId`, `GroupTrainingExerciseId`
  (Restrict; beim Baustein-Löschen entfernt der Service die Referenzen),
  `SortOrder`.

Migration `RebuildGroupTrainingLibrary` (löscht die wegwerfbaren Alt-Daten des
ersetzten Modells und baut die Tabellen um).

## API (`api/group-training`, alle ClubTrainer-gated)

- `GET clubs/{clubId}/library` → `{ clubId, clubName, exercises[], units[] }`
- `POST clubs/{clubId}/import-starter` → übernimmt den Starterkatalog (idempotent)
- `POST clubs/{clubId}/exercises` · `PUT exercises/{id}` · `DELETE exercises/{id}`
- `POST clubs/{clubId}/units` · `PUT units/{id}` · `DELETE units/{id}` ·
  `POST units/{id}/duplicate`

Einheit-Requests übergeben `ExerciseIds` in der gewünschten Reihenfolge.

## Spätere Ausbaustufe

Tiefe Verknüpfung der Basis-Bausteine mit den echten BH-/IBGH-Prüfungsübungen
aus dem Sport-Katalog (statt reiner Tags) – bewusst zurückgestellt.

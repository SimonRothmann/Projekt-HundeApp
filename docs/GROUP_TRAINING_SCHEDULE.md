# Gruppentraining-Terminplanung

Baut auf der Vereins-Trainingsbibliothek (`GROUP_TRAINING_LIBRARY.md`) auf.
Entscheidungen im Grilling-Interview 2026-07-29.

## Idee

Trainer planen **Termine** fürs Gruppentraining: **wann** (Gruppe + Datum/
Uhrzeit) und **was** (Inhalt = geordnete Mischung aus **Bausteinen** + Freitext).
Mitglieder sehen die Termine ihrer Gruppen **read-only**.

## Modell

- `GroupTrainingSession`: ClubId, GroupId, Category (Welpen/Junghunde/Basis),
  StartsAt, DurationMinutes, Location? (z.B. Wald/Parkplatz/Biergarten), Notes?,
  Status (Planned/Cancelled), CreatedByUserId.
- `GroupTrainingSessionItem`: Referenz auf einen `GroupTrainingExercise`
  (Baustein) **ODER** FreeText, + SortOrder.
- `GroupTrainingSessionTrainer`: mehrere zuständige Trainer:innen je Termin.

Migration `AddGroupTrainingSchedule` (rein additiv, 3 neue Tabellen).

## Rechte & Sichtbarkeit

- **Jede:r ClubTrainer** plant/bearbeitet/sagt ab/löscht Termine des Vereins
  (gemeinsames Planen/Vertretung); **keine formalen Sub-Rollen** – der
  persönliche Kalender entsteht über Gruppe + Trainer-Zuweisung + Filter
  (Gruppe/Kategorie/„nur meine").
- **Mitglieder** sehen die Termine der Gruppen, in denen sie aktives Mitglied
  sind (read-only), inkl. Inhalt/Ort/Absage.

## Serien-Generator

Das Frontend rechnet „Wochentag + Uhrzeit + Zeitraum" (zeitzonen-korrekt im
Browser) in konkrete Zeitpunkte um und postet sie; der Server materialisiert
**eigenständige** Einzeltermine (danach einzeln editier-/absagbar) – bewusst
**keine** abstrakte Wiederholungsregel mit Ausnahmen-Handling.

## Mix-Generator (Inhalt)

`GroupTrainingMixGenerator` komponiert einen Inhalts-Entwurf aus den Bausteinen
des Vereins nach Kategorie:

- **Welpen**: Ankommen → Entspannen → Futterhand → 1+ wechselnde Übung(en) →
  Spielen zum Abschluss.
- **Junghunde/Basis**: **Leinenführigkeit** zuerst + Zusatz aus **Ablenkung /
  Ablage / Hinterhand** (wechselnd).

Auswahl nach **Fokus** (mit Fallback-Priorität); fehlt ein Fokus im Pool, wird
der Slot übersprungen. Ergebnis ist immer ein **editierbarer Entwurf** – der
Trainer generiert, übernimmt eine Bibliotheks-Einheit oder mischt manuell und
passt Reihenfolge/Inhalt frei an. Der Starter-Katalog deckt die Struktur-Slots
mit passenden Fokus-Labels ab (Futterhand/Spielen/Ablage/Ablenkung …).

## API (`api/group-training/schedule`)

- `GET clubs/{clubId}?from&to&groupId&category&mineOnly` – Vereins-Kalender (Trainer).
- `GET mine?from` – Termine der eigenen Gruppen (Mitglieder, read-only).
- `POST clubs/{clubId}/sessions` · `PUT sessions/{id}` · `POST sessions/{id}/cancel` · `DELETE sessions/{id}`.
- `POST clubs/{clubId}/series` – Serie materialisieren.
- `GET clubs/{clubId}/generate-content?category` – Mix-Generator-Entwurf.

## Frontend

- Trainer: `/trainer/schedule` (Agenda + Filter, Termin/Serie anlegen/bearbeiten,
  Inhalt generieren/aus Bibliothek/manuell, Ort, Absage/Löschen).
- Mitglieder: „Nächste Gruppentrainings"-Sektion auf dem Dashboard.

## Zurückgestellt (Follow-up)

- **Co-Trainer-Auswahl** im UI (Backend unterstützt mehrere zuständige
  Trainer:innen; v1 weist den planenden Trainer automatisch zu). Braucht einen
  sicheren „Vereinstrainer auflisten"-Endpoint.
- **Benachrichtigungen** bei neuem/abgesagtem Termin.

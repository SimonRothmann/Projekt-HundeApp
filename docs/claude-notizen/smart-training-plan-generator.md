---
name: smart-training-plan-generator
description: Dogity adaptiver Trainingsplan-Generator — IMPLEMENTIERT & auf Prod (Leitner-Mastery, wöchentliche Auto-Regen); docs/SMART_TRAINING_PLAN.md ist als „geplant" veraltet
metadata: 
  node_type: memory
  type: project
  originSessionId: c7704137-f57e-4eda-afed-38612467dea5
  modified: 2026-08-10T13:35:50.193Z
---

Der „klügere, adaptive Trainingsplan-Generator" ist **umgesetzt und auf Prod** (Stand 2026-08-10) — NICHT mehr nur geplant. `docs/SMART_TRAINING_PLAN.md` trägt noch „Status: geplant", das ist **veraltet**; vor Aussagen den Code prüfen.

**Was tatsächlich existiert (verifiziert am Code):**
- Domain `ExerciseMastery` (je Hund+Übung: Leitner-`Box` 1–5, `DueAt`, `RecentAvgRating`, `SessionCount`, `ManualPriority`) + Migration `AddAdaptivePlanningModel`.
- `ExerciseMasteryService` (`ApplyLogAsync` nach jedem geloggten Training via `TrainingService`, `BackfillIfEmptyAsync` beim Start, `SetManualPriorityAsync`).
- `AdaptivePlanGenerator.GenerateWeek` (Slot-Budget ~50/30/20 Schwäche/Wiederholung/Neu, Score = überfällig + 3·Schwäche + 3·ManualPriority) — wird von `GoalService` genutzt.
- `PlanRegenerationBackgroundService` (wöchentlich, `RegenerateDuePlansAsync`); `RegenerateWeekCoreAsync` **erhält erledigte + manuelle Items**, ersetzt nur fortschrittslose Auto-Slots. Aktueller-Woche-Anker = `Goal.CreatedAt`.
- Frontend surfacet den Grund je Übung (`PlanItemReason`: Schwäche/Wiederholung/Neu) in `goal-plan-card.tsx`.

**Feature „Übungen gewichten" (ManualPriority-UI) — 2026-08-10 gebaut:** Owner setzt je PO-Übung eines Ziels einen 5-Stufen-Regler (−2…+2), wirkt ab nächster Woche. Endpoints `GET /api/goals/{id}/weightable-exercises`, `PUT /api/goals/{id}/exercises/{exerciseId}/priority`; Frontend `exercise-weighting-sheet.tsx` (Sheet auf der Ziel-Karte).

Noch offen als spätere Einzeltraining-Ideen (aus Grilling): Prüfungsreife-Score + Probeprüfung, Fortschritts-Analyse. [[deployment-workflow-user-deploys]] fürs Ausrollen, [[mobile-app-first-no-horizontal-scroll]] fürs UI.

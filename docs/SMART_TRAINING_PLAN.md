# Klügerer, adaptiver Trainingsplan-Generator — Umsetzungsplan

Status: **geplant** (noch nicht implementiert). Entscheidungen mit dem Nutzer abgestimmt.
Vor der Umsetzung: aktuellen `TrainingPlanGenerator` + `GoalService` gegen die Ist-Annahmen (Abschnitt 1) abgleichen.

## 1. Ist-Zustand (bei Umsetzung am Code bestätigen)
- Plan wird **einmalig bei Zielerstellung** erzeugt (regulations-/prüfungsbezogen, ≥4 Übungen/Woche, gruppiert nach `weekNumber`).
- Statisch: keine Reaktion auf Trainingsverlauf, keine Schwierigkeits-Sortierung, keine Wiedervorlage, kein Mastery-Tracking.
- Ungenutzte Signale: pro `TrainingExercise` gibt es `rating` (1–5), `success`, `date`, optional `trainerRating` → echte Leistungshistorie je Übung.

## 2. Soll-Zustand
Adaptiver Planer, der pro Woche Übungen wählt aus **Schwierigkeit**, **individueller Leistungshistorie** und **Wiedervorlage-Intervallen**. Gut sitzende Übungen kommen in Intervallen wieder, schwache häufiger, neue/schwerere gestaffelt. Plan wird **wöchentlich automatisch** neu erzeugt und ist **auf Trainingstage** verteilt.

## 3. Entscheidungen des Nutzers (verbindlich)
1. **Wochenumfang**: konfigurierbar pro Ziel **mit** sinnvollem Default (≥4). Zusätzlich **Trainingstage pro Woche** definierbar → Plan ist tages-strukturiert (Woche → Tage → Übungen).
2. **Auto-Regen**: **stabile, persistente Lösung** (kein Wegwerf-MVP). → echter Scheduler + persistenter Zustand.
3. **Mastery**: **persistiert** (folgt aus 2), nicht nur on-the-fly abgeleitet.
4. **Manuelles Editieren**: bleibt erhalten; **manuelle/Trainer-Übungen werden höher gewichtet** (Absprache mit Trainer) und zählen als Teil des bestehenden Plans/der Punkte. Nie von der Auto-Generierung überschrieben.
5. **Aggressivität**: konservativ starten (siehe empfohlene Gewichte, Abschnitt 6).

## 4. Datenmodell (neu/erweitert) — 1–2 additive Migrationen
- **`Goal`**: `WeeklyExerciseCount` (int, Default z.B. 5), `TrainingDaysPerWeek` (int) **oder** `TrainingWeekdays` (Set von Wochentagen), `LastPlanGeneratedAt` (DateTimeOffset?).
- **`TrainingPlanItem`**: `DayIndex` (welcher Trainingstag der Woche, 1..n), `Source` (`Auto` | `Manual` | `Trainer`), `Reason` (`Schwachstelle` | `Wiederholung` | `Neu`), optional denormalisierte `Difficulty` (Sortierung/Anzeige).
- **Neue Entity `ExerciseMastery`** (persistenter Wiedervorlage-Zustand): `DogId`, `ExerciseId` (bzw. Exercise-Key inkl. Freitext), optional `GoalId`, `Box` (1–5 Leitner), `LastTrainedAt`, `DueAt`, `RecentAvgRating`, `SessionCount`, **`ManualPriority`** (int, Default 0; manueller Score-Einfluss durch Nutzer/Trainer, z.B. −2..+2 „diese Übung mehr/weniger üben"). Eindeutig je (Dog, Exercise[, Goal]).

## 5. Mastery-Modell (Leitner-Boxen — einfach, robust, erklärbar)
Nach **jedem geloggten** `TrainingExercise` (Update in `TrainingService`):
- `success && rating ≥ 4` → `Box = min(5, Box+1)`
- `rating == 3` (oder success mit rating 3) → `Box` unverändert
- `!success || rating ≤ 2` → `Box = max(1, Box−1)`
- Intervall je Box (Tage): **Box1=2, Box2=4, Box3=7, Box4=14, Box5=28** → `DueAt = LastTrainedAt + Intervall[Box]`.
- Effekt: gemeistert (Box5) taucht ~alle 4 Wochen wieder auf (**„sitzt gut → kommt im Intervall wieder"**); schwach (Box1) alle 2 Tage.

## 6. Wochen-Auswahl — **Slot-Budget** (empfohlen, konservativ & vorhersehbar)
Statt reinem Score-Ranking ein festes Budget je Woche (N = `WeeklyExerciseCount`), das Vielfalt garantiert:
- **Zuerst** alle offenen **manuellen/Trainer-Übungen** setzen (Source=Manual/Trainer) — sie belegen Slots vorrangig.
- Restliche Slots nach Budget:
  - **~50 % Schwachstellen / überfällig** (`DueAt` überschritten und/oder `avgRating < 3`, `success=false`),
  - **~30 % Wiederholung** (hohe Mastery, aber `DueAt` fällig → Spaced Repetition),
  - **~20 % Neu** (noch nicht/kaum trainiert), **nach Schwierigkeit gestaffelt** eingeführt und gegated an Mindest-Mastery der leichteren (nicht „alles Schwere sofort").
- Innerhalb jedes Buckets nach **Score** ranken:
  `score = overdue + W_weak·weakness + W_trainer·trainerWeight + W_new·newReady + W_manual·manualPriority`
  - `overdue = max(0, today − DueAt in Tagen)`
  - `weakness = clamp(3 − recentAvgRating, 0, 2) + failureRate`
  - `trainerWeight` = 1 falls Source∈{Manual,Trainer}, sonst 0
  - `newReady` = 1, falls die nächst-schwerere noch nicht gestartete Übung freigeschaltet ist (Prerequisites gemeistert)
  - `manualPriority` = vom Nutzer/Trainer gesetzter Boost je Übung (`ExerciseMastery.ManualPriority`, z.B. −2..+2). Erlaubt gezieltes „diese Übung mehr/weniger üben" unabhängig von der Historie.

**Empfohlene Startgewichte (konservativ, tunebar):**
- `W_weak = 3` (eine schwache Übung ≈ 3–6 Tage überfällig → kommt bald wieder, aber nicht spammy)
- `W_trainer = 4` (Trainer/manuell klar bevorzugt, ohne die Woche zu monopolisieren)
- `W_new = 2` (neue Übungen stetig, aber fällig/schwach hat Vorrang)
- `W_manual = 3` (Nutzer/Trainer-Boost je Übung wirkt spürbar: ManualPriority +1 ≈ „3 Tage überfällig")
- Guardrails: keine Übung > 1× pro Trainingstag; pro Woche mind. 1 „Neu"-Slot solange Kandidaten offen; Budget-Prozente auf Integer runden (Rest zugunsten Schwachstellen).

## 7. Verteilung auf Trainingstage
- Die N Wochen-Übungen auf die definierten Trainingstage verteilen (Round-Robin oder balanciert je Tag).
- **Innerhalb eines Tages nach Schwierigkeit aufsteigend** ordnen (leicht → schwer, Aufwärm-Progression).
- Persistiert via `TrainingPlanItem.DayIndex`.

## 8. Auto-Regenerierung (persistent + Scheduler)
- **`BackgroundService`** (in-process, `PeriodicTimer`, täglich): erzeugt für jedes aktive Ziel die **kommende Woche**, wenn die aktuelle endet bzw. `LastPlanGeneratedAt` fällig ist. (Alternative Quartz/Hangfire nur falls später mehr Jobs nötig — für einen Job überdimensioniert.)
- **Manueller Endpoint** `POST /api/goals/{id}/regenerate-week` + Button „Diese Woche neu generieren".
- **Historie schützen (idempotent):** nur **zukünftige/unbegonnene** Wochen (neu) generieren; bereits geloggte Items und manuelle/Trainer-Items bleiben unangetastet.
- Optional: Benachrichtigung „Neuer Wochenplan für Bello" (bestehendes Notification-System).

## 9. API + Frontend
- API: Ziel-Konfig (WeeklyExerciseCount, Trainingstage) beim Anlegen/Bearbeiten; `regenerate-week`; Generierung lazy als Fallback beim Abruf.
- Frontend (`goal-plan-card`): Konfig-Felder (Übungen/Woche, Trainingstage), **tages-gruppierte** Wochenansicht, **Grund-Badges** (Schwachstelle/Wiederholung/Neu), optional Mastery-Indikator, „Woche neu generieren"-Button.

## 10. Phasen (spätere Umsetzung in Scheiben)
- **P1 – Datenmodell:** ✅ erledigt (Migration `AddAdaptivePlanningModel`). Goal: `WeeklyExerciseCount` (Default 5), `TrainingDaysPerWeek` (Default 2), `LastPlanGeneratedAt`. `TrainingPlanItem`: `DayIndex` (Default 1), `Source` (Default Auto), `Reason`, `Difficulty`. Neue Entity `ExerciseMastery` (inkl. `ManualPriority`) + Unique-Index (Dog, Übung).
- **P2 – Mastery-Update:** ✅ erledigt. `ExerciseMasteryService` (Leitner-Boxen 2/4/7/14/28 Tage + EMA der Bewertungen); Hook in `TrainingService.CreateAsync` (beide Zweige, im selben SaveChanges); einmaliger, idempotenter Backfill (`BackfillIfEmptyAsync`, beim Anwendungsstart nach den Seedern). 5 Unit-Tests.
- **P3 – Generator v2:** pure Auswahlfunktion (Slot-Budget + Score + Tagesverteilung + Schwierigkeits-Sortierung), idempotent, unit-getestet.
- **P4 – Scheduler + API:** `BackgroundService`, `regenerate-week`, Schutz manueller/geloggter Items.
- **P5 – Frontend:** Konfig-UI, tages-gruppierte Ansicht, Grund-Badges, Regen-Button, Notification.

## 11. Tests (Generator v2 als pure Funktion)
Input: Katalog (Regulations-Übungen + Schwierigkeit) + Mastery/Historie + `today` + Ziel-Konfig → deterministisch.
Fälle: schwache Übung nächste Woche wieder · gemeisterte Übung nach Intervall wieder · Sortierung nach Schwierigkeit je Tag · genau N/Woche über Tage verteilt · Trainer/manuell bevorzugt & geschützt · „nicht alles Schwere sofort" · idempotent (kein Überschreiben geloggter/vergangener Wochen).

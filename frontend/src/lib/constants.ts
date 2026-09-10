import type { ExerciseDifficulty } from "@/lib/types";

// War vorher in 4 Dateien (sports/page.tsx, dogs/[id]/page.tsx,
// global-exercises-section.tsx, club-exercises-section.tsx) identisch
// dupliziert.
//
// Seit 2026-09-10 nur noch in der Katalogpflege zu sehen. An den Übungen
// selbst (Prüfungskatalog, Übungsauswahl, Druckansicht) standen die Stufen
// auf Wunsch des Betreibers nicht mehr: "total unnötig und irreführend" -
// eine Pflichtübung der Prüfung ist nicht "Einsteiger". Der Wert bleibt
// trotzdem gepflegt, weil die Planer daraus die Wiederholungen ableiten
// (TrainingPlanGenerator, AdaptivePlanGenerator).
export const difficultyLabel: Record<ExerciseDifficulty, string> = {
  0: "Einsteiger",
  1: "Fortgeschritten",
  2: "Erfahren",
};

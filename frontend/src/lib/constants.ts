import type { ExerciseDifficulty } from "@/lib/types";

// War vorher in 4 Dateien (sports/page.tsx, dogs/[id]/page.tsx,
// global-exercises-section.tsx, club-exercises-section.tsx) identisch
// dupliziert.
export const difficultyLabel: Record<ExerciseDifficulty, string> = {
  0: "Einsteiger",
  1: "Fortgeschritten",
  2: "Erfahren",
};

import type { TrainingSession } from "@/lib/types";

/**
 * Eine Übungszeile im Formular "Neues Training".
 *
 * Steht hier und nicht in der Formularkomponente, damit das Bauen der Zeilen
 * für sich prüfbar bleibt - es ist die Stelle, an der aus einer gespeicherten
 * Einheit wieder ein Formular wird.
 */
export type ExerciseRow = {
  sportId: string;
  // Für spontane Spaß-/Sonstige Übungen, die nicht im Katalog stehen und
  // nicht extra dort angelegt werden sollen (siehe TrainingExercise.FreeTextLabel) -
  // schließt Sportart/Übung/Plan-Ziel-Auswahl aus, dafür freier Text.
  exerciseId: string;
  isFreeText: boolean;
  freeText: string;
  rating: number;
  success: boolean;
  notes: string;
  trainingPlanItemId: string;
};

/** Vorbelegte Bewertung einer neuen Zeile: die Mitte, kein Urteil. */
export const VORGABE_BEWERTUNG = 3;

export function emptyRow(sportId = ""): ExerciseRow {
  return {
    sportId,
    exerciseId: "",
    isFreeText: false,
    freeText: "",
    rating: VORGABE_BEWERTUNG,
    success: true,
    notes: "",
    trainingPlanItemId: "",
  };
}

/**
 * Die zuletzt gewählte Sportart einer Zeilenliste.
 *
 * Nicht einfach die letzte Zeile: war die eine Freitext-Übung ("Spaziergang"),
 * trägt sie gar keine Sportart, und die nächste Zeile stünde wieder leer da.
 */
export function letzteSportart(zeilen: ExerciseRow[]): string {
  for (let i = zeilen.length - 1; i >= 0; i--) {
    if (zeilen[i].sportId) return zeilen[i].sportId;
  }
  return "";
}

/**
 * Baut aus einer gespeicherten Einheit die Zeilen für eine neue.
 *
 * Übernommen wird der ÜBUNGSSATZ - das, was sich von Woche zu Woche nicht
 * ändert. Bewusst NICHT übernommen wird die Bewertung: sie ist der eine Wert,
 * der jedes Mal ein anderer ist, und sie ist die Grundlage, auf der der
 * adaptive Plangenerator Schwächen erkennt. Eine mitgeschleppte Bewertung von
 * letzter Woche wäre keine Zeitersparnis, sondern eine falsche Angabe - und
 * würde den Plan in die Irre führen. Dasselbe gilt für Notizen und die
 * Zuordnung zum Plan-Ziel: die gehören zur damaligen Einheit.
 *
 * sportVonUebung ordnet eine Übung ihrer Sportart zu. Die Historie führt sie
 * nicht mit (siehe TrainingExercise) - ohne die Zuordnung bliebe die
 * Sportart-Auswahl der Zeile leer und die Übungsliste ausgegraut.
 */
export function zeilenAusEinheit(
  einheit: TrainingSession,
  sportVonUebung: Record<string, string>,
): ExerciseRow[] {
  const zeilen = einheit.exercises.map((uebung) => {
    // exerciseId null heißt: es war eine frei eingetragene Übung, der Name IST
    // der Freitext (siehe TrainingExercise).
    if (uebung.exerciseId === null) {
      return { ...emptyRow(), isFreeText: true, freeText: uebung.exerciseName };
    }
    return {
      ...emptyRow(sportVonUebung[uebung.exerciseId] ?? ""),
      exerciseId: uebung.exerciseId,
    };
  });

  // Eine Einheit ohne Übungen kann es zwar nicht geben, aber ein leeres
  // Formular ohne einzige Zeile wäre eine Sackgasse.
  return zeilen.length > 0 ? zeilen : [emptyRow()];
}

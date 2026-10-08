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
  /**
   * Das Plan-Ziel, das diese Zeile zählt. Im Formular kein Eingabefeld mehr:
   * es ergibt sich aus Übung und Datum (siehe passendesPlanItem) und wird beim
   * Anzeigen eingesetzt. Im gespeicherten Zeilenzustand bleibt es leer.
   */
  trainingPlanItemId: string;
  /** Der Nutzer hat "nicht zählen" gewählt - das Plan-Ziel wird für diese Zeile nicht wieder gesetzt. */
  planGeloest: boolean;
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
    planGeloest: false,
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
 * würde den Plan in die Irre führen. Dasselbe gilt für Notizen.
 *
 * Plan-Ziel: wird neu aus Übung und Datum bestimmt (passendesPlanItem), die
 * damalige Zuordnung gehört zur damaligen Einheit und wird nicht übernommen.
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

/**
 * Die Dauer der letzten Einheit, in der wirklich geübt wurde - Vorbelegung für
 * den Schnelleintrag eines Plan-Ziels. Einheiten ohne Übungen (die Einheit, die
 * eine gelegte Fährte anlegt) bleiben außen vor, ihre Dauer ist keine
 * Übungsdauer. Die Liste kommt absteigend nach Datum vom Server.
 */
export function letzteUebungsdauer(einheiten: readonly TrainingSession[] | null | undefined): number | null {
  return einheiten?.find((einheit) => einheit.exercises.length > 0)?.durationMinutes ?? null;
}

/** Dauer des Schnelleintrags, wenn die letzte nicht bekannt ist (z. B. auf der Startseite). */
export const SCHNELLEINTRAG_DAUER_VORGABE = 15;

/**
 * Eine einzelne Plan-Übung dauert kürzer als ein ganzes Training - die
 * Trainingsdauern des Formulars (30 bis 90 Minuten) passen hier nicht.
 */
export const SCHNELLEINTRAG_DAUERN = [10, 15, 30, 45];

const SCHNELLEINTRAG_DAUER_MAX = 240;

/** Vorbelegte Dauer des Schnelleintrags: die der letzten Einheit, sonst 15 Minuten. */
export function schnelleintragDauer(letzte: number | null | undefined): number {
  return letzte != null && Number.isInteger(letzte) && letzte > 0 && letzte <= SCHNELLEINTRAG_DAUER_MAX
    ? letzte
    : SCHNELLEINTRAG_DAUER_VORGABE;
}

/**
 * Die Dauern zum Antippen: die festen, dazu die vorbelegte, falls sie keine
 * davon ist (60 Minuten der letzten Einheit müssen sichtbar und gewählt sein).
 */
export function schnelleintragDauerAuswahl(vorbelegt: number): number[] {
  return [...new Set([...SCHNELLEINTRAG_DAUERN, vorbelegt])].sort((a, b) => a - b);
}

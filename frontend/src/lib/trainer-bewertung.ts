import type { TrainerSessionToRate } from "@/lib/types";

/**
 * Gibt es an diesem Training etwas, das "Passt so" übernehmen könnte: eine
 * Übung ohne Trainer-Bewertung, zu der die Hundeführer:in sich selbst
 * eingeschätzt hat (1-5)? Nur dann zeigt die Karte den Knopf - sonst
 * versprächen die Worte „übernimmt die Selbsteinschätzung“ etwas, das nicht
 * passiert. Das Backend wendet dieselbe Regel an (TrainingService.AcceptSelfRatingsAsync).
 */
export function kannSelbsteinschaetzungUebernehmen(session: Pick<TrainerSessionToRate, "exercises">): boolean {
  return session.exercises.some((e) => e.trainerRating === null && e.rating >= 1 && e.rating <= 5);
}

/**
 * Ist das Training schon erledigt, ohne dass die Trainer:in es abgehakt hat?
 * Entweder liegt ein Feedback-Text vor oder es gibt Übungen und alle haben eine
 * Trainer-Bewertung - dieselbe Regel wie im Backend (TrainerSessionQueries).
 * Die Liste nutzt das für Karten, die nach dem letzten Stern stehen bleiben:
 * der Server zählt sie nicht mehr als offen, sie sollen aber nicht unter dem
 * Finger wegspringen.
 */
export function istDurchBewertungErledigt(session: Pick<TrainerSessionToRate, "exercises" | "trainerFeedback">): boolean {
  if (session.trainerFeedback) return true;
  return session.exercises.length > 0 && session.exercises.every((e) => e.trainerRating !== null);
}

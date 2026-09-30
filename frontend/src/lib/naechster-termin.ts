import type { GroupTrainingSession } from "@/lib/types";

/**
 * Der nächste geplante, noch nicht begonnene Termin aus mehreren Listen
 * (eine je Verein). Abgesagte zählen nicht, vergangene auch nicht - die
 * Schnittstelle liefert alles ab dem heutigen Tag, also auch heute Morgen.
 */
export function naechsterTermin(
  listen: readonly (readonly GroupTrainingSession[])[],
  jetzt: number = Date.now(),
): GroupTrainingSession | null {
  let beste: GroupTrainingSession | null = null;
  let besteZeit = Infinity;
  for (const termin of listen.flat()) {
    if (termin.status !== 0) continue;
    const zeit = new Date(termin.startsAt).getTime();
    if (zeit <= jetzt || zeit >= besteZeit) continue;
    beste = termin;
    besteZeit = zeit;
  }
  return beste;
}

/** Wie viele Namen die Karte zeigt, bevor "+n" übernimmt. */
export const SICHTBARE_NAMEN = 4;

/** Namen der Zusagen für die Chips: höchstens `max` sichtbar, der Rest als Zahl. */
export function zusageNamen(
  termin: Pick<GroupTrainingSession, "responses">,
  max: number = SICHTBARE_NAMEN,
): { namen: string[]; weitere: number } {
  const alle = termin.responses
    .filter((r) => r.isAttending)
    .map((r) => `${r.firstName} ${r.lastName}`.trim())
    // Ohne Namen (Konto nicht mehr auffindbar) bleibt ein Fragezeichen, damit
    // die Zahl der Chips zur Zahl der Zusagen passt.
    .map((n) => n || "?");
  return { namen: alle.slice(0, max), weitere: Math.max(0, alle.length - max) };
}

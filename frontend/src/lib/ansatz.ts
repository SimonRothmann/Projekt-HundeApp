import { automatischePunkte, bearingDegrees, haversineMeters } from "@/lib/geo";
import { normalisiere } from "@/lib/wind";

/**
 * Den Ansatz einer verdeckt abgesuchten Fährte finden (Paket F4).
 *
 * Bei "Verdeckt absuchen" sind Verlauf, Marker und Haptik aus. Den Anfang der Fährte muss man aber finden,
 * sonst gäbe es nichts zu suchen: Solange man noch weiter als ANSATZ_RADIUS_M vom ersten Punkt der Legung
 * weg ist, zeigt die Aufzeichnung nur Entfernung und Richtung dorthin. Wer einmal nah genug war, bekommt
 * den Hinweis nicht wieder - auch nicht, wenn die Spur sich später wieder entfernt.
 */

type Koordinate = { latitude: number; longitude: number };

/** Näher als so viele Meter am ersten Punkt der Legung gilt der Ansatz als gefunden. */
export const ANSATZ_RADIUS_M = 10;

/** Der Ansatz: der erste automatische Punkt der Legung (nach Zeit); null ohne Punkte. */
export function ansatzPunkt<T extends Koordinate & { timestamp: string; pointType?: number }>(legung: readonly T[]): T | null {
  const linie = automatischePunkte(legung).sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  return linie[0] ?? null;
}

export type AnsatzHinweis = {
  /** Entfernung von der aktuellen Position zum Ansatz in ganzen Metern. */
  meter: number;
  /** Kompassgrad (0 = Nord) von der aktuellen Position zum Ansatz. */
  peilung: number;
};

/**
 * Was zum Ansatz anzuzeigen ist. `bisher`: alle bisherigen Positionen des Absuchens, die letzte ist die
 * aktuelle. Null, wenn es nichts zu zeigen gibt: noch keine Position, kein Ansatz, oder der Ansatz wurde
 * schon einmal erreicht (auch der aktuelle Punkt zählt).
 */
export function ansatzHinweis(bisher: readonly Koordinate[], ansatz: Koordinate | null): AnsatzHinweis | null {
  if (!ansatz || bisher.length === 0) return null;
  if (bisher.some((position) => haversineMeters(position, ansatz) <= ANSATZ_RADIUS_M)) return null;

  const aktuell = bisher[bisher.length - 1];
  return { meter: Math.round(haversineMeters(aktuell, ansatz)), peilung: bearingDegrees(aktuell, ansatz) };
}

/**
 * Um wie viel Grad der Pfeil zum Ansatz gedreht ist, wenn "oben" die Laufrichtung ist; ohne bekannte
 * Laufrichtung zeigt "oben" nach Norden.
 */
export function pfeilWinkel(peilungZumAnsatz: number, laufrichtung: number | null): number {
  return normalisiere(peilungZumAnsatz - (laufrichtung ?? 0));
}

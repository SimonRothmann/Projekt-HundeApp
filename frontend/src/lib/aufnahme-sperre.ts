/**
 * Auto-Sperre der laufenden GPS-Aufzeichnung (siehe AufzeichnungVollbild).
 *
 * Der Bildschirm bleibt beim Aufzeichnen absichtlich an, das Handy steckt
 * zwischendurch in der Tasche. Nach dieser Zeit ohne Berührung sperrt die
 * Hülle die Bedienung, bis jemand bewusst wischt.
 */
export const SPERRE_NACH_MS = 15_000;

/** Ab diesem Anteil des Wegs (0..1) gilt das Wischen als gewollt. */
export const ENTSPERR_SCHWELLE = 0.85;

/**
 * Wie weit der Griff des Schiebers ist (0..1), wenn der Finger um `dx` Pixel
 * nach rechts gewandert ist. Der Weg ist die Spur ohne den Griff selbst;
 * Wischen nach links bleibt bei 0.
 */
export function entsperrFortschritt(dx: number, spurBreite: number, griffBreite: number): number {
  const weg = spurBreite - griffBreite;
  if (!(weg > 0) || !Number.isFinite(dx)) return 0;
  return Math.min(1, Math.max(0, dx / weg));
}

/** Ob das Loslassen bei diesem Fortschritt entsperrt; davor springt der Griff zurück. */
export function entsperrt(fortschritt: number): boolean {
  return fortschritt >= ENTSPERR_SCHWELLE;
}

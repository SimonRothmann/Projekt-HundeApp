/**
 * Breite eines Balkens in Prozent (0-100, ganze Zahl) für die schlichten
 * Balken der Admin-Kennzahlen.
 *
 * Ein kleiner, aber echter Wert bekommt mindestens 2 % - ein Balken von 0,3 %
 * wäre unsichtbar und sähe aus wie "nichts", obwohl es jemanden gibt. Null
 * bleibt null. Ist der Bezugswert 0 (noch keine neuen Konten), gibt es keinen
 * Balken statt einer Division durch null.
 */
export function balkenProzent(wert: number, bezug: number): number {
  if (wert <= 0 || bezug <= 0) return 0;
  const prozent = Math.round((wert / bezug) * 100);
  return Math.min(100, Math.max(2, prozent));
}

/** Anteil in ganzen Prozent, für die Zahl neben dem Balken; null ohne Bezugswert. */
export function anteilProzent(wert: number, bezug: number): number | null {
  if (bezug <= 0) return null;
  return Math.round((wert / bezug) * 100);
}

/**
 * Welche Wochen eines Trainingsplans die Hundeseite zeigt.
 *
 * Der Plan stand mit allen Wochen untereinander über dem Tagebuch - bei einem
 * Zwölf-Wochen-Plan sind das zwölf Zeilen, mal Anzahl der Ziele, bevor man
 * überhaupt beim Tagebuch ankommt. Gebraucht wird beim Öffnen der Seite aber
 * genau eine: die laufende.
 *
 * Zwei Ausnahmen, in denen nichts verborgen wird: wenn sich die laufende Woche
 * nicht bestimmen lässt (abgeschlossener Plan), und wenn es ohnehin nur zwei
 * Wochen gibt - dann kostet das Ausblenden mehr Aufmerksamkeit, als es Platz
 * spart.
 */
export const OHNE_VERKUERZUNG_BIS = 2;

export function sichtbareWochen<T>(
  wochen: [number, T][],
  // null wie undefined heißt "keine laufende Woche" - der Aufrufer bestimmt
  // sie aus den Wochennummern und kommt dabei ohne Treffer aus.
  aktuelleWoche: number | null | undefined,
  alleZeigen: boolean,
): [number, T][] {
  if (alleZeigen || aktuelleWoche == null || wochen.length <= OHNE_VERKUERZUNG_BIS) return wochen;
  return wochen.filter(([nummer]) => nummer === aktuelleWoche);
}

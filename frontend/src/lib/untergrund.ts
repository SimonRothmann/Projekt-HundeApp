import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Untergründe zum Antippen beim Fährtelegen.
 *
 * Vorher ein Textfeld - und genau das war auf dem iPhone der Auslöser für
 * "Eingabe widerrufen": iOS bietet beim Schütteln an, zuletzt Getipptes
 * rückgängig zu machen, und mit dem Telefon in der Tasche wird beim Legen
 * ständig geschüttelt. Eine Web-App kann diese Systemfunktion nicht
 * abschalten. Ohne Tastatureingabe gibt es aber nichts zu widerrufen.
 *
 * Gespeichert wird der deutsche Begriff (wie zuvor der getippte Text),
 * angezeigt in der eingestellten Sprache.
 */
export const UNTERGRUENDE: readonly string[] = [
  uebersetzbar("Wiese"),
  uebersetzbar("Acker"),
  uebersetzbar("Stoppelfeld"),
  uebersetzbar("Wald"),
  uebersetzbar("Feldweg"),
  uebersetzbar("Sand"),
];

/**
 * Untergründe beim Trail-Legen (Mantrailing): Der Trail führt meist durch
 * Wohngebiet und über Straßen, Acker und Stoppelfeld sind dort selten.
 */
export const TRAIL_UNTERGRUENDE: readonly string[] = [
  uebersetzbar("Wohngebiet"),
  uebersetzbar("Straße"),
  uebersetzbar("Park"),
  uebersetzbar("Wald"),
  uebersetzbar("Wiese"),
  uebersetzbar("Feldweg"),
];

/**
 * Einen Untergrund an- oder abwählen.
 *
 * Mehrfachauswahl, weil eine Fährte über einen Untergrundwechsel führen kann.
 * Das Ergebnis folgt immer der Reihenfolge der Liste - sonst stünde derselbe
 * Wechsel mal als "Acker, Wiese" und mal als "Wiese, Acker" in den Daten.
 */
export function untergrundUmschalten(
  auswahl: readonly string[],
  untergrund: string,
  liste: readonly string[] = UNTERGRUENDE,
): string[] {
  const neu = auswahl.includes(untergrund) ? auswahl.filter((u) => u !== untergrund) : [...auswahl, untergrund];
  return liste.filter((u) => neu.includes(u));
}

/** Für die Fährte: "Wiese, Acker" - oder null, wenn nichts gewählt ist. */
export function untergrundAlsText(auswahl: readonly string[]): string | null {
  return auswahl.length > 0 ? auswahl.join(", ") : null;
}

import type { Sprache } from "@/lib/i18n/sprachen";

/**
 * Das Gebietsschema für Datum, Uhrzeit und Zahlen.
 *
 * Englisch bewusst als en-GB: Die App richtet sich an Hundesport in
 * Deutschland, Österreich und der Schweiz; wer sie auf Englisch liest, kennt
 * Tag vor Monat und die 24-Stunden-Uhr, nicht den US-Stil.
 */
export function ortsformat(sprache: Sprache): string {
  return sprache === "en" ? "en-GB" : "de-DE";
}

/** Zahl mit Dezimaltrennzeichen der Sprache: "1,8" bzw. "1.8"; ganze Zahlen ohne Nachkommastelle, wenn nachkomma = 0. */
export function zahlText(wert: number, sprache: Sprache, nachkomma = 1): string {
  return wert.toLocaleString(ortsformat(sprache), {
    minimumFractionDigits: nachkomma,
    maximumFractionDigits: nachkomma,
  });
}

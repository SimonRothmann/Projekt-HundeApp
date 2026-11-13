/**
 * Länge einer Fährte in Schritten (Paket F4): Prüfungsordnungen rechnen in Schritten, nicht in Metern.
 *
 * Die Schrittlänge ist eine Einstellung der Person (UserPreference.StepLengthCm); ohne Angabe gilt der
 * Standard. Der Bereich spiegelt den Server (PreferenceService), der Wert außerhalb ablehnt.
 */

/** Die Übersetzungsfunktion aus useT() - hier nur als Form, ohne React. */
type TextUebersetzer = (text: string, werte?: Record<string, string | number>) => string;

export const STANDARD_SCHRITT_CM = 75;
export const SCHRITT_MIN_CM = 40;
export const SCHRITT_MAX_CM = 120;

/** Die wirksame Schrittlänge: die gespeicherte, wenn sie im erlaubten Bereich liegt, sonst der Standard. */
export function wirksameSchrittlaenge(gespeichert: number | null | undefined): number {
  return gespeichert != null && Number.isInteger(gespeichert) && gespeichert >= SCHRITT_MIN_CM && gespeichert <= SCHRITT_MAX_CM
    ? gespeichert
    : STANDARD_SCHRITT_CM;
}

/**
 * Anzahl Schritte für eine Strecke, auf 10 gerundet - mehr Genauigkeit gäbe die Messung nicht her.
 * Null bei einer Strecke unter fünf Schritten: "≈ 0 Schritte" sagte nichts.
 */
export function schritte(meter: number, schrittlaengeCm: number): number | null {
  if (!(meter > 0)) return null;
  const gerundet = Math.round(meter / (schrittlaengeCm / 100) / 10) * 10;
  return gerundet > 0 ? gerundet : null;
}

/** "≈ 410 Schritte" - oder null, wenn es nichts anzuzeigen gibt. */
export function schritteText(meter: number | null | undefined, schrittlaengeCm: number, t: TextUebersetzer): string | null {
  const anzahl = meter == null ? null : schritte(meter, schrittlaengeCm);
  return anzahl == null ? null : t("≈ {anzahl} Schritte", { anzahl });
}

/** Ob die Eingabe im Einstellungsfeld eine gültige Schrittlänge ist (ganze Zahl im erlaubten Bereich). */
export function gueltigeSchrittlaenge(wert: number): boolean {
  return Number.isInteger(wert) && wert >= SCHRITT_MIN_CM && wert <= SCHRITT_MAX_CM;
}

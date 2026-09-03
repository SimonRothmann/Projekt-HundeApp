import { VORGABE_SPRACHE, type Sprache } from "./sprachen";

/**
 * Vorgabe, solange niemand etwas gewählt hat.
 *
 * Deutschland, weil der gesamte hinterlegte Katalog deutsch ist - eine
 * Vorgabe, die auf einen leeren Bildschirm führt, wäre keine.
 */
export const VORGABE_LAND = "DE";

/**
 * Der Name eines Landes in der Oberflächensprache.
 *
 * Intl.DisplayNames steckt in jedem Browser und kennt alle Länder in allen
 * Sprachen. Eine eigene Namensliste zu pflegen hieße, eine fertige
 * Übersetzungstabelle nachzubauen - und sie bei jeder neuen Sprache erneut.
 *
 * Der Rückfall auf das Kürzel ist kein Schönheitsfehler, sondern die
 * ehrliche Antwort: Lieber "XK" als eine erfundene Bezeichnung.
 */
export function landName(code: string, sprache: Sprache = VORGABE_SPRACHE): string {
  try {
    return new Intl.DisplayNames([sprache], { type: "region" }).of(code) ?? code;
  } catch {
    return code;
  }
}

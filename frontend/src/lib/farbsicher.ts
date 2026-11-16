/**
 * Farbsichere Darstellung für Menschen mit Rot-Grün-Schwäche.
 *
 * Eine Einstellung je Konto wie die Schriftgröße (lib/schriftgroesse.ts):
 * Der Server ist maßgeblich, der Zwischenspeicher im Browser verhindert nur,
 * dass bei jedem Seitenaufbau erst die falschen Farben erscheinen und dann
 * umspringen. Das Attribut am Wurzelelement steuert die CSS-Variablen der
 * Ampel (globals.css) und - zur Laufzeit abgelesen - die Hex-Farben für
 * Karte und Canvas (lib/spur-ampel.ts).
 */

/** Wo der Wert zusätzlich im Browser liegt (Zwischenspeicher fürs erste Bild). */
export const FARBEN_SPEICHER = "dogity.farben";

/** Attribut am Wurzelelement, auf das die Regeln in globals.css greifen. */
export const FARBEN_ATTRIBUT = "data-farben";

/** Der Wert des Attributs bei eingeschaltetem Modus. */
export const FARBEN_SICHER = "sicher";

/** Ob die farbsichere Darstellung gerade gilt - ohne Dokument (Server, Tests) nein. */
export function farbsicherAktiv(): boolean {
  if (typeof document === "undefined") return false;
  return document.documentElement.getAttribute(FARBEN_ATTRIBUT) === FARBEN_SICHER;
}

/** Legt den Modus auf das Wurzelelement und in den Zwischenspeicher; aus entfernt beides. */
export function wendeFarbsicherAn(an: boolean): void {
  if (typeof document === "undefined") return;

  if (an) document.documentElement.setAttribute(FARBEN_ATTRIBUT, FARBEN_SICHER);
  else document.documentElement.removeAttribute(FARBEN_ATTRIBUT);

  try {
    if (an) localStorage.setItem(FARBEN_SPEICHER, FARBEN_SICHER);
    else localStorage.removeItem(FARBEN_SPEICHER);
  } catch {
    // Privater Modus oder gesperrter Speicher: Die Einstellung wirkt trotzdem
    // und kommt beim nächsten Aufbau wieder vom Server.
  }
}

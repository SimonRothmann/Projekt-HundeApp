import { VORGABE_SPRACHE, type Sprache } from "./sprachen";

/**
 * Die gerade geltende Oberflächensprache, modulweit.
 *
 * Gebraucht von Code, der keinen Hook aufrufen kann: ApiError übersetzt seine
 * Meldung beim Anzeigen, lib-Funktionen werfen Fehler mit deutschem Text. Der
 * I18nProvider setzt den Wert in einem Effekt, sobald die Sprache feststeht.
 *
 * Bewusst nur im Browser gesetzt: Auf dem Server (Rendern) bleibt es bei der
 * Vorgabe, und weil dort nie ein Effekt läuft, kann keine Anfrage die Sprache
 * einer anderen hinterlassen.
 */
let aktuell: Sprache = VORGABE_SPRACHE;

export function setzeAktuelleSprache(sprache: Sprache): void {
  aktuell = sprache;
}

export function aktuelleSprache(): Sprache {
  return aktuell;
}

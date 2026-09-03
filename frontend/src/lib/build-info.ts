/**
 * Was in diesem Build tatsächlich steckt: Commit und Bauzeitpunkt.
 *
 * Die Versionshinweise sagen, WAS sich geändert hat - von Hand geschrieben
 * und damit prinzipiell irrtumsfähig. Diese beiden Werte sagen, WELCHER
 * Stand hier gerade läuft, und sie entstehen beim Bauen, nicht beim
 * Aufschreiben. Genau deshalb stehen sie zusätzlich auf der Seite: Ob eine
 * Änderung wirklich draußen ist, beantwortet der Commit, nicht die
 * Erinnerung.
 *
 * NEXT_PUBLIC_* wird zur Build-Zeit ins Client-Bundle eingebacken (siehe
 * frontend/Dockerfile). Zur Laufzeit gesetzte Werte kämen hier nicht an -
 * das ist kein Versehen, sondern der Grund, warum der Wert zum Bundle passt.
 */

// Zugriff bewusst als vollständiger Ausdruck: Next ersetzt nur genau diese
// Schreibweise durch den Literalwert. Ein Umweg über process.env["..."]
// oder Destrukturierung liefert im Browser undefined.
const rohCommit = process.env.NEXT_PUBLIC_BUILD_COMMIT ?? "";
const rohZeit = process.env.NEXT_PUBLIC_BUILD_TIME ?? "";

/**
 * Kurz-Hash des gebauten Commits, oder null bei einem lokalen Build.
 *
 * Die Hex-Prüfung ist die Abgrenzung gegen den Vorgabewert "lokal" aus
 * docker-compose.yml: Ohne sie stünde auf jedem Entwicklungs-Build
 * "Build lokal" - was aussieht wie eine Angabe, aber keine ist.
 */
export const BUILD_COMMIT: string | null = /^[0-9a-f]{7,40}$/.test(rohCommit) ? rohCommit : null;

/** Zeitpunkt des Builds, oder null wenn nicht gesetzt bzw. nicht lesbar. */
export const BUILD_ZEIT: Date | null = (() => {
  if (!rohZeit) return null;
  const datum = new Date(rohZeit);
  return Number.isNaN(datum.getTime()) ? null : datum;
})();

/**
 * "3. September 2026 um 18:09 Uhr".
 *
 * Datum und Uhrzeit werden einzeln formatiert und selbst zusammengesetzt.
 * Ein einzelnes toLocaleString mit allen Feldern liefert zwar dasselbe
 * Datum, endet aber bei "um 18:09" - ohne "Uhr". Das liest sich in einer
 * Zeile, die ohnehin schon Fassungsnummer und Commit trägt, wie eine
 * abgeschnittene Angabe.
 *
 * Zeitzone fest auf Europe/Berlin statt auf die des Betrachters. Zwei Gründe:
 * Der Server rendert im Container unter UTC, der Browser hydriert in der
 * Zeitzone des Nutzers - ohne feste Angabe entstünden zwei verschiedene
 * Zeichenketten für dieselbe Stelle und React meldet eine Hydration-
 * Abweichung. Und inhaltlich ist die deutsche Zeit die gemeinte: Der Deploy
 * fand zu einer deutschen Uhrzeit statt.
 */
export function formatiereBuildZeit(zeitpunkt: Date): string {
  const datum = zeitpunkt.toLocaleDateString("de-DE", {
    day: "numeric",
    month: "long",
    year: "numeric",
    timeZone: "Europe/Berlin",
  });
  const uhrzeit = zeitpunkt.toLocaleTimeString("de-DE", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Europe/Berlin",
  });
  return `${datum} um ${uhrzeit} Uhr`;
}

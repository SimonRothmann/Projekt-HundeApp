/**
 * Nur echte, seiteninterne Pfade zulassen - für Rücksprung-Parameter wie
 * ?from=, die aus geteilten Links stammen können.
 *
 * Ein Vergleich der Anfangszeichen reicht nicht: Browser entfernen Tab und
 * Zeilenumbruch aus Adressen und lesen "\" wie "/". Aus "/\t/fremd.example"
 * würde so "//fremd.example" - eine fremde Seite. Deshalb wird der Wert so
 * aufgelöst, wie der Browser es täte, und nur angenommen, wenn er auf
 * derselben Seite bleibt. Zurück kommt der bereinigte Pfad.
 */
export function internerPfad(wert: string | null | undefined): string | null {
  if (!wert || !wert.startsWith("/")) return null;
  const basis = "https://dogity.invalid";
  let url: URL;
  try {
    url = new URL(wert, basis);
  } catch {
    return null;
  }
  if (url.origin !== basis) return null;
  return url.pathname + url.search + url.hash;
}

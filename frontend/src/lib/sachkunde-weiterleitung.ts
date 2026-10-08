/**
 * Wohin eine angemeldete Person von den öffentlichen Sachkunde-Seiten
 * geschickt wird.
 *
 * Die öffentlichen Seiten (/sachkunde, /sachkunde/bh) stehen außerhalb der
 * App-Hülle: keine untere Leiste, der Rückweg nur über "Zur App". Für
 * Angemeldete gibt es dieselben Inhalte unter /lernen innerhalb der App.
 * Gäste und Suchmaschinen bleiben auf der öffentlichen Seite - dort sitzt
 * die Auffindbarkeit.
 *
 * Nur die beiden Seiten selbst werden umgeleitet. Die Zeichnungen unter
 * /sachkunde/<datei>.png liegen im public-Ordner und werden nie als Seite
 * gerendert, die Weiterleitung kommt an ihnen also nicht vorbei.
 */
const SACHKUNDE_SEITE = /^\/sachkunde(\/[^/]+)?\/?$/;

/** Der App-interne Pfad zu einer öffentlichen Sachkunde-Adresse, sonst null. */
export function appPfadFuerSachkunde(pfad: string): string | null {
  const treffer = SACHKUNDE_SEITE.exec(pfad);
  return treffer ? `/lernen${treffer[1] ?? ""}` : null;
}

/**
 * Ob und wohin weitergeleitet wird. Solange noch nicht feststeht, ob jemand
 * angemeldet ist, bleibt es bei der öffentlichen Seite - sie ist die richtige
 * Antwort für alle anderen und flackert nur kurz.
 */
export function sachkundeWeiterleitung(pfad: string, angemeldet: boolean, laedt: boolean): string | null {
  if (laedt || !angemeldet) return null;
  return appPfadFuerSachkunde(pfad);
}

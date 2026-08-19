/**
 * Zentrale Angaben für Suchmaschinen. An einer Stelle, weil Titel, Domain und
 * Beschreibung an vielen Orten gebraucht werden (Metadaten, Sitemap, JSON-LD,
 * OG-Bild) und auseinanderlaufen, sobald sie mehrfach gepflegt werden.
 */
export const SITE = {
  name: "Dogity",
  url: "https://dogity.net",
  /**
   * Unter 160 Zeichen: Google schneidet längere Beschreibungen im Suchergebnis
   * ab. Nennt bewusst die Begriffe, nach denen tatsächlich gesucht wird
   * (Hundesport, Trainingstagebuch, Fährte, IGP), statt nur den Markennamen.
   */
  description:
    "Kostenloses Trainingstagebuch für den Hundesport: Training dokumentieren, Fährten per GPS aufzeichnen und auswerten, Prüfungsordnungen für BH, IBGH und IGP.",
  locale: "de_DE",
} as const;

/**
 * URL-Bezeichner aus einem Namen. Umlaute werden ausgeschrieben statt entfernt
 * ("Fährte" -> "faehrte", nicht "fhrte") - so bleibt der Bezeichner lesbar und
 * enthält weiterhin die Wörter, nach denen gesucht wird.
 */
export function slugify(value: string): string {
  return value
    .toLowerCase()
    .replace(/ä/g, "ae")
    .replace(/ö/g, "oe")
    .replace(/ü/g, "ue")
    .replace(/ß/g, "ss")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

/** Vollständige URL - für Sitemap und JSON-LD, die keine relativen Pfade dürfen. */
export function absoluteUrl(path: string): string {
  return new URL(path, SITE.url).toString();
}

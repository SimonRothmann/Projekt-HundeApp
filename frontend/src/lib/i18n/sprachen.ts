/**
 * Welche Sprachen es gibt und wie entschieden wird, welche gilt.
 *
 * Bewusst KEINE Sprache in der Adresse (kein /en/dashboard). Das ist der Weg,
 * den die Next-Doku für Inhaltsseiten vorschlägt, und für diese App der
 * falsche:
 *
 * - Die App hinter der Anmeldung wird im Browser gerendert und kennt ihren
 *   Nutzer erst dort - eine Sprache im Pfad müsste geraten werden, bevor
 *   feststeht, wer schaut.
 * - Die öffentlichen Seiten beschreiben deutsche Prüfungsordnungen. Eine
 *   englische Adressvariante mit demselben deutschen Inhalt wäre eine
 *   Dublette ohne eigenen Wert - Suchmaschinen werten so etwas ab, und die
 *   vorhandene Sichtbarkeit wäre der Preis.
 *
 * Die Sprache ist deshalb eine Einstellung am Nutzer und keine Eigenschaft
 * der Adresse.
 */

export const SPRACHEN = ["de", "en"] as const;
export type Sprache = (typeof SPRACHEN)[number];

export const VORGABE_SPRACHE: Sprache = "de";

/** Wie die Sprache in der jeweiligen Sprache selbst heißt. */
export const SPRACHE_NAME: Record<Sprache, string> = {
  de: "Deutsch",
  en: "English",
};

export function istSprache(wert: unknown): wert is Sprache {
  return typeof wert === "string" && (SPRACHEN as readonly string[]).includes(wert);
}

/**
 * Entscheidet die Oberflächensprache.
 *
 * Reihenfolge: ausdrückliche Wahl des Nutzers, sonst die Sprache des Geräts,
 * sonst Deutsch. Die Gerätesprache steht bewusst vor der Vorgabe - wer die
 * App zum ersten Mal öffnet, hat noch nichts gewählt, und ein englisches
 * Gerät ist ein deutlicheres Signal als der Standard der App.
 *
 * "en-US" wird zu "en" gekürzt: Regionen unterscheidet die App nicht, und
 * ein unbekanntes Kürzel darf nicht dazu führen, dass gar nichts passt.
 */
export function bestimmeSprache(gewaehlt: string | null | undefined, geraet: readonly string[] = []): Sprache {
  if (istSprache(gewaehlt)) return gewaehlt;

  for (const eintrag of geraet) {
    const basis = eintrag.split("-")[0]?.toLowerCase();
    if (istSprache(basis)) return basis;
  }

  return VORGABE_SPRACHE;
}

/**
 * Markiert einen Satz als übersetzbar, ohne ihn zu übersetzen.
 *
 * Für Texte, die als Daten irgendwo stehen und erst weit entfernt gerendert
 * werden - Beschriftungen der Navigation etwa. Dort steht am Ende `t(label)`
 * mit einer Variablen, und eine Variable kann der Vollständigkeitstest nicht
 * lesen. Ohne diese Markierung hielte er den Satz für unbenutzt und
 * verlangte, ihn zu löschen - womit die Beschriftung still deutsch bliebe.
 *
 * Zur Laufzeit tut sie nichts. Ihr ganzer Zweck ist, im Quelltext sichtbar
 * zu sein.
 */
export function uebersetzbar(text: string): string {
  return text;
}

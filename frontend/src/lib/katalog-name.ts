import { uebersetze } from "@/lib/i18n";
import { uebersetzbar, type Sprache } from "@/lib/i18n/sprachen";

/**
 * Anzeigename einer Katalogzeile (Sportart, Prüfungsordnung, Übung).
 *
 * Die internationalen FCI-Ordnungen tragen einen englischen Namen (NameEn):
 * Englisch ist dort die Originalsprache der Ordnung und hat Vorrang vor den
 * Übersetzungen (docs/VERBAENDE_SPRACHEN_MODULE.md). Auf Englisch erscheint
 * dieser Name, sonst - und überall, wo es keinen gibt - der deutsche. Das
 * gilt für nationale Ordnungen (VDH), vom Verein angelegte Übungen und
 * Freitext, die nur auf Deutsch existieren.
 *
 * Der deutsche Name bleibt der Schlüssel (Gruppierung, Dopplungsprüfung,
 * Speichern). Dieser Helfer ist nur für die Anzeige da.
 */
export type KatalogName = { name: string; nameEn?: string | null };

export function katalogName(eintrag: KatalogName, sprache: Sprache): string {
  if (sprache === "en") {
    const englisch = eintrag.nameEn?.trim();
    if (englisch) return englisch;
  }
  return eintrag.name;
}

/**
 * Dasselbe für Einträge, die den Namen als exerciseName/exerciseNameEn tragen
 * (Tagebuch, Plan, Statistik, Traineransicht). Freitext hat keinen englischen
 * Namen und bleibt, wie er eingegeben wurde.
 */
export function uebungName(
  eintrag: { exerciseName: string; exerciseNameEn?: string | null },
  sprache: Sprache,
): string {
  return katalogName({ name: eintrag.exerciseName, nameEn: eintrag.exerciseNameEn }, sprache);
}

/**
 * Ein Katalogtext (Beschreibung, Bewertungskriterien, Bewertungshinweis) in
 * der Sprache der Oberfläche.
 *
 * Wie beim Namen: Die FCI-Ordnungen tragen seit Paket E4 auch ihre Texte auf
 * Englisch (Spalten ...En, vom Server mitgeliefert). Auf Englisch erscheint
 * diese Fassung, sonst - und überall, wo es keine gibt (nationale Ordnungen,
 * Vereinsübungen, ein leer gelassenes Feld) - der deutsche Text. Gibt es
 * gar keinen Text, kommt null zurück, damit der Aufrufer nichts anzeigt.
 */
export function katalogText(
  de: string | null | undefined,
  en: string | null | undefined,
  sprache: Sprache,
): string | null {
  if (sprache === "en") {
    const englisch = en?.trim();
    if (englisch) return englisch;
  }
  return de?.trim() ? de : null;
}

/**
 * Die Kategorien der FCI-Übungen. Der Server liefert sie deutsch, und sie sind
 * eine kleine feste Menge - deshalb übersetzt die Oberfläche sie (en.ts), statt
 * eine weitere Spalte zu führen. "Fährte" gibt es schon als "Track".
 */
const FCI_KATEGORIEN = [
  uebersetzbar("Abteilung A"),
  uebersetzbar("Abteilung B"),
  uebersetzbar("Abteilung C"),
  uebersetzbar("Unterordnung"),
  uebersetzbar("Verhalten"),
  uebersetzbar("Stöbern"),
  uebersetzbar("Ausdauer"),
  uebersetzbar("Fährte"),
];

/**
 * Kategorie einer Übung in der Sprache der Oberfläche. Übersetzt wird nur bei
 * Übungen, die auch einen englischen Namen tragen (= FCI-Übungen) und deren
 * Kategorie zu den festen Werten gehört: Dieselbe Bezeichnung ("Verhalten",
 * "Unterordnung") steht auch bei nationalen Übungen und frei vergebenen
 * Kategorien, und die bleiben - wie ihr Name - deutsch.
 */
export function katalogKategorie(
  eintrag: { category?: string | null; nameEn?: string | null },
  sprache: Sprache,
): string | null {
  const kategorie = eintrag.category?.trim();
  if (!kategorie) return null;
  if (sprache === "en" && eintrag.nameEn?.trim() && FCI_KATEGORIEN.includes(kategorie)) {
    return uebersetze(sprache, kategorie);
  }
  return kategorie;
}

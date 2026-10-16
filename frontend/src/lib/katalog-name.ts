import type { Sprache } from "@/lib/i18n/sprachen";

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

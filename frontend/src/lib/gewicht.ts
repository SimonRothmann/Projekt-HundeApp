import { GESUNDHEIT_ART } from "@/lib/types";
import type { DogHealthRecord } from "@/lib/types";
import type { Sprache } from "@/lib/i18n/sprachen";
import { heuteIso, plusJahre, tagesnummer } from "@/lib/kalendertag";
import { ortsformat } from "@/lib/ortsformat";
import { MAX_JAHRE_VORAUS, MAX_JAHRE_ZURUECK } from "@/lib/gesundheit";

/**
 * Die Gewichtskurve im Bereich "Gesundheit" als reine Funktionen: Eingabe lesen und prüfen,
 * Werte anzeigen, Achsenbereich und Punkte der Kurve berechnen. Die Grenzen sind die des
 * Servers (DogHealthService).
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

export const MIN_KG = 0.5;
export const MAX_KG = 150;

/** "24,3" oder "24.3" -> 24.3 (eine Nachkommastelle, wie der Server rundet); alles andere -> null. */
export function parseKg(text: string): number | null {
  const roh = text.trim().replace(",", ".");
  if (!/^\d+(\.\d+)?$/.test(roh)) return null;
  return Math.round(Number(roh) * 10) / 10;
}

/** "24,3 kg" bzw. "24.3 kg". */
export function kgText(kg: number, sprache: Sprache = "de"): string {
  return `${kg.toLocaleString(ortsformat(sprache), { minimumFractionDigits: 1, maximumFractionDigits: 1 })} kg`;
}

/** Fehlermeldung zur Eingabe, sonst null - die Regeln des Servers. */
export function pruefeGewicht(t: Uebersetzer, eingabe: { datum: string; kg: string }, heute: string = heuteIso()): string | null {
  const kg = parseKg(eingabe.kg);
  if (kg === null) return t("Bitte ein Gewicht angeben.");
  if (kg < MIN_KG || kg > MAX_KG) return t("Das Gewicht muss zwischen 0,5 und 150 kg liegen.");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(eingabe.datum)) return t("Bitte ein Datum angeben.");
  if (eingabe.datum < plusJahre(heute, -MAX_JAHRE_ZURUECK) || eingabe.datum > plusJahre(heute, MAX_JAHRE_VORAUS)) {
    return t("Das Datum liegt zu weit entfernt.");
  }
  return null;
}

/** Die Wägungen nach Datum, älteste zuerst; bei gleichem Datum steht die später angelegte hinten (der Server liefert jüngste zuerst). */
export function gewichte(eintraege: readonly DogHealthRecord[]): DogHealthRecord[] {
  return eintraege
    .filter((e) => e.kind === GESUNDHEIT_ART.Gewicht && e.weightKg !== null)
    .reverse()
    .sort((a, b) => a.date.localeCompare(b.date));
}

/** Veränderung des letzten Eintrags zum vorigen in kg (eine Nachkommastelle); null bei weniger als zwei. */
export function veraenderung(sortiert: readonly DogHealthRecord[]): number | null {
  if (sortiert.length < 2) return null;
  const [vorher, zuletzt] = sortiert.slice(-2);
  return Math.round((zuletzt.weightKg! - vorher.weightKg!) * 10) / 10;
}

/** "+0,4 kg", "−0,2 kg", "±0,0 kg" - das Vorzeichen steht immer, die Richtung also nie nur in der Farbe. */
export function veraenderungText(diff: number, sprache: Sprache = "de"): string {
  const zeichen = diff > 0 ? "+" : diff < 0 ? "−" : "±";
  return `${zeichen}${kgText(Math.abs(diff), sprache)}`;
}

/** Der Bereich der Y-Achse: nicht ab 0, mit etwas Luft um die Werte, auf ganze kg gerundet, mindestens 2 kg hoch. */
export function achsenbereich(werte: readonly number[]): { min: number; max: number } {
  const lo = Math.min(...werte);
  const hi = Math.max(...werte);
  const luft = Math.max((hi - lo) * 0.2, 0.5);
  const min = Math.max(0, Math.floor(lo - luft));
  const max = Math.max(Math.ceil(hi + luft), min + 2);
  return { min, max };
}

export type KurvenPunkt = { x: number; y: number; eintrag: DogHealthRecord };

/**
 * Die Punkte der Kurve in einer Fläche `breite` x `hoehe` (y wächst nach unten, wie in SVG): x nach Datum
 * (mit echtem Abstand, nicht gleichmäßig verteilt), y nach Gewicht im Achsenbereich. Liegen alle Einträge
 * am selben Tag, stehen sie in der Mitte übereinander.
 */
export function kurvenPunkte(sortiert: readonly DogHealthRecord[], breite: number, hoehe: number): KurvenPunkt[] {
  if (sortiert.length === 0) return [];
  const { min, max } = achsenbereich(sortiert.map((e) => e.weightKg!));
  const ersterTag = tagesnummer(sortiert[0].date);
  const spanne = tagesnummer(sortiert[sortiert.length - 1].date) - ersterTag;
  return sortiert.map((eintrag) => ({
    x: spanne === 0 ? breite / 2 : ((tagesnummer(eintrag.date) - ersterTag) / spanne) * breite,
    y: hoehe - ((eintrag.weightKg! - min) / (max - min)) * hoehe,
    eintrag,
  }));
}

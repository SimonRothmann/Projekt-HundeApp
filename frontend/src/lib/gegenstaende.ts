import { uebersetzbar } from "@/lib/i18n/sprachen";
import { istMarker } from "@/lib/geo";
import type { GegenstandsErgebnis, GpsPoint, GpsWalkRun, GpsWalkRunArticle } from "@/lib/types";

/**
 * Gegenstände der Fährte (Paket F3): das Material, aus dem sie sind, und was der Hund beim Absuchen mit
 * ihnen gemacht hat. Beides trägt der Mensch nach - beim Legen bleibt es bei einem Tipp je Marker, und GPS
 * sieht nur, ob das Gespann in die Nähe kam. Reine Entscheidungen ohne Oberfläche, damit sie prüfbar sind.
 */

/**
 * Das Material zum Antippen. Gespeichert wird der deutsche Begriff (wie beim Untergrund, siehe
 * lib/untergrund.ts), angezeigt in der eingestellten Sprache. Dieselbe Liste führt der Server
 * (GegenstandsMaterial) und lehnt anderes ab.
 */
export const MATERIALIEN: readonly string[] = [
  uebersetzbar("Leder"),
  uebersetzbar("Holz"),
  uebersetzbar("Stoff"),
  uebersetzbar("Teppich"),
  uebersetzbar("Kunststoff"),
  uebersetzbar("Sonstiges"),
];

/** Was der Hund mit dem Gegenstand gemacht hat; die Zahl ist die des Servers (ArticleResult). */
export const GEGENSTANDS_ERGEBNISSE: readonly { wert: GegenstandsErgebnis; label: string }[] = [
  { wert: 0, label: uebersetzbar("verwiesen") },
  { wert: 1, label: uebersetzbar("aufgenommen") },
  { wert: 2, label: uebersetzbar("überlaufen") },
];

/** Verwiesen und aufgenommen gelten als gefunden, überlaufen nicht. */
const ERGEBNIS_UEBERLAUFEN: GegenstandsErgebnis = 2;

export type Gegenstand = {
  /** Gegenstand 1, 2, 3 ... in Legereihenfolge. */
  nummer: number;
  /** Id des Markers; ohne sie (noch nicht gespeichert, älterer Stand) lässt sich nichts eintragen. */
  id: string;
  label: string | null;
  material: string | null;
};

/**
 * Die Gegenstände einer Fährte in Legereihenfolge. Nur Marker der Art Gegenstand (markerType 0) mit Id;
 * die Reihenfolge ist die der Zeitstempel, bei gleichem Zeitpunkt die der Eingabe.
 */
export function gegenstaende(punkte: readonly GpsPoint[]): Gegenstand[] {
  return punkte
    .filter((p) => istMarker(p) && p.markerType === 0 && p.id != null)
    .map((p, index) => ({ p, index }))
    .sort((a, b) => a.p.timestamp.localeCompare(b.p.timestamp) || a.index - b.index)
    .map(({ p }, i) => ({ nummer: i + 1, id: p.id as string, label: p.label, material: p.material ?? null }));
}

/** Material wählen oder erneut antippen = abwählen (null). Es gilt höchstens eine Wahl. */
export function materialUmschalten(aktuell: string | null | undefined, material: string): string | null {
  return aktuell === material ? null : material;
}

/** Ergebnis wählen oder erneut antippen = Angabe entfernen (null). */
export function ergebnisUmschalten(
  aktuell: GegenstandsErgebnis | null | undefined,
  wahl: GegenstandsErgebnis,
): GegenstandsErgebnis | null {
  return aktuell === wahl ? null : wahl;
}

/** Die gespeicherten Angaben eines Ablaufs als Wert je Gegenstand (Marker-Id). */
export function ergebnisseJeGegenstand(angaben: readonly GpsWalkRunArticle[] | undefined): Record<string, GegenstandsErgebnis> {
  return Object.fromEntries((angaben ?? []).map((a) => [a.pointId, a.result]));
}

/**
 * Was an den Server geht: nur die Gegenstände, deren Wahl vom Gespeicherten abweicht (null = Angabe
 * entfernen). Leer, wenn nichts zu tun ist - dann kostet das Schließen des Fensters keine Anfrage.
 */
export function ergebnisAenderungen(
  gespeichert: readonly GpsWalkRunArticle[] | undefined,
  wahl: Readonly<Record<string, GegenstandsErgebnis | null>>,
): { pointId: string; result: GegenstandsErgebnis | null }[] {
  const alt = ergebnisseJeGegenstand(gespeichert);
  return Object.entries(wahl)
    .filter(([id, ergebnis]) => (alt[id] ?? null) !== ergebnis)
    .map(([pointId, result]) => ({ pointId, result }));
}

export type GegenstandsZahlen = {
  /** Verwiesen + aufgenommen. */
  gefunden: number;
  gesamt: number;
  verwiesen: number;
  aufgenommen: number;
  ueberlaufen: number;
};

/**
 * Die bestätigte Gegenstandszahl eines Ablaufs - oder null, wenn die Hundeführer:in nichts angegeben hat
 * (dann zeigt die Anzeige die GPS-Zahl wie bisher). Gesamt ist immer die Zahl der gelegten Gegenstände.
 */
export function bestaetigteZahlen(run: Pick<GpsWalkRun, "articles" | "articlesTotal">): GegenstandsZahlen | null {
  const angaben = run.articles ?? [];
  if (angaben.length === 0 || !run.articlesTotal) return null;

  const verwiesen = angaben.filter((a) => a.result === 0).length;
  const aufgenommen = angaben.filter((a) => a.result === 1).length;
  const ueberlaufen = angaben.filter((a) => a.result === ERGEBNIS_UEBERLAUFEN).length;
  return { gefunden: verwiesen + aufgenommen, gesamt: run.articlesTotal, verwiesen, aufgenommen, ueberlaufen };
}

/** Die Aufschlüsselung als Teile ("1 verwiesen"); ein Ergebnis ohne Treffer fehlt, statt "0 überlaufen" zu melden. */
export function aufschluesselung(zahlen: GegenstandsZahlen, t: (text: string, werte?: Record<string, string | number>) => string): string {
  return [
    zahlen.verwiesen > 0 ? t("{n} verwiesen", { n: zahlen.verwiesen }) : null,
    zahlen.aufgenommen > 0 ? t("{n} aufgenommen", { n: zahlen.aufgenommen }) : null,
    zahlen.ueberlaufen > 0 ? t("{n} überlaufen", { n: zahlen.ueberlaufen }) : null,
  ]
    .filter((teil) => teil !== null)
    .join(", ");
}

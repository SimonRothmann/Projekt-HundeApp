import type { Goal } from "@/lib/types";
import type { Sprache } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";

/**
 * Rechnen und Sortieren rund um Prüfungstermin und Prüfungsergebnis - als reine
 * Funktionen, damit die Startseite, die Hundeseite und der Abschluss-Dialog
 * dasselbe meinen und die Regeln getestet werden können.
 *
 * Daten sind Kalendertage ("2026-10-03"), keine Zeitpunkte. Gerechnet wird
 * deshalb mit dem Kalendertag des Geräts und nie mit `new Date("2026-10-03")`:
 * Das wäre UTC-Mitternacht und fiele westlich von Greenwich auf den Vortag.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

const TAG_MS = 24 * 60 * 60 * 1000;

function teile(iso: string): [number, number, number] {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  return [jahr, monat, tag];
}

/** Anzahl ganzer Tage seit 1970 für einen Kalendertag - unabhängig von Zeitzone und Sommerzeit. */
function tagesnummer(iso: string): number {
  const [jahr, monat, tag] = teile(iso);
  return Math.round(Date.UTC(jahr, monat - 1, tag) / TAG_MS);
}

/** Der heutige Kalendertag auf dem Gerät als "JJJJ-MM-TT". */
export function heuteIso(jetzt: number = Date.now()): string {
  const d = new Date(jetzt);
  const zweistellig = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${zweistellig(d.getMonth() + 1)}-${zweistellig(d.getDate())}`;
}

/** Tage von heute bis zum Datum: positiv = noch hin, 0 = heute, negativ = vorbei. */
export function tageBisPruefung(zieldatum: string, jetzt: number = Date.now()): number {
  return tagesnummer(zieldatum) - tagesnummer(heuteIso(jetzt));
}

/**
 * Vorgabe für "Prüfungstag" beim Abschließen: das Zieldatum, wenn es schon
 * erreicht ist (dann war die Prüfung meist genau da), sonst heute - wer vor dem
 * Termin abschließt, hat die Prüfung offenbar früher abgelegt.
 */
export function vorgabePruefungstag(zieldatum: string, jetzt: number = Date.now()): string {
  return tageBisPruefung(zieldatum, jetzt) <= 0 ? zieldatum.slice(0, 10) : heuteIso(jetzt);
}

/** Der morgige Tag - frühestes Datum für einen neuen Prüfungstermin. */
export function morgenIso(jetzt: number = Date.now()): string {
  const d = new Date(jetzt);
  d.setDate(d.getDate() + 1);
  return heuteIso(d.getTime());
}

/**
 * Das Ziel, das eine Zielkarte verdient: unter den aktiven das mit dem
 * nächsten Zieldatum. Ein überschrittenes Datum zählt mit - es wartet auf
 * sein Ergebnis und gehört dann gerade nach oben.
 */
export function naechstesZiel(goals: Goal[]): Goal | null {
  const aktive = goals.filter((g) => g.status === 0);
  if (aktive.length === 0) return null;
  return aktive.reduce((bestes, g) => (g.targetDate < bestes.targetDate ? g : bestes));
}

/**
 * Die erreichten Ziele eines Hundes, neueste Prüfung zuerst. Ohne Prüfungstag
 * (früher ohne Ergebnis als erreicht markiert) geht ans Ende, dort nach
 * Zieldatum absteigend.
 */
export function leistungen(goals: Goal[]): Goal[] {
  return goals
    .filter((g) => g.status === 1)
    .sort((a, b) => {
      const tagA = a.examDate ?? null;
      const tagB = b.examDate ?? null;
      if (tagA && tagB) return tagB.localeCompare(tagA);
      if (tagA) return -1;
      if (tagB) return 1;
      return b.targetDate.localeCompare(a.targetDate);
    });
}

/** Name der Prüfung für Überschriften: die Prüfungsordnung, sonst die Sportart. */
export function pruefungsName(goal: Pick<Goal, "regulationName" | "sportName">): string {
  return goal.regulationName ?? goal.sportName;
}

/** "272 von 300 Punkten" bzw. "272 Punkte", wenn keine Höchstpunktzahl bekannt ist. */
export function punkteText(t: Uebersetzer, punkte: number, hoechstens: number | null | undefined): string {
  return hoechstens != null
    ? t("{punkte} von {max} Punkten", { punkte, max: hoechstens })
    : t("{punkte} Punkte", { punkte });
}

/** "03.10.2026" bzw. "03/10/2026". */
export function datumKurz(iso: string, sprache: Sprache = "de"): string {
  const [jahr, monat, tag] = teile(iso);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/** "Samstag, 3. Oktober" - für "am …" auf der Zielkarte. */
export function datumMitWochentag(iso: string, sprache: Sprache = "de"): string {
  const [jahr, monat, tag] = teile(iso);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
}

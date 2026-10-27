import type { Dog, DogGender, DogPause, Goal, PauseGrund } from "@/lib/types";
import { PAUSE_GRUND } from "@/lib/types";
import type { Sprache } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { heuteIso } from "@/lib/pruefung";
import { ortsformat } from "@/lib/ortsformat";

/**
 * Trainingspausen des Hundes (verletzt, krank, läufig) als reine Funktionen:
 * wann eine Pause gilt, wie sie in einer Zeile heißt, was beim Eintragen
 * vorbelegt wird und was der Server ablehnt (hier dieselben Regeln, damit man
 * den Fehler vor dem Senden sieht).
 *
 * Daten sind Kalendertage ("2026-10-03"); gerechnet wird wie in
 * lib/pruefung.ts mit Tagesnummern und nie mit `new Date("2026-10-03")`.
 * Beide Grenzen einer Pause gelten einschließlich.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** So lange dauert eine Läufigkeit als Vorschlag: Das Ende steht 21 Tage nach dem Beginn (änderbar). */
export const LAEUFIGKEIT_TAGE = 21;

/** Gleiche Grenzen wie der Server (DogPauseService). */
const MAX_JAHRE_ZURUECK = 5;
const MAX_JAHRE_VORAUS = 2;
const MAX_DAUER_JAHRE = 2;

const TAG_MS = 24 * 60 * 60 * 1000;

/** Nummer des Kalendertags (Tage seit 1970) - Datumsrechnung ohne Zeitzonen. */
export function tagesnummer(iso: string): number {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  return Math.round(Date.UTC(jahr, monat - 1, tag) / TAG_MS);
}

/** Der Kalendertag n Tage später (oder früher) als "JJJJ-MM-TT". */
export function plusTage(iso: string, tage: number): string {
  return new Date((tagesnummer(iso) + tage) * TAG_MS).toISOString().slice(0, 10);
}

/** Der Kalendertag n Jahre später (am 29.2. des Schaltjahres: der 28.). */
export function plusJahre(iso: string, jahre: number): string {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  const ziel = new Date(Date.UTC(jahr + jahre, monat - 1, tag));
  // Ein Überlauf (29.2. -> 1.3.) fiele in den nächsten Monat: zurück auf den letzten Tag.
  if (ziel.getUTCMonth() !== monat - 1) ziel.setUTCDate(0);
  return ziel.toISOString().slice(0, 10);
}

/** Vorschlag für das Ende einer Läufigkeit, die an diesem Tag beginnt. */
export function laeufigkeitEnde(beginn: string): string {
  return plusTage(beginn, LAEUFIGKEIT_TAGE);
}

/** Ob die Pause an diesem Tag gilt (Standard: heute auf dem Gerät). */
export function pausiertAm(
  pause: Pick<DogPause, "startDate" | "endDate"> | null | undefined,
  tag: string = heuteIso(),
): boolean {
  if (!pause) return false;
  return pause.startDate <= tag && (pause.endDate === null || tag <= pause.endDate);
}

/**
 * Die Pause, die der Hund heute hat - oder null. Der Server liefert sie schon
 * für heute; geprüft wird sie hier trotzdem noch einmal, weil der Lesecache des
 * Geräts einen Stand von gestern zeigen kann, in dem die Pause inzwischen zu
 * Ende ist.
 */
export function aktuellePause(
  hund: Pick<Dog, "pause"> | null | undefined,
  tag: string = heuteIso(),
): DogPause | null {
  const pause = hund?.pause ?? null;
  return pausiertAm(pause, tag) ? pause : null;
}

/** Ob eine Pause vorbei ist, gerade läuft oder noch bevorsteht. */
export function pausenStand(
  pause: Pick<DogPause, "startDate" | "endDate">,
  tag: string = heuteIso(),
): "vorbei" | "laufend" | "geplant" {
  if (pause.endDate !== null && pause.endDate < tag) return "vorbei";
  return pause.startDate > tag ? "geplant" : "laufend";
}

/** "03.11." bzw. "03/11" - Tag und Monat für "bis …". */
export function datumTagMonat(iso: string, sprache: Sprache = "de"): string {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), { day: "2-digit", month: "2-digit" });
}

/** Die Gründe mit ihren Beschriftungen, in der Reihenfolge der Auswahl. */
export const PAUSE_GRUND_TEXT: Record<PauseGrund, string> = {
  [PAUSE_GRUND.Verletzt]: uebersetzbar("verletzt"),
  [PAUSE_GRUND.Krank]: uebersetzbar("krank"),
  [PAUSE_GRUND.Laeufig]: uebersetzbar("läufig"),
};

// Ganze Sätze je Grund statt eines eingesetzten Wortes: Englisch stellt sie
// anders, und ein übersetzter Satzteil als Wert ließe sich dort nicht mehr
// grammatisch einsetzen.
const ZEILE_BIS: Record<PauseGrund, string> = {
  [PAUSE_GRUND.Verletzt]: uebersetzbar("Pause: verletzt · bis {datum}"),
  [PAUSE_GRUND.Krank]: uebersetzbar("Pause: krank · bis {datum}"),
  [PAUSE_GRUND.Laeufig]: uebersetzbar("Pause: läufig · bis {datum}"),
};
const ZEILE_OFFEN: Record<PauseGrund, string> = {
  [PAUSE_GRUND.Verletzt]: uebersetzbar("Pause: verletzt · bis auf Weiteres"),
  [PAUSE_GRUND.Krank]: uebersetzbar("Pause: krank · bis auf Weiteres"),
  [PAUSE_GRUND.Laeufig]: uebersetzbar("Pause: läufig · bis auf Weiteres"),
};

/**
 * Die Zeile, die eine laufende Pause beschreibt: "Pause: verletzt · bis 03.11."
 * bzw. "… bis auf Weiteres". Wer den Grund nicht kennt (betreuende Trainer:in),
 * liest nur "Pause bis 03.11." - der Server liefert ihn dort gar nicht aus.
 */
export function pausenZeile(t: Uebersetzer, pause: DogPause, sprache: Sprache = "de"): string {
  const datum = pause.endDate !== null ? datumTagMonat(pause.endDate, sprache) : null;
  if (pause.reason === null) {
    return datum !== null ? t("Pause bis {datum}", { datum }) : t("Pause bis auf Weiteres");
  }
  return datum !== null ? t(ZEILE_BIS[pause.reason], { datum }) : t(ZEILE_OFFEN[pause.reason]);
}

/** Der Zeitraum einer Pause für den Verlauf: "03.10.2026 – 20.10.2026", "ab 03.10.2026", bei einem Tag nur der Tag. */
export function pausenZeitraumText(t: Uebersetzer, pause: Pick<DogPause, "startDate" | "endDate">, datum: (iso: string) => string): string {
  if (pause.endDate === null) return t("ab {datum}", { datum: datum(pause.startDate) });
  if (pause.endDate === pause.startDate) return datum(pause.startDate);
  return `${datum(pause.startDate)} – ${datum(pause.endDate)}`;
}

/** Die wählbaren Gründe: Läufigkeit nur bei Hündinnen (DogGender 1). */
export function waehlbareGruende(geschlecht: DogGender): PauseGrund[] {
  return geschlecht === 1
    ? [PAUSE_GRUND.Verletzt, PAUSE_GRUND.Krank, PAUSE_GRUND.Laeufig]
    : [PAUSE_GRUND.Verletzt, PAUSE_GRUND.Krank];
}

export type PausenEingabe = {
  grund: PauseGrund | null;
  /** Erster Pausentag (JJJJ-MM-TT). */
  von: string;
  /** Letzter Pausentag; leer = bis auf Weiteres. */
  bis: string;
  notiz: string;
};

/**
 * Das Ende, das nach einer Änderung des Beginns oder des Grundes passt.
 *
 * Läufigkeit schlägt Beginn + 21 Tage vor. Hat die Person das Ende aber selbst
 * eingestellt (`bisVonHand`), bleibt es, wie es ist: Ein Vorschlag, der unter
 * den Fingern wegspringt, wäre ärgerlicher als keiner.
 */
export function vorgeschlagenesEnde(
  grund: PauseGrund | null,
  von: string,
  aktuellesEnde: string,
  bisVonHand: boolean,
): string {
  if (bisVonHand) return aktuellesEnde;
  return grund === PAUSE_GRUND.Laeufig && /^\d{4}-\d{2}-\d{2}$/.test(von) ? laeufigkeitEnde(von) : "";
}

/** Fehlermeldung zur Eingabe, sonst null - die Regeln des Servers, damit man sie vor dem Senden sieht. */
export function pruefePause(
  t: Uebersetzer,
  eingabe: PausenEingabe,
  geschlecht: DogGender,
  heute: string = heuteIso(),
): string | null {
  if (eingabe.grund === null) return t("Bitte einen Grund für die Pause wählen.");
  if (eingabe.grund === PAUSE_GRUND.Laeufig && geschlecht !== 1) return t("Läufigkeit gibt es nur bei Hündinnen.");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(eingabe.von)) return t("Bitte den Beginn der Pause angeben.");
  if (eingabe.von < plusJahre(heute, -MAX_JAHRE_ZURUECK) || eingabe.von > plusJahre(heute, MAX_JAHRE_VORAUS)) {
    return t("Der Beginn der Pause liegt zu weit entfernt.");
  }
  if (eingabe.bis !== "") {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(eingabe.bis)) return t("Bitte das Ende der Pause angeben.");
    if (eingabe.bis < eingabe.von) return t("Das Ende der Pause darf nicht vor ihrem Beginn liegen.");
    if (eingabe.bis > plusJahre(eingabe.von, MAX_DAUER_JAHRE)) {
      return t("Die Pause ist zu lang - für eine unbestimmte Dauer das Ende offen lassen.");
    }
  }
  return null;
}

/**
 * Wie viele Tage von `von` (einschließlich) bis `bis` (ausschließlich) auf
 * Pausen fallen. Dieselbe Rechnung wie im Server (Trainingspausen.PausentageZwischen):
 * Überschneidungen zählen einmal, ein offenes Ende gilt bis `heute` - sein
 * weiterer Verlauf ist unbekannt.
 */
export function pausentageZwischen(
  pausen: readonly { start: string; end: string | null }[],
  von: string,
  bis: string,
  heute: string = heuteIso(),
): number {
  const ersterTag = tagesnummer(von);
  const letzterTag = tagesnummer(bis) - 1;
  const stuecke = pausen
    .map((p) => [Math.max(tagesnummer(p.start), ersterTag), Math.min(tagesnummer(p.end ?? (p.start > heute ? p.start : heute)), letzterTag)] as const)
    .filter(([anfang, ende]) => anfang <= ende)
    .sort((a, b) => a[0] - b[0]);

  let summe = 0;
  let bisher = -Infinity;
  for (const [anfang, ende] of stuecke) {
    const ab = Math.max(anfang, bisher + 1);
    if (ab <= ende) summe += ende - ab + 1;
    bisher = Math.max(bisher, ende);
  }
  return summe;
}

/**
 * Das Zieldatum, das der Hinweis "Zieldatum verschieben?" vorschlägt: so viele
 * Tage später, wie dem Plan bis zu seiner letzten Woche fehlen (die bekannten
 * Pausen eingerechnet). Ohne Anhaltspunkt zwei Wochen später. Nur ein Vorschlag -
 * die Person ändert das Datum im Fenster.
 */
export function vorschlagZieldatum(
  goal: Pick<Goal, "targetDate" | "planPausen" | "trainingPlan">,
  heute: string = heuteIso(),
): string {
  const plan = goal.trainingPlan;
  const zurueckfall = plusTage(goal.targetDate, 14);
  if (!plan || plan.items.length === 0) return zurueckfall;

  const beginn = heuteIso(new Date(plan.generatedAt).getTime());
  const letzteWoche = Math.max(...plan.items.map((item) => item.weekNumber));
  const pausentage = pausentageZwischen(goal.planPausen ?? [], beginn, goal.targetDate, heute);
  const aktiveTage = tagesnummer(goal.targetDate) - tagesnummer(beginn) - pausentage;
  const fehlend = (letzteWoche - 1) * 7 - aktiveTage;
  return fehlend > 0 ? plusTage(goal.targetDate, fehlend) : zurueckfall;
}

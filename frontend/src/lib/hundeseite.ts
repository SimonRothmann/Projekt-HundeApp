import type { Dog, Goal, TrainingSession } from "@/lib/types";
import { pruefungsName, tageBisPruefung } from "@/lib/pruefung";
import { wochenFortschritt } from "@/lib/trainingsplan";
import { istNeuesFeedback } from "@/lib/feedback-gesehen";

/**
 * Entscheidungen der Hundeseite als reine Funktionen: welcher Reiter offen ist,
 * was alte Anker bedeuten, was die Statuszeile sagt, wer als Chip erscheint.
 *
 * Die Seite hat dafür mehrere Quellen - Parameter in der Adresse, das
 * Fragment alter Links (#trainingsplan), die Ziele des Hundes - und genau dort
 * entstehen die Fehler: ein von Hand getippter Wert, ein Link von der
 * Startseite, ein Hund ohne Ziel. Nach dem Muster von gruppen-ansicht.ts.
 */

export type HundeReiter = "plan" | "tagebuch";

/** Name des Parameters in der Adresse: /dogs/{id}?tab=tagebuch. */
export const REITER_PARAMETER = "tab";

/** Nur die zwei bekannten Werte; alles andere (leer, falsch geschrieben, fremd) ist "keine Angabe". */
export function reiterAusParameter(wert: string | null | undefined): HundeReiter | null {
  return wert === "plan" || wert === "tagebuch" ? wert : null;
}

/**
 * Das Ziel, auf das sich der Hund gerade vorbereitet: unter den aktiven das mit
 * dem nächsten Zieldatum, das noch nicht verstrichen ist. Ein abgelaufenes Ziel
 * wartet auf sein Ergebnis und hat keine laufende Woche mehr.
 */
export function laufendesZiel(goals: readonly Goal[] | null | undefined, jetzt: number = Date.now()): Goal | null {
  const laufende = (goals ?? []).filter((g) => g.status === 0 && tageBisPruefung(g.targetDate, jetzt) >= 0);
  return laufende.reduce<Goal | null>((bestes, g) => (bestes === null || g.targetDate < bestes.targetDate ? g : bestes), null);
}

/**
 * Welcher Reiter offen ist.
 *
 * Ein gültiger Parameter gewinnt. Ohne ihn öffnet ein Link auf einen Eintrag
 * (?eintrag=) das Tagebuch, sonst der Plan, solange der Hund ein laufendes Ziel
 * hat - wer wegen des Plans kommt, soll ihn sehen. Hat er keins, gibt es dort
 * nichts außer dem Knopf "Ziel setzen", und das Tagebuch ist die bessere Wahl.
 */
export function waehleReiter(
  parameter: string | null | undefined,
  goals: readonly Goal[] | null | undefined,
  optionen: { eintrag?: boolean; jetzt?: number } = {},
): HundeReiter {
  const gewuenscht = reiterAusParameter(parameter);
  if (gewuenscht) return gewuenscht;
  if (optionen.eintrag) return "tagebuch";
  return laufendesZiel(goals, optionen.jetzt) ? "plan" : "tagebuch";
}

/**
 * Der Reiter, der gerade gilt: ein Parameter in der Adresse, sonst der beim
 * ersten vollständigen Laden festgelegte Standard (`eingefroren`), sonst - bis
 * dahin - die laufende Berechnung aus den Zielen.
 *
 * Eingefroren, weil sich die Ziele auf der Seite selbst ändern: Wer den Plan
 * offen hat und sein einziges laufendes Ziel als erreicht einträgt, soll nicht
 * unter den Händen auf das Tagebuch umgeschaltet werden - die Rückmeldung
 * seiner Aktion stünde im Reiter, der gerade verschwindet. Dasselbe gilt für
 * den Lesecache, dessen Ziele von den frischen abweichen können.
 */
export function geltenderReiter(
  parameter: string | null | undefined,
  eingefroren: HundeReiter | null,
  goals: readonly Goal[] | null | undefined,
  optionen: { eintrag?: boolean; jetzt?: number } = {},
): HundeReiter {
  return reiterAusParameter(parameter) ?? eingefroren ?? waehleReiter(null, goals, optionen);
}

// ---- Alte Anker ----

export type AnkerWunsch = {
  /** Reiter, der zu öffnen ist; null = der Anker sagt dazu nichts. */
  reiter: HundeReiter | null;
  /** Das Eintragen-Fenster öffnen. */
  formular: boolean;
  /** Das Fenster "Fährte legen" öffnen. */
  faehrte: boolean;
};

/**
 * Was die Fragmente alter Links (Startseite, Trainerübersicht, Hinweis auf eine
 * unterbrochene Aufzeichnung) auf der neuen Seite bedeuten. Die Anker selbst
 * gibt es nicht mehr; die Links bleiben aber gültig.
 */
export function ankerWunsch(hash: string | null | undefined): AnkerWunsch | null {
  switch ((hash ?? "").replace(/^#/, "")) {
    case "trainingsplan":
      return { reiter: "plan", formular: false, faehrte: false };
    case "training-erfassen":
      // Das Fenster liegt über jedem Reiter; ein Wechsel im Hintergrund wäre nur ein Sprung.
      return { reiter: null, formular: true, faehrte: false };
    case "faehrte-aufnehmen":
      return { reiter: null, formular: false, faehrte: true };
    default:
      return null;
  }
}

/**
 * Die Adresse mit geänderten Parametern, ohne Fragment. Ein Wert null entfernt
 * den Parameter. Alles andere bleibt, wie es war (?from= für den Zurück-Knopf).
 */
export function adresseMit(
  pfad: string,
  suchparameter: string,
  aenderungen: Record<string, string | null>,
): string {
  const parameter = new URLSearchParams(suchparameter);
  for (const [name, wert] of Object.entries(aenderungen)) {
    if (wert === null) parameter.delete(name);
    else parameter.set(name, wert);
  }
  const rest = parameter.toString();
  return rest ? `${pfad}?${rest}` : pfad;
}

// ---- Hunde-Chips ----

/** Die Hunde, zwischen denen man wechseln kann: alle nicht archivierten. */
export function aktiveHunde(hunde: readonly Dog[] | null | undefined): Dog[] {
  return (hunde ?? []).filter((d) => !d.archivedAt);
}

/**
 * Ob die Chips zum Wechseln erscheinen: nur mit mehr als einem aktiven Hund, und
 * nur auf der Seite eines eigenen oder mitbesessenen Hundes. Eine Trainer:in, die
 * den Hund eines Mitglieds ansieht, wechselt nicht zwischen ihren eigenen Hunden.
 */
export function zeigeHundeChips(hunde: readonly Dog[] | null | undefined, gehoertMir: boolean): boolean {
  return gehoertMir && aktiveHunde(hunde).length > 1;
}

/** Adresse der Seite eines anderen Hundes: Reiter und Zurück-Ziel bleiben, ein Eintrag nicht. */
export function hundeAdresse(hundId: string, suchparameter: string): string {
  const parameter = new URLSearchParams(suchparameter);
  const behalten = new URLSearchParams();
  for (const name of [REITER_PARAMETER, "from"]) {
    const wert = parameter.get(name);
    if (wert !== null) behalten.set(name, wert);
  }
  return adresseMit(`/dogs/${hundId}`, behalten.toString(), {});
}

// ---- Statuszeile ----

export type ZielStatus = {
  /** Name der Prüfung (Prüfungsordnung, sonst Sportart). */
  name: string;
  /** Stand der laufenden Woche; null ohne geplante Übung (Pause, leerer Plan). */
  woche: { woche: number; erledigt: number; geplant: number } | null;
  /** Tage bis zur Prüfung (0 = heute). */
  tage: number;
};

/** Was die Statuszeile über das laufende Ziel sagt; null, wenn es keins gibt. */
export function zielStatus(goals: readonly Goal[] | null | undefined, jetzt: number = Date.now()): ZielStatus | null {
  const ziel = laufendesZiel(goals, jetzt);
  if (!ziel) return null;
  const stand = wochenFortschritt(ziel, jetzt);
  return {
    name: pruefungsName(ziel),
    woche: stand ? { woche: stand.woche, erledigt: stand.erledigt, geplant: stand.geplant } : null,
    tage: tageBisPruefung(ziel.targetDate, jetzt),
  };
}

/**
 * Die Einträge mit Feedback, das diese Person noch nicht gesehen hat - in der
 * Reihenfolge der Liste vom Server (neueste zuerst). Nur für Besitzer:innen: die
 * Trainer:in, die es schrieb, muss es nicht "sehen".
 */
export function ungeseheneRueckmeldungen(
  sessions: readonly TrainingSession[] | null | undefined,
  gesehen: ReadonlySet<string>,
  istBesitzer: boolean,
): TrainingSession[] {
  if (!istBesitzer) return [];
  return (sessions ?? []).filter((s) => istNeuesFeedback(s, gesehen));
}

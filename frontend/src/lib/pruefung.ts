import type { Goal } from "@/lib/types";
import type { Sprache } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { ortsformat } from "@/lib/ortsformat";
import { heuteIso, plusTage, tageBis, tagTeile } from "@/lib/kalendertag";

/**
 * Rechnen und Sortieren rund um Prüfungstermin und Prüfungsergebnis - als reine
 * Funktionen, damit die Startseite, die Hundeseite und der Abschluss-Dialog
 * dasselbe meinen und die Regeln getestet werden können.
 *
 * Daten sind Kalendertage ("2026-10-03"), keine Zeitpunkte. Gerechnet und
 * formatiert wird mit lib/kalendertag.ts und nie mit `new Date("2026-10-03")`:
 * Das wäre UTC-Mitternacht und fiele westlich von Greenwich auf den Vortag.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** Tage von heute bis zum Datum: positiv = noch hin, 0 = heute, negativ = vorbei. */
export function tageBisPruefung(zieldatum: string, jetzt: number = Date.now()): number {
  return tageBis(zieldatum, heuteIso(jetzt));
}

/**
 * Die Restzeit bis zum Prüfungstag in Worten: "noch 12 Tage", "heute". Ist der
 * Tag vorbei, fehlt nur noch das Ergebnis - das sagt der Text statt einer
 * negativen Zahl.
 */
export function restzeitText(t: Uebersetzer, tage: number): string {
  if (tage < 0) return t("Ergebnis fehlt");
  if (tage === 0) return t("heute");
  return tage === 1 ? t("noch 1 Tag") : t("noch {n} Tage", { n: tage });
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
  return plusTage(heuteIso(jetzt), 1);
}

/** Die Felder eines Prüfungsergebnisses, wie sie im Formular stehen: Tag (JJJJ-MM-TT), Punkte, Notiz. */
export type ErgebnisEingabe = { datum: string; punkte: string; notiz: string };

/**
 * Fehlermeldung zu einem Prüfungsergebnis, sonst null - die Regeln des Servers, damit man sie vor
 * dem Senden sieht. Der Prüfungstag darf höchstens morgen sein (ein Gerät kurz vor Mitternacht
 * kann dem Server voraus sein); Punkte sind optional, ganz und nicht über der Höchstpunktzahl der
 * Prüfungsordnung (falls bekannt).
 */
export function pruefeErgebnis(
  t: Uebersetzer,
  eingabe: ErgebnisEingabe,
  hoechstens: number | null | undefined,
  jetzt: number = Date.now(),
): string | null {
  if (!eingabe.datum) return t("Bitte den Prüfungstag angeben.");
  if (eingabe.datum > morgenIso(jetzt)) return t("Der Prüfungstag darf nicht in der Zukunft liegen.");
  const text = eingabe.punkte.trim();
  if (text === "") return null;
  const punkte = Number(text);
  if (!Number.isInteger(punkte) || punkte < 0) return t("Die Punktzahl muss eine ganze Zahl ab 0 sein.");
  if (hoechstens != null && punkte > hoechstens) return t("Die Punktzahl darf höchstens {max} betragen.", { max: hoechstens });
  return null;
}

/**
 * Fehlermeldung zu einem neuen Prüfungstermin (nach nicht bestanden, oder beim Verschieben des
 * Zieldatums), sonst null: Er muss angegeben sein und mindestens morgen liegen. Die eine Regel für
 * beide Dialoge.
 */
export function pruefeNeuenTermin(t: Uebersetzer, termin: string, jetzt: number = Date.now()): string | null {
  if (termin === "") return t("Bitte den neuen Prüfungstermin angeben.");
  return termin < morgenIso(jetzt) ? t("Der neue Termin muss in der Zukunft liegen.") : null;
}

/** Aktive Ziele, deren Prüfungstag noch nicht verstrichen ist - alles, worauf sich der Hund gerade vorbereitet. */
export function laufendeZiele(goals: readonly Goal[] | null | undefined, jetzt: number = Date.now()): Goal[] {
  return (goals ?? []).filter((g) => g.status === 0 && tageBisPruefung(g.targetDate, jetzt) >= 0);
}

/** Das Ziel mit dem nächsten Zieldatum. */
export function fruehestesZiel(goals: readonly Goal[]): Goal | null {
  return goals.reduce<Goal | null>((bestes, g) => (bestes === null || g.targetDate < bestes.targetDate ? g : bestes), null);
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

/**
 * Name der Prüfung für Überschriften: die Prüfungsordnung, sonst die Sportart.
 * Mit Sprache zeigt sie bei FCI-Ordnungen auf Englisch den englischen Namen
 * (siehe lib/katalog-name.ts); ohne bleibt es der deutsche.
 */
export function pruefungsName(
  goal: Pick<Goal, "regulationName" | "sportName"> & Partial<Pick<Goal, "regulationNameEn" | "sportNameEn">>,
  sprache: Sprache = "de",
): string {
  return goal.regulationName
    ? katalogName({ name: goal.regulationName, nameEn: goal.regulationNameEn }, sprache)
    : katalogName({ name: goal.sportName, nameEn: goal.sportNameEn }, sprache);
}

/** "272 von 300 Punkten" bzw. "272 Punkte", wenn keine Höchstpunktzahl bekannt ist. */
export function punkteText(t: Uebersetzer, punkte: number, hoechstens: number | null | undefined): string {
  return hoechstens != null
    ? t("{punkte} von {max} Punkten", { punkte, max: hoechstens })
    : t("{punkte} Punkte", { punkte });
}

/** "03.10.2026" bzw. "03/10/2026". */
export function datumKurz(iso: string, sprache: Sprache = "de"): string {
  const [jahr, monat, tag] = tagTeile(iso);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/** "Samstag, 3. Oktober" - für "am …" auf der Zielkarte. */
export function datumMitWochentag(iso: string, sprache: Sprache = "de"): string {
  const [jahr, monat, tag] = tagTeile(iso);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(ortsformat(sprache), {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
}

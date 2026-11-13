import { MERKZETTEL_ZIEL } from "@/lib/types";
import type { Goal, MerkzettelEintrag, MerkzettelZiel, NaechstesTraining, TrainingSession } from "@/lib/types";
import { TRAININGS_QUELLE } from "@/lib/types";
import { inhaltsSchluessel, katalogZeile, wochenChips, zeilenSchluessel, zuletztChips, type EintragZeile } from "@/lib/eintragen";
import { WOCHENTAGE } from "@/lib/erinnerungen";
import { heuteIso, plusTage, tageBis, tagAlsDatum } from "@/lib/kalendertag";
import { datumTagMonat } from "@/lib/pausen";
import type { Sprache } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";

/**
 * Der Merkzettel fürs nächste Training als reine Funktionen: Beschriftungen wie
 * "Nächstes Training · Di 18:00", die Übungs-Chips im Merken-Fenster, was das
 * Eintragen-Fenster als "Gemerkt fürs heutige Training" zeigt und wie das
 * Abhaken eine Übung ergänzt. Die Regeln für das nächste Training selbst
 * (Gruppentermin, Rhythmus, Tagebuch-Muster) rechnet der Server - hier steht nur,
 * was die Oberfläche daraus macht.
 *
 * Tage sind Kalendertage ("2026-11-04"), gerechnet wie in lib/kalendertag.ts.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** So viele Übungs-Chips zeigt das Merken-Fenster höchstens (dazu "Andere…"). */
export const MAX_UEBUNGS_CHIPS = 6;

const ISO = /^\d{4}-\d{2}-\d{2}$/;

/** Wie weit voraus ein Datum gewählt werden kann (ein Jahr; der Server erlaubt 366 Tage). */
export const MAX_TAGE_VORAUS = 365;

/** "18:00:00" wird zu "18:00"; null bleibt null. */
export function uhrzeitKurz(zeit: string | null | undefined): string | null {
  return zeit ? zeit.slice(0, 5) : null;
}

/**
 * Wann ein Training ist, kurz: "heute 18:00", "morgen", "Di 18:00" - bis sechs Tage voraus
 * der Wochentag, danach mit Tag und Monat ("Di 17.11. 18:00"). Ohne Uhrzeit steht nur der Tag.
 */
export function zeitpunktText(
  t: Uebersetzer,
  sprache: Sprache,
  datum: string,
  uhrzeit: string | null | undefined,
  heute: string = heuteIso(),
): string {
  const tage = tageBis(datum, heute);
  const wochentag = t(WOCHENTAGE[(tagAlsDatum(datum).getDay() + 6) % 7].kurz);
  const tag =
    tage === 0
      ? t("heute")
      : tage === 1
        ? t("morgen")
        : tage >= 2 && tage <= 6
          ? wochentag
          : `${wochentag} ${datumTagMonat(datum, sprache)}`;
  return [tag, uhrzeitKurz(uhrzeit)].filter(Boolean).join(" ");
}

/**
 * Beschriftung des Chips "Nächstes Training · Di 18:00". Ist das nächste Training unbekannt
 * (oder noch nicht geladen), steht nur "Nächstes Training".
 */
export function naechstesTrainingText(
  t: Uebersetzer,
  sprache: Sprache,
  naechstes: NaechstesTraining | null,
  heute: string = heuteIso(),
): string {
  if (!naechstes || naechstes.source === TRAININGS_QUELLE.Unbekannt || !naechstes.date) return t("Nächstes Training");
  return t("Nächstes Training · {wann}", { wann: zeitpunktText(t, sprache, naechstes.date, naechstes.time, heute) });
}

/** Wohin ein Eintrag in der Liste zeigt: "Di 18:00", "Nächstes Training" (noch unbekannt), "Nächste Woche" oder das gewählte Datum. */
export function zielText(t: Uebersetzer, sprache: Sprache, eintrag: MerkzettelEintrag, heute: string = heuteIso()): string {
  if (eintrag.targetDate) return zeitpunktText(t, sprache, eintrag.targetDate, eintrag.targetTime, heute);
  return eintrag.goal === MERKZETTEL_ZIEL.NaechsteWoche ? t("Nächste Woche") : t("Nächstes Training");
}

// ---- Eingabe ----

export function istGueltigerText(text: string): boolean {
  const gekuerzt = text.trim();
  return gekuerzt.length > 0 && gekuerzt.length <= TEXTLAENGE.merkzettelText;
}

/** Ein Datum fürs Ziel "Datum": heute oder später, höchstens ein Jahr voraus. */
export function istGueltigesZielDatum(datum: string, heute: string = heuteIso()): boolean {
  if (!ISO.test(datum)) return false;
  const tage = tageBis(datum, heute);
  return tage >= 0 && tage <= MAX_TAGE_VORAUS;
}

/** Das Datum, mit dem das Feld "Datum…" beginnt: morgen. */
export function vorgabeZielDatum(heute: string = heuteIso()): string {
  return plusTage(heute, 1);
}

/**
 * Der Eintrag, wie ihn POST /api/dogs/{id}/merkzettel erwartet. Die Id vergibt das Gerät: Wiederholt die
 * Offline-Warteschlange die Anfrage, entsteht der Eintrag nicht doppelt. Das Datum geht nur beim Ziel "Datum" mit.
 */
export function baueMerkenNutzlast(eingabe: {
  id: string;
  text: string;
  exerciseId: string | null;
  ziel: MerkzettelZiel;
  datum: string;
}) {
  return {
    id: eingabe.id,
    text: eingabe.text.trim(),
    exerciseId: eingabe.exerciseId,
    goal: eingabe.ziel,
    targetDate: eingabe.ziel === MERKZETTEL_ZIEL.Datum ? eingabe.datum : null,
  };
}

// ---- Übungs-Chips ----

export type UebungsChip = { exerciseId: string; name: string; nameEn: string | null };

/**
 * Die Übungen, die das Merken-Fenster als Chips anbietet: erst die offenen Übungen der laufenden Planwoche,
 * dann die zuletzt geübten - höchstens {@link MAX_UEBUNGS_CHIPS}, jede nur einmal. Nur Katalog-Übungen: Der
 * Eintrag verknüpft eine Übung aus dem Katalog; was frei eingetragen wurde, steht im Text.
 */
export function uebungsChips(
  goals: readonly Goal[] | null | undefined,
  sessions: readonly TrainingSession[] | null | undefined,
  max: number = MAX_UEBUNGS_CHIPS,
  jetzt: number = Date.now(),
): UebungsChip[] {
  const chips: UebungsChip[] = [];
  const gesehen = new Set<string>();
  const nimm = (exerciseId: string | null, name: string, nameEn: string | null) => {
    if (!exerciseId || !name.trim() || chips.length >= max || gesehen.has(exerciseId)) return;
    gesehen.add(exerciseId);
    chips.push({ exerciseId, name, nameEn });
  };
  for (const chip of wochenChips(goals, jetzt)) nimm(chip.item.exerciseId, chip.name, chip.nameEn);
  // Die Zuletzt-Liste ist nicht auf Katalog-Übungen beschränkt: genug anfordern, dann filtern.
  for (const chip of zuletztChips(sessions, new Set(), 1000)) nimm(chip.exerciseId, chip.name, chip.nameEn);
  return chips;
}

// ---- Training erfassen ----

/**
 * Die offenen Einträge, die "Gemerkt fürs heutige Training" zeigt: alle für das nächste Training (das ist
 * das, was jetzt eingetragen wird) und die, deren Tag heute ist oder schon vorbei. Einträge mit einem Tag in
 * der Zukunft warten. Erledigte zählen nie.
 */
export function fuerHeutigesTraining(eintraege: readonly MerkzettelEintrag[] | null | undefined, heute: string = heuteIso()): MerkzettelEintrag[] {
  return (eintraege ?? []).filter(
    (e) =>
      e.doneAt === null &&
      (e.goal === MERKZETTEL_ZIEL.NaechstesTraining || (e.targetDate !== null && tageBis(e.targetDate, heute) <= 0)),
  );
}

/**
 * Was am heutigen Tag ansteht (Karte auf der Startseite): die offenen Einträge, deren Ziel heute ist, und die
 * früheste Uhrzeit darunter - bei mehreren ist das die der Erinnerung.
 */
export function heutigeEintraege(
  eintraege: readonly MerkzettelEintrag[] | null | undefined,
  heute: string = heuteIso(),
): { eintraege: MerkzettelEintrag[]; uhrzeit: string | null } {
  const heutige = (eintraege ?? []).filter((e) => e.doneAt === null && e.targetDate === heute);
  const zeiten = heutige.map((e) => e.targetTime).filter((zeit): zeit is string => zeit !== null).sort();
  return { eintraege: heutige, uhrzeit: zeiten[0] ?? null };
}

/** Welche Zeile im Eintragen-Fenster durch das Anhaken welches Eintrags entstanden ist (Eintrags-Id -> Kennung der Zeile). */
export type Ergaenzt = Readonly<Record<string, string>>;

/**
 * Hakt einen Eintrag an oder ab. Beim Anhaken wird seine verknüpfte Übung als Zeile ergänzt - sichtbar im
 * Fenster, bevor gespeichert wird - sofern sie nicht schon darin steht (keine Dublette). Beim Abhaken verschwindet
 * die Zeile wieder, aber nur, wenn das Anhaken sie angelegt hat und kein anderer angehakter Eintrag dieselbe
 * Übung meint.
 *
 * @param angehakt die angehakten Einträge vor dieser Änderung.
 */
export function hakeAn(
  zeilen: readonly EintragZeile[],
  ergaenzt: Ergaenzt,
  eintrag: MerkzettelEintrag,
  an: boolean,
  angehakt: readonly MerkzettelEintrag[],
): { zeilen: EintragZeile[]; ergaenzt: Ergaenzt } {
  if (an) {
    if (!eintrag.exerciseId || !eintrag.exerciseName) return { zeilen: [...zeilen], ergaenzt };
    const schluessel = inhaltsSchluessel(eintrag.exerciseId, eintrag.exerciseName);
    if (zeilen.some((zeile) => zeilenSchluessel(zeile) === schluessel)) return { zeilen: [...zeilen], ergaenzt };

    const zeile = katalogZeile({ id: eintrag.exerciseId, name: eintrag.exerciseName, nameEn: eintrag.exerciseNameEn });
    return { zeilen: [...zeilen, zeile], ergaenzt: { ...ergaenzt, [eintrag.id]: zeile.schluessel } };
  }

  const kennung = ergaenzt[eintrag.id];
  const rest = Object.fromEntries(Object.entries(ergaenzt).filter(([id]) => id !== eintrag.id));
  if (kennung === undefined) return { zeilen: [...zeilen], ergaenzt };

  const jemandAnderes = angehakt.some((e) => e.id !== eintrag.id && e.exerciseId !== null && e.exerciseId === eintrag.exerciseId);
  return { zeilen: jemandAnderes ? [...zeilen] : zeilen.filter((zeile) => zeile.schluessel !== kennung), ergaenzt: rest };
}

// ---- "Wann trainierst du meistens?" ----

/** Schaltet einen Wochentag in der Maske (Mo = 1 ... So = 64) um; die Auswahl darf leer sein, dann ist "Speichern" gesperrt. */
export function schalteTag(maske: number, bit: number): number {
  return maske ^ bit;
}

/** Was PUT /api/dogs/{id}/trainingsrhythmus erwartet; null ohne gewählten Tag. */
export function rhythmusNutzlast(tage: number, stunde: number | null): { days: number; hour: number | null } | null {
  return tage >= 1 && tage <= 127 ? { days: tage, hour: stunde } : null;
}

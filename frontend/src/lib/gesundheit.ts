import { GESUNDHEIT_ART, GESUNDHEIT_ZUSTAND } from "@/lib/types";
import type { Dog, DogGesundheitStand, DogHealthRecord, GesundheitsArt, GesundheitsZustand } from "@/lib/types";
import type { Sprache } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { datumKurz, heuteIso } from "@/lib/pruefung";
import { datumTagMonat, plusJahre, tagesnummer } from "@/lib/pausen";
import { TEXTLAENGE } from "@/lib/textlaengen";

/**
 * Gesundheit des Hundes (Impfungen, Entwurmung, Parasitenschutz, Tierarzttermine) als
 * reine Funktionen: wie es um eine Frist steht, was "Als Nächstes" ansteht, was die
 * Hinweise auf der Startseite sagen und was der Server beim Eintragen ablehnt (hier
 * dieselben Regeln, damit man den Fehler vor dem Senden sieht).
 *
 * Dogity rechnet keine Impfintervalle aus: Die Person trägt "gültig bis" bzw. "nächste
 * fällig" selbst ein, wie der Tierarzt es im Impfpass vermerkt hat. Die Schnellwahl
 * (+1 Jahr ...) füllt das Feld nur vor. Maßgeblich ist der Impfpass.
 *
 * Daten sind Kalendertage ("2026-10-03"); gerechnet wird wie in lib/pausen.ts mit
 * Tagesnummern und nie mit `new Date("2026-10-03")`. Der Server rechnet in Vereinszeit,
 * das Gerät mit seinem Kalendertag - beides ist in Deutschland derselbe Tag.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** Gleiche Schwelle wie der Server (Gesundheitsstand.BaldTage). */
export const BALD_TAGE = 30;

/** Ab so vielen Tagen vor Ablauf erscheint der Hinweis auf der Startseite. */
export const HINWEIS_TAGE = 14;

/** Ein Tierarzttermin in höchstens so vielen Tagen erscheint auf der Startseite. */
export const HINWEIS_TERMIN_TAGE = 2;

/** Gleiche Grenzen wie der Server (DogHealthService). */
const MAX_JAHRE_ZURUECK = 25;
const MAX_JAHRE_VORAUS = 2;
const MAX_FRIST_JAHRE = 10;

const ISO = /^\d{4}-\d{2}-\d{2}$/;

/** Wie es um eine Frist steht - dieselbe Rechnung wie Gesundheitsstand.Zustand im Server. */
export function zustand(faellig: string | null, heute: string = heuteIso()): GesundheitsZustand {
  if (faellig === null) return GESUNDHEIT_ZUSTAND.Ok;
  const rest = tagesnummer(faellig) - tagesnummer(heute);
  if (rest < 0) return GESUNDHEIT_ZUSTAND.Ueberfaellig;
  return rest <= BALD_TAGE ? GESUNDHEIT_ZUSTAND.Bald : GESUNDHEIT_ZUSTAND.Ok;
}

/** Tage von heute bis zum Datum: positiv = noch hin, 0 = heute, negativ = vorbei. */
export function tageBis(iso: string, heute: string = heuteIso()): number {
  return tagesnummer(iso) - tagesnummer(heute);
}

export const ART_TEXT: Record<GesundheitsArt, string> = {
  [GESUNDHEIT_ART.Impfung]: uebersetzbar("Impfung"),
  [GESUNDHEIT_ART.Entwurmung]: uebersetzbar("Entwurmung"),
  [GESUNDHEIT_ART.Parasitenschutz]: uebersetzbar("Parasitenschutz"),
  [GESUNDHEIT_ART.Tierarzttermin]: uebersetzbar("Tierarzttermin"),
  [GESUNDHEIT_ART.Sonstiges]: uebersetzbar("Sonstiges"),
};

/** Überschrift je Art im Bereich "Aktuell" (Mehrzahl). */
export const ART_UEBERSCHRIFT: Record<GesundheitsArt, string> = {
  [GESUNDHEIT_ART.Impfung]: uebersetzbar("Impfungen"),
  [GESUNDHEIT_ART.Entwurmung]: uebersetzbar("Entwurmung"),
  [GESUNDHEIT_ART.Parasitenschutz]: uebersetzbar("Parasitenschutz"),
  [GESUNDHEIT_ART.Tierarzttermin]: uebersetzbar("Tierarzttermine"),
  [GESUNDHEIT_ART.Sonstiges]: uebersetzbar("Sonstiges"),
};

/** Die Arten in der Reihenfolge der Auswahl und der Liste. */
export const ARTEN: readonly GesundheitsArt[] = [
  GESUNDHEIT_ART.Impfung,
  GESUNDHEIT_ART.Entwurmung,
  GESUNDHEIT_ART.Parasitenschutz,
  GESUNDHEIT_ART.Tierarzttermin,
  GESUNDHEIT_ART.Sonstiges,
];

/**
 * Vorschläge für den Titel. Der gewählte Text wird so gespeichert, wie er in der Sprache der
 * Person steht, und bleibt frei änderbar - es sind Vorschläge, keine Liste.
 */
export const TITEL_VORSCHLAEGE: Record<GesundheitsArt, string[]> = {
  [GESUNDHEIT_ART.Impfung]: [
    uebersetzbar("Tollwut"),
    uebersetzbar("SHPPi"),
    uebersetzbar("Leptospirose"),
    uebersetzbar("Zwingerhusten"),
    uebersetzbar("Borreliose"),
  ],
  [GESUNDHEIT_ART.Entwurmung]: [uebersetzbar("Entwurmung")],
  [GESUNDHEIT_ART.Parasitenschutz]: [uebersetzbar("Zecken und Flöhe")],
  [GESUNDHEIT_ART.Tierarzttermin]: [uebersetzbar("Kontrolle"), uebersetzbar("Impfung"), uebersetzbar("Untersuchung")],
  [GESUNDHEIT_ART.Sonstiges]: [],
};

/** Der Titel, der nach einem Wechsel der Art vorbelegt wird, solange noch nichts getippt wurde. */
export function standardTitel(art: GesundheitsArt): string {
  return art === GESUNDHEIT_ART.Entwurmung ? uebersetzbar("Entwurmung") : "";
}

/** Eine Schnellwahl für "gültig bis" bzw. "nächste fällig": ein fester Abstand zum Datum des Eintrags. */
export type Schnellwahl = { label: string; jahre?: number; monate?: number };

/** Impfung: +1 oder +3 Jahre; Entwurmung und Parasitenschutz: +1 bis +6 Monate (Präparate wirken unterschiedlich lange). Bei den übrigen Arten gibt es keine. */
export function schnellwahlen(art: GesundheitsArt): Schnellwahl[] {
  if (art === GESUNDHEIT_ART.Impfung) {
    return [
      { label: uebersetzbar("+1 Jahr"), jahre: 1 },
      { label: uebersetzbar("+3 Jahre"), jahre: 3 },
    ];
  }
  if (art === GESUNDHEIT_ART.Entwurmung || art === GESUNDHEIT_ART.Parasitenschutz) {
    return [
      { label: uebersetzbar("+1 Monat"), monate: 1 },
      { label: uebersetzbar("+2 Monate"), monate: 2 },
      { label: uebersetzbar("+3 Monate"), monate: 3 },
      { label: uebersetzbar("+4 Monate"), monate: 4 },
      { label: uebersetzbar("+5 Monate"), monate: 5 },
      { label: uebersetzbar("+6 Monate"), monate: 6 },
    ];
  }
  return [];
}

/** Der Kalendertag n Monate später (der 31. + 1 Monat: der letzte Tag des Folgemonats). */
export function plusMonate(iso: string, monate: number): string {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  const ziel = new Date(Date.UTC(jahr, monat - 1 + monate, tag));
  if (ziel.getUTCMonth() !== (((monat - 1 + monate) % 12) + 12) % 12) ziel.setUTCDate(0);
  return ziel.toISOString().slice(0, 10);
}

/** Das Datum, das eine Schnellwahl ab dem Datum des Eintrags einträgt. */
export function faelligNach(datum: string, wahl: Schnellwahl): string {
  return wahl.jahre !== undefined ? plusJahre(datum, wahl.jahre) : plusMonate(datum, wahl.monate ?? 0);
}

/** "10:30:00" -> "10:30"; leer bzw. fehlend -> null. */
export function uhrzeitKurz(zeit: string | null | undefined): string | null {
  return zeit ? zeit.slice(0, 5) : null;
}

/** "10:30" -> "10:30:00", wie der Server die Uhrzeit liest; leer -> null. */
export function uhrzeitFuerServer(uhrzeit: string): string | null {
  return uhrzeit === "" ? null : uhrzeit.length === 5 ? `${uhrzeit}:00` : uhrzeit;
}

export type GesundheitsEingabe = {
  art: GesundheitsArt;
  titel: string;
  /** Gegeben am bzw. Tag des Termins (JJJJ-MM-TT). */
  datum: string;
  /** "HH:MM" oder leer; nur beim Tierarzttermin. */
  uhrzeit: string;
  /** Gültig bis bzw. nächste fällig (JJJJ-MM-TT) oder leer. */
  faellig: string;
  notiz: string;
};

/** Fehlermeldung zur Eingabe, sonst null - die Regeln des Servers, damit man sie vor dem Senden sieht. */
export function pruefeEintrag(t: Uebersetzer, eingabe: GesundheitsEingabe, heute: string = heuteIso()): string | null {
  const titel = eingabe.titel.trim();
  if (titel === "") return t("Bitte einen Titel angeben.");
  if (titel.length > TEXTLAENGE.gesundheitTitel) {
    return t("{was} ist zu lang (höchstens {hoechstens} Zeichen).", { was: t("Der Titel"), hoechstens: TEXTLAENGE.gesundheitTitel });
  }
  if (eingabe.notiz.trim().length > TEXTLAENGE.gesundheitNotiz) {
    return t("{was} ist zu lang (höchstens {hoechstens} Zeichen).", { was: t("Die Notiz"), hoechstens: TEXTLAENGE.gesundheitNotiz });
  }
  if (!ISO.test(eingabe.datum)) return t("Bitte ein Datum angeben.");
  if (eingabe.datum < plusJahre(heute, -MAX_JAHRE_ZURUECK) || eingabe.datum > plusJahre(heute, MAX_JAHRE_VORAUS)) {
    return t("Das Datum liegt zu weit entfernt.");
  }
  if (eingabe.art !== GESUNDHEIT_ART.Tierarzttermin && eingabe.faellig !== "") {
    if (!ISO.test(eingabe.faellig)) return t("Bitte das Ablaufdatum angeben.");
    if (eingabe.faellig < eingabe.datum) return t("Das Ablaufdatum darf nicht vor dem Datum des Eintrags liegen.");
    if (eingabe.faellig > plusJahre(eingabe.datum, MAX_FRIST_JAHRE)) return t("Das Ablaufdatum liegt zu weit entfernt.");
  }
  return null;
}

/** Ein Eintrag mit Frist, in "bis wann" gesprochen: Impfungen sind "gültig bis", alles andere "fällig". */
function istImpfung(art: GesundheitsArt): boolean {
  return art === GESUNDHEIT_ART.Impfung;
}

/**
 * Die Zeile zur Frist eines Eintrags, in ganzen Sätzen je Fall (Englisch stellt sie anders):
 * "gültig bis 03.11.2026", "läuft heute ab", "abgelaufen am 28.10.2026" bei Impfungen,
 * "nächste fällig am ...", "heute fällig", "fällig seit ..." bei allem anderen.
 */
export function fristText(
  t: Uebersetzer,
  art: GesundheitsArt,
  faellig: string,
  heute: string = heuteIso(),
  sprache: Sprache = "de",
): string {
  const rest = tageBis(faellig, heute);
  const datum = datumKurz(faellig, sprache);
  if (istImpfung(art)) {
    if (rest < 0) return t("abgelaufen am {datum}", { datum });
    return rest === 0 ? t("läuft heute ab") : t("gültig bis {datum}", { datum });
  }
  if (rest < 0) return t("fällig seit {datum}", { datum });
  return rest === 0 ? t("heute fällig") : t("nächste fällig am {datum}", { datum });
}

/** "Heute", "Morgen", "Übermorgen" bzw. das Datum, für Tierarzttermine in der Nähe. */
export function terminTagText(t: Uebersetzer, datum: string, heute: string = heuteIso(), sprache: Sprache = "de"): string {
  const rest = tageBis(datum, heute);
  if (rest === 0) return t("heute");
  if (rest === 1) return t("morgen");
  if (rest === 2) return t("übermorgen");
  return t("am {datum}", { datum: datumKurz(datum, sprache) });
}

/** Die Zeile zu einem Termin: "Mittwoch, 04.11.2026 · 10:30" - Datum und, falls eingetragen, Uhrzeit. */
export function terminText(t: Uebersetzer, datum: string, uhrzeit: string | null, sprache: Sprache = "de"): string {
  const zeit = uhrzeitKurz(uhrzeit);
  return zeit === null ? datumKurz(datum, sprache) : t("{datum}, {zeit} Uhr", { datum: datumKurz(datum, sprache), zeit });
}

// ---- Bereich "Gesundheit" auf der Hundeseite ----

export type Ton = "rot" | "gelb" | "neutral";

export type NaechstesZeile = {
  id: string;
  ton: Ton;
  /** Der Titel des Eintrags. */
  titel: string;
  art: GesundheitsArt;
  /** Wann bzw. wie lange: Frist- oder Terminzeile. */
  text: string;
};

/**
 * "Als Nächstes": überfällige Fristen (rot), dann bald fällige (gelb), dann der nächste
 * Tierarzttermin. Nur aktuelle Einträge - eine erneuerte Impfung beendet die Frist der alten.
 */
export function naechstes(
  t: Uebersetzer,
  eintraege: readonly DogHealthRecord[],
  heute: string = heuteIso(),
  sprache: Sprache = "de",
): NaechstesZeile[] {
  const fristen = eintraege
    .filter((e) => e.isCurrent && e.kind !== GESUNDHEIT_ART.Tierarzttermin && e.dueDate !== null)
    .map((e) => ({ e, z: zustand(e.dueDate, heute) }))
    .filter(({ z }) => z !== GESUNDHEIT_ZUSTAND.Ok)
    .sort((a, b) => (a.e.dueDate! < b.e.dueDate! ? -1 : a.e.dueDate! > b.e.dueDate! ? 1 : 0));

  const zeilen: NaechstesZeile[] = fristen.map(({ e, z }) => ({
    id: e.id,
    ton: z === GESUNDHEIT_ZUSTAND.Ueberfaellig ? "rot" : "gelb",
    titel: e.title,
    art: e.kind,
    text: fristText(t, e.kind, e.dueDate!, heute, sprache),
  }));

  const termin = eintraege
    .filter((e) => e.kind === GESUNDHEIT_ART.Tierarzttermin && e.date >= heute)
    .sort((a, b) => (a.date + (a.time ?? "99")).localeCompare(b.date + (b.time ?? "99")))[0];
  if (termin) {
    zeilen.push({
      id: termin.id,
      ton: "neutral",
      titel: termin.title,
      art: termin.kind,
      text: terminText(t, termin.date, termin.time, sprache),
    });
  }
  return zeilen;
}

export type Gruppe = { art: GesundheitsArt; eintraege: DogHealthRecord[] };

/**
 * Die Einträge für die Liste: "Aktuell" je Art gruppiert (Fristen: früheste zuerst, Termine: nächster
 * zuerst), "Verlauf" alles Übrige, jüngstes zuerst. Leere Gruppen fehlen.
 */
export function gliedere(eintraege: readonly DogHealthRecord[]): { aktuell: Gruppe[]; verlauf: DogHealthRecord[] } {
  const aktuell = ARTEN.map((art) => ({
    art,
    eintraege: eintraege
      .filter((e) => e.isCurrent && e.kind === art)
      .sort((a, b) => (art === GESUNDHEIT_ART.Tierarzttermin ? a.date.localeCompare(b.date) : (a.dueDate ?? "9999").localeCompare(b.dueDate ?? "9999"))),
  })).filter((g) => g.eintraege.length > 0);
  const verlauf = eintraege.filter((e) => !e.isCurrent).sort((a, b) => b.date.localeCompare(a.date));
  return { aktuell, verlauf };
}

// ---- Hinweis auf der Startseite ----

export type Hinweis = { ton: Ton; text: string };

/**
 * Die dezenten Hinweise je Hund auf der Startseite: etwas ist überfällig oder in höchstens
 * 14 Tagen fällig ("Bello: Tollwut gültig bis 03.11."), und/oder ein Tierarzttermin steht in
 * höchstens zwei Tagen an. Sonst nichts - die Startseite bleibt ruhig. Der Stand kommt vom Server,
 * die Tage werden hier noch einmal gegen heute geprüft (der Lesecache kann von gestern sein).
 */
export function hinweiseFuerHund(
  t: Uebersetzer,
  hund: Pick<Dog, "name"> & { gesundheit?: DogGesundheitStand | null },
  heute: string = heuteIso(),
  sprache: Sprache = "de",
): Hinweis[] {
  const stand = hund.gesundheit;
  if (!stand) return [];
  const ergebnis: Hinweis[] = [];

  const faellig = stand.nextDue;
  if (faellig && tageBis(faellig.dueDate, heute) <= HINWEIS_TAGE) {
    const ton: Ton = tageBis(faellig.dueDate, heute) < 0 ? "rot" : "gelb";
    const tagMonat = datumTagMonat(faellig.dueDate, sprache);
    const rest = tageBis(faellig.dueDate, heute);
    const name = hund.name;
    const titel = faellig.title;
    let text: string;
    if (faellig.kind === GESUNDHEIT_ART.Impfung) {
      text = rest < 0
        ? t("{hund}: {titel} seit {datum} abgelaufen", { hund: name, titel, datum: tagMonat })
        : rest === 0
          ? t("{hund}: {titel} läuft heute ab", { hund: name, titel })
          : t("{hund}: {titel} gültig bis {datum}", { hund: name, titel, datum: tagMonat });
    } else {
      text = rest < 0
        ? t("{hund}: {titel} seit {datum} fällig", { hund: name, titel, datum: tagMonat })
        : rest === 0
          ? t("{hund}: {titel} heute fällig", { hund: name, titel })
          : t("{hund}: {titel} am {datum} fällig", { hund: name, titel, datum: tagMonat });
    }
    // Weitere Fristen, die auch Aufmerksamkeit brauchen (überfällig oder in 30 Tagen fällig).
    const weitere = stand.overdue + stand.dueSoon - 1;
    if (weitere > 0) text = weitere === 1 ? t("{text} und 1 weitere", { text }) : t("{text} und {n} weitere", { text, n: weitere });
    ergebnis.push({ ton, text });
  }

  const termin = stand.nextAppointment;
  if (termin) {
    const rest = tageBis(termin.date, heute);
    if (rest >= 0 && rest <= HINWEIS_TERMIN_TAGE) {
      const zeit = uhrzeitKurz(termin.time);
      const name = hund.name;
      const wann = terminTagText(t, termin.date, heute, sprache);
      ergebnis.push({
        ton: "neutral",
        text: zeit === null
          ? t("{hund}: Tierarzttermin {wann}", { hund: name, wann })
          : t("{hund}: Tierarzttermin {wann}, {zeit} Uhr", { hund: name, wann, zeit }),
      });
    }
  }
  return ergebnis;
}

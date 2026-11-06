import type { Sprache } from "@/lib/i18n";
import { plusTage, tageBis } from "@/lib/kalendertag";
import { ortsformat } from "@/lib/ortsformat";
import type { ArbeitseinsatzAuswertung, ArbeitseinsatzKandidat } from "@/lib/types";

/**
 * Rechnen und Prüfen rund um die Arbeitseinsätze des Vereins - als reine Funktionen, damit Liste,
 * Detail, Auswertung und Export dasselbe meinen. Die Rechte und alle Grenzen entscheidet der Server
 * (ClubWorkService); hier steht, wie die Oberfläche Eingaben vor dem Senden prüft und Zahlen zeigt.
 */

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** Kürzeste und längste Dauer in Minuten - für den Einsatz wie für abweichende Minuten einer Person (ClubWorkService). */
export const MIN_MINUTEN = 15;
export const MAX_MINUTEN = 720;

/** So weit im Voraus darf ein Einsatz liegen (ClubWorkService.MaxTageVoraus). */
export const MAX_TAGE_VORAUS = 30;

/** Vorgabe im Formular: zwei Stunden. */
export const STANDARD_MINUTEN = 120;

/** "45 min", "2 h", "2 h 30 min" - Einheiten sind in beiden Sprachen dieselben. */
export function dauerText(minuten: number): string {
  if (minuten < 60) return `${minuten} min`;
  const rest = minuten % 60;
  return rest === 0 ? `${minuten / 60} h` : `${Math.floor(minuten / 60)} h ${rest} min`;
}

/** Stunden mit höchstens zwei Nachkommastellen: 90 -> 1,5. */
export function stundenZahl(minuten: number): number {
  return Math.round((minuten / 60) * 100) / 100;
}

/** Die Stunden einer Summe zur Anzeige: "12,5 h" bzw. "12.5 h". */
export function stundenText(minuten: number, sprache: Sprache): string {
  return `${stundenZahl(minuten).toLocaleString(ortsformat(sprache), { maximumFractionDigits: 2 })} h`;
}

/** Ganze Minuten aus einer Eingabe; null bei allem anderen (leer, Dezimalzahl, Text). */
export function minutenAusEingabe(text: string): number | null {
  const bereinigt = text.trim();
  return /^\d{1,4}$/.test(bereinigt) ? Number(bereinigt) : null;
}

/** Fehlermeldung zu den Minuten, sonst null. */
export function pruefeMinuten(t: Uebersetzer, minuten: number | null): string | null {
  return minuten === null || minuten < MIN_MINUTEN || minuten > MAX_MINUTEN
    ? t("Die Dauer muss zwischen {MinMinuten} und {MaxMinuten} Minuten liegen.", { MinMinuten: MIN_MINUTEN, MaxMinuten: MAX_MINUTEN })
    : null;
}

export type EinsatzEingabe = { datum: string; titel: string; minuten: string };

/**
 * Fehlermeldung zu Datum, Titel und Dauer eines Einsatzes, sonst null. Die Längen der Texte hält
 * maxLength am Eingabefeld; ältere Einsätze, als der Verein aufbewahrt, lehnt der Server mit eigener Meldung ab.
 */
export function pruefeEinsatz(t: Uebersetzer, e: EinsatzEingabe, heute: string): string | null {
  if (!e.datum) return t("Bitte das Datum des Einsatzes angeben.");
  if (tageBis(e.datum, heute) > MAX_TAGE_VORAUS) {
    return t("Ein Arbeitseinsatz lässt sich höchstens {MaxTageVoraus} Tage im Voraus anlegen.", { MaxTageVoraus: MAX_TAGE_VORAUS });
  }
  if (!e.titel.trim()) return t("Bitte einen Titel angeben.");
  return pruefeMinuten(t, minutenAusEingabe(e.minuten));
}

/** Der späteste Tag, den das Datumsfeld zulässt (heute + 30 Tage). */
export function spaetesterTag(heute: string): string {
  return plusTage(heute, MAX_TAGE_VORAUS);
}

/** Ob der Einsatz schon stattfindet oder stattgefunden hat - erst dann lassen sich Helfer:innen abhaken. */
export function abhakbar(datum: string, heute: string): boolean {
  return tageBis(datum, heute) <= 0;
}

function normal(text: string): string {
  return text.toLocaleLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "");
}

/** Die Personen, die zur Suche passen - nur im Browser. Jedes Wort muss im Namen vorkommen (Groß-/Kleinschreibung und Akzente egal); leer heißt: alle. */
export function filtereKandidaten(liste: readonly ArbeitseinsatzKandidat[], suche: string): ArbeitseinsatzKandidat[] {
  const worte = normal(suche).split(/\s+/).filter(Boolean);
  if (worte.length === 0) return [...liste];
  return liste.filter((k) => {
    const heu = normal(`${k.firstName} ${k.lastName}`);
    return worte.every((w) => heu.includes(w));
  });
}

/** Vor- und Nachname zur Anzeige; bei einem ehemaligen Mitglied (leere Namen) der Platzhalter. */
export function helferName(t: Uebersetzer, h: { firstName: string; lastName: string }): string {
  return `${h.firstName} ${h.lastName}`.trim() || t("Ehemaliges Mitglied");
}

// ---------- CSV ----------

/**
 * Ein Feld der CSV-Datei: in Anführungszeichen, wenn es Trenner, Anführungszeichen oder Zeilenumbruch
 * enthält. Beginnt es mit =, +, - oder @, setzt ein Hochkomma davor, damit eine Tabellenkalkulation einen Namen
 * nie als Formel auswertet.
 */
export function csvFeld(wert: string): string {
  const sicher = /^[=+\-@\t\r]/.test(wert) ? `'${wert}` : wert;
  return /[";\n\r]/.test(sicher) ? `"${sicher.replace(/"/g, '""')}"` : sicher;
}

/**
 * Die Jahresauswertung als CSV (Name; Einsätze; Stunden): Semikolon als Trenner und Komma als
 * Dezimalzeichen, wie ein deutsches Excel es erwartet; im englischen Gebietsschema der Punkt.
 * Dazu die Sammelzeile der Ehemaligen und eine Zeile "Gesamt" (Einsätze des Jahres, Stunden aller Helfer:innen).
 * Mit Byte-Order-Mark, damit Umlaute in Excel stimmen.
 */
export function auswertungAlsCsv(t: Uebersetzer, a: ArbeitseinsatzAuswertung, sprache: Sprache): string {
  const stunden = (minuten: number) =>
    stundenZahl(minuten).toLocaleString(ortsformat(sprache), { maximumFractionDigits: 2, useGrouping: false });
  const zeilen = [
    [t("Name"), t("Einsätze"), t("Stunden")],
    ...a.rows.map((r) => [`${r.firstName} ${r.lastName}`.trim(), String(r.eventCount), stunden(r.minutes)]),
    ...(a.former ? [[t("Ehemalige Mitglieder"), String(a.former.eventCount), stunden(a.former.minutes)]] : []),
    [t("Gesamt"), String(a.eventCount), stunden(a.totalMinutes)],
  ];
  return "﻿" + zeilen.map((z) => z.map(csvFeld).join(";")).join("\r\n") + "\r\n";
}

/** "arbeitseinsaetze-2026.csv" - ohne den Vereinsnamen, der Dateinamen mit Sonderzeichen zurückließe. */
export function csvDateiname(jahr: number): string {
  return `arbeitseinsaetze-${jahr}.csv`;
}

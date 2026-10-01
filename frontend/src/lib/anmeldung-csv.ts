import { istDoppelteAnmeldung, pruefeAnmeldung, tagDerAnmeldung, type AnmeldeFehler } from "@/lib/anmeldung";
import { heuteIso } from "@/lib/pruefung";

/**
 * Einlesen der Antworten eines Google-Formulars als CSV (Google Formulare ->
 * Antworten -> In Google Tabellen ansehen -> Datei -> Herunterladen -> CSV).
 *
 * Alles hier sind reine Funktionen: Die Datei wird im Browser gelesen und
 * nichts davon verlässt das Gerät, bevor die Trainer:in "Importieren" tippt.
 *
 * Gelesen wird nach RFC 4180: Felder in Anführungszeichen dürfen Kommas,
 * Zeilenumbrüche und doppelte Anführungszeichen ("") enthalten. Trennzeichen
 * ist Komma oder Semikolon - Google Tabellen schreibt Komma, ein deutsches
 * Excel Semikolon -, erkannt wird es an der Kopfzeile.
 */

/** Mehr Zeilen nimmt der Server je Aufruf nicht an (GroupRegistrationRules.MaxImportZeilen). */
export const MAX_IMPORT_ZEILEN = 500;

/** Größer als das, was 500 Anmeldungen je brauchen: schützt den Browser vor einer versehentlich gewählten Riesendatei. */
export const MAX_DATEIGROESSE = 2_000_000;

/** Zerlegt CSV-Text in Datensätze aus Feldern. Leere Datensätze (nur Trennzeichen oder Zeilenumbruch) bleiben erhalten. */
export function parseCsv(eingabe: string): string[][] {
  const text = eingabe.replace(/^﻿/, "");
  const trenner = erkenneTrennzeichen(text);
  const saetze: string[][] = [];
  let satz: string[] = [];
  let feld = "";
  let inAnfuehrung = false;
  // Ob in diesem Satz schon etwas stand (auch ein leeres "" zählt) - unterscheidet
  // eine echte Leerzeile am Dateiende vom letzten Datensatz ohne Zeilenumbruch.
  let satzBegonnen = false;

  for (let i = 0; i < text.length; i++) {
    const zeichen = text[i];

    if (inAnfuehrung) {
      if (zeichen === '"') {
        if (text[i + 1] === '"') {
          feld += '"';
          i++;
        } else {
          inAnfuehrung = false;
        }
      } else {
        feld += zeichen;
      }
      continue;
    }

    if (zeichen === '"' && feld === "") {
      inAnfuehrung = true;
      satzBegonnen = true;
    } else if (zeichen === trenner) {
      satz.push(feld);
      feld = "";
      satzBegonnen = true;
    } else if (zeichen === "\n" || zeichen === "\r") {
      // \r\n zählt als ein Zeilenumbruch.
      if (zeichen === "\r" && text[i + 1] === "\n") i++;
      if (satzBegonnen || feld !== "") {
        satz.push(feld);
        saetze.push(satz);
      }
      satz = [];
      feld = "";
      satzBegonnen = false;
    } else {
      feld += zeichen;
      satzBegonnen = true;
    }
  }
  if (satzBegonnen || feld !== "") {
    satz.push(feld);
    saetze.push(satz);
  }
  return saetze;
}

/** Komma oder Semikolon - je nachdem, was die erste Zeile (außerhalb von Anführungszeichen) häufiger enthält. */
function erkenneTrennzeichen(text: string): "," | ";" {
  let kommas = 0;
  let semikolons = 0;
  let inAnfuehrung = false;
  for (const zeichen of text) {
    if (zeichen === '"') inAnfuehrung = !inAnfuehrung;
    else if (!inAnfuehrung) {
      if (zeichen === "\n" || zeichen === "\r") break;
      if (zeichen === ",") kommas++;
      else if (zeichen === ";") semikolons++;
    }
  }
  return semikolons > kommas ? ";" : ",";
}

/** Kopfzeile vergleichbar machen: klein, ohne Leerzeichen und Zeichen wie "*" oder ":". */
function schluessel(text: string): string {
  return text.toLowerCase().replace(/[^a-z0-9äöüß]/g, "");
}

export type Spalte = "zeitstempel" | "vorname" | "nachname" | "rufname" | "rasse" | "wurftag" | "telefon";

/** Die Spalten, ohne die ein Import keinen Sinn hat. Der Zeitstempel ist freiwillig. */
export const PFLICHTSPALTEN: Spalte[] = ["vorname", "nachname", "rufname", "rasse", "wurftag", "telefon"];

/** Welche Spalte der Datei welches Feld ist - Index in der Kopfzeile oder fehlend. */
export type Spaltenzuordnung = Partial<Record<Spalte, number>>;

function spalteFuer(kopf: string): Spalte | null {
  const k = schluessel(kopf);
  if (k === "zeitstempel" || k === "timestamp") return "zeitstempel";
  if (k === "vorname") return "vorname";
  if (k === "nachname") return "nachname";
  if (k.startsWith("rufname")) return "rufname";
  // "rasse", "hunderasse" - aber nicht "strasse" aus einer Adressspalte,
  // falls ein Verein sein Formular erweitert.
  if (k.endsWith("rasse") && !k.endsWith("strasse")) return "rasse";
  if (k.includes("wurftag")) return "wurftag";
  if (k.includes("telefon")) return "telefon";
  return null;
}

/** Ordnet die Kopfzeile den Feldern zu; die jeweils erste passende Spalte gewinnt. */
export function ordneSpalten(kopfzeile: string[]): Spaltenzuordnung {
  const zuordnung: Spaltenzuordnung = {};
  kopfzeile.forEach((kopf, index) => {
    const spalte = spalteFuer(kopf);
    if (spalte && zuordnung[spalte] === undefined) zuordnung[spalte] = index;
  });
  return zuordnung;
}

export function fehlendeSpalten(zuordnung: Spaltenzuordnung): Spalte[] {
  return PFLICHTSPALTEN.filter((s) => zuordnung[s] === undefined);
}

function zweistellig(n: number): string {
  return String(n).padStart(2, "0");
}

/** Ob (jahr, monat, tag) ein wirklich existierender Kalendertag ist (kein 31.02.). */
function istKalendertag(jahr: number, monat: number, tag: number): boolean {
  if (jahr < 1900 || jahr > 2100 || monat < 1 || monat > 12 || tag < 1) return false;
  return tag <= new Date(Date.UTC(jahr, monat, 0)).getUTCDate();
}

function alsIso(jahr: number, monat: number, tag: number): string | null {
  return istKalendertag(jahr, monat, tag) ? `${jahr}-${zweistellig(monat)}-${zweistellig(tag)}` : null;
}

/**
 * Liest ein Datum als "JJJJ-MM-TT". Erkannt werden dd.MM.yyyy, d.M.yyyy,
 * yyyy-MM-dd und M/d/yyyy (so schreibt Google Tabellen in englischer
 * Spracheinstellung). Ein angehängter Uhrzeit-Teil wird ignoriert. Null, wenn
 * nichts davon passt oder der Tag nicht existiert.
 */
export function parseDatum(text: string): string | null {
  const t = text.trim();
  let m = /^(\d{1,2})\.(\d{1,2})\.(\d{4})(?:\D.*)?$/.exec(t);
  if (m) return alsIso(Number(m[3]), Number(m[2]), Number(m[1]));
  m = /^(\d{4})-(\d{1,2})-(\d{1,2})(?:\D.*)?$/.exec(t);
  if (m) return alsIso(Number(m[1]), Number(m[2]), Number(m[3]));
  m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\D.*)?$/.exec(t);
  if (m) return alsIso(Number(m[3]), Number(m[1]), Number(m[2]));
  return null;
}

/**
 * Liest den Zeitstempel des Formulars (dasselbe Datum plus Uhrzeit) als
 * Zeitpunkt in ISO-Schreibweise. Die Uhrzeit gilt als Ortszeit des Geräts - so
 * hat Google sie aufgeschrieben. Ohne Uhrzeit: 12 Uhr, damit der Tag in keiner
 * Zeitzone kippt. Null, wenn nichts Lesbares dasteht (der Server nimmt dann den
 * Moment des Imports).
 */
export function parseZeitstempel(text: string): string | null {
  const iso = parseDatum(text);
  if (!iso) return null;
  const [jahr, monat, tag] = iso.split("-").map(Number);
  const uhr = /(\d{1,2}):(\d{2})(?::(\d{2}))?/.exec(text);
  const stunde = uhr ? Number(uhr[1]) : 12;
  const minute = uhr ? Number(uhr[2]) : 0;
  const sekunde = uhr?.[3] ? Number(uhr[3]) : 0;
  if (stunde > 23 || minute > 59 || sekunde > 59) return null;
  const zeitpunkt = new Date(jahr, monat - 1, tag, stunde, minute, sekunde);
  return Number.isNaN(zeitpunkt.getTime()) ? null : zeitpunkt.toISOString();
}

export type AnmeldeZeile = {
  /** Zeilennummer wie in der Tabelle (die Kopfzeile ist Zeile 1). */
  zeile: number;
  vorname: string;
  nachname: string;
  rufname: string;
  rasse: string;
  /** "JJJJ-MM-TT" oder null, wenn leer/unlesbar. */
  wurftag: string | null;
  telefon: string;
  /** Zeitpunkt in ISO-Schreibweise oder null. */
  zeitstempel: string | null;
  fehler: AnmeldeFehler | null;
};

export type CsvLesen =
  | { art: "leer" }
  | { art: "spaltenFehlen"; fehlend: Spalte[] }
  | { art: "ok"; zeilen: AnmeldeZeile[] };

/** Der erste Fehler einer Zeile - dieselben Regeln wie das Formular und der Server (siehe pruefeAnmeldung). */
function pruefeZeile(z: Omit<AnmeldeZeile, "fehler">, wurftagText: string, heute: string): AnmeldeFehler | null {
  // Gegen den Tag der Anmeldung (aus dem Zeitstempel), nicht gegen heute.
  const referenz = z.zeitstempel ? tagDerAnmeldung(z.zeitstempel) : heute;
  return pruefeAnmeldung(
    { ...z, wurftag: z.wurftag ?? "" },
    heute,
    referenz,
    // Es stand etwas im Feld, aber kein lesbares Datum.
    wurftagText !== "" && z.wurftag === null,
  );
}

/**
 * Liest den CSV-Text einer Formular-Antwortliste: Datensätze zerlegen, Spalten
 * über die Überschriften zuordnen, jede Zeile prüfen. Vollständig leere Zeilen
 * werden übergangen.
 */
export function leseAnmeldungen(csv: string, heute: string = heuteIso()): CsvLesen {
  const saetze = parseCsv(csv);
  if (saetze.length === 0) return { art: "leer" };

  const zuordnung = ordneSpalten(saetze[0]);
  const fehlend = fehlendeSpalten(zuordnung);
  if (fehlend.length > 0) return { art: "spaltenFehlen", fehlend };

  const feld = (satz: string[], spalte: Spalte) => {
    const index = zuordnung[spalte];
    return index === undefined ? "" : (satz[index] ?? "").trim();
  };

  const zeilen: AnmeldeZeile[] = [];
  saetze.slice(1).forEach((satz, index) => {
    if (satz.every((f) => f.trim() === "")) return;
    const wurftagText = feld(satz, "wurftag");
    const ohneFehler = {
      zeile: index + 2,
      vorname: feld(satz, "vorname"),
      nachname: feld(satz, "nachname"),
      rufname: feld(satz, "rufname"),
      rasse: feld(satz, "rasse"),
      wurftag: parseDatum(wurftagText),
      telefon: feld(satz, "telefon"),
      zeitstempel: parseZeitstempel(feld(satz, "zeitstempel")),
    };
    zeilen.push({ ...ohneFehler, fehler: pruefeZeile(ohneFehler, wurftagText, heute) });
  });

  return zeilen.length === 0 ? { art: "leer" } : { art: "ok", zeilen };
}

export type ImportVorschau = {
  /** Gültige, noch nicht vorhandene Zeilen - die werden gesendet. */
  neu: AnmeldeZeile[];
  /** Schon in der Gruppe oder weiter oben in der Datei. */
  vorhanden: AnmeldeZeile[];
  fehlerhaft: AnmeldeZeile[];
};

/**
 * Teilt die Zeilen in neu, schon vorhanden und fehlerhaft ein. "Vorhanden"
 * heißt wie auf dem Server: gleiche Nummer (normalisiert) und gleicher Rufname,
 * ohne Zeitgrenze - auch eine zweite Zeile derselben Datei zählt, die erste
 * gewinnt.
 */
export function bildeVorschau(
  zeilen: AnmeldeZeile[],
  bestehende: { phone: string; dogName: string }[],
): ImportVorschau {
  const bekannt = [...bestehende];
  const vorschau: ImportVorschau = { neu: [], vorhanden: [], fehlerhaft: [] };
  for (const z of zeilen) {
    if (z.fehler) {
      vorschau.fehlerhaft.push(z);
      continue;
    }
    const kandidat = { phone: z.telefon, dogName: z.rufname };
    if (bekannt.some((b) => istDoppelteAnmeldung(b, kandidat))) {
      vorschau.vorhanden.push(z);
      continue;
    }
    bekannt.push(kandidat);
    vorschau.neu.push(z);
  }
  return vorschau;
}

/** Die Zeilen in Stücken, die der Server je Aufruf annimmt. */
export function inStuecke<T>(zeilen: T[], groesse: number = MAX_IMPORT_ZEILEN): T[][] {
  const stuecke: T[][] = [];
  for (let i = 0; i < zeilen.length; i += groesse) stuecke.push(zeilen.slice(i, i + groesse));
  return stuecke;
}

import { istGueltigerEinladungscode } from "@/lib/einladung";
import { heuteIso } from "@/lib/pruefung";
import { TEXTLAENGE } from "@/lib/textlaengen";

/**
 * Anmeldung zu einer Gruppe über das Anmeldeformular (Adresse
 * /anmeldung/{code}, dazu der QR-Code auf dem Aushang).
 *
 * Die Prüfungen hier spiegeln Dogity.Application/Community/GroupRegistrationRules.cs:
 * Das Frontend zeigt Fehler vor dem Senden, maßgeblich ist immer der Server.
 * Ändert sich dort eine Grenze, gehört sie hier mit angefasst.
 *
 * Daten sind Kalendertage ("2026-10-03") und werden als Text verglichen - nie
 * über `new Date("2026-10-03")`, das wäre UTC-Mitternacht (siehe pruefung.ts).
 */

/** Der Code hat dieselbe Form wie der Einladungscode eines Vereins (derselbe Generator im Backend). */
export const istGueltigerAnmeldecode = istGueltigerEinladungscode;

/** Die Adresse, die in den QR-Code und in den Link kommt. */
export function anmeldungsUrl(origin: string, code: string): string {
  return `${origin}/anmeldung/${code}`;
}

/** So alt darf der Hund bei der Anmeldung höchstens sein, in Jahren. */
export const MAX_HUNDEALTER_JAHRE = 2;

const TELEFON_MIN_ZEICHEN = 6;
const TELEFON_MIN_ZIFFERN = 6;
export const TELEFON_MAX_ZEICHEN = 30;
// Ziffern, +, Leerzeichen, /, -, ( ) - lose, weil Nummern in jeder Schreibweise aufgeschrieben werden.
const TELEFON_ZEICHEN = /^[0-9+\s/()-]+$/;

function ziffern(text: string): string {
  return text.replace(/\D/g, "");
}

/** Ob die Telefonnummer plausibel eine sein kann (nicht, ob sie existiert). */
export function istGueltigeTelefonnummer(wert: string): boolean {
  const text = wert.trim();
  return (
    text.length >= TELEFON_MIN_ZEICHEN &&
    text.length <= TELEFON_MAX_ZEICHEN &&
    TELEFON_ZEICHEN.test(text) &&
    ziffern(text).length >= TELEFON_MIN_ZIFFERN
  );
}

/**
 * Die Nummer so, dass gleiche Anschlüsse gleich aussehen: nur Ziffern, die
 * deutsche Vorwahl "+49"/"0049" wird zur führenden 0 - wie im Backend.
 */
export function normalisiereTelefon(wert: string): string {
  // "+49 (0) 721 …": Die Null der Inlandswahl fällt bei internationaler Wahl
  // weg und zählt nicht mit - wie im Backend (GroupRegistrationRules).
  const ohneKlammerNull = wert.replace(KLAMMER_NULL, "");
  let nur = ziffern(ohneKlammerNull);
  const international = ohneKlammerNull.trimStart().startsWith("+") || nur.startsWith("00");
  if (international) {
    if (nur.startsWith("00")) nur = nur.slice(2);
    if (nur.startsWith("49")) nur = "0" + nur.slice(2).replace(/^0+/, "");
  }
  return nur;
}

const KLAMMER_NULL = /\(\s*0\s*\)/g;

/**
 * Wählbarer tel:-Link. Bei internationaler Schreibweise bleibt das "+", die
 * eingeklammerte "(0)" und eine Null direkt nach der Landesvorwahl aus DACH
 * fallen weg - sonst wählt das Telefon "+490721…" und landet im Leeren.
 */
export function telefonLink(wert: string): string {
  const ohneKlammerNull = wert.replace(KLAMMER_NULL, "");
  if (!ohneKlammerNull.trimStart().startsWith("+")) return `tel:${ziffern(ohneKlammerNull)}`;
  const nummer = ziffern(ohneKlammerNull).replace(/^(49|43|41)0+/, "$1");
  return `tel:+${nummer}`;
}

/** Derselbe Mensch mit demselben Hund: gleiche (normalisierte) Nummer und gleicher Rufname, Schreibweise egal. */
export function istDoppelteAnmeldung(
  a: { phone: string; dogName: string },
  b: { phone: string; dogName: string },
): boolean {
  return (
    normalisiereTelefon(a.phone) === normalisiereTelefon(b.phone) &&
    a.dogName.trim().toLowerCase() === b.dogName.trim().toLowerCase()
  );
}

/**
 * Welpen kommen frühestens mit 8 Wochen in die Gruppe, meist mit 10-12. Der
 * Vorschlag beim ersten Antippen des Wurftags spart das Zurückblättern vom
 * heutigen Monat aus - geändert werden kann er wie jede Eingabe.
 */
export const WURFTAG_VORSCHLAG_WOCHEN = 8;

/** Heute (ISO-Tag) minus 8 Wochen, als ISO-Tag. Rechnet in UTC, damit Sommerzeit nichts verschiebt. */
export function wurftagVorschlag(heuteIsoTag: string): string {
  const [jahr, monat, tag] = heuteIsoTag.slice(0, 10).split("-").map(Number);
  const datum = new Date(Date.UTC(jahr, monat - 1, tag - WURFTAG_VORSCHLAG_WOCHEN * 7));
  return datum.toISOString().slice(0, 10);
}

/** Ein Kalendertag um ganze Jahre zurück; der 29. Februar wird zum 28. (wie DateOnly.AddYears im Backend). */
export function jahreZurueck(iso: string, jahre: number): string {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  const ziel = jahr - jahre;
  const schaltjahr = (ziel % 4 === 0 && ziel % 100 !== 0) || ziel % 400 === 0;
  const letzterTag = monat === 2 ? (schaltjahr ? 29 : 28) : null;
  const gekappt = letzterTag !== null ? Math.min(tag, letzterTag) : tag;
  const zweistellig = (n: number) => String(n).padStart(2, "0");
  return `${String(ziel).padStart(4, "0")}-${zweistellig(monat)}-${zweistellig(gekappt)}`;
}

export type WurftagFehler = "fehlt" | "zukunft" | "zuAlt";

/**
 * Prüft den Wurftag gegen den Tag der Anmeldung. `referenztag` ist heute beim
 * Formular und der Tag des Zeitstempels beim Import - sonst ließe sich eine
 * Anmeldung von damals nie mehr übernehmen.
 */
export function wurftagFehler(
  wurftag: string,
  heute: string = heuteIso(),
  referenztag: string = heute,
): WurftagFehler | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(wurftag)) return "fehlt";
  if (wurftag > heute) return "zukunft";
  if (wurftag < jahreZurueck(referenztag, MAX_HUNDEALTER_JAHRE)) return "zuAlt";
  return null;
}

/** Was an einer Anmeldung nicht stimmt - die Oberfläche macht daraus den (übersetzten) Satz, siehe anmeldung-texte.ts. */
export type AnmeldeFehler =
  | "vorname"
  | "nachname"
  | "rufname"
  | "rasse"
  | "wurftagFehlt"
  | "wurftagFormat"
  | "wurftagZukunft"
  | "wurftagAlt"
  | "telefon"
  | "zuLang";

export type AnmeldeEingabe = {
  vorname: string;
  nachname: string;
  rufname: string;
  rasse: string;
  /** "JJJJ-MM-TT" oder "" (leer bzw. nicht lesbar). */
  wurftag: string;
  telefon: string;
};

/**
 * Der erste Fehler einer Anmeldung, sonst null - dieselben Regeln wie auf dem
 * Server. Eine Stelle für das öffentliche Formular, das Eintragen von Hand und
 * den Import.
 *
 * @param wurftagUnlesbar nur für den Import: Es stand etwas im Wurftag-Feld,
 *   aber kein Datum - das ist ein anderer Fehler als "leer".
 */
export function pruefeAnmeldung(
  e: AnmeldeEingabe,
  heute: string = heuteIso(),
  referenztag: string = heute,
  wurftagUnlesbar = false,
): AnmeldeFehler | null {
  const vorname = e.vorname.trim();
  const nachname = e.nachname.trim();
  const rufname = e.rufname.trim();
  const rasse = e.rasse.trim();
  const telefon = e.telefon.trim();

  if (vorname === "") return "vorname";
  if (nachname === "") return "nachname";
  if (rufname === "") return "rufname";
  if (rasse === "") return "rasse";
  if (e.wurftag === "") return wurftagUnlesbar ? "wurftagFormat" : "wurftagFehlt";
  if (
    vorname.length > TEXTLAENGE.anmeldeName ||
    nachname.length > TEXTLAENGE.anmeldeName ||
    rufname.length > TEXTLAENGE.anmeldeName ||
    rasse.length > TEXTLAENGE.anmeldeRasse
  )
    return "zuLang";
  if (!istGueltigeTelefonnummer(telefon)) return telefon.length > TELEFON_MAX_ZEICHEN ? "zuLang" : "telefon";
  const wurf = wurftagFehler(e.wurftag, heute, referenztag);
  if (wurf === "fehlt") return "wurftagFormat";
  if (wurf === "zukunft") return "wurftagZukunft";
  if (wurf === "zuAlt") return "wurftagAlt";
  return null;
}

/** Der Kalendertag eines Zeitstempels (Anmeldezeitpunkt) auf dem Gerät. */
export function tagDerAnmeldung(zeitpunkt: string): string {
  return heuteIso(new Date(zeitpunkt).getTime());
}

/** "Sa., 03.10." bzw. "Sat, 03/10" - kurz genug für einen Chip. */
export function tagKurz(iso: string, sprache: "de" | "en" = "de"): string {
  const [jahr, monat, tag] = iso.slice(0, 10).split("-").map(Number);
  return new Date(jahr, monat - 1, tag).toLocaleDateString(sprache === "en" ? "en-GB" : "de-DE", {
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
  });
}

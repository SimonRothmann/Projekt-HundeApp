import type { GroupTrainingSession } from "@/lib/types";

/**
 * Ein Gruppentraining als Kalendereintrag (iCalendar, RFC 5545).
 *
 * Bewusst von Hand gebaut statt mit einer Bibliothek: Gebraucht wird genau ein
 * Termin ohne Wiederholung, ohne Zeitzonenbeschreibung (Start und Ende in UTC)
 * und ohne Erinnerung. Das sind gut 20 Zeilen Format, die hier an einer Stelle
 * stehen und getestet sind. Es gibt auch keinen Abo-Feed: Die Datei ist eine
 * Momentaufnahme; wird der Termin später verschoben, bleibt der Eintrag im
 * Kalender stehen, bis man ihn selbst ändert.
 *
 * Die Datei enthält keine Personendaten - weder Namen der Trainer:innen noch
 * die Zu- und Absagen -, nur Gruppe, Zeit, Ort und Dauer.
 */

export type KalenderTermin = {
  /** Wird zur Kennung "{id}@dogity": derselbe Termin ersetzt sich beim erneuten Import. */
  id: string;
  /** Start als ISO-Zeitpunkt. */
  startsAt: string;
  durationMinutes: number;
  /** "{Gruppe}: Gruppentraining" - bereits übersetzt vom Aufrufer. */
  titel: string;
  ort: string | null;
  beschreibung: string;
};

const CRLF = "\r\n";
/** RFC 5545, 3.1: Zeilen sind höchstens 75 Oktette lang (ohne den Zeilenumbruch). */
const MAX_OKTETTE = 75;

const utf8 = new TextEncoder();
const oktette = (text: string) => utf8.encode(text).length;

/**
 * Maskiert einen TEXT-Wert (RFC 5545, 3.3.11): Backslash, Semikolon und Komma
 * bekommen einen Backslash, Zeilenumbrüche werden zu "\n".
 */
export function maskiereIcsText(text: string): string {
  return text
    .replace(/\\/g, "\\\\")
    .replace(/;/g, "\\;")
    .replace(/,/g, "\\,")
    .replace(/\r\n|\r|\n/g, "\\n");
}

/**
 * Bricht eine Zeile nach 75 Oktetten um (RFC 5545, 3.1): Die Fortsetzung
 * beginnt mit einem Leerzeichen, das selbst zur Länge zählt.
 *
 * Gemessen wird in UTF-8-Oktetten, nicht in Zeichen - ein Umlaut sind zwei,
 * ein Emoji vier. Geschnitten wird nie mitten in einem Zeichen, sonst käme
 * beim Zusammensetzen Unsinn heraus; dafür läuft die Schleife über ganze
 * Codepoints (for…of), nicht über UTF-16-Einheiten.
 */
export function faltIcsZeile(zeile: string): string {
  if (oktette(zeile) <= MAX_OKTETTE) return zeile;

  const teile: string[] = [];
  let aktuell = "";
  let laenge = 0;
  for (const zeichen of zeile) {
    const breite = oktette(zeichen);
    // Fortsetzungszeilen haben ein führendes Leerzeichen, also 74 Oktette Platz.
    const platz = teile.length === 0 ? MAX_OKTETTE : MAX_OKTETTE - 1;
    if (laenge + breite > platz) {
      teile.push(aktuell);
      aktuell = "";
      laenge = 0;
    }
    aktuell += zeichen;
    laenge += breite;
  }
  teile.push(aktuell);
  return teile.join(CRLF + " ");
}

/** "20261005T163000Z" - der Zeitpunkt in UTC, wie ihn DTSTART/DTEND verlangen. */
export function icsZeitpunkt(zeit: Date): string {
  return zeit.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}

/**
 * Baut die Datei. `jetzt` ist nur für DTSTAMP (Zeitpunkt der Erzeugung) da und
 * macht den Aufruf in Tests reproduzierbar.
 */
export function erzeugeIcs(termin: KalenderTermin, jetzt: Date = new Date()): string {
  const start = new Date(termin.startsAt);
  const ende = new Date(start.getTime() + Math.max(1, termin.durationMinutes) * 60_000);

  const zeilen = [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Dogity//Gruppentraining//DE",
    "CALSCALE:GREGORIAN",
    "METHOD:PUBLISH",
    "BEGIN:VEVENT",
    `UID:${termin.id}@dogity`,
    `DTSTAMP:${icsZeitpunkt(jetzt)}`,
    `DTSTART:${icsZeitpunkt(start)}`,
    `DTEND:${icsZeitpunkt(ende)}`,
    `SUMMARY:${maskiereIcsText(termin.titel)}`,
    ...(termin.ort ? [`LOCATION:${maskiereIcsText(termin.ort)}`] : []),
    `DESCRIPTION:${maskiereIcsText(termin.beschreibung)}`,
    "END:VEVENT",
    "END:VCALENDAR",
  ];
  // Auch die letzte Zeile endet mit CRLF: Das Format kennt keine Zeile ohne.
  return zeilen.map(faltIcsZeile).join(CRLF) + CRLF;
}

/** Nur geplante Termine, die noch nicht begonnen haben, gehören in den Kalender. */
export function kannInKalender(termin: Pick<GroupTrainingSession, "status" | "startsAt">, jetzt: number = Date.now()): boolean {
  return termin.status === 0 && new Date(termin.startsAt).getTime() > jetzt;
}

/** Dateiname mit Datum, nur aus sicheren Zeichen ("Gruppentraining-2026-10-05.ics"). */
export function icsDateiname(startsAt: string): string {
  const tag = new Date(startsAt).toISOString().slice(0, 10);
  return `Gruppentraining-${tag}.ics`;
}

export type KalenderErgebnis = "geteilt" | "geladen" | "abgebrochen";

/**
 * Übergibt die Datei dem Gerät: Auf dem Telefon über das Teilen-Blatt (dort
 * steht "Kalender" zur Wahl), sonst als Download. Das Teilen-Blatt ist der
 * bessere Weg, weil ein heruntergeladenes .ics auf dem Handy in den Downloads
 * liegt und erst gesucht werden will.
 */
export async function inKalenderUebergeben(termin: KalenderTermin): Promise<KalenderErgebnis> {
  const inhalt = erzeugeIcs(termin);
  const name = icsDateiname(termin.startsAt);
  const datei = new File([inhalt], name, { type: "text/calendar" });

  if (typeof navigator !== "undefined" && typeof navigator.canShare === "function" && navigator.canShare({ files: [datei] })) {
    try {
      await navigator.share({ files: [datei], title: termin.titel });
      return "geteilt";
    } catch (err) {
      // Wer das Blatt wieder wegwischt, hat nichts falsch gemacht.
      if (err instanceof DOMException && err.name === "AbortError") return "abgebrochen";
      // Jeder andere Fehler: auf den Download zurückfallen, statt nichts zu tun.
    }
  }

  const adresse = URL.createObjectURL(datei);
  try {
    const anker = document.createElement("a");
    anker.href = adresse;
    anker.download = name;
    document.body.appendChild(anker);
    anker.click();
    anker.remove();
  } finally {
    // Nicht sofort freigeben: Manche Browser lesen die Adresse erst nach dem Klick.
    setTimeout(() => URL.revokeObjectURL(adresse), 10_000);
  }
  return "geladen";
}

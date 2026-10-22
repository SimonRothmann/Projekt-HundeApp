import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Einstellungen der Erinnerungen als reine Funktionen: die Wochentage der
 * Zusammenfassung für Trainer:innen als Bitmaske und die wählbaren Uhrzeiten.
 *
 * Die Regeln stehen auf dem Server (Erinnerungsregeln.cs) und gelten dort; hier
 * sind sie nachgebaut, damit die Oberfläche gar nicht erst etwas anbietet, das
 * abgelehnt würde - eine Auswahl ohne Tag oder eine Uhrzeit in der Ruhezeit.
 */

/** Montag = 1, Dienstag = 2, ... Sonntag = 64: das Bit ist der Abstand zum Montag. */
export const WOCHENTAGE = [
  { bit: 1, kurz: uebersetzbar("Mo"), lang: uebersetzbar("Montag") },
  { bit: 2, kurz: uebersetzbar("Di"), lang: uebersetzbar("Dienstag") },
  { bit: 4, kurz: uebersetzbar("Mi"), lang: uebersetzbar("Mittwoch") },
  { bit: 8, kurz: uebersetzbar("Do"), lang: uebersetzbar("Donnerstag") },
  { bit: 16, kurz: uebersetzbar("Fr"), lang: uebersetzbar("Freitag") },
  { bit: 32, kurz: uebersetzbar("Sa"), lang: uebersetzbar("Samstag") },
  { bit: 64, kurz: uebersetzbar("So"), lang: uebersetzbar("Sonntag") },
] as const;

/** Vorgabe der Zusammenfassung: Sonntag, 18 Uhr (wie der Server sie ohne Einstellung annimmt). */
export const VORGABE_TAGE = 64;
export const VORGABE_STUNDE = 18;

/** Wählbare Uhrzeiten: volle Stunden von 8 bis 21 Uhr. Außerhalb davon wird nichts versendet. */
export const ERSTE_STUNDE = 8;
export const LETZTE_STUNDE = 21;
export const STUNDEN: readonly number[] = Array.from({ length: LETZTE_STUNDE - ERSTE_STUNDE + 1 }, (_, i) => ERSTE_STUNDE + i);

const ALLE_TAGE = 127;

export function tagGewaehlt(maske: number, bit: number): boolean {
  return (maske & bit) !== 0;
}

/** Wie viele Tage gewählt sind. */
export function anzahlTage(maske: number): number {
  return WOCHENTAGE.filter((t) => tagGewaehlt(maske, t.bit)).length;
}

/**
 * Schaltet einen Wochentag um. Der letzte gewählte Tag lässt sich nicht
 * abwählen: Eine Auswahl ohne Tag hieße "nie", und das ist der Schalter darüber.
 * Bleibt die Maske dann unverändert, ändert sich nichts und es wird nichts gesendet.
 */
export function tagUmschalten(maske: number, bit: number): number {
  const neu = maske ^ bit;
  return neu >= 1 && neu <= ALLE_TAGE ? neu : maske;
}

export function stundeText(stunde: number): string {
  return `${String(stunde).padStart(2, "0")}:00`;
}

/** Ob die Uhrzeit eine wählbare ist; sonst (kann vom Server nicht kommen) gilt die Vorgabe. */
export function gueltigeStunde(stunde: number): number {
  return Number.isInteger(stunde) && stunde >= ERSTE_STUNDE && stunde <= LETZTE_STUNDE ? stunde : VORGABE_STUNDE;
}

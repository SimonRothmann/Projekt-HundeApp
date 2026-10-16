import { istSprache, type Sprache } from "./sprachen";

/**
 * Die Sprachwahl ohne Konto.
 *
 * Wer nicht angemeldet ist, hat keine Kontoeinstellung (preferences.locale)
 * und konnte bisher nur die Sprache seines Geräts bekommen. Diese Wahl liegt
 * deshalb im Browser. Sie steht in der Reihenfolge NACH dem Konto und VOR dem
 * Gerät (siehe SprachProvider): Wer angemeldet ist und im Profil eine Sprache
 * gewählt hat, behält sie, egal was am Gerät zuletzt angeklickt wurde.
 *
 * Der Zugriff auf den Speicher ist als Parameter herausgezogen, damit er ohne
 * Browser prüfbar ist - und weil er scheitern darf (privater Modus, gesperrter
 * Speicher): Dann gilt die Wahl nur bis zum Schließen der Seite.
 */
export const SPRACH_SPEICHER = "dogity.sprache";

/** Ereignis, mit dem eine Wahl innerhalb derselben Seite sofort ankommt. */
export const SPRACH_EREIGNIS = "dogity:sprache";

type Speicher = Pick<Storage, "getItem" | "setItem" | "removeItem">;

function browserSpeicher(): Speicher | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    // Schon der Zugriff auf localStorage kann werfen (gesperrte Website-Daten).
    return null;
  }
}

/** Die gespeicherte Wahl - oder null, wenn es keine (gültige) gibt. */
export function leseLokaleSprache(speicher: Speicher | null = browserSpeicher()): Sprache | null {
  if (!speicher) return null;
  try {
    const wert = speicher.getItem(SPRACH_SPEICHER);
    return istSprache(wert) ? wert : null;
  } catch {
    return null;
  }
}

/**
 * Merkt sich die Wahl und sagt es der Seite, damit sie ohne Neuladen wirkt.
 * Gelingt das Speichern nicht, wirkt die Wahl trotzdem für diese Sitzung der
 * Seite - das Ereignis trägt den Wert selbst.
 */
export function speichereLokaleSprache(sprache: Sprache, speicher: Speicher | null = browserSpeicher()): void {
  try {
    speicher?.setItem(SPRACH_SPEICHER, sprache);
  } catch {
    // Siehe oben: Ohne Speicher gilt die Wahl nur bis zum Neuladen.
  }
  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent<Sprache>(SPRACH_EREIGNIS, { detail: sprache }));
  }
}

/**
 * Welches Trainer-Feedback auf diesem Gerät schon gesehen wurde.
 *
 * Die "Neu"-Markierung am Tagebucheintrag braucht ein Gedächtnis, und der
 * Server hat keins dafür: Er weiß, WANN das Feedback kam, nicht, ob die
 * Besitzerin es schon aufgeschlagen hat. Ein Merker pro Gerät genügt - wer ein
 * zweites Gerät nutzt, sieht dort einmal mehr "Neu", und das ist harmlos.
 *
 * Gemerkt wird je Feedback nur eine Kennung aus Eintrags-Id und Zeitpunkt des
 * Feedbacks. Der Zeitpunkt gehört dazu, damit überarbeitetes Feedback wieder
 * als neu gilt. Es steht kein Text und kein Name im Speicher, und die Liste ist
 * begrenzt, damit sie nicht ewig wächst. Beim Abmelden wird sie gelöscht (siehe
 * auth-context): Sie gehört zur Person, nicht zum Gerät.
 */

export const GESEHEN_SCHLUESSEL = "dogity_feedback_gesehen";

/** Höchstens so viele Kennungen; die ältesten fallen zuerst heraus. */
export const GESEHEN_MAX = 200;

/** Kennung eines Feedbacks: ändert sich, sobald die Trainer:in neu schreibt. */
export function feedbackKennung(eintragId: string, feedbackAt: string): string {
  return `${eintragId}|${feedbackAt}`;
}

/** Eine Eintrags-Id in GUID-Form (8-4-4-4-12 Hexziffern), sonst null. */
const GUID_FORM = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function eintragIdAus(wert: string | null | undefined): string | null {
  return typeof wert === "string" && GUID_FORM.test(wert) ? wert.toLowerCase() : null;
}

/** Nimmt eine Kennung hinten auf (neueste zuletzt), ohne Doppelte, und kappt auf GESEHEN_MAX. */
export function mitKennung(liste: readonly string[], kennung: string, max: number = GESEHEN_MAX): string[] {
  const ohne = liste.filter((k) => k !== kennung);
  ohne.push(kennung);
  return ohne.length > max ? ohne.slice(ohne.length - max) : ohne;
}

/** Ob ein Feedback noch nicht gesehen wurde. Ohne Feedback gibt es nichts zu sehen. */
export function istNeuesFeedback(
  eintrag: { id: string; trainerFeedback: string | null; feedbackAt: string | null },
  gesehen: ReadonlySet<string> | readonly string[],
): boolean {
  if (!eintrag.trainerFeedback || !eintrag.feedbackAt) return false;
  const kennung = feedbackKennung(eintrag.id, eintrag.feedbackAt);
  return Array.isArray(gesehen) ? !gesehen.includes(kennung) : !(gesehen as ReadonlySet<string>).has(kennung);
}

/** Liest die Liste; bei fehlendem oder unbrauchbarem Speicher leer. */
export function leseGesehen(): string[] {
  try {
    const roh = window.localStorage.getItem(GESEHEN_SCHLUESSEL);
    if (!roh) return [];
    const wert: unknown = JSON.parse(roh);
    if (!Array.isArray(wert)) return [];
    // Nur Zeichenketten, und nie mehr als erlaubt - auch wenn jemand im
    // Speicher herumgeschrieben hat.
    return wert.filter((k): k is string => typeof k === "string").slice(-GESEHEN_MAX);
  } catch {
    return [];
  }
}

/** Merkt sich das Feedback als gesehen. Ohne Speicher (privates Fenster) bleibt es beim "Neu". */
export function markiereGesehen(eintragId: string, feedbackAt: string): void {
  try {
    const kennung = feedbackKennung(eintragId, feedbackAt);
    const aktuell = leseGesehen();
    // Schon drin und zuletzt: nichts zu schreiben.
    if (aktuell[aktuell.length - 1] === kennung) return;
    window.localStorage.setItem(GESEHEN_SCHLUESSEL, JSON.stringify(mitKennung(aktuell, kennung)));
  } catch {
    // siehe oben - die Markierung ist nur ein Komfort.
  }
}

export function loescheGesehen(): void {
  try {
    window.localStorage.removeItem(GESEHEN_SCHLUESSEL);
  } catch {
    // Nichts zu löschen, wenn es keinen Speicher gibt.
  }
}

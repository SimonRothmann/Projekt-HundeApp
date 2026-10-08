import { useSyncExternalStore } from "react";
import { AKTUELLE_VERSION } from "@/lib/versionshinweise";

/**
 * Merkt sich je Gerät, welche Fassung schon zur Kenntnis genommen wurde - der
 * Punkt am Reiter "Profil" hängt daran (früher die Karte "Neu in Dogity" auf
 * der Startseite).
 *
 * Bewusst localStorage und keine Servereinstellung: Die Frage "hast du das
 * gesehen?" ist eine des Geräts, nicht des Kontos - und eine Tabellenspalte
 * samt Migration und Endpunkt wäre für einen weggeklickten Hinweis ein hoher
 * Preis. Jeder Zugriff ist abgesichert, weil das Lesen in privaten Fenstern
 * und bei gesperrten Website-Daten nicht nur leer zurückkommt, sondern
 * wirft - eine ungefangene Ausnahme hier würde die ganze untere Leiste
 * mitreißen.
 */
const SPEICHER_SCHLUESSEL = "dogity.neuerungen.gesehen";

/** Meldet dem Punkt in derselben Seite, dass sich der Stand geändert hat ("storage" kommt nur aus anderen Tabs). */
const EREIGNIS = "dogity:neuerungen-gelesen";

export function leseGelesen(): string | null {
  try {
    return window.localStorage.getItem(SPEICHER_SCHLUESSEL);
  } catch {
    return null;
  }
}

/** Vermerkt eine Fassung als gelesen und sagt es allen Anzeigen auf der Seite. */
export function merkeGelesen(version: string = AKTUELLE_VERSION): void {
  try {
    window.localStorage.setItem(SPEICHER_SCHLUESSEL, version);
  } catch {
    // Ohne Speicher erscheint der Punkt beim nächsten Besuch erneut.
    // Lästig, aber harmlos - und allemal besser als ein Absturz.
  }
  try {
    window.dispatchEvent(new Event(EREIGNIS));
  } catch {
    // Nur die Anzeige zieht dann erst beim nächsten Rendern nach.
  }
}

/**
 * Die ganze Regel an einer Stelle - und damit prüfbar, ohne einen Browser
 * zu starten.
 *
 * Wer gerade erst anfängt (Erststart), für den ist alles neu: Ein Hinweis
 * "es gibt Neuerungen" erklärt nichts und lenkt von den ersten Schritten ab.
 */
export function sollHinweisZeigen(
  gesehen: string | null,
  aktuell: string,
  erststartLaeuft: boolean,
): boolean {
  if (gesehen === aktuell) return false;
  if (erststartLaeuft) return false;
  return true;
}

/**
 * Im Erststart still als gelesen vermerken, damit der Punkt auch danach nicht
 * nachträglich auftaucht.
 */
export function merkeGelesenImErststart(erststartLaeuft: boolean): void {
  if (erststartLaeuft && leseGelesen() !== AKTUELLE_VERSION) merkeGelesen();
}

function abonnieren(aenderung: () => void): () => void {
  window.addEventListener(EREIGNIS, aenderung);
  window.addEventListener("storage", aenderung);
  return () => {
    window.removeEventListener(EREIGNIS, aenderung);
    window.removeEventListener("storage", aenderung);
  };
}

/**
 * Ob die laufende Fassung noch ungelesen ist.
 *
 * Auf dem Server und beim ersten Rendern im Browser "nein" (useSyncExternalStore
 * nimmt dort den dritten Wert): localStorage gibt es beim Vorab-Rendern nicht,
 * und ein anderes Server-HTML als im Browser wäre eine Hydration-Abweichung.
 */
export function useNeuerungenUngelesen(): boolean {
  return useSyncExternalStore(
    abonnieren,
    () => sollHinweisZeigen(leseGelesen(), AKTUELLE_VERSION, false),
    () => false,
  );
}

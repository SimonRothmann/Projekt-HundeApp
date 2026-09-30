import { vergebeKuerzel } from "@/lib/public-catalog";
import type { Regulation, Sport } from "@/lib/types";

/**
 * Gezielter Start von einer öffentlichen Prüfungsordnungs-Seite.
 *
 * Wer dort auf "Kostenlos starten" tippt, landet mit dem Kürzel der Prüfung
 * (/register?po=ibgh1) bei der Registrierung. Das Kürzel wird nach dem
 * Anlegen des Kontos auf dem Gerät gemerkt und beim ersten Ziel
 * vorausgewählt - aktiviert wird dadurch nichts, es ist nur eine Vorauswahl.
 *
 * Gemerkt wird ausschließlich das Kürzel, eine Zeichenkette aus dem
 * öffentlichen Katalog. Nichts Personenbezogenes, und es verlässt das Gerät
 * nicht.
 */

export const START_PO_SCHLUESSEL = "dogity_start_po";

/** Kleinbuchstaben, Ziffern und Bindestriche - so, wie slugify() sie erzeugt. */
const SLUG_FORM = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const SLUG_MAX = 80;

export function istGueltigerPoSlug(wert: string | null | undefined): wert is string {
  return typeof wert === "string" && wert.length <= SLUG_MAX && SLUG_FORM.test(wert);
}

/** Das Kürzel aus einem Parameter, oder null, wenn es fehlt oder unbrauchbar ist. */
export function poSlugAus(wert: string | null | undefined): string | null {
  return istGueltigerPoSlug(wert) ? wert : null;
}

export function merkeStartPo(slug: string): void {
  if (!istGueltigerPoSlug(slug)) return;
  try {
    window.localStorage.setItem(START_PO_SCHLUESSEL, slug);
  } catch {
    // Ohne Speicher (privates Fenster, gesperrte Daten) bleibt es eben bei
    // der normalen Anlage des Ziels - die Vorauswahl ist nur eine Hilfe.
  }
}

export function leseStartPo(): string | null {
  try {
    return poSlugAus(window.localStorage.getItem(START_PO_SCHLUESSEL));
  } catch {
    return null;
  }
}

export function loescheStartPo(): void {
  try {
    window.localStorage.removeItem(START_PO_SCHLUESSEL);
  } catch {
    // siehe merkeStartPo
  }
}

export type SportMitOrdnungen = { sport: Sport; regulations: Regulation[] };

export type StartPoTreffer = { sportId: string; regulationId: string };

/**
 * Sucht die Sportart und Prüfungsordnung zu einem Kürzel.
 *
 * Die Kürzel vergibt der öffentliche Katalog (lib/public-catalog.ts,
 * getCatalog): aus dem Namen, und bei einer Kollision bekommt die zweite die
 * Id angehängt. Damit hier dieselben herauskommen, läuft die Zuteilung über
 * dieselbe Funktion (vergebeKuerzel) und in derselben Reihenfolge - über ALLE
 * Sportarten, nicht nur die gesuchte, sonst stimmten die Kollisionsfälle nicht.
 *
 * Vereinseigene Sportarten zählen nicht: Der öffentliche Katalog kennt sie
 * nicht, ein Kürzel kann also nie auf sie zeigen.
 */
export function findeStartPo(slug: string, katalog: SportMitOrdnungen[]): StartPoTreffer | null {
  const vergeben = new Set<string>();
  for (const { sport, regulations } of katalog) {
    if (sport.clubId !== null) continue;
    for (const regulation of regulations) {
      if (vergebeKuerzel(regulation, vergeben) === slug) return { sportId: sport.id, regulationId: regulation.id };
    }
  }
  return null;
}

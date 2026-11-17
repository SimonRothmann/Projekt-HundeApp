import type { Dog } from "@/lib/types";

/**
 * Die Hundeliste als Gesamt- und Archivansicht: Der Reiter "Hunde" führt sonst
 * immer gleich auf eine Hundeseite. Erreichbar über "Archivierte Hunde" im
 * ⋯-Fenster der Hundeseite.
 */
export const HUNDELISTE_IMMER = "/dogs?liste=1";

/**
 * ?from= der Hundeseite, wenn der Reiter "Hunde" sie geöffnet hat: Dann ist sie
 * der Startpunkt des Reiters und hat wie eine Hauptseite kein "Zurück"
 * (siehe istHundeStartseite in zurueck.ts).
 */
export const REITER_HERKUNFT = "/dogs";

/** Die Hundeseite als Startpunkt des Reiters "Hunde". */
export function hundeseiteVomReiter(hundId: string): string {
  return `/dogs/${hundId}?from=${encodeURIComponent(REITER_HERKUNFT)}`;
}

const ZULETZT_SCHLUESSEL = "dogity-zuletzt-angesehener-hund";

/** Der Hund, dessen Seite zuletzt offen war (nur ein Komfort, nie nötig). */
export function leseZuletztHund(): string | null {
  try {
    return window.localStorage.getItem(ZULETZT_SCHLUESSEL);
  } catch {
    return null;
  }
}

export function merkeZuletztHund(hundId: string): void {
  try {
    window.localStorage.setItem(ZULETZT_SCHLUESSEL, hundId);
  } catch {
    // Ohne Speicher führt der Reiter wieder zum ersten Hund - kein Schaden.
  }
}

/**
 * Wohin der Reiter "Hunde" führt: auf die Seite des zuletzt angesehenen Hundes,
 * sofern er noch aktiv in der eigenen Liste steht (Hund gelöscht, archiviert oder
 * ein anderes Konto am selben Gerät), sonst auf den ersten aktiven Hund. Null,
 * wenn es keinen aktiven Hund gibt oder die Liste ausdrücklich verlangt ist
 * (?liste=1) - dann bleibt die Seite mit Liste bzw. Leerzustand.
 *
 * Wartende Einladungen halten nicht mehr auf: Sie stehen oben auf der Hundeseite.
 */
export function direktZumHund(
  dogs: readonly Dog[],
  { zuletzt, listeGewuenscht }: { zuletzt: string | null; listeGewuenscht: boolean },
): string | null {
  if (listeGewuenscht) return null;
  const aktive = dogs.filter((d) => !d.archivedAt);
  if (aktive.length === 0) return null;
  return aktive.find((d) => d.id === zuletzt)?.id ?? aktive[0].id;
}

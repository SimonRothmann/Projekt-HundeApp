import { useSyncExternalStore } from "react";

/**
 * Ob gerade eine GPS-Aufzeichnung im Vollbild läuft (AufzeichnungVollbild).
 *
 * Der Toaster liest das und zeigt Meldungen dann oben statt unten: Unten liegen
 * Marker und der Halte-Knopf zum Beenden, und eine Meldung darüber fing die
 * nächste Berührung ab (Rückmeldung vom Fährtelegen: Beenden verzögerte sich).
 * Ein Zähler statt eines Schalters, damit ein kurz doppelt eingehängtes
 * Vollbild (Strict Mode) beim Aushängen nicht zu früh "aus" meldet.
 */
let offen = 0;
const hoerer = new Set<() => void>();

export function aufzeichnungGeoeffnet(): () => void {
  offen++;
  hoerer.forEach((h) => h());
  return () => {
    offen--;
    hoerer.forEach((h) => h());
  };
}

export function istAufzeichnungOffen(): boolean {
  return offen > 0;
}

export function useAufzeichnungOffen(): boolean {
  return useSyncExternalStore(
    (h) => {
      hoerer.add(h);
      return () => hoerer.delete(h);
    },
    istAufzeichnungOffen,
    () => false,
  );
}

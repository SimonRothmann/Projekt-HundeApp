"use client";

import { useEffect, useState } from "react";

/** Ab dieser Höhe (px), die dem sichtbaren Bereich fehlt, gilt die Bildschirmtastatur als offen. */
const TASTATUR_AB = 100;

export type TastaturMass = {
  /** Abstand vom unteren Rand der Seite zum sichtbaren Bereich; 0 ohne Tastatur. */
  unten: number;
  /** Höhe des sichtbaren Bereichs bei offener Tastatur; sonst null. */
  hoehe: number | null;
};

/**
 * Wie viel von der Seite die Bildschirmtastatur verdeckt.
 *
 * Safari auf dem iPhone verkleinert beim Einblenden der Tastatur nur den
 * sichtbaren Bereich (visualViewport), nicht die Seite: Ein unten verankertes
 * Fenster bliebe samt "Speichern" hinter der Tastatur. Mit diesem Maß rückt es
 * über die Tastatur und schrumpft auf das, was zu sehen ist. Anderswo (Desktop,
 * Android mit angepasster Fenstergröße) bleibt es bei 0 / null, und das Fenster
 * verhält sich wie jedes andere.
 */
export function useTastatur(aktiv: boolean): TastaturMass {
  const [mass, setMass] = useState<TastaturMass>({ unten: 0, hoehe: null });

  useEffect(() => {
    const sicht = typeof window === "undefined" ? null : window.visualViewport;
    if (!aktiv || !sicht) return;
    const aktualisieren = () => {
      const verdeckt = Math.max(0, window.innerHeight - sicht.height - sicht.offsetTop);
      setMass((vorher) => {
        const neu: TastaturMass =
          verdeckt >= TASTATUR_AB ? { unten: Math.round(verdeckt), hoehe: Math.round(sicht.height) } : { unten: 0, hoehe: null };
        return vorher.unten === neu.unten && vorher.hoehe === neu.hoehe ? vorher : neu;
      });
    };
    sicht.addEventListener("resize", aktualisieren);
    sicht.addEventListener("scroll", aktualisieren);
    return () => {
      sicht.removeEventListener("resize", aktualisieren);
      sicht.removeEventListener("scroll", aktualisieren);
    };
  }, [aktiv]);

  return aktiv ? mass : { unten: 0, hoehe: null };
}

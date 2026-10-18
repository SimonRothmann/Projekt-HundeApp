"use client";

import { useEffect, useRef } from "react";
import { wetterNachladenIn, zuletztGespeichertUm } from "@/lib/wetter-nachladen";

/**
 * Lädt EINMAL nach, wenn gerade etwas gespeichert wurde und das Wetter noch
 * fehlt (siehe wetter-nachladen.ts). `schluessel` benennt, WAS fehlt (z. B. die
 * Ids): Ändert er sich, gilt es als neuer Anlass; bleibt er gleich, lädt die Seite
 * nicht erneut - auch dann nicht, wenn das Wetter nach dem Nachladen weiter fehlt.
 */
export function useWetterNachladen(schluessel: string | null, neuLaden: () => unknown): void {
  // Immer die jüngste Funktion aufrufen, ohne dass ihr Wechsel den Zeitgeber neu startet.
  const aktuell = useRef(neuLaden);
  useEffect(() => {
    aktuell.current = neuLaden;
  });

  useEffect(() => {
    if (!schluessel) return;
    const warten = wetterNachladenIn(zuletztGespeichertUm());
    if (warten === null) return;
    const zeitgeber = setTimeout(() => {
      void aktuell.current();
    }, warten);
    return () => clearTimeout(zeitgeber);
  }, [schluessel]);
}

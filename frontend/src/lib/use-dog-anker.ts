"use client";

import { useEffect, useRef } from "react";
import { ankerWunsch, type AnkerWunsch } from "@/lib/hundeseite";

/**
 * Setzt die Fragmente alter Links auf der Hundeseite um (#trainingsplan,
 * #training-erfassen, #faehrte-aufnehmen - siehe ankerWunsch).
 *
 * Erst wenn `bereit` ist (der Hund geladen): Der Server sieht das Fragment einer
 * Adresse nie, und die Seite zeigt vorher nur "Lädt…". Danach hört der Hook auch
 * auf Änderungen des Fragments, damit ein Link auf derselben Seite wirkt (etwa
 * "Fortsetzen" bei einer unterbrochenen Fährte). `anwenden` muss das Fragment
 * aus der Adresse nehmen, sonst öffnete ein Neuladen dasselbe noch einmal.
 */
export function useDogAnker(bereit: boolean, anwenden: (wunsch: AnkerWunsch) => void) {
  const aktuell = useRef(anwenden);
  useEffect(() => {
    aktuell.current = anwenden;
  });

  useEffect(() => {
    if (!bereit) return;
    const pruefen = () => {
      const wunsch = ankerWunsch(window.location.hash);
      if (wunsch) aktuell.current(wunsch);
    };
    pruefen();
    window.addEventListener("hashchange", pruefen);
    return () => window.removeEventListener("hashchange", pruefen);
  }, [bereit]);
}

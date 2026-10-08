"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { DogTrackStats } from "@/lib/types";

/**
 * Die Auswertung der Fährten-Abläufe eines Hundes (jüngste ausgewertete Abläufe,
 * Trend) - einmal geladen für den Verlauf über dem Tagebuch und die Kurzbilder
 * der Tageszeilen.
 *
 * `bereit` heißt: es steht fest, was angezeigt wird (Antwort oder Fehler da,
 * oder es gibt nichts zu laden). Die Seite wartet damit, zu einem Eintrag zu
 * springen: der Verlauf erscheint über dem Tagebuch und schöbe es sonst nach dem
 * Hinscrollen wieder weg.
 *
 * Ein Fehler zählt als "keine Abläufe": der Block ist optional und soll die
 * Seite nicht stören.
 */
export function useFaehrtenStats(dogId: string, aktiv: boolean, aktualisiert: unknown) {
  const [stats, setStats] = useState<DogTrackStats | null>(null);

  useEffect(() => {
    if (!aktiv) return;
    let active = true;
    api
      .get<DogTrackStats>(`/api/stats/dogs/${dogId}/tracks`)
      .then((daten) => {
        if (active) setStats(daten);
      })
      .catch(() => {
        if (active) setStats({ runs: [], deviationTrend: null, onTrackTrend: null });
      });
    return () => {
      active = false;
    };
  }, [dogId, aktiv, aktualisiert]);

  return { stats: aktiv ? stats : null, bereit: !aktiv || stats !== null };
}

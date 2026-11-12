"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { MerkzettelEintrag } from "@/lib/types";

/**
 * Die offenen Einträge des Merkzettels eines Hundes. Nur Besitzer:innen bekommen sie - bei
 * jedem anderen (Trainer:in, Fremde) antwortet der Server mit 404, und die Liste bleibt leer.
 * Fällt der Abruf aus (offline), bleibt der letzte Stand stehen; es gibt hier keinen Lesecache,
 * der Merkzettel ist eine Zugabe und keine Voraussetzung fürs Eintragen.
 *
 * `eintraege` ist null, solange nichts geladen ist (oder `aktiv` aus ist) - und nach einem Wechsel
 * des Hundes, bis dessen Liste da ist.
 */
export function useMerkzettel(dogId: string, aktiv: boolean = true): {
  eintraege: MerkzettelEintrag[] | null;
  reload: () => void;
} {
  const [geladen, setGeladen] = useState<{ dogId: string; liste: MerkzettelEintrag[] } | null>(null);
  const [stand, setStand] = useState(0);

  useEffect(() => {
    if (!aktiv) return;
    let abgebrochen = false;
    api.get<MerkzettelEintrag[]>(`/api/dogs/${dogId}/merkzettel`).then(
      (liste) => {
        if (!abgebrochen) setGeladen({ dogId, liste });
      },
      () => {
        // Kein Zugriff oder kein Netz: ohne Merkzettel weiter.
      },
    );
    return () => {
      abgebrochen = true;
    };
  }, [dogId, aktiv, stand]);

  const reload = useCallback(() => setStand((n) => n + 1), []);
  return { eintraege: aktiv && geladen?.dogId === dogId ? geladen.liste : null, reload };
}

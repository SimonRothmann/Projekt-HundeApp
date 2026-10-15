"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";

/**
 * Was der Server kann - Spiegel von Application/Notifications/SystemFeaturesDto.
 * Ohne Anmeldung abrufbar und frei von Geheimnissen (nur Wahrheitswerte und der
 * öffentliche Push-Schlüssel).
 */
export type SystemFeatures = {
  /** Ob der Server E-Mails versendet. */
  email: boolean;
  /** Öffentlicher VAPID-Schlüssel; null = Push nicht eingerichtet. */
  pushPublicKey: string | null;
};

let abruf: Promise<SystemFeatures> | null = null;

/**
 * Einmal je Seitenaufruf holen, nicht je Komponente. Schlägt der Abruf fehl
 * (offline), wird er beim nächsten Mal neu versucht.
 */
function ladeFeatures(): Promise<SystemFeatures> {
  abruf ??= api.get<SystemFeatures>("/api/system/features").catch((fehler) => {
    abruf = null;
    throw fehler;
  });
  return abruf;
}

/**
 * Die Fähigkeiten des Servers; null, solange sie nicht da sind (oder der Abruf
 * scheiterte). Aufrufer verhalten sich in dem Fall wie "nichts eingerichtet" -
 * lieber eine Funktion weniger anbieten als eine versprechen, die nicht läuft.
 */
export function useSystemFeatures(): SystemFeatures | null {
  const [features, setFeatures] = useState<SystemFeatures | null>(null);

  useEffect(() => {
    let abgebrochen = false;
    ladeFeatures()
      .then((f) => {
        if (!abgebrochen) setFeatures(f);
      })
      .catch(() => {
        // Bleibt bei null.
      });
    return () => {
      abgebrochen = true;
    };
  }, []);

  return features;
}

"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api } from "@/lib/api";
import type { LocationValue } from "@/components/dogs/location-time-fields";
import type { GeocodeResult } from "@/lib/types";
import { useT } from "@/lib/i18n";

/**
 * Den Standort des Geräts als Trainingsort übernehmen - samt Name aus der
 * Rückwärtssuche. Aus LocationTimeFields herausgelöst, weil das Eintragen-
 * Fenster denselben Handgriff als einzelnen Knopf anbietet ("Standort
 * verwenden"): zwei Fassungen davon liefen sonst auseinander.
 *
 * `ort` ist der Stand beim Antippen: Ein schon vergebener Name bleibt
 * unangetastet, der Vorschlag aus der Rückwärtssuche füllt nur Leeres.
 */
export function useAktuellerStandort(ort: LocationValue, onChange: (ort: LocationValue) => void) {
  const t = useT();
  const [laeuft, setLaeuft] = useState(false);

  function ermitteln() {
    if (!navigator.geolocation) {
      toast.error(t("Standort wird von diesem Gerät nicht unterstützt."));
      return;
    }
    setLaeuft(true);
    navigator.geolocation.getCurrentPosition(
      async (pos) => {
        // Namen zu den Koordinaten holen. Ohne das hieße jeder so gesetzte Ort
        // "Aktueller Standort" - in der Liste der zuletzt genutzten Orte
        // fielen sie zu einem Knopf zusammen, der auf die zuletzt
        // gespeicherten Koordinaten zeigt. Nur vorbelegen, wenn noch nichts
        // dasteht; ein selbst vergebener Name bleibt unangetastet.
        let vorschlag: string | null = null;
        try {
          const params = new URLSearchParams({ lat: String(pos.coords.latitude), lon: String(pos.coords.longitude) });
          vorschlag = (await api.get<GeocodeResult | null>(`/api/weather/locations/reverse?${params}`))?.name ?? null;
        } catch {
          // Reiner Komfort - schlägt es fehl, tippt man den Namen eben selbst.
        }
        onChange({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
          locationName: ort.locationName || vorschlag || "Aktueller Standort",
        });
        setLaeuft(false);
        toast.success(t("Standort übernommen."));
      },
      () => {
        setLaeuft(false);
        toast.error(t("Standort konnte nicht ermittelt werden."));
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  return { ermitteln, laeuft };
}

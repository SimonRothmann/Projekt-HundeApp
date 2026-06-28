"use client";

import { useEffect, useRef } from "react";
import type { GpsPoint } from "@/lib/types";

/// <reference types="leaflet" />

/**
 * Rendert eine Fährte auf einer OpenStreetMap-Karte (kostenlos, kein API-Key
 * nötig - siehe COST STRATEGY.md "Ziel: 0-10€ monatlich"). Leaflet wird
 * dynamisch importiert, da es auf `window`/`document` zugreift und daher
 * nicht serverseitig gerendert werden darf.
 */
export function TrackMap({ points }: { points: GpsPoint[] }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<import("leaflet").Map | null>(null);

  useEffect(() => {
    if (!containerRef.current || points.length === 0) return;

    let map: import("leaflet").Map | undefined;

    import("leaflet").then((L) => {
      if (!containerRef.current) return;

      const latLngs = points.map((p) => [p.latitude, p.longitude] as [number, number]);

      map = L.map(containerRef.current).fitBounds(latLngs, { padding: [16, 16] });
      mapRef.current = map;

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: "&copy; OpenStreetMap-Mitwirkende",
        maxZoom: 19,
      }).addTo(map);

      L.polyline(latLngs, { color: "var(--color-primary)" }).addTo(map);
      L.circleMarker(latLngs[0], { radius: 6, color: "green" }).addTo(map).bindTooltip("Start");
      L.circleMarker(latLngs[latLngs.length - 1], { radius: 6, color: "red" })
        .addTo(map)
        .bindTooltip("Ende");
    });

    return () => {
      map?.remove();
      mapRef.current = null;
    };
  }, [points]);

  if (points.length === 0) {
    return <p className="text-sm text-muted-foreground">Keine GPS-Punkte aufgezeichnet.</p>;
  }

  return <div ref={containerRef} className="h-64 w-full rounded-md" />;
}

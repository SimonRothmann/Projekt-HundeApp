"use client";

import { useEffect, useRef } from "react";
import type { GpsPoint, GpsWalkRun } from "@/lib/types";

/// <reference types="leaflet" />

// Eigene Farbe pro Ablauf-Versuch, damit mehrere Wiederholungen auf der
// Karte unterscheidbar bleiben (zyklisch wiederverwendet, falls mehr
// Versuche als Farben vorhanden sind).
const WALK_RUN_COLORS = ["#2563eb", "#9333ea", "#0d9488", "#dc2626"];

/**
 * Rendert eine Fährte auf einer OpenStreetMap-Karte (kostenlos, kein API-Key
 * nötig - siehe COST STRATEGY.md "Ziel: 0-10€ monatlich"). Leaflet wird
 * dynamisch importiert, da es auf `window`/`document` zugreift und daher
 * nicht serverseitig gerendert werden darf. Optional werden Ablauf-Versuche
 * (siehe FahrteRecorder "Fährte erneut ablaufen") als zusätzliche Linien
 * zum Vergleich mit der gelegten Fährte eingezeichnet.
 */
export function TrackMap({ points, walkRuns = [] }: { points: GpsPoint[]; walkRuns?: GpsWalkRun[] }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<import("leaflet").Map | null>(null);

  useEffect(() => {
    if (!containerRef.current || (points.length === 0 && walkRuns.length === 0)) return;

    // import("leaflet") ist asynchron - läuft der Effect erneut, bevor das
    // Promise aufgelöst ist (React Strict Mode ruft Effects im Dev-Modus
    // doppelt auf, ebenso schnell wechselnde points/walkRuns-Referenzen),
    // würde sonst ein zweites L.map() auf demselben Container aufgerufen
    // werden, während das erste noch nicht aufgeräumt ist - Leaflet wirft
    // dann "Map container is already initialized". Das Flag sorgt dafür,
    // dass nur der zuletzt gestartete, nicht abgebrochene Lauf tatsächlich
    // eine Karte erzeugt.
    let cancelled = false;

    import("leaflet").then((L) => {
      if (cancelled || !containerRef.current) return;

      // Linie/Start/Ende beziehen sich nur auf die automatischen GPS-Punkte -
      // manuell gesetzte Marker für gelegte Gegenstände (Schussstelle,
      // Apportel etc.) gehören nicht zur eigentlichen Laufstrecke und würden
      // die Linienführung verzerren.
      const automaticPoints = points.filter((p) => p.pointType !== 1);
      const manualPoints = points.filter((p) => p.pointType === 1);
      const latLngs = automaticPoints.map((p) => [p.latitude, p.longitude] as [number, number]);

      const allLatLngs = [
        ...points.map((p) => [p.latitude, p.longitude] as [number, number]),
        ...walkRuns.flatMap((r) => r.points.map((p) => [p.latitude, p.longitude] as [number, number])),
      ];

      const map = L.map(containerRef.current).fitBounds(allLatLngs, { padding: [16, 16] });
      mapRef.current = map;

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: "&copy; OpenStreetMap-Mitwirkende",
        maxZoom: 19,
      }).addTo(map);

      if (latLngs.length > 0) {
        L.polyline(latLngs, { color: "var(--color-primary)" }).addTo(map);
        L.circleMarker(latLngs[0], { radius: 6, color: "green" }).addTo(map).bindTooltip("Start (gelegt)");
        L.circleMarker(latLngs[latLngs.length - 1], { radius: 6, color: "red" })
          .addTo(map)
          .bindTooltip("Ende (gelegt)");
      }

      manualPoints.forEach((p) => {
        L.circleMarker([p.latitude, p.longitude], {
          radius: 7,
          color: "orange",
          fillColor: "orange",
          fillOpacity: 0.9,
        })
          .addTo(map)
          .bindTooltip(p.label || "Gegenstand");
      });

      walkRuns.forEach((run, index) => {
        if (run.points.length === 0) return;
        const color = WALK_RUN_COLORS[index % WALK_RUN_COLORS.length];
        const runLatLngs = run.points.map((p) => [p.latitude, p.longitude] as [number, number]);
        L.polyline(runLatLngs, { color, dashArray: "6 6" })
          .addTo(map)
          .bindTooltip(`Ablauf-Versuch ${index + 1}`);
      });
    });

    return () => {
      cancelled = true;
      mapRef.current?.remove();
      mapRef.current = null;
    };
  }, [points, walkRuns]);

  if (points.length === 0 && walkRuns.length === 0) {
    return <p className="text-sm text-muted-foreground">Keine GPS-Punkte aufgezeichnet.</p>;
  }

  return <div ref={containerRef} className="h-64 w-full rounded-md" />;
}

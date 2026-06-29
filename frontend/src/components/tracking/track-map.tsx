"use client";

import { useEffect, useRef, useState } from "react";
import type { GpsWalkRun } from "@/lib/types";

/// <reference types="leaflet" />

// Kompatibel zu sowohl GpsPoint (pointType/label gesetzt) als auch
// GpsWalkPoint (kennt beide Felder nicht - zählt dann automatisch als
// "automatischer Punkt", siehe pointType !== 1 unten).
type MapPoint = { latitude: number; longitude: number; pointType?: number; label?: string | null };

// Eigene Farbe pro Ablauf-Versuch, damit mehrere Wiederholungen auf der
// Karte unterscheidbar bleiben (zyklisch wiederverwendet, falls mehr
// Versuche als Farben vorhanden sind).
const WALK_RUN_COLORS = ["#2563eb", "#9333ea", "#0d9488", "#dc2626"];

// Fester Farbwert statt einer Theme-CSS-Variable: Leaflet setzt "color" als
// reines SVG-Attribut (stroke="..."), nicht als CSS-Eigenschaft - var(...)
// wird in einem XML-Attribut nicht aufgelöst, die Linie blieb dadurch
// unsichtbar (nur Kacheln/Marker waren zu sehen).
const TRACK_LINE_COLOR = "#16a34a";

// Schrittgeschwindigkeit, kurze Distanzen - ein nahes Zoom-Level zeigt
// einzelne Abbiegungen deutlich, statt die ganze (noch kurze) Strecke winzig
// in der Bildmitte darzustellen.
const LIVE_INITIAL_ZOOM = 18;

/**
 * Rendert eine Fährte auf einer OpenStreetMap-Karte (kostenlos, kein API-Key
 * nötig - siehe COST STRATEGY.md "Ziel: 0-10€ monatlich"). Leaflet wird
 * dynamisch importiert, da es auf `window`/`document` zugreift und daher
 * nicht serverseitig gerendert werden darf. Optional werden Ablauf-Versuche
 * (siehe FahrteRecorder "Fährte erneut ablaufen") als zusätzliche Linien
 * zum Vergleich mit der gelegten Fährte eingezeichnet.
 *
 * Mit `live` Karte einmalig erzeugen und bei neuen Punkten nur die Layer
 * aktualisieren (statt komplett neu aufzubauen) - damit ruckelt die Karte
 * nicht bei jedem GPS-Tick während einer laufenden Aufnahme. Zusätzlich
 * folgt die Kartenmitte per `panTo` dem aktuellen Standort, ohne den vom
 * Nutzer gewählten Zoom zu verändern.
 */
export function TrackMap({
  points,
  walkRuns = [],
  live = false,
}: {
  points: MapPoint[];
  walkRuns?: GpsWalkRun[];
  live?: boolean;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<import("leaflet").Map | null>(null);
  const leafletRef = useRef<typeof import("leaflet") | null>(null);
  const layerGroupRef = useRef<import("leaflet").LayerGroup | null>(null);
  const hasSetInitialViewRef = useRef(false);
  const [mapReady, setMapReady] = useState(false);

  // Effect 1: Karte + Tile-Layer genau einmal erzeugen (nur beim Mount).
  useEffect(() => {
    if (!containerRef.current) return;

    // import("leaflet") ist asynchron - läuft der Effect erneut, bevor das
    // Promise aufgelöst ist (React Strict Mode ruft Effects im Dev-Modus
    // doppelt auf), würde sonst ein zweites L.map() auf demselben Container
    // aufgerufen werden, während das erste noch nicht aufgeräumt ist -
    // Leaflet wirft dann "Map container is already initialized".
    let cancelled = false;

    import("leaflet").then((L) => {
      if (cancelled || !containerRef.current) return;

      leafletRef.current = L;
      // Vorläufige Sicht (Deutschland-Mitte), bis der erste GPS-Punkt
      // eintrifft und Effect 2 per setView auf den tatsächlichen Standort
      // springt - ohne das bliebe die Karte ohne jede Kachel leer/grau.
      const map = L.map(containerRef.current).setView([51.1657, 10.4515], 6);
      mapRef.current = map;

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: "&copy; OpenStreetMap-Mitwirkende",
        maxZoom: 19,
      }).addTo(map);

      layerGroupRef.current = L.layerGroup().addTo(map);
      setMapReady(true);
    });

    return () => {
      cancelled = true;
      mapRef.current?.remove();
      mapRef.current = null;
      leafletRef.current = null;
      layerGroupRef.current = null;
      hasSetInitialViewRef.current = false;
    };
  }, []);

  // Effect 2: bei jeder Änderung von points/walkRuns nur die Layer neu
  // zeichnen (Karte selbst bleibt bestehen) - läuft auch einmal direkt
  // nachdem die Karte fertig initialisiert ist (mapReady wechselt auf true).
  useEffect(() => {
    const L = leafletRef.current;
    const map = mapRef.current;
    const layerGroup = layerGroupRef.current;
    if (!L || !map || !layerGroup) return;

    layerGroup.clearLayers();

    // Linie/Start/Ende beziehen sich nur auf die automatischen GPS-Punkte -
    // manuell gesetzte Marker für gelegte Gegenstände (Schussstelle,
    // Apportel etc.) gehören nicht zur eigentlichen Laufstrecke und würden
    // die Linienführung verzerren.
    const automaticPoints = points.filter((p) => p.pointType !== 1);
    const manualPoints = points.filter((p) => p.pointType === 1);
    const latLngs = automaticPoints.map((p) => [p.latitude, p.longitude] as [number, number]);

    if (latLngs.length > 0) {
      L.polyline(latLngs, { color: TRACK_LINE_COLOR }).addTo(layerGroup);
      L.circleMarker(latLngs[0], { radius: 6, color: "green" }).addTo(layerGroup).bindTooltip("Start (gelegt)");
      if (!live) {
        L.circleMarker(latLngs[latLngs.length - 1], { radius: 6, color: "red" })
          .addTo(layerGroup)
          .bindTooltip("Ende (gelegt)");
      }
    }

    manualPoints.forEach((p) => {
      L.circleMarker([p.latitude, p.longitude], {
        radius: 7,
        color: "orange",
        fillColor: "orange",
        fillOpacity: 0.9,
      })
        .addTo(layerGroup)
        .bindTooltip(p.label || "Gegenstand");
    });

    walkRuns.forEach((run, index) => {
      if (run.points.length === 0) return;
      const color = WALK_RUN_COLORS[index % WALK_RUN_COLORS.length];
      const runLatLngs = run.points.map((p) => [p.latitude, p.longitude] as [number, number]);
      L.polyline(runLatLngs, { color, dashArray: "6 6" })
        .addTo(layerGroup)
        .bindTooltip(`Ablauf-Versuch ${index + 1}`);
    });

    const allLatLngs = [...latLngs, ...manualPoints.map((p) => [p.latitude, p.longitude] as [number, number])];
    if (live) {
      // Während der Aufnahme: Kartenmitte folgt dem aktuellen Standort, der
      // Zoom bleibt unverändert, damit der Nutzer nicht laufend neu
      // hineinzoomen muss, sobald er die Karte einmal passend eingestellt hat.
      if (latLngs.length > 0) {
        const latest = latLngs[latLngs.length - 1];
        if (!hasSetInitialViewRef.current) {
          map.setView(latest, LIVE_INITIAL_ZOOM);
          hasSetInitialViewRef.current = true;
        } else {
          map.panTo(latest);
        }
      }
    } else if (allLatLngs.length > 0) {
      // Abgeschlossene/historische Fährte: ganze Strecke ins Bild einpassen.
      const allWalkRunLatLngs = walkRuns.flatMap((r) => r.points.map((p) => [p.latitude, p.longitude] as [number, number]));
      map.fitBounds([...allLatLngs, ...allWalkRunLatLngs], { padding: [16, 16] });
    }
  }, [mapReady, points, walkRuns, live]);

  // Im Live-Modus die Karte schon vor dem ersten Punkt anzeigen (wartet auf
  // die erste Positionsmessung), damit sie nicht erst nach einigen Sekunden
  // "einschnappt".
  if (!live && points.length === 0 && walkRuns.length === 0) {
    return <p className="text-sm text-muted-foreground">Keine GPS-Punkte aufgezeichnet.</p>;
  }

  return <div ref={containerRef} className="h-64 w-full rounded-md" />;
}

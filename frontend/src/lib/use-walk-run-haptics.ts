"use client";

import { useEffect, useMemo, useRef } from "react";
import { bearingDegrees, haversineMeters } from "@/lib/geo";
import type { GpsPoint, GpsWalkPoint } from "@/lib/types";

// Auslöseradius für die Vibration vor einem POI (Gegenstand oder Abbiegung).
// "10 Schritte" bei ~75 cm Schrittlänge ≈ 7-8 m; wir nehmen 8 m als
// Kompromiss zwischen Vorwarnzeit und GPS-Rauschen (unser Fährten-Filter
// akzeptiert Punkte bis 8 m Fehlerkreis, kleiner wäre zu unzuverlässig).
const TRIGGER_RADIUS_M = 8;

// Nach dem Vibrieren wird der POI ausgesperrt, damit er nicht immer wieder
// feuert wenn der Nutzer sich unter Rauschen mehrfach in den Radius bewegt.
// Kein Cooldown pro POI - stattdessen einmalig pro Ablauf-Versuch: ein POI
// wurde entweder gemeldet oder nicht. Zurückgesetzt beim Neustart.

// Winkeländerung zwischen zwei aufeinanderfolgenden Segmenten der Legung,
// ab der wir eine "Abbiegung" annehmen. 30° reicht, um eine echte Richtungs-
// änderung von einem GPS-typischen Zittern zu unterscheiden.
const TURN_ANGLE_THRESHOLD_DEG = 30;
// Mindestlänge der beiden Nachbarsegmente einer Abbiegung, damit Rauschen
// bei kurzen Punktabständen keine falschen Abbiegungen vortäuscht.
const TURN_SEGMENT_MIN_LENGTH_M = 5;

// Vibrationsmuster (ms; abwechselnd Vibration/Pause). navigator.vibrate
// ignoriert unbekannte Werte still, daher überall der Optional-Aufruf.
const VIBRATE_OBJECT = [200, 80, 200, 80, 200];
const VIBRATE_TURN = [120, 80, 120];

type Poi = {
  key: string;
  latitude: number;
  longitude: number;
  kind: "object" | "turn";
};

// Abbiegungen aus der Legung extrahieren: für jedes innere Punktpaar
// prüfen, ob die Peilung zwischen den beiden Segmenten (davor / danach)
// stark abknickt. Bei kleinen Segmenten ignorieren wir das Ergebnis
// (Rauschen). Manuell gesetzte Marker (pointType=1) fließen hier nicht ein,
// die kommen als "object"-POIs separat.
function findTurnPoints(points: GpsPoint[]): Poi[] {
  const auto = points.filter((p) => p.pointType !== 1);
  const turns: Poi[] = [];
  for (let i = 1; i < auto.length - 1; i++) {
    const prev = auto[i - 1];
    const cur = auto[i];
    const next = auto[i + 1];
    if (haversineMeters(prev, cur) < TURN_SEGMENT_MIN_LENGTH_M) continue;
    if (haversineMeters(cur, next) < TURN_SEGMENT_MIN_LENGTH_M) continue;
    let diff = Math.abs(bearingDegrees(prev, cur) - bearingDegrees(cur, next));
    if (diff > 180) diff = 360 - diff;
    if (diff >= TURN_ANGLE_THRESHOLD_DEG) {
      turns.push({ key: `turn-${i}`, latitude: cur.latitude, longitude: cur.longitude, kind: "turn" });
    }
  }
  return turns;
}

/**
 * Vibriert das Handy, wenn der aktuell aufzeichnende Ablauf-Versuch sich
 * einem Gegenstand-Marker der Legung oder einer Abbiegung nähert. Jeder POI
 * feuert nur einmal pro Ablauf-Versuch; Wechsel des `sessionKey` (typisch:
 * neue Aufnahme) setzt die "schon-vibriert"-Menge zurück.
 */
export function useWalkRunHaptics(
  laidTrackPoints: GpsPoint[] | undefined,
  currentPoint: GpsWalkPoint | null,
  sessionKey: string,
) {
  const pois = useMemo<Poi[]>(() => {
    if (!laidTrackPoints || laidTrackPoints.length < 2) return [];
    const markers = laidTrackPoints
      .filter((p) => p.pointType === 1)
      .map<Poi>((p, i) => ({ key: `obj-${i}`, latitude: p.latitude, longitude: p.longitude, kind: "object" }));
    return [...markers, ...findTurnPoints(laidTrackPoints)];
  }, [laidTrackPoints]);

  const notifiedRef = useRef<Set<string>>(new Set());
  const lastSessionKeyRef = useRef(sessionKey);

  useEffect(() => {
    if (lastSessionKeyRef.current !== sessionKey) {
      notifiedRef.current = new Set();
      lastSessionKeyRef.current = sessionKey;
    }
  }, [sessionKey]);

  useEffect(() => {
    if (!currentPoint || pois.length === 0) return;
    if (typeof navigator === "undefined" || typeof navigator.vibrate !== "function") return;

    for (const poi of pois) {
      if (notifiedRef.current.has(poi.key)) continue;
      const dist = haversineMeters(
        { latitude: currentPoint.latitude, longitude: currentPoint.longitude },
        { latitude: poi.latitude, longitude: poi.longitude },
      );
      if (dist <= TRIGGER_RADIUS_M) {
        notifiedRef.current.add(poi.key);
        navigator.vibrate(poi.kind === "object" ? VIBRATE_OBJECT : VIBRATE_TURN);
      }
    }
  }, [currentPoint, pois]);
}

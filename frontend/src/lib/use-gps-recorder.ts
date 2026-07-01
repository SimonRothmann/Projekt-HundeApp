"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { haversineMeters } from "@/lib/geo";

// maximumAge: wie alt eine vom Browser zwischengespeicherte Position
// maximal sein darf, um statt einer frischen GPS-Messung wiederverwendet zu
// werden. 0 erzwingt bei jeder Messung eine frische Position statt einer
// zwischengespeicherten.
const GPS_MAX_POSITION_AGE_MS = 0;

// watchPosition() hat keinen Parameter für die gewünschte Abtastrate - manche
// Browser/Betriebssysteme drosseln die Callback-Frequenz zusätzlich aus
// Energiespargründen, selbst mit enableHighAccuracy (beobachtet u.a. auf
// iOS Safari). Ein eigener Poll-Loop mit getCurrentPosition() erzwingt bei
// jedem Tick eine frische Messung im gewünschten Takt - das verbessert die
// sichtbare Winkelauflösung von Abbiegungen deutlich. Schneller als die
// tatsächliche Update-Rate des GPS-Chips (üblich ca. 1 Hz) liefert das
// keine zusätzlichen echten Punkte, ist aber harmlos (nahezu identische
// Wiederholpunkte tragen praktisch nichts zur Streckenlänge bei).
const POSITION_POLL_INTERVAL_MS = 500;

// Standardwerte - können pro Aufzeichnungstyp überschrieben werden.
// Punkte ungenauer als MAX_ACCURACY_METERS werden verworfen. Ohne aGPS
// (Assisted GPS über Mobilfunk/WLAN) — also offline — braucht der GPS-Chip
// einen Kaltstart, der mehrere Sekunden dauert und in dieser Zeit Positionen
// mit 50–200 m Ungenauigkeit liefert.
const DEFAULT_MAX_ACCURACY_METERS = 25;

// Mindestabstand zwischen zwei aufeinanderfolgenden automatischen Trackpunkten.
const DEFAULT_MIN_DISTANCE_METERS = 2;

export type GpsRecorderOptions = {
  // Punkte mit größerem Fehlerkreis werden verworfen. Kleiner = genauer, aber
  // längere Wartezeit beim Kaltstart bis die Aufzeichnung erste Punkte liefert.
  maxAccuracyMeters?: number;
  // Mindestabstand; eliminiert GPS-Rauschen im Stillstand.
  minDistanceMeters?: number;
  // EMA-Glättungsfaktor α ∈ (0, 1]. Jeder aufgezeichnete Punkt wird mit dem
  // gewichteten Mittel der Vorgänger kombiniert:
  //   smoothed = α * gemessen + (1 - α) * smoothed_vorher
  // Niedriger Wert → starke Glättung (mehr Verzögerung bei Kurven), hoher
  // Wert → weniger Glättung (verhält sich eher wie roh). 0 = deaktiviert.
  // Empfehlung Fährte: 0.35 → reduziert effektives Rauschen um ~50 % bei
  // Gehgeschwindigkeit mit minimalem Versatz (<1 m Lag bei 1 m/s).
  smoothAlpha?: number;
};

// Erstellt ein synthetisches GeolocationPosition-Objekt mit überschriebenen
// Koordinaten (für EMA-geglättete Positionen). ToPoint-Funktionen der Aufrufer
// greifen nur auf coords.* und timestamp zu - der Rest bleibt aus der echten
// Messung erhalten (accuracy für UI-Anzeige, heading/speed für spätere Nutzung).
function withSmoothCoords(
  position: GeolocationPosition,
  latitude: number,
  longitude: number,
): GeolocationPosition {
  return {
    coords: {
      latitude,
      longitude,
      accuracy: position.coords.accuracy,
      altitude: position.coords.altitude,
      altitudeAccuracy: position.coords.altitudeAccuracy,
      heading: position.coords.heading,
      speed: position.coords.speed,
      toJSON: () => ({}),
    },
    timestamp: position.timestamp,
    toJSON: () => ({}),
  } as GeolocationPosition;
}

/**
 * Gemeinsame GPS-Aufzeichnungslogik (Poll-Loop inkl. Cleanup beim Unmount)
 * für Fährten- und Ablauf-Aufzeichnung - vorher dreifach nahezu identisch
 * dupliziert in fahrte-recorder.tsx, gps-track-section.tsx und
 * walk-run-recorder.tsx.
 */
export function useGpsRecorder<T>(
  toPoint: (position: GeolocationPosition) => T,
  options: GpsRecorderOptions = {},
) {
  const {
    maxAccuracyMeters = DEFAULT_MAX_ACCURACY_METERS,
    minDistanceMeters = DEFAULT_MIN_DISTANCE_METERS,
    smoothAlpha = 0,
  } = options;

  const [isRecording, setIsRecording] = useState(false);
  const [points, setPoints] = useState<T[]>([]);
  const [currentAccuracy, setCurrentAccuracy] = useState<number | null>(null);
  const isRecordingRef = useRef(false);
  const timeoutRef = useRef<number | null>(null);
  // Letzter aufgezeichneter Punkt (geglättet falls smoothAlpha > 0) für den
  // Mindestabstand-Filter.
  const lastRecordedRef = useRef<{ latitude: number; longitude: number } | null>(null);
  // Laufender EMA-Zustand; null bis die erste akzeptierte Messung ankommt.
  const smoothedRef = useRef<{ latitude: number; longitude: number } | null>(null);

  useEffect(() => {
    // Falls die Seite verlassen wird, während noch aufgezeichnet wird (z.B.
    // Navigation ohne vorher "Stoppen" zu klicken): Poll-Loop beenden, sonst
    // fragt er unbegrenzt im Hintergrund weiter nach der Position und ruft
    // den Callback einer längst unmounteten Komponente weiter auf - mit
    // jeder vergessenen Aufnahme ein weiterer, nie endender GPS-Poller, der
    // die App zunehmend träge macht.
    return () => {
      isRecordingRef.current = false;
      if (timeoutRef.current !== null) {
        window.clearTimeout(timeoutRef.current);
      }
    };
  }, []);

  function poll() {
    if (!isRecordingRef.current) return;
    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (!isRecordingRef.current) return;
        const { latitude, longitude, accuracy } = position.coords;
        setCurrentAccuracy(accuracy);

        if (accuracy > maxAccuracyMeters) {
          // GPS noch nicht eingeschwungen (typisch beim Kaltstart ohne aGPS) -
          // Punkt verwerfen und auf bessere Genauigkeit warten.
          scheduleNextPoll();
          return;
        }

        // EMA-Glättung: ersten Punkt ungeglättet übernehmen (kein Vorgänger),
        // danach gewichtetes Mittel aus gemessener und geglätteter Position.
        let smoothedLat = latitude;
        let smoothedLon = longitude;
        if (smoothAlpha > 0) {
          if (smoothedRef.current !== null) {
            smoothedLat = smoothAlpha * latitude + (1 - smoothAlpha) * smoothedRef.current.latitude;
            smoothedLon = smoothAlpha * longitude + (1 - smoothAlpha) * smoothedRef.current.longitude;
          }
          smoothedRef.current = { latitude: smoothedLat, longitude: smoothedLon };
        }

        const last = lastRecordedRef.current;
        if (
          last !== null &&
          haversineMeters(last, { latitude: smoothedLat, longitude: smoothedLon }) < minDistanceMeters
        ) {
          // Zu nah am letzten Punkt - GPS-Rauschen im Stillstand, nicht aufzeichnen.
          scheduleNextPoll();
          return;
        }

        lastRecordedRef.current = { latitude: smoothedLat, longitude: smoothedLon };
        const recordedPosition =
          smoothAlpha > 0 ? withSmoothCoords(position, smoothedLat, smoothedLon) : position;
        setPoints((prev) => [...prev, toPoint(recordedPosition)]);
        scheduleNextPoll();
      },
      (error) => {
        if (!isRecordingRef.current) return;
        toast.error(`GPS-Fehler: ${error.message}`);
        scheduleNextPoll();
      },
      { enableHighAccuracy: true, maximumAge: GPS_MAX_POSITION_AGE_MS, timeout: POSITION_POLL_INTERVAL_MS * 4 },
    );
  }

  function scheduleNextPoll() {
    // Rekursives setTimeout statt setInterval: die nächste Anfrage wird
    // erst nach Abschluss der vorigen geplant, damit sich bei schlechtem
    // GPS-Empfang (lange Antwortzeit) keine überlappenden Anfragen stauen.
    if (!isRecordingRef.current) return;
    timeoutRef.current = window.setTimeout(poll, POSITION_POLL_INTERVAL_MS);
  }

  function start() {
    if (!("geolocation" in navigator)) {
      toast.error("Geolocation wird von diesem Browser nicht unterstützt.");
      return;
    }
    setPoints([]);
    setCurrentAccuracy(null);
    lastRecordedRef.current = null;
    smoothedRef.current = null;
    setIsRecording(true);
    isRecordingRef.current = true;
    poll();
  }

  function stop() {
    isRecordingRef.current = false;
    if (timeoutRef.current !== null) {
      window.clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
    setIsRecording(false);
  }

  function markPoint(onMarked: (point: T) => void, onError?: () => void) {
    if (!("geolocation" in navigator)) {
      toast.error("Geolocation wird von diesem Browser nicht unterstützt.");
      onError?.();
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (position) => onMarked(toPoint(position)),
      (error) => {
        toast.error(`GPS-Fehler: ${error.message}`);
        onError?.();
      },
      { enableHighAccuracy: true, maximumAge: GPS_MAX_POSITION_AGE_MS },
    );
  }

  return { isRecording, points, setPoints, currentAccuracy, start, stop, markPoint };
}

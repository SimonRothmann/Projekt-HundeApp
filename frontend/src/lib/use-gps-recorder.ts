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
const DEFAULT_MAX_ACCURACY_METERS = 25;
const DEFAULT_MIN_DISTANCE_METERS = 2;

// Kalman-Filter: erwartete Bewegung zwischen zwei Samples in Metern.
// Bei Gehgeschwindigkeit (~1 m/s) und 500 ms Interval ≈ 0.5 m.
// Wird als Prozessrauschen Q genutzt: höher = mehr Vertrauen in neue Messung
// (reagiert schneller auf Richtungswechsel), niedriger = mehr Glättung.
const KALMAN_PROCESS_NOISE_M = 0.5;

export type GpsRecorderOptions = {
  // Punkte mit größerem Fehlerkreis werden verworfen. Kleiner = genauer, aber
  // längere Wartezeit beim Kaltstart bis die Aufzeichnung erste Punkte liefert.
  maxAccuracyMeters?: number;
  // Mindestabstand; eliminiert GPS-Rauschen im Stillstand.
  minDistanceMeters?: number;
  // Kalman-Filter: adaptiv gewichtete Positionsglättung. Im Gegensatz zu EMA
  // variiert das Gewicht pro Messung abhängig von der gemeldeten GPS-Genauigkeit:
  // schlechte Messung → weniger Gewicht (stärker geglättet), gute Messung →
  // mehr Gewicht. Besser als EMA für Präzisionsanwendungen (z.B. Fährte).
  kalman?: boolean;
};

// Kalman-Zustand pro Koordinate. P ist die Schätzunsicherheit in Grad² -
// startet mit der ersten Messunsicherheit und konvergiert schnell gegen den
// Gleichgewichtswert.
type KalmanState = {
  lat: number;
  lon: number;
  P_lat: number;
  P_lon: number;
};

// Einen Kalman-Predict-Update-Schritt für eine 2D-Position (lat/lon getrennt,
// da der Fehlerkreis rund ist und keine Kreuzkorrelation nötig ist).
// Koordinaten bleiben in Grad; Unsicherheiten werden intern in Meter²
// umgerechnet, damit die Prozess- und Messrauschparameter in Metern angegeben
// werden können statt in Grad (intuitiver und von der Breitengrad-abhängigen
// Grad-Meter-Relation entkoppelt).
function kalmanStep(state: KalmanState, lat: number, lon: number, accuracyM: number): KalmanState {
  const cosLat = Math.cos((state.lat * Math.PI) / 180);
  const mPerDegLat = 111111;
  const mPerDegLon = 111111 * (cosLat === 0 ? 1 : cosLat);

  // Prozessrauschen Q: erwartete Positionsänderung zwischen Samples (in Grad²)
  const Q_lat = (KALMAN_PROCESS_NOISE_M / mPerDegLat) ** 2;
  const Q_lon = (KALMAN_PROCESS_NOISE_M / mPerDegLon) ** 2;

  // Messrauschen R: von der GPS-API gemeldeter Fehlerkreisradius → 1-σ-Varianz.
  // Die Geolocation-Spec definiert accuracy als 95%-Konfidenzkreis-Radius,
  // viele Browser (inkl. iOS Safari) liefern aber de facto den 1-σ-Wert -
  // konservativer Ansatz: als 1-σ behandeln (σ = accuracy, R = accuracy²).
  const R_lat = (accuracyM / mPerDegLat) ** 2;
  const R_lon = (accuracyM / mPerDegLon) ** 2;

  // Predict (Random-Walk-Modell: keine Geschwindigkeitsschätzung, da Gehen
  // unregelmäßig ist und ein Velocity-Modell bei häufigen Richtungswechseln
  // nicht besser abschneidet)
  const P_pred_lat = state.P_lat + Q_lat;
  const P_pred_lon = state.P_lon + Q_lon;

  // Kalman-Gain: K nahe 1 → neue Messung stark gewichten (Unsicherheit groß
  // oder GPS-Fehler klein), K nahe 0 → alte Schätzung bevorzugen.
  const K_lat = P_pred_lat / (P_pred_lat + R_lat);
  const K_lon = P_pred_lon / (P_pred_lon + R_lon);

  return {
    lat: state.lat + K_lat * (lat - state.lat),
    lon: state.lon + K_lon * (lon - state.lon),
    P_lat: (1 - K_lat) * P_pred_lat,
    P_lon: (1 - K_lon) * P_pred_lon,
  };
}

// Erstellt ein synthetisches GeolocationPosition-Objekt mit überschriebenen
// Koordinaten (Kalman-geglättete Position). ToPoint-Funktionen der Aufrufer
// lesen nur coords.* und timestamp - der Rest bleibt aus der echten Messung.
function withSmoothedCoords(
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
 * für Fährten- und Ablauf-Aufzeichnung.
 */
export function useGpsRecorder<T>(
  toPoint: (position: GeolocationPosition) => T,
  options: GpsRecorderOptions = {},
) {
  const {
    maxAccuracyMeters = DEFAULT_MAX_ACCURACY_METERS,
    minDistanceMeters = DEFAULT_MIN_DISTANCE_METERS,
    kalman = false,
  } = options;

  const [isRecording, setIsRecording] = useState(false);
  const [points, setPoints] = useState<T[]>([]);
  const [currentAccuracy, setCurrentAccuracy] = useState<number | null>(null);
  const isRecordingRef = useRef(false);
  const timeoutRef = useRef<number | null>(null);
  // Letzter aufgezeichneter (geglätteter) Punkt für den Mindestabstand-Filter.
  const lastRecordedRef = useRef<{ latitude: number; longitude: number } | null>(null);
  // Kalman-Filterzustand; null bis zur ersten akzeptierten Messung.
  const kalmanRef = useRef<KalmanState | null>(null);

  useEffect(() => {
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
          // GPS noch nicht eingeschwungen (typisch beim Kaltstart ohne aGPS).
          scheduleNextPoll();
          return;
        }

        let smoothedLat = latitude;
        let smoothedLon = longitude;

        if (kalman) {
          if (kalmanRef.current === null) {
            // Ersten Punkt als Initialzustand verwenden.
            const cosLat = Math.cos((latitude * Math.PI) / 180);
            kalmanRef.current = {
              lat: latitude,
              lon: longitude,
              P_lat: (accuracy / 111111) ** 2,
              P_lon: (accuracy / (111111 * (cosLat === 0 ? 1 : cosLat))) ** 2,
            };
          } else {
            kalmanRef.current = kalmanStep(kalmanRef.current, latitude, longitude, accuracy);
          }
          smoothedLat = kalmanRef.current.lat;
          smoothedLon = kalmanRef.current.lon;
        }

        const last = lastRecordedRef.current;
        if (
          last !== null &&
          haversineMeters(last, { latitude: smoothedLat, longitude: smoothedLon }) < minDistanceMeters
        ) {
          scheduleNextPoll();
          return;
        }

        lastRecordedRef.current = { latitude: smoothedLat, longitude: smoothedLon };
        const recordedPosition = kalman
          ? withSmoothedCoords(position, smoothedLat, smoothedLon)
          : position;
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
    kalmanRef.current = null;
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

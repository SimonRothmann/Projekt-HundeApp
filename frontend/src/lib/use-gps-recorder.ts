"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { haversineMeters } from "@/lib/geo";

// maximumAge während der Aufzeichnung: erlaubt dem Browser, eine bis zu 1 s
// alte Position wiederzuverwenden. Der GPS-Chip liefert auch bei
// enableHighAccuracy nativ nur ~1 Hz - eine "frische" Messung darunter
// erzwingen bringt keine echten neuen Datenpunkte, hält den Chip aber auf
// voller Leistung und ist ein signifikanter Akku-Fresser (bisher mit
// maximumAge:0 + 500 ms Poll-Loop kombiniert waren wir bei ~2× Chip-Last).
const GPS_MAX_POSITION_AGE_MS = 1000;
// Für markPoint() bleiben wir bei 0 - Nutzer erwartet dort eine wirklich
// aktuelle Position, und der Aufruf ist einmalig statt kontinuierlich.
const MARK_POINT_MAX_POSITION_AGE_MS = 0;

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
  // Obergrenze für die adaptive Lockerung des Genauigkeitsfilters. Ohne Netz
  // (kein aGPS, keine WLAN-Ortung) konvergiert der GPS-Chip oft nur bis
  // 10-20 m - ein harter 8m-Filter würde dann NIE einen Punkt akzeptieren
  // und die Aufzeichnung bliebe komplett leer. Deshalb: startet der Filter
  // bei maxAccuracyMeters und wird, wenn RELAX_AFTER_MS lang kein Punkt
  // akzeptiert wurde, schrittweise bis zu dieser Obergrenze gelockert. Der
  // Kalman-Filter gewichtet die schlechteren Messungen ohnehin schwächer -
  // ein leicht verrauschter Track ist besser als gar keiner.
  // Default: identisch zu maxAccuracyMeters (keine Lockerung).
  relaxedMaxAccuracyMeters?: number;
  // Mindestabstand; eliminiert GPS-Rauschen im Stillstand.
  minDistanceMeters?: number;
  // Kalman-Filter: adaptiv gewichtete Positionsglättung. Im Gegensatz zu EMA
  // variiert das Gewicht pro Messung abhängig von der gemeldeten GPS-Genauigkeit:
  // schlechte Messung → weniger Gewicht (stärker geglättet), gute Messung →
  // mehr Gewicht. Besser als EMA für Präzisionsanwendungen (z.B. Fährte).
  kalman?: boolean;
};

// Nach so vielen ms ohne akzeptierten Punkt beginnt die Lockerung des
// Genauigkeitsfilters (sofern relaxedMaxAccuracyMeters > maxAccuracyMeters).
const RELAX_AFTER_MS = 15_000;
// Pro weiterem Intervall dieser Länge wird der Schwellwert um 1 m gelockert,
// bis relaxedMaxAccuracyMeters erreicht ist. Beispiel Fährte (8 m → 20 m):
// nach 15 s → 9 m, nach 18 s → 10 m, ..., nach 48 s → 20 m (Maximum).
const RELAX_STEP_MS = 3_000;

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
    relaxedMaxAccuracyMeters = maxAccuracyMeters,
    minDistanceMeters = DEFAULT_MIN_DISTANCE_METERS,
    kalman = false,
  } = options;

  const [isRecording, setIsRecording] = useState(false);
  const [points, setPoints] = useState<T[]>([]);
  const [currentAccuracy, setCurrentAccuracy] = useState<number | null>(null);
  const isRecordingRef = useRef(false);
  // Ein einziges watchPosition-Abo während der Aufzeichnung. Bisher lief
  // parallel dazu ein 500 ms getCurrentPosition-Poll - das hielt den GPS-
  // Chip doppelt aktiv, ohne echte Zusatzpunkte zu liefern (der GNSS-Chip
  // gibt nativ 1 Hz aus), und war der Hauptverbraucher der Akkulaufzeit.
  // watchPosition liefert dieselben Punkte energieeffizienter, weil das
  // Betriebssystem intern batched.
  const watchIdRef = useRef<number | null>(null);
  // Zeitpunkt des letzten akzeptierten Punkts (bzw. Aufnahmestart) für die
  // adaptive Lockerung des Genauigkeitsfilters.
  const lastAcceptedAtRef = useRef<number>(0);
  // Letzter aufgezeichneter (geglätteter) Punkt für den Mindestabstand-Filter.
  const lastRecordedRef = useRef<{ latitude: number; longitude: number } | null>(null);
  // Kalman-Filterzustand; null bis zur ersten akzeptierten Messung.
  const kalmanRef = useRef<KalmanState | null>(null);
  // Screen Wake Lock: verhindert, dass das Display während der Aufzeichnung
  // automatisch ausgeht - auf iOS pausiert Safari sonst die Geolocation
  // sobald der Bildschirm sperrt, und die Aufzeichnung bricht ab. Gegen
  // bewusstes Sperren per Power-Taste hilft das nicht (dann greift der
  // visibilitychange-Reacquire beim nächsten Entsperren).
  const wakeLockRef = useRef<WakeLockSentinel | null>(null);

  async function acquireWakeLock() {
    // Nicht überall verfügbar (iOS Safari ab 16.4, kein Firefox Android) -
    // bewusst still degradieren, die Aufzeichnung selbst funktioniert auch
    // ohne, nur eben nicht mit automatisch wachem Display.
    if (!("wakeLock" in navigator)) return;
    try {
      wakeLockRef.current = await navigator.wakeLock.request("screen");
    } catch {
      // Z.B. abgelehnt bei niedrigem Akkustand - kein Fehler für den Nutzer.
    }
  }

  function releaseWakeLock() {
    wakeLockRef.current?.release().catch(() => {});
    wakeLockRef.current = null;
  }

  useEffect(() => {
    // Der Browser gibt den Wake Lock automatisch frei, wenn der Tab in den
    // Hintergrund geht (App-Wechsel, Sperren). Beim Zurückkehren während
    // einer laufenden Aufzeichnung neu anfordern.
    function handleVisibilityChange() {
      if (document.visibilityState === "visible" && isRecordingRef.current) {
        acquireWakeLock();
      }
    }
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      isRecordingRef.current = false;
      if (watchIdRef.current !== null) {
        navigator.geolocation.clearWatch(watchIdRef.current);
      }
      releaseWakeLock();
    };
  }, []);

  // Aktuell wirksamer Genauigkeits-Schwellwert: startet bei maxAccuracyMeters
  // und lockert sich zeitgesteuert Richtung relaxedMaxAccuracyMeters, wenn
  // längere Zeit kein Punkt akzeptiert wurde (siehe RELAX_AFTER_MS).
  function effectiveMaxAccuracy(): number {
    if (relaxedMaxAccuracyMeters <= maxAccuracyMeters) return maxAccuracyMeters;
    const waitedMs = Date.now() - lastAcceptedAtRef.current;
    if (waitedMs <= RELAX_AFTER_MS) return maxAccuracyMeters;
    const extraMeters = Math.floor((waitedMs - RELAX_AFTER_MS) / RELAX_STEP_MS) + 1;
    return Math.min(maxAccuracyMeters + extraMeters, relaxedMaxAccuracyMeters);
  }

  // Verarbeitet eine einzelne GPS-Messung: Accuracy-Filter, Kalman-Glättung,
  // Mindestabstands-Filter, Punkt anhängen. Wird aus dem watchPosition-
  // Callback aufgerufen - kein zusätzlicher Poll-Loop mehr.
  function handlePosition(position: GeolocationPosition) {
    if (!isRecordingRef.current) return;
    const { latitude, longitude, accuracy } = position.coords;
    setCurrentAccuracy(accuracy);

    if (accuracy > effectiveMaxAccuracy()) {
      // GPS noch nicht eingeschwungen (typisch beim Kaltstart ohne aGPS).
      // Der Schwellwert lockert sich über die Zeit (effectiveMaxAccuracy),
      // damit die Aufzeichnung ohne Netz nicht dauerhaft leer bleibt.
      return;
    }

    // Genauigkeitsfilter passiert - Referenzzeitpunkt zurücksetzen, auch
    // wenn der Punkt gleich noch am Mindestabstand scheitert (Stillstand
    // ist kein Grund, den Schwellwert zu lockern: das GPS liefert ja).
    lastAcceptedAtRef.current = Date.now();

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
      return;
    }

    lastRecordedRef.current = { latitude: smoothedLat, longitude: smoothedLon };
    const recordedPosition = kalman
      ? withSmoothedCoords(position, smoothedLat, smoothedLon)
      : position;
    setPoints((prev) => [...prev, toPoint(recordedPosition)]);
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
    lastAcceptedAtRef.current = Date.now();
    setIsRecording(true);
    isRecordingRef.current = true;

    // Einziger GPS-Datenzugriff während der Aufzeichnung. watchPosition
    // lässt das OS die Sample-Rate intern batchen - im Gegensatz zu einem
    // eigenen 500 ms-Poll mit getCurrentPosition, der den Chip auf voller
    // Leistung hielt und der Hauptverbraucher der Akkulaufzeit war.
    watchIdRef.current = navigator.geolocation.watchPosition(
      handlePosition,
      (error) => {
        if (!isRecordingRef.current) return;
        toast.error(`GPS-Fehler: ${error.message}`);
      },
      { enableHighAccuracy: true, maximumAge: GPS_MAX_POSITION_AGE_MS },
    );

    // Display während der Aufzeichnung wach halten (fire-and-forget).
    acquireWakeLock();
  }

  function stop() {
    isRecordingRef.current = false;
    if (watchIdRef.current !== null) {
      navigator.geolocation.clearWatch(watchIdRef.current);
      watchIdRef.current = null;
    }
    releaseWakeLock();
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
      { enableHighAccuracy: true, maximumAge: MARK_POINT_MAX_POSITION_AGE_MS },
    );
  }

  return { isRecording, points, setPoints, currentAccuracy, start, stop, markPoint };
}

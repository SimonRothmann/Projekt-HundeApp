"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";

// maximumAge: wie alt eine vom Browser zwischengespeicherte Position
// maximal sein darf, um statt einer frischen GPS-Messung wiederverwendet zu
// werden. 0 erzwingt bei jedem watchPosition-Tick eine frische Messung -
// das erhöht die tatsächliche Punktdichte der aufgezeichneten Fährtenlinie
// spürbar gegenüber einem Wert > 0, bei dem der Browser kurz aufeinander
// folgende Ticks sonst mit derselben (zwischengespeicherten) Position
// beantworten kann.
const GPS_MAX_POSITION_AGE_MS = 0;

/**
 * Gemeinsame GPS-Aufzeichnungslogik (watchPosition/getCurrentPosition-
 * Lifecycle inkl. Cleanup beim Unmount) für Fährten- und
 * Ablauf-Aufzeichnung - vorher dreifach nahezu identisch dupliziert in
 * fahrte-recorder.tsx, gps-track-section.tsx und walk-run-recorder.tsx.
 */
export function useGpsRecorder<T>(toPoint: (position: GeolocationPosition) => T) {
  const [isRecording, setIsRecording] = useState(false);
  const [points, setPoints] = useState<T[]>([]);
  const watchIdRef = useRef<number | null>(null);

  useEffect(() => {
    // Falls die Seite verlassen wird, während noch aufgezeichnet wird (z.B.
    // Navigation ohne vorher "Stoppen" zu klicken): GPS-Watch beenden, sonst
    // läuft watchPosition unbegrenzt im Hintergrund weiter und ruft den
    // Callback einer längst unmounteten Komponente weiter auf - mit jeder
    // vergessenen Aufnahme ein weiterer, nie endender hochfrequenter
    // GPS-Listener, der die App zunehmend träge macht.
    return () => {
      if (watchIdRef.current !== null) {
        navigator.geolocation.clearWatch(watchIdRef.current);
      }
    };
  }, []);

  function start() {
    if (!("geolocation" in navigator)) {
      toast.error("Geolocation wird von diesem Browser nicht unterstützt.");
      return;
    }
    setPoints([]);
    setIsRecording(true);
    watchIdRef.current = navigator.geolocation.watchPosition(
      (position) => setPoints((prev) => [...prev, toPoint(position)]),
      (error) => toast.error(`GPS-Fehler: ${error.message}`),
      { enableHighAccuracy: true, maximumAge: GPS_MAX_POSITION_AGE_MS },
    );
  }

  function stop() {
    if (watchIdRef.current !== null) {
      navigator.geolocation.clearWatch(watchIdRef.current);
      watchIdRef.current = null;
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

  return { isRecording, points, setPoints, start, stop, markPoint };
}

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

// Punkte ungenauer als dieser Schwellwert werden verworfen. Ohne aGPS
// (Assisted GPS über Mobilfunk/WLAN) — also offline — braucht der GPS-Chip
// einen Kaltstart, der mehrere Sekunden dauert und in dieser Zeit Positionen
// mit 50–200 m Ungenauigkeit liefert. Diese frühen Schlechtpunkte sind die
// häufigste Ursache für Ausreißer im aufgezeichneten Track.
const MAX_ACCURACY_METERS = 25;

// Mindestabstand zwischen zwei aufeinanderfolgenden automatischen Trackpunkten.
// Ist das Gerät nahezu still, liefert der GPS-Chip trotzdem leicht
// abweichende Koordinaten (thermisches Rauschen ~1–3 m) - ohne diesen Filter
// entsteht ein "Wackeln" rund um den Standpunkt, das die Tracklänge künstlich
// aufbläht und den Track optisch unruhig macht.
const MIN_DISTANCE_METERS = 2;

/**
 * Gemeinsame GPS-Aufzeichnungslogik (Poll-Loop inkl. Cleanup beim Unmount)
 * für Fährten- und Ablauf-Aufzeichnung - vorher dreifach nahezu identisch
 * dupliziert in fahrte-recorder.tsx, gps-track-section.tsx und
 * walk-run-recorder.tsx.
 */
export function useGpsRecorder<T>(toPoint: (position: GeolocationPosition) => T) {
  const [isRecording, setIsRecording] = useState(false);
  const [points, setPoints] = useState<T[]>([]);
  const [currentAccuracy, setCurrentAccuracy] = useState<number | null>(null);
  const isRecordingRef = useRef(false);
  const timeoutRef = useRef<number | null>(null);
  // Rohkoordinaten des letzten aufgezeichneten Punkts für den Mindestabstand-Filter.
  const lastRawRef = useRef<{ latitude: number; longitude: number } | null>(null);

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

        if (accuracy > MAX_ACCURACY_METERS) {
          // GPS noch nicht eingeschwungen (typisch beim Kaltstart ohne aGPS) -
          // Punkt verwerfen und auf bessere Genauigkeit warten.
          scheduleNextPoll();
          return;
        }

        const lastRaw = lastRawRef.current;
        if (lastRaw !== null && haversineMeters(lastRaw, { latitude, longitude }) < MIN_DISTANCE_METERS) {
          // Zu nah am letzten Punkt - GPS-Rauschen im Stillstand, nicht aufzeichnen.
          scheduleNextPoll();
          return;
        }

        lastRawRef.current = { latitude, longitude };
        setPoints((prev) => [...prev, toPoint(position)]);
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
    lastRawRef.current = null;
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

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, GpsWalkPoint, GpsWalkRun } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Footprints, Square } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { estimateLengthMeters } from "@/lib/geo";
import { useGpsRecorder } from "@/lib/use-gps-recorder";
import { primeHapticsAudio, useWalkRunHaptics } from "@/lib/use-walk-run-haptics";

// Stabile Leerreferenz für den Idle-Fall (keine Aufzeichnung läuft) - siehe
// onLivePointsChange-Effect. Ein Inline-[] wäre bei jedem Render neu.
const EMPTY_WALK_POINTS: GpsWalkPoint[] = [];

function toWalkPoint(position: GeolocationPosition): GpsWalkPoint {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    timestamp: new Date(position.timestamp).toISOString(),
    accuracy: position.coords.accuracy,
  };
}

/**
 * Zeichnet einen Ablauf-Versuch für eine bereits gelegte Fährte auf (siehe
 * TODO.md "Fährte erneut ablaufen können"): separate Aufzeichnung, die als
 * eigene Linie zum Vergleich mit der gelegten Fährte auf der Karte erscheint
 * (siehe TrackMap), statt die ursprünglichen Punkte zu überschreiben.
 *
 * Die Live-Punkte werden per `onLivePointsChange` an den Aufrufer gemeldet -
 * so kann er die eine, gemeinsame Karte der Fährte um die frisch entstehende
 * Ablauf-Linie ergänzen, statt eine zweite Karte anzuzeigen.
 */
export function WalkRunRecorder({
  trackId,
  onSaved,
  onLivePointsChange,
  laidTrackPoints,
}: {
  trackId: string;
  onSaved: () => Promise<void>;
  // (trackId, points): der Aufrufer kann so EINEN stabilen Callback für alle
  // Tracks nutzen, statt pro Track eine neue Funktion zu erzeugen - Letzteres
  // führte zu einer Endlos-Render-Schleife (Effect unten hängt von
  // onLivePointsChange ab).
  onLivePointsChange?: (trackId: string, points: GpsWalkPoint[]) => void;
  // Punkte der gelegten Fährte - Grundlage für die Vibration vor
  // Gegenständen und Abbiegungen. Optional, damit Aufrufer, die keine
  // Legung haben, den Recorder ohne Haptics-Nebeneffekt nutzen können.
  laidTrackPoints?: GpsPoint[];
}) {
  const { isRecording, points, setPoints, currentAccuracy, start: startRecording, stop } = useGpsRecorder(toWalkPoint);
  // Optionaler Kommentar zu diesem Ablauf-Versuch (z.B. "bei Regen", "Hund
  // hat an Winkel 2 verloren") - wird beim Stoppen mitgespeichert.
  const [comment, setComment] = useState("");

  useEffect(() => {
    // EMPTY_WALK_POINTS ist eine stabile Referenz (Modul-Konstante), damit der
    // Idle-Fall NICHT bei jedem Render ein neues [] meldet und so einen
    // Update-Loop im Aufrufer auslöst.
    onLivePointsChange?.(trackId, isRecording ? points : EMPTY_WALK_POINTS);
  }, [points, isRecording, onLivePointsChange, trackId]);

  // Vibriere 10 Schritte (~8 m) vor jedem markierten Gegenstand und vor
  // jeder erkannten Abbiegung der Legung. sessionKey = trackId+isRecording:
  // beim Neustart der Aufnahme wird die "schon-vibriert"-Menge zurückgesetzt.
  useWalkRunHaptics(
    laidTrackPoints,
    isRecording && points.length > 0 ? points[points.length - 1] : null,
    `${trackId}:${isRecording ? "on" : "off"}`,
  );

  async function stopRecording() {
    stop();

    if (points.length === 0) {
      toast.error("Keine GPS-Punkte aufgezeichnet.");
      return;
    }

    const path = `/api/gps-tracks/${trackId}/walk-runs`;
    const payload = { lengthMeters: estimateLengthMeters(points), comment: comment.trim() || null, points };

    try {
      await api.post<GpsWalkRun>(path, payload);
      toast.success("Ablauf-Versuch gespeichert.");
      setPoints([]);
      setComment("");
      await onSaved();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        // Netzwerkfehler (offline) - siehe PRODUCT_REQUIREMENTS.md "Offline": GPS speichern ohne Internet.
        await enqueueRequest({ path, method: "POST", body: payload, label: "Ablauf-Versuch" });
        toast.success("Ablauf-Versuch offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
        setPoints([]);
        setComment("");
      }
    }
  }

  if (!isRecording) {
    return (
      <Button
        size="sm"
        variant="outline"
        onClick={() => {
          // Muss aus dem Klick-Handler synchron passieren, sonst bleibt der
          // AudioContext auf iOS suspended und die späteren Alarmtöne bleiben stumm.
          primeHapticsAudio();
          startRecording();
        }}
      >
        <Footprints className="size-4" />
        Fährte erneut ablaufen
      </Button>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <Button size="sm" variant="destructive" onClick={stopRecording}>
          <Square className="size-4" />
          Stoppen ({points.length} Punkte)
        </Button>
        {currentAccuracy !== null && (
          <span
            className={`text-xs font-mono tabular-nums ${
              currentAccuracy <= 10 ? "text-green-600" : currentAccuracy <= 25 ? "text-yellow-600" : "text-red-600"
            }`}
            title="GPS-Genauigkeit (Radius des Fehlerkreises)."
          >
            ±{Math.round(currentAccuracy)} m
          </span>
        )}
      </div>
      <Input
        className="sm:max-w-xs"
        placeholder="Kommentar zum Ablauf (optional)"
        value={comment}
        onChange={(e) => setComment(e.target.value)}
      />
    </div>
  );
}

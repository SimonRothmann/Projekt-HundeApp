"use client";

import { useEffect } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsWalkPoint, GpsWalkRun } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Footprints, Square } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { estimateLengthMeters } from "@/lib/geo";
import { useGpsRecorder } from "@/lib/use-gps-recorder";

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
}: {
  trackId: string;
  onSaved: () => Promise<void>;
  onLivePointsChange?: (points: GpsWalkPoint[]) => void;
}) {
  const { isRecording, points, setPoints, currentAccuracy, start: startRecording, stop } = useGpsRecorder(toWalkPoint);

  useEffect(() => {
    onLivePointsChange?.(isRecording ? points : []);
  }, [points, isRecording, onLivePointsChange]);

  async function stopRecording() {
    stop();

    if (points.length === 0) {
      toast.error("Keine GPS-Punkte aufgezeichnet.");
      return;
    }

    const path = `/api/gps-tracks/${trackId}/walk-runs`;
    const payload = { lengthMeters: estimateLengthMeters(points), comment: null, points };

    try {
      await api.post<GpsWalkRun>(path, payload);
      toast.success("Ablauf-Versuch gespeichert.");
      setPoints([]);
      await onSaved();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        // Netzwerkfehler (offline) - siehe PRODUCT_REQUIREMENTS.md "Offline": GPS speichern ohne Internet.
        await enqueueRequest({ path, method: "POST", body: payload, label: "Ablauf-Versuch" });
        toast.success("Ablauf-Versuch offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
        setPoints([]);
      }
    }
  }

  if (!isRecording) {
    return (
      <Button size="sm" variant="outline" onClick={startRecording}>
        <Footprints className="size-4" />
        Fährte erneut ablaufen
      </Button>
    );
  }

  return (
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
  );
}

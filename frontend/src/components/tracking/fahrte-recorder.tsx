"use client";

import { useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MapPin, MapPinPlus, Square } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { estimateLengthMeters } from "@/lib/geo";
import { useGpsRecorder } from "@/lib/use-gps-recorder";
import { TrackMap } from "@/components/tracking/track-map";

function toAutomaticPoint(position: GeolocationPosition): GpsPoint {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    timestamp: new Date(position.timestamp).toISOString(),
    accuracy: position.coords.accuracy,
    pointType: 0,
    label: null,
  };
}

/**
 * Direkter Einstiegspunkt für die GPS-Fährtenaufzeichnung, ohne vorher ein
 * vollständiges Trainingstagebuch-Training mit bewerteten Übungen anlegen
 * zu müssen (Training + Fährte werden hier in einem Schritt erzeugt). Die
 * Session-Id wird clientseitig erzeugt, damit beide Requests unabhängig
 * voneinander - auch offline - synchronisiert werden können.
 */
export function FahrteRecorder({ dogId, onSaved }: { dogId: string; onSaved: () => Promise<void> }) {
  const { isRecording, points, setPoints, currentAccuracy, start, stop, markPoint } = useGpsRecorder(
    toAutomaticPoint,
    // Fährte braucht höhere Präzision als ein normaler Spaziergang: Erstaufnahme
    // und Wiederholung sollen deckungsgleich sein. Schwellwert 8 m verwirft
    // Kaltstart-Schlechtpunkte früh; EMA α=0.35 reduziert das Grundrauschen
    // des GPS-Chips (3-5 m) durch gewichtete Mittelung um weitere ~50 %.
    { maxAccuracyMeters: 8, smoothAlpha: 0.35 },
  );
  const [surface, setSurface] = useState("");
  const [markLabel, setMarkLabel] = useState("");
  const [isMarking, setIsMarking] = useState(false);
  const startedAtRef = useRef<number>(0);

  function startRecording() {
    startedAtRef.current = Date.now();
    start();
  }

  function markObject() {
    setIsMarking(true);
    markPoint(
      (point) => {
        setPoints((prev) => [...prev, { ...point, pointType: 1, label: markLabel.trim() || null }]);
        setMarkLabel("");
        setIsMarking(false);
        toast.success("Gegenstand markiert.");
      },
      () => setIsMarking(false),
    );
  }

  async function stopRecording() {
    stop();

    if (points.length === 0) {
      toast.error("Keine GPS-Punkte aufgezeichnet.");
      return;
    }

    const durationMinutes = Math.max(1, Math.round((Date.now() - startedAtRef.current) / 60000));
    const sessionId = crypto.randomUUID();

    const sessionPayload = {
      id: sessionId,
      dogId,
      date: new Date().toISOString().slice(0, 10),
      durationMinutes,
      notes: "Fährtenaufnahme",
      exercises: [],
    };
    const trackPayload = {
      trainingSessionId: sessionId,
      lengthMeters: estimateLengthMeters(points),
      ageMinutes: null,
      surface: surface || null,
      weather: null,
      wind: null,
      comment: null,
      points,
    };

    try {
      await api.post<TrainingSession>("/api/trainings", sessionPayload);
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
        return;
      }
      await enqueueRequest({ path: "/api/trainings", method: "POST", body: sessionPayload, label: "Fährten-Training" });
      toast.success("Training offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
    }

    try {
      await api.post("/api/gps-tracks", trackPayload);
      toast.success("Fährte gespeichert.");
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: trackPayload, label: "Fährte" });
        toast.success("Fährte offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
      }
    }

    setPoints([]);
    setSurface("");
    await onSaved();
  }

  return (
    <Card className="border-primary/40">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <MapPin className="size-5 text-primary" />
          Fährte aufnehmen
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {!isRecording ? (
          <Button onClick={startRecording} className="self-start">
            <MapPin className="size-4" />
            Aufnahme starten
          </Button>
        ) : (
          <>
            <div className="flex flex-col gap-2 sm:w-64">
              <Label htmlFor="fahrte-surface">Untergrund (optional)</Label>
              <Input
                id="fahrte-surface"
                placeholder="z.B. Wiese, Acker, Wald"
                value={surface}
                onChange={(e) => setSurface(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
              <div className="flex flex-col gap-2 sm:w-64">
                <Label htmlFor="fahrte-mark-label">Gegenstand markieren (optional)</Label>
                <Input
                  id="fahrte-mark-label"
                  placeholder="z.B. Schussstelle, Apportel"
                  value={markLabel}
                  onChange={(e) => setMarkLabel(e.target.value)}
                />
              </div>
              <Button type="button" variant="outline" onClick={markObject} disabled={isMarking}>
                <MapPinPlus className="size-4" />
                {isMarking ? "Markiere…" : "Punkt setzen"}
              </Button>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              {currentAccuracy !== null && (
                <span
                  className={`text-xs font-mono tabular-nums ${
                    currentAccuracy <= 8
                      ? "text-green-600"
                      : currentAccuracy <= 15
                        ? "text-yellow-600"
                        : "text-red-600"
                  }`}
                  title="GPS-Genauigkeit (Fehlerkreis-Radius). Punkte ungenauer als 8 m werden für Fährten automatisch verworfen."
                >
                  ±{Math.round(currentAccuracy)} m
                </span>
              )}
              <Button variant="destructive" onClick={stopRecording} className="self-start">
                <Square className="size-4" />
                Stoppen ({points.filter((p) => p.pointType !== 1).length} Punkte,{" "}
                {points.filter((p) => p.pointType === 1).length} Marker)
              </Button>
            </div>
            <TrackMap points={points} live />
          </>
        )}
      </CardContent>
    </Card>
  );
}

"use client";

import { useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MapPin, Square } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";

/**
 * Direkter Einstiegspunkt für die GPS-Fährtenaufzeichnung, ohne vorher ein
 * vollständiges Trainingstagebuch-Training mit bewerteten Übungen anlegen
 * zu müssen (Training + Fährte werden hier in einem Schritt erzeugt). Die
 * Session-Id wird clientseitig erzeugt, damit beide Requests unabhängig
 * voneinander - auch offline - synchronisiert werden können.
 */
export function FahrteRecorder({ dogId, onSaved }: { dogId: string; onSaved: () => Promise<void> }) {
  const [isRecording, setIsRecording] = useState(false);
  const [points, setPoints] = useState<GpsPoint[]>([]);
  const [surface, setSurface] = useState("");
  const watchIdRef = useRef<number | null>(null);
  const startedAtRef = useRef<number>(0);

  function startRecording() {
    if (!("geolocation" in navigator)) {
      toast.error("Geolocation wird von diesem Browser nicht unterstützt.");
      return;
    }
    setPoints([]);
    startedAtRef.current = Date.now();
    setIsRecording(true);
    watchIdRef.current = navigator.geolocation.watchPosition(
      (position) => {
        const point: GpsPoint = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          timestamp: new Date(position.timestamp).toISOString(),
          accuracy: position.coords.accuracy,
        };
        setPoints((prev) => [...prev, point]);
      },
      (error) => toast.error(`GPS-Fehler: ${error.message}`),
      { enableHighAccuracy: true, maximumAge: 1000 },
    );
  }

  async function stopRecording() {
    if (watchIdRef.current !== null) {
      navigator.geolocation.clearWatch(watchIdRef.current);
      watchIdRef.current = null;
    }
    setIsRecording(false);

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
            <Button variant="destructive" onClick={stopRecording} className="self-start">
              <Square className="size-4" />
              Stoppen ({points.length} Punkte)
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function estimateLengthMeters(points: GpsPoint[]): number {
  let total = 0;
  for (let i = 1; i < points.length; i++) {
    total += haversineMeters(points[i - 1], points[i]);
  }
  return Math.round(total);
}

function haversineMeters(a: GpsPoint, b: GpsPoint): number {
  const earthRadiusMeters = 6371000;
  const toRad = (deg: number) => (deg * Math.PI) / 180;
  const dLat = toRad(b.latitude - a.latitude);
  const dLon = toRad(b.longitude - a.longitude);
  const sinDLat = Math.sin(dLat / 2);
  const sinDLon = Math.sin(dLon / 2);
  const h = sinDLat * sinDLat + Math.cos(toRad(a.latitude)) * Math.cos(toRad(b.latitude)) * sinDLon * sinDLon;
  return 2 * earthRadiusMeters * Math.asin(Math.sqrt(h));
}

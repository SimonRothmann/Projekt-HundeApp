"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, GpsTrack } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TrackMap } from "@/components/tracking/track-map";
import { MapPin, Square } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";

export function GpsTrackSection({ trainingSessionId }: { trainingSessionId: string }) {
  const [tracks, setTracks] = useState<GpsTrack[] | null>(null);
  const [isRecording, setIsRecording] = useState(false);
  const [recordedPoints, setRecordedPoints] = useState<GpsPoint[]>([]);
  const [surface, setSurface] = useState("");
  const watchIdRef = useRef<number | null>(null);

  async function loadTracks() {
    try {
      const data = await api.get<GpsTrack[]>(`/api/gps-tracks?trainingSessionId=${trainingSessionId}`);
      setTracks(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Fährten konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadTracks();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trainingSessionId]);

  useEffect(() => {
    // Falls die Seite verlassen wird, während noch aufgezeichnet wird (z.B.
    // Navigation ohne vorher "Stoppen" zu klicken): GPS-Watch beenden, sonst
    // läuft watchPosition unbegrenzt im Hintergrund weiter - mit jeder
    // vergessenen Aufnahme ein weiterer, nie endender hochfrequenter
    // GPS-Listener, der die App zunehmend träge macht.
    return () => {
      if (watchIdRef.current !== null) {
        navigator.geolocation.clearWatch(watchIdRef.current);
      }
    };
  }, []);

  function startRecording() {
    if (!("geolocation" in navigator)) {
      toast.error("Geolocation wird von diesem Browser nicht unterstützt.");
      return;
    }
    setRecordedPoints([]);
    setIsRecording(true);
    watchIdRef.current = navigator.geolocation.watchPosition(
      (position) => {
        const point: GpsPoint = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          timestamp: new Date(position.timestamp).toISOString(),
          accuracy: position.coords.accuracy,
        };
        setRecordedPoints((prev) => [...prev, point]);
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

    if (recordedPoints.length === 0) {
      toast.error("Keine GPS-Punkte aufgezeichnet.");
      return;
    }

    const payload = {
      trainingSessionId,
      lengthMeters: estimateLengthMeters(recordedPoints),
      ageMinutes: null,
      surface: surface || null,
      weather: null,
      wind: null,
      comment: null,
      points: recordedPoints,
    };

    try {
      await api.post<GpsTrack>("/api/gps-tracks", payload);
      toast.success("Fährte gespeichert.");
      setRecordedPoints([]);
      setSurface("");
      await loadTracks();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        // Netzwerkfehler (offline) - siehe PRODUCT_REQUIREMENTS.md "Offline": GPS speichern ohne Internet.
        await enqueueRequest({ path: "/api/gps-tracks", method: "POST", body: payload, label: "Fährte" });
        toast.success("Fährte offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
        setRecordedPoints([]);
        setSurface("");
      }
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border p-3">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-semibold">Fährte</h4>
        {!isRecording ? (
          <Button size="sm" variant="outline" onClick={startRecording}>
            <MapPin className="size-4" />
            Aufnahme starten
          </Button>
        ) : (
          <Button size="sm" variant="destructive" onClick={stopRecording}>
            <Square className="size-4" />
            Stoppen ({recordedPoints.length} Punkte)
          </Button>
        )}
      </div>

      {isRecording && (
        <div className="flex flex-col gap-2">
          <Label htmlFor={`surface-${trainingSessionId}`}>Untergrund</Label>
          <Input
            id={`surface-${trainingSessionId}`}
            placeholder="z.B. Wiese, Acker, Wald"
            value={surface}
            onChange={(e) => setSurface(e.target.value)}
          />
        </div>
      )}

      {tracks === null ? (
        <p className="text-sm text-muted-foreground">Lädt…</p>
      ) : tracks.length === 0 ? (
        <p className="text-sm text-muted-foreground">Noch keine Fährte für dieses Training.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {tracks.map((track) => (
            <div key={track.id} className="flex flex-col gap-2">
              <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                {track.lengthMeters && <span>{Math.round(track.lengthMeters)} m</span>}
                {track.surface && <span>{track.surface}</span>}
                {track.comment && <span>{track.comment}</span>}
              </div>
              <TrackMap points={track.points} />
            </div>
          ))}
        </div>
      )}
    </div>
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

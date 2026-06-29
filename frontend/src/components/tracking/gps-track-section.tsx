"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, GpsTrack } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TrackMap } from "@/components/tracking/track-map";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";
import { MapPin, MapPinPlus, Square } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { estimateLengthMeters } from "@/lib/geo";
import { useGpsRecorder } from "@/lib/use-gps-recorder";

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

export function GpsTrackSection({ trainingSessionId }: { trainingSessionId: string }) {
  const [tracks, setTracks] = useState<GpsTrack[] | null>(null);
  const {
    isRecording,
    points: recordedPoints,
    setPoints: setRecordedPoints,
    start: startRecording,
    stop,
    markPoint,
  } = useGpsRecorder(toAutomaticPoint);
  const [surface, setSurface] = useState("");
  const [markLabel, setMarkLabel] = useState("");
  const [isMarking, setIsMarking] = useState(false);

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

  function markObject() {
    setIsMarking(true);
    markPoint(
      (point) => {
        setRecordedPoints((prev) => [...prev, { ...point, pointType: 1, label: markLabel.trim() || null }]);
        setMarkLabel("");
        setIsMarking(false);
        toast.success("Gegenstand markiert.");
      },
      () => setIsMarking(false),
    );
  }

  async function stopRecording() {
    stop();

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
            Stoppen ({recordedPoints.filter((p) => p.pointType !== 1).length} Punkte,{" "}
            {recordedPoints.filter((p) => p.pointType === 1).length} Marker)
          </Button>
        )}
      </div>

      {isRecording && (
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2">
            <Label htmlFor={`surface-${trainingSessionId}`}>Untergrund</Label>
            <Input
              id={`surface-${trainingSessionId}`}
              placeholder="z.B. Wiese, Acker, Wald"
              value={surface}
              onChange={(e) => setSurface(e.target.value)}
            />
          </div>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
            <div className="flex flex-col gap-2 sm:w-64">
              <Label htmlFor={`mark-label-${trainingSessionId}`}>Gegenstand markieren (optional)</Label>
              <Input
                id={`mark-label-${trainingSessionId}`}
                placeholder="z.B. Schussstelle, Apportel"
                value={markLabel}
                onChange={(e) => setMarkLabel(e.target.value)}
              />
            </div>
            <Button type="button" size="sm" variant="outline" onClick={markObject} disabled={isMarking}>
              <MapPinPlus className="size-4" />
              {isMarking ? "Markiere…" : "Punkt setzen"}
            </Button>
          </div>
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
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                  {track.lengthMeters && <span>{Math.round(track.lengthMeters)} m</span>}
                  {track.surface && <span>{track.surface}</span>}
                  {track.comment && <span>{track.comment}</span>}
                </div>
                <WalkRunRecorder trackId={track.id} onSaved={loadTracks} />
              </div>
              <TrackMap points={track.points} walkRuns={track.walkRuns} />
              {track.walkRuns.length > 0 && (
                <p className="text-xs text-muted-foreground">
                  {track.walkRuns.length} Ablauf-Versuch(e) - gestrichelt auf der Karte zum Vergleich.
                </p>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

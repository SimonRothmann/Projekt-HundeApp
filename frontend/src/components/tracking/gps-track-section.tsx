"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, GpsTrack, GpsWalkPoint } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TrackMap } from "@/components/tracking/track-map";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";
import { MapPin, MapPinPlus, Square, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { estimateLengthMeters } from "@/lib/geo";
import { useGpsRecorder } from "@/lib/use-gps-recorder";

// Für Zeitangaben in der Fährten-Übersicht: nur automatische Trackpunkte,
// nicht die manuell gesetzten Marker (die tragen ggf. einen späteren
// Zeitstempel und würden die Legezeit verzerren).
function trackTimes(points: GpsPoint[]): { start: Date; durationMs: number } | null {
  const auto = points.filter((p) => p.pointType !== 1);
  if (auto.length < 2) return null;
  const start = new Date(auto[0].timestamp);
  const end = new Date(auto[auto.length - 1].timestamp);
  return { start, durationMs: end.getTime() - start.getTime() };
}

function walkRunDurationMs(run: { points: { timestamp: string }[] }): number | null {
  if (run.points.length < 2) return null;
  return new Date(run.points[run.points.length - 1].timestamp).getTime() - new Date(run.points[0].timestamp).getTime();
}

function formatTime(d: Date): string {
  return d.toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" });
}

function formatDate(d: Date): string {
  return d.toLocaleDateString("de-DE", { day: "2-digit", month: "2-digit", year: "numeric" });
}

function formatDuration(ms: number): string {
  const totalSec = Math.max(0, Math.round(ms / 1000));
  const min = Math.floor(totalSec / 60);
  const sec = totalSec % 60;
  return `${min}:${sec.toString().padStart(2, "0")} min`;
}

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

// readOnly: für abgeschlossene Trainings (Datum in der Vergangenheit). Dann
// entfällt die komplette Aufnahme-Funktion (neue Fährte legen UND erneutes
// Ablaufen) - eine Live-GPS-Aufzeichnung ergibt nur für das laufende Training
// von heute Sinn. Bereits gespeicherte Fährten bleiben als Karten sichtbar.
export function GpsTrackSection({
  trainingSessionId,
  readOnly = false,
}: {
  trainingSessionId: string;
  readOnly?: boolean;
}) {
  const [tracks, setTracks] = useState<GpsTrack[] | null>(null);
  // Live-Punkte des gerade laufenden Ablauf-Versuchs, pro Track-Id. Die
  // Karte des jeweiligen Tracks zeigt diese Punkte zusätzlich zur Legung -
  // so entsteht die neue Linie in der SELBEN Karte, statt in einer zweiten.
  const [liveWalkPoints, setLiveWalkPoints] = useState<Record<string, GpsWalkPoint[]>>({});

  // EINE stabile Funktion für alle Tracks (trackId als Parameter statt
  // curried) - eine curried Variante `handleLivePoints(track.id)` erzeugte pro
  // Render eine neue Funktion, wodurch der onLivePointsChange-Effect im
  // WalkRunRecorder bei jedem Render feuerte und einen Endlos-Update-Loop
  // ("Maximum update depth exceeded") auslöste, der den Router blockierte.
  // Der Gleichheits-Guard verhindert zusätzlich einen State-Update, wenn sich
  // die Referenz nicht geändert hat (Idle-Fall meldet die stabile Leerliste).
  const handleLivePoints = useCallback((trackId: string, points: GpsWalkPoint[]) => {
    setLiveWalkPoints((prev) => (prev[trackId] === points ? prev : { ...prev, [trackId]: points }));
  }, []);
  const {
    isRecording,
    points: recordedPoints,
    setPoints: setRecordedPoints,
    currentAccuracy,
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

  // Abgeschlossenes Training ohne Fährte: nichts anzeigen (kein leerer Block,
  // da hier weder aufgezeichnet werden kann noch etwas zu sehen ist).
  if (readOnly && tracks !== null && tracks.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border p-3">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-semibold">Fährte</h4>
        {readOnly ? null : !isRecording ? (
          <Button size="sm" variant="outline" onClick={startRecording}>
            <MapPin className="size-4" />
            Aufnahme starten
          </Button>
        ) : (
          <div className="flex items-center gap-2">
            {currentAccuracy !== null && (
              <span
                className={`text-xs font-mono tabular-nums ${
                  currentAccuracy <= 10
                    ? "text-green-600"
                    : currentAccuracy <= 25
                      ? "text-yellow-600"
                      : "text-red-600"
                }`}
                title="GPS-Genauigkeit (Radius des Fehlerkreises). Punkte ungenauer als 25 m werden automatisch verworfen."
              >
                ±{Math.round(currentAccuracy)} m
              </span>
            )}
            <Button size="sm" variant="destructive" onClick={stopRecording}>
              <Square className="size-4" />
              Stoppen ({recordedPoints.filter((p) => p.pointType !== 1).length} Punkte,{" "}
              {recordedPoints.filter((p) => p.pointType === 1).length} Marker)
            </Button>
          </div>
        )}
      </div>

      {!readOnly && isRecording && (
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
          <TrackMap points={recordedPoints} live />
        </div>
      )}

      {tracks === null ? (
        <p className="text-sm text-muted-foreground">Lädt…</p>
      ) : tracks.length === 0 ? (
        <p className="text-sm text-muted-foreground">Noch keine Fährte für dieses Training.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {tracks.map((track) => {
            const times = trackTimes(track.points);
            return (
              <div key={track.id} className="flex flex-col gap-2">
                <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                  {times && (
                    <span title={`${formatDate(times.start)} ${formatTime(times.start)}`}>
                      Gelegt {formatTime(times.start)} · Dauer {formatDuration(times.durationMs)}
                    </span>
                  )}
                  {track.lengthMeters && <span>{Math.round(track.lengthMeters)} m</span>}
                  {track.surface && <span>{track.surface}</span>}
                  {track.comment && <span>{track.comment}</span>}
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  {!readOnly && (
                    <WalkRunRecorder
                      trackId={track.id}
                      onSaved={loadTracks}
                      onLivePointsChange={handleLivePoints}
                      laidTrackPoints={track.points}
                    />
                  )}
                  <Button
                    size="sm"
                    variant="ghost"
                    className="text-destructive hover:text-destructive"
                    onClick={async () => {
                      if (!confirm("Fährte wirklich löschen?")) return;
                      try {
                        await api.delete(`/api/gps-tracks/${track.id}`);
                        toast.success("Fährte gelöscht.");
                        await loadTracks();
                      } catch (err) {
                        toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
                      }
                    }}
                  >
                    <Trash2 className="size-4" />
                    Löschen
                  </Button>
                </div>
                <TrackMap
                  points={track.points}
                  walkRuns={track.walkRuns}
                  liveWalkRunPoints={liveWalkPoints[track.id]}
                />
                {track.walkRuns.length > 0 && (
                  <ul className="text-xs text-muted-foreground flex flex-col gap-0.5">
                    {track.walkRuns.map((run, i) => {
                      const durMs = walkRunDurationMs(run);
                      const started = new Date(run.createdAt);
                      return (
                        <li key={run.id}>
                          Ablauf {i + 1}: gestartet {formatTime(started)}
                          {durMs !== null && ` · abgelaufen in ${formatDuration(durMs)}`}
                          {run.lengthMeters !== null && ` · ${Math.round(run.lengthMeters)} m`}
                        </li>
                      );
                    })}
                  </ul>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

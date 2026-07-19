"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, GpsTrack, GpsWalkPoint } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { TrackMap } from "@/components/tracking/track-map";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";
import { WalkRunComment } from "@/components/tracking/walk-run-comment";
import { Trash2 } from "lucide-react";
import { toast } from "sonner";

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

/**
 * Zeigt die Fährten eines Trainings (Karte, Metadaten, Ablauf-Versuche).
 * Das AUFNEHMEN neuer Fährten passiert bewusst NICHT mehr hier, sondern
 * ausschließlich über den "Fährte aufnehmen"-Recorder oberhalb des
 * Tagebuchs (FahrteRecorder) - die aufgenommene Fährte erscheint dann als
 * Bestandteil des jeweiligen Trainingstags (siehe SessionHistory).
 *
 * readOnly (Trainingstag in der Vergangenheit): blendet zusätzlich das
 * erneute Ablaufen aus - eine Live-GPS-Aufzeichnung ergibt nur für den
 * heutigen Trainingstag Sinn. Kommentare zu Ablauf-Versuchen bleiben immer
 * bearbeitbar (nachträgliche Notiz, keine Aufzeichnung).
 */
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

  // Ohne Fährte nichts anzeigen: aufgenommen wird hier nicht mehr (siehe
  // Klassenkommentar), ein leerer Block hätte also keinen Zweck.
  if (tracks === null || tracks.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border p-3">
      <h4 className="text-sm font-semibold">Fährte</h4>

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
                <ul className="text-xs text-muted-foreground flex flex-col gap-1">
                  {track.walkRuns.map((run, i) => {
                    const durMs = walkRunDurationMs(run);
                    const started = new Date(run.createdAt);
                    return (
                      <li key={run.id} className="flex flex-col gap-0.5">
                        <span>
                          Ablauf {i + 1}: gestartet {formatTime(started)}
                          {durMs !== null && ` · abgelaufen in ${formatDuration(durMs)}`}
                          {run.lengthMeters !== null && ` · ${Math.round(run.lengthMeters)} m`}
                        </span>
                        <WalkRunComment trackId={track.id} run={run} onSaved={loadTracks} />
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

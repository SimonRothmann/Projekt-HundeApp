"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ChevronDown, ChevronRight, History, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { GpsTrackSection } from "@/components/tracking/gps-track-section";
import { TrainerFeedback } from "@/components/dogs/trainer-feedback";

// Monatsschlüssel im Format "2026-07" für die Gruppierung; toLocaleDateString
// mit month:"long" liefert die Anzeige-Version ("Juli 2026").
function monthKey(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, "0")}`;
}
function monthLabel(iso: string): string {
  return new Date(iso).toLocaleDateString("de-DE", { month: "long", year: "numeric" });
}

// Ein Training gilt als abgeschlossen, sobald sein Datum in der Vergangenheit
// liegt (vor dem heutigen Tag). Für solche Trainings entfällt die
// Fährten-Aufnahme - eine Live-GPS-Aufzeichnung ergibt nur für das Training
// von heute Sinn.
function isCompletedSession(iso: string): boolean {
  const d = new Date(iso);
  d.setHours(0, 0, 0, 0);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return d.getTime() < today.getTime();
}

/**
 * Trainingshistorie, nach Monat gruppiert (neuester zuerst, automatisch
 * aufgeklappt). Aus der Hundeseite herausgelöst (siehe TODO.md Roadmap 5b,
 * gleiches Muster wie der goals-section-Refactor).
 *
 * GpsTrackSection wird nur noch gemountet, wenn es dort etwas zu tun gibt:
 * bei laufenden Trainings (heute - Aufnahme möglich) oder wenn laut
 * hasGpsTrack tatsächlich eine Fährte existiert. Das beseitigt das
 * HTTP-N+1 der alten Seite (ein GPS-Request pro Trainings-Karte, auch wenn
 * es nichts anzuzeigen gab).
 *
 * onLoadOlder: lädt die komplette Historie nach (initial sind nur die
 * letzten 3 Monate geladen) - null, wenn bereits alles geladen ist.
 */
export function SessionHistory({
  sessions,
  isOwner,
  onChanged,
  onLoadOlder,
}: {
  sessions: TrainingSession[] | null;
  isOwner: boolean;
  onChanged: () => Promise<void>;
  onLoadOlder: (() => Promise<void>) | null;
}) {
  // Aufgeklappte Monate. Der neueste Monat wird beim Erstrender automatisch
  // aufgeklappt, ältere bleiben zunächst zu.
  const [openMonths, setOpenMonths] = useState<Set<string>>(new Set());
  const [loadingOlder, setLoadingOlder] = useState(false);

  async function deleteSession(sessionId: string) {
    if (!confirm("Training wirklich löschen? Zugehörige Fährten werden ebenfalls entfernt.")) return;
    try {
      await api.delete(`/api/trainings/${sessionId}`);
      toast.success("Training gelöscht.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  async function handleLoadOlder() {
    if (!onLoadOlder) return;
    setLoadingOlder(true);
    try {
      await onLoadOlder();
    } finally {
      setLoadingOlder(false);
    }
  }

  if (sessions === null) return <p className="text-muted-foreground">Lädt…</p>;

  if (sessions.length === 0) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-muted-foreground">
          Noch keine Trainingseinheiten erfasst.
        </CardContent>
      </Card>
    );
  }

  // Nach Monat gruppieren, Reihenfolge: neuester Monat zuerst.
  // Sessions kommen sortiert nach Datum absteigend vom Backend.
  const groups = new Map<string, TrainingSession[]>();
  for (const s of sessions) {
    const key = monthKey(s.date);
    const list = groups.get(key);
    if (list) list.push(s);
    else groups.set(key, [s]);
  }
  const orderedKeys = Array.from(groups.keys());
  // Neuesten Monat automatisch aufklappen, sofern der Nutzer die
  // Sichtbarkeit noch nicht selbst gesteuert hat.
  const effectiveOpen = openMonths.size === 0 && orderedKeys.length > 0 ? new Set([orderedKeys[0]]) : openMonths;

  function toggleMonth(key: string) {
    setOpenMonths((prev) => {
      const next = new Set(prev.size === 0 ? [orderedKeys[0]] : prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <div className="flex flex-col gap-2">
      {orderedKeys.map((key) => {
        const list = groups.get(key)!;
        const isOpen = effectiveOpen.has(key);
        return (
          <div key={key} className="rounded-md border">
            <button
              type="button"
              onClick={() => toggleMonth(key)}
              className="flex w-full items-center justify-between px-3 py-2 text-left"
            >
              <span className="flex items-center gap-2 font-medium capitalize">
                {isOpen ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                {monthLabel(list[0].date)}
              </span>
              <Badge variant="secondary">{list.length}</Badge>
            </button>
            {isOpen && (
              <div className="flex flex-col gap-3 border-t p-3">
                {list.map((session) => {
                  const completed = isCompletedSession(session.date);
                  return (
                    <Card key={session.id}>
                      <CardHeader className="flex-row items-center justify-between space-y-0">
                        <CardTitle className="text-base">
                          {new Date(session.date).toLocaleDateString("de-DE")}
                        </CardTitle>
                        <div className="flex items-center gap-2">
                          <Badge variant="secondary">{session.durationMinutes} Min.</Badge>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="text-destructive hover:text-destructive"
                            onClick={() => deleteSession(session.id)}
                            title="Training löschen"
                          >
                            <Trash2 className="size-4" />
                          </Button>
                        </div>
                      </CardHeader>
                      <CardContent className="flex flex-col gap-2">
                        {session.notes && <p className="text-sm text-muted-foreground">{session.notes}</p>}
                        <ul className="flex flex-col gap-1">
                          {session.exercises.map((ex) => (
                            <li key={ex.id} className="flex items-center justify-between text-sm">
                              <span>{ex.exerciseName}</span>
                              <span className="text-muted-foreground">
                                {"★".repeat(ex.rating)}
                                {"☆".repeat(5 - ex.rating)} {ex.success ? "✓" : "✗"}
                              </span>
                            </li>
                          ))}
                        </ul>
                        {(session.hasGpsTrack || !completed) && (
                          <GpsTrackSection trainingSessionId={session.id} readOnly={completed} />
                        )}
                        <TrainerFeedback session={session} isOwner={isOwner} onUpdated={onChanged} />
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            )}
          </div>
        );
      })}
      {onLoadOlder && (
        <Button variant="outline" size="sm" className="self-center" onClick={handleLoadOlder} disabled={loadingOlder}>
          <History className="size-4" />
          {loadingOlder ? "Lädt…" : "Ältere Trainings anzeigen"}
        </Button>
      )}
    </div>
  );
}

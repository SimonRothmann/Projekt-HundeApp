"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainerSessionToRate } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ClipboardCheck, MessageSquarePlus, Pencil } from "lucide-react";
import { toast } from "sonner";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

/**
 * Trainerseite: alle offenen Trainings der betreuten Hunde in EINER Ansicht -
 * je Trainingstag das Gesamt-Feedback und alle Übungen, ohne ins Tagebuch des
 * jeweiligen Hundes zu wechseln. Ein Training verschwindet, sobald Feedback
 * gegeben UND alle Übungen bewertet sind.
 *
 * Auf Überblick gebaut, nicht auf Vollständigkeit:
 * - nach Hund gruppiert, der Name steht einmal statt über jedem Training;
 * - je Training ein Zähler "2/3 bewertet", je Übung EINE Zeile;
 * - fertig Bewertetes tritt zurück (gedämpft), Offenes bleibt sichtbar.
 * Vorher stand der Hundename über jeder Karte, jede Übung brauchte zwei Zeilen
 * plus einen Knopf, und nirgends stand, wie viel überhaupt noch offen ist.
 */

/** Was an einem Training noch fehlt - Grundlage für Zähler und Sortierung. */
function openCount(session: TrainerSessionToRate): number {
  const unrated = session.exercises.filter((e) => e.trainerRating === null).length;
  return unrated + (session.trainerFeedback ? 0 : 1);
}

export function TrainerReviewSection() {
  const [sessions, setSessions] = useState<TrainerSessionToRate[] | null>(null);
  const [openFeedbackId, setOpenFeedbackId] = useState<string | null>(null);
  const [feedbackText, setFeedbackText] = useState("");
  const [savingFeedback, setSavingFeedback] = useState(false);

  async function load() {
    try {
      const data = await api.get<TrainerSessionToRate[]>("/api/trainings/trainer/sessions");
      setSessions(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Trainings konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, []);

  function startFeedback(sessionId: string, current: string | null) {
    setOpenFeedbackId(sessionId);
    setFeedbackText(current ?? "");
  }

  async function saveFeedback(sessionId: string) {
    if (!feedbackText.trim()) return;
    setSavingFeedback(true);
    try {
      await api.put(`/api/trainings/${sessionId}/feedback`, { feedback: feedbackText });
      toast.success("Feedback gespeichert.");
      setOpenFeedbackId(null);
      await load();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Feedback konnte nicht gespeichert werden.");
    } finally {
      setSavingFeedback(false);
    }
  }

  // Nach Hund gruppieren, Reihenfolge der Hunde nach dem ältesten offenen
  // Training: was am längsten wartet, steht oben.
  const byDog = new Map<string, { handlerName: string; sessions: TrainerSessionToRate[] }>();
  for (const s of sessions ?? []) {
    const entry = byDog.get(s.dogName) ?? { handlerName: s.handlerName, sessions: [] };
    entry.sessions.push(s);
    byDog.set(s.dogName, entry);
  }
  for (const entry of byDog.values()) entry.sessions.sort((a, b) => a.date.localeCompare(b.date));

  const totalOpen = (sessions ?? []).reduce((sum, s) => sum + openCount(s), 0);

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <ClipboardCheck className="size-5" />
          Trainings bewerten
        </CardTitle>
        {totalOpen > 0 && <Badge variant="secondary">{totalOpen} offen</Badge>}
      </CardHeader>
      <CardContent>
        {sessions === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Keine offenen Trainings – alle betreuten Trainings sind bewertet und kommentiert.
          </p>
        ) : (
          <div className="flex flex-col gap-4">
            {Array.from(byDog.entries()).map(([dogName, { handlerName, sessions: dogSessions }]) => (
              <div key={dogName} className="flex min-w-0 flex-col gap-2">
                <p className="flex flex-wrap items-baseline gap-x-1.5 text-sm font-semibold">
                  <span className="[overflow-wrap:anywhere]">{dogName}</span>
                  <span className="text-xs font-normal text-muted-foreground [overflow-wrap:anywhere]">
                    {handlerName}
                  </span>
                </p>

                {dogSessions.map((s) => {
                  const rated = s.exercises.filter((e) => e.trainerRating !== null).length;
                  const open = openCount(s);
                  return (
                    <div key={s.sessionId} className="min-w-0 rounded-md border p-2.5">
                      <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
                        <span className="text-xs text-muted-foreground">
                          {new Date(s.date).toLocaleDateString("de-DE")} · {s.durationMinutes} Min.
                        </span>
                        <Badge variant={open === 0 ? "secondary" : "outline"} className="shrink-0">
                          {s.exercises.length > 0
                            ? `${rated}/${s.exercises.length} bewertet`
                            : "nur Feedback"}
                        </Badge>
                      </div>

                      {s.exercises.length > 0 && (
                        <ul className="mt-2 flex flex-col gap-2">
                          {s.exercises.map((ex) => (
                            <li
                              key={ex.exerciseId}
                              // Bewertetes tritt zurück, damit das Auge beim
                              // Offenen hängen bleibt.
                              className={`flex min-w-0 flex-col gap-0.5 ${ex.trainerRating !== null ? "opacity-60" : ""}`}
                            >
                              <span className="flex flex-wrap items-baseline justify-between gap-x-2 text-sm">
                                <span className="min-w-0 [overflow-wrap:anywhere]">{ex.exerciseName}</span>
                                {/* Selbsteinschätzung des Hundeführers - klein
                                    und gedämpft, sie ist hier nur Kontext. */}
                                <span className="shrink-0 text-xs text-muted-foreground">
                                  Selbst: {"★".repeat(ex.rating)}
                                  {"☆".repeat(5 - ex.rating)} {ex.success ? "✓" : "✗"}
                                </span>
                              </span>
                              <ExerciseTrainerRating
                                exerciseId={ex.exerciseId}
                                rating={ex.trainerRating}
                                note={ex.trainerNote}
                                canEdit
                                onSaved={load}
                              />
                            </li>
                          ))}
                        </ul>
                      )}

                      <div className="mt-2 border-t pt-2">
                        {openFeedbackId === s.sessionId ? (
                          <div className="flex flex-col gap-2">
                            <textarea
                              className="min-h-16 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
                              value={feedbackText}
                              onChange={(e) => setFeedbackText(e.target.value)}
                              placeholder="Gesamt-Feedback zu diesem Training…"
                              autoFocus
                            />
                            <div className="flex gap-2 self-start">
                              <Button size="sm" onClick={() => saveFeedback(s.sessionId)} disabled={savingFeedback}>
                                Speichern
                              </Button>
                              <Button size="sm" variant="ghost" onClick={() => setOpenFeedbackId(null)}>
                                Abbrechen
                              </Button>
                            </div>
                          </div>
                        ) : s.trainerFeedback ? (
                          <div className="flex min-w-0 items-start gap-1 text-xs opacity-60">
                            <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">„{s.trainerFeedback}“</span>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="size-6 shrink-0"
                              onClick={() => startFeedback(s.sessionId, s.trainerFeedback)}
                              title="Feedback bearbeiten"
                            >
                              <Pencil className="size-3" />
                            </Button>
                          </div>
                        ) : (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 px-2 text-xs text-muted-foreground"
                            onClick={() => startFeedback(s.sessionId, null)}
                          >
                            <MessageSquarePlus className="size-3.5" />
                            Gesamt-Feedback geben
                          </Button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainerSessionToRate } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ClipboardCheck, MessageSquarePlus, Pencil } from "lucide-react";
import { toast } from "sonner";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

/**
 * Trainerseite: alle offenen Trainings der betreuten Hunde in EINER Ansicht -
 * pro Trainingstag das Gesamt-Feedback UND alle Übungen mit Sterne-Bewertung,
 * damit der Trainer alles auf einen Blick bewerten kann, ohne ins Tagebuch des
 * jeweiligen Hundes zu wechseln. Ein Training verschwindet, sobald Gesamt-
 * Feedback gegeben UND alle Übungen bewertet sind (Neuladen nach jeder Aktion).
 */
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

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ClipboardCheck className="size-5" />
          Trainings bewerten
        </CardTitle>
      </CardHeader>
      <CardContent>
        {sessions === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Keine offenen Trainings - alle betreuten Trainings sind bewertet und kommentiert.
          </p>
        ) : (
          <div className="flex flex-col gap-3">
            {sessions.map((s) => (
              <div key={s.sessionId} className="rounded-md border p-3">
                <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
                  <span className="text-sm font-medium">
                    {s.dogName} <span className="font-normal text-muted-foreground">({s.handlerName})</span>
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {new Date(s.date).toLocaleDateString("de-DE")} · {s.durationMinutes} Min.
                  </span>
                </div>

                <div className="mt-2">
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
                    <div className="flex items-start gap-1 text-sm">
                      <span className="shrink-0 text-xs font-medium text-muted-foreground">Gesamt-Feedback:</span>
                      <span className="flex-1">{s.trainerFeedback}</span>
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

                {s.exercises.length > 0 && (
                  <ul className="mt-2 flex flex-col gap-2 border-t pt-2">
                    {s.exercises.map((ex) => (
                      <li key={ex.exerciseId} className="flex flex-col gap-0.5">
                        <div className="flex items-center justify-between text-sm">
                          <span>{ex.exerciseName}</span>
                          <span className="text-muted-foreground">
                            {"★".repeat(ex.rating)}
                            {"☆".repeat(5 - ex.rating)} {ex.success ? "✓" : "✗"}
                          </span>
                        </div>
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
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

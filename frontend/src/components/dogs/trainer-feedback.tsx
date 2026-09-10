"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { BlockLabel } from "@/components/ui/block-label";
import { MessageSquare } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
export function TrainerFeedback({
  session,
  isOwner,
  onUpdated,
}: {
  session: TrainingSession;
  isOwner: boolean;
  onUpdated: () => Promise<void>;
}) {
  const t = useT();
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(session.trainerFeedback ?? "");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!text.trim()) return;
    setSubmitting(true);
    try {
      await api.put(`/api/trainings/${session.id}/feedback`, { feedback: text });
      toast.success(t("Feedback gespeichert."));
      setEditing(false);
      await onUpdated();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Feedback konnte nicht gespeichert werden."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    // Vorhandenes Feedback ist eine fremde Stimme im eigenen Tagebuch und
    // bekommt deshalb eine eigene, farbig abgesetzte Fläche. Fehlt es, bleibt
    // der gestrichelte Rahmen: dann ist der Block eine Leerstelle, kein Inhalt.
    <div
      className={
        session.trainerFeedback && !editing
          ? "flex flex-col gap-1.5 rounded-lg border border-l-2 border-primary/25 border-l-primary bg-primary/6 p-3 dark:border-primary/40 dark:bg-primary/15"
          : "rounded-lg border border-dashed border-surface-border p-3"
      }
    >
      {session.trainerFeedback && !editing ? (
        <>
          <div className="flex items-start justify-between gap-2">
            <BlockLabel icon={MessageSquare}>{t("Trainer-Feedback")}</BlockLabel>
            {!isOwner && (
              <Button size="sm" variant="ghost" className="h-6 shrink-0 px-2 text-xs" onClick={() => setEditing(true)}>
{t("Bearbeiten")}
              </Button>
            )}
          </div>
          <p className="text-sm [overflow-wrap:anywhere]">{session.trainerFeedback}</p>
        </>
      ) : !isOwner ? (
        <form onSubmit={handleSubmit} className="flex flex-col gap-2">
          <BlockLabel icon={MessageSquare}>{t("Feedback geben")}</BlockLabel>
          <textarea
            className="min-h-16 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder={t("Rückmeldung zu diesem Training…")}
          />
          <div className="flex gap-2 self-start">
            <Button type="submit" size="sm" disabled={submitting}>
{t("Speichern")}
            </Button>
            {editing && (
              <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
{t("Abbrechen")}
              </Button>
            )}
          </div>
        </form>
      ) : (
        <p className="text-sm text-muted-foreground">{t("Noch kein Trainer-Feedback zu diesem Training.")}</p>
      )}
    </div>
  );
}

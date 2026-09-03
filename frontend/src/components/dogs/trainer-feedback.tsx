"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
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
    <div className="rounded-md border border-dashed p-3">
      {session.trainerFeedback && !editing ? (
        <div className="flex items-start gap-2">
          <MessageSquare className="mt-0.5 size-4 shrink-0 text-primary" />
          <div className="flex-1">
            <p className="text-xs font-medium text-muted-foreground">{t("Trainer-Feedback")}</p>
            <p className="text-sm">{session.trainerFeedback}</p>
          </div>
          {!isOwner && (
            <Button size="sm" variant="ghost" onClick={() => setEditing(true)}>
{t("Bearbeiten")}
            </Button>
          )}
        </div>
      ) : !isOwner ? (
        <form onSubmit={handleSubmit} className="flex flex-col gap-2">
          <p className="text-xs font-medium text-muted-foreground">Feedback geben</p>
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

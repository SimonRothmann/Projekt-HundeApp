"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Check, MessageSquarePlus, Pencil, X } from "lucide-react";
import { toast } from "sonner";

/**
 * Zeigt die Notiz einer durchgeführten Übung (TrainingExercise.Notes) und
 * erlaubt, sie inline zu bearbeiten. Bewusst geteilt zwischen
 * Trainingstagebuch (SessionHistory) und Trainingsplan-Log (GoalPlanCard),
 * damit dieselbe Notiz an beiden Stellen sichtbar UND editierbar ist
 * (siehe Wunsch 2). `compact` verkleinert die Darstellung fürs Plan-Log.
 */
export function ExerciseNotes({
  exerciseId,
  notes,
  onSaved,
  compact = false,
}: {
  exerciseId: string;
  notes: string | null;
  onSaved: () => Promise<void>;
  compact?: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(notes ?? "");
  const [saving, setSaving] = useState(false);

  async function save() {
    setSaving(true);
    try {
      await api.put(`/api/trainings/exercises/${exerciseId}/notes`, { notes: value.trim() || null });
      setEditing(false);
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Notiz konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  if (editing) {
    return (
      <span className="flex items-center gap-1">
        <Input
          className="h-7 text-xs"
          placeholder="Notiz zur Übung"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          autoFocus
        />
        <Button size="icon" variant="ghost" className="size-7" onClick={save} disabled={saving} title="Speichern">
          <Check className="size-3.5" />
        </Button>
        <Button
          size="icon"
          variant="ghost"
          className="size-7"
          onClick={() => {
            setValue(notes ?? "");
            setEditing(false);
          }}
          title="Abbrechen"
        >
          <X className="size-3.5" />
        </Button>
      </span>
    );
  }

  // Ohne Notiz: unaufdringlicher "Notiz hinzufügen"-Knopf.
  if (!notes) {
    return (
      <Button
        size="sm"
        variant="ghost"
        className={compact ? "h-6 px-1 text-[11px] text-muted-foreground" : "h-7 self-start px-2 text-xs text-muted-foreground"}
        onClick={() => {
          setValue("");
          setEditing(true);
        }}
      >
        <MessageSquarePlus className="size-3.5" />
        Notiz
      </Button>
    );
  }

  return (
    <span className={`inline-flex items-start gap-1 ${compact ? "text-[11px]" : "text-xs"} text-muted-foreground`}>
      <span className="italic">„{notes}“</span>
      <Button
        size="icon"
        variant="ghost"
        className="size-5 shrink-0"
        onClick={() => {
          setValue(notes ?? "");
          setEditing(true);
        }}
        title="Notiz bearbeiten"
      >
        <Pencil className="size-3" />
      </Button>
    </span>
  );
}

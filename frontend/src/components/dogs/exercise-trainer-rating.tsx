"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Check, Pencil, Star, X } from "lucide-react";
import { toast } from "sonner";

/**
 * Strukturierte Trainer-Bewertung einer Übung (1-5 Sterne + optionale Notiz),
 * getrennt von der Selbstbewertung des Besitzers (siehe TrainingExercise.rating
 * vs. trainerRating). Nur ein für den Hund zugewiesener Trainer (canEdit) darf
 * sie setzen; der Besitzer sieht sie ausschließlich lesend. Ohne vorhandene
 * Bewertung UND ohne Editierrecht wird nichts gerendert.
 */
export function ExerciseTrainerRating({
  exerciseId,
  rating,
  note,
  canEdit,
  onSaved,
}: {
  exerciseId: string;
  rating: number | null;
  note: string | null;
  canEdit: boolean;
  onSaved: () => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(rating ?? 0);
  const [noteValue, setNoteValue] = useState(note ?? "");
  const [saving, setSaving] = useState(false);

  async function save() {
    if (value < 1 || value > 5) {
      toast.error("Bitte 1 bis 5 Sterne wählen.");
      return;
    }
    setSaving(true);
    try {
      await api.put(`/api/trainings/exercises/${exerciseId}/trainer-rating`, {
        rating: value,
        note: noteValue.trim() || null,
      });
      setEditing(false);
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Trainer-Bewertung konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  if (editing) {
    return (
      <div className="flex flex-col gap-1 rounded-md border border-dashed p-2">
        <div className="flex items-center gap-1">
          <span className="text-xs font-medium text-muted-foreground">Trainer:</span>
          <span role="group" aria-label="Trainer-Bewertung, 1 bis 5 Sterne" className="inline-flex items-center">
            {[1, 2, 3, 4, 5].map((n) => (
              <button
                key={n}
                type="button"
                onClick={() => setValue(n)}
                aria-label={`${n} von 5`}
                aria-pressed={value === n}
                className="inline-flex size-8 items-center justify-center rounded text-base leading-none text-primary coarse:size-11"
              >
                {n <= value ? "★" : "☆"}
              </button>
            ))}
          </span>
          <Button size="icon" variant="ghost" className="size-7" onClick={save} disabled={saving} title="Speichern">
            <Check className="size-3.5" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            className="size-7"
            onClick={() => {
              setValue(rating ?? 0);
              setNoteValue(note ?? "");
              setEditing(false);
            }}
            title="Abbrechen"
          >
            <X className="size-3.5" />
          </Button>
        </div>
        <Input
          className="h-7 text-xs"
          placeholder="Notiz des Trainers (optional)"
          value={noteValue}
          onChange={(e) => setNoteValue(e.target.value)}
        />
      </div>
    );
  }

  // Besitzer ohne vorhandene Trainer-Bewertung sieht nichts (kein leerer Block).
  if (rating === null) {
    if (!canEdit) return null;
    return (
      <Button
        size="sm"
        variant="ghost"
        className="h-7 self-start px-2 text-xs text-muted-foreground"
        onClick={() => {
          setValue(0);
          setNoteValue("");
          setEditing(true);
        }}
      >
        <Star className="size-3.5" />
        Als Trainer bewerten
      </Button>
    );
  }

  return (
    <div className="flex items-start gap-1 text-xs text-muted-foreground">
      <span className="shrink-0 font-medium">Trainer:</span>
      <span className="text-primary">
        {"★".repeat(rating)}
        {"☆".repeat(5 - rating)}
      </span>
      {note && <span className="italic">„{note}“</span>}
      {canEdit && (
        <Button
          size="icon"
          variant="ghost"
          className="size-5 shrink-0"
          onClick={() => {
            setValue(rating);
            setNoteValue(note ?? "");
            setEditing(true);
          }}
          title="Trainer-Bewertung bearbeiten"
        >
          <Pencil className="size-3" />
        </Button>
      )}
    </div>
  );
}

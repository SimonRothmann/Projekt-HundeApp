"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Check, MessageSquarePlus, X } from "lucide-react";
import { toast } from "sonner";

/**
 * Trainer-Bewertung einer Übung (1-5 Sterne + optionale Notiz), getrennt von
 * der Selbstbewertung des Hundeführers (TrainingExercise.rating vs.
 * trainerRating). Nur ein zugewiesener Trainer (canEdit) darf sie setzen; der
 * Besitzer sieht sie ausschließlich lesend.
 *
 * Ein Tipp genügt: Der Stern IST der Knopf und speichert sofort. Vorher lag
 * die Bewertung hinter "Als Trainer bewerten" -> Stern wählen -> Haken
 * bestätigen, also drei Tipps je Übung. Wer nach dem Gruppentraining zehn
 * Übungen durchgeht, tippt damit dreißig Mal statt zehn.
 *
 * Die Notiz bleibt hinter einem eigenen Knopf - sie ist der seltene Fall.
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
  const [editingNote, setEditingNote] = useState(false);
  const [noteValue, setNoteValue] = useState(note ?? "");
  const [saving, setSaving] = useState(false);
  // Sofort anzeigen, worauf getippt wurde - die Antwort des Servers und das
  // Neuladen der Liste dauern sonst spürbar länger als der Tipp.
  const [optimistic, setOptimistic] = useState<number | null>(null);
  const shown = optimistic ?? rating;

  async function save(nextRating: number, nextNote: string | null) {
    if (nextRating < 1 || nextRating > 5) return;
    setSaving(true);
    setOptimistic(nextRating);
    try {
      await api.put(`/api/trainings/exercises/${exerciseId}/trainer-rating`, {
        rating: nextRating,
        note: nextNote?.trim() || null,
      });
      setEditingNote(false);
      await onSaved();
    } catch (err) {
      setOptimistic(null);
      toast.error(err instanceof ApiError ? err.message : "Trainer-Bewertung konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  // Besitzer ohne vorhandene Trainer-Bewertung sieht nichts (kein leerer Block).
  if (!canEdit && rating === null) return null;

  if (!canEdit) {
    return (
      <span className="flex min-w-0 items-baseline gap-1 text-xs text-muted-foreground">
        <span className="shrink-0 font-medium">Trainer:</span>
        <span className="shrink-0 text-primary">
          {"★".repeat(rating!)}
          {"☆".repeat(5 - rating!)}
        </span>
        {note && <span className="min-w-0 break-words italic">„{note}“</span>}
      </span>
    );
  }

  return (
    <span className="flex min-w-0 flex-col gap-1">
      <span className="flex items-center gap-1">
        <span className="shrink-0 text-xs font-medium text-muted-foreground">Trainer:</span>
        <span role="group" aria-label="Trainer-Bewertung, 1 bis 5 Sterne" className="inline-flex items-center">
          {[1, 2, 3, 4, 5].map((n) => (
            <button
              key={n}
              type="button"
              disabled={saving}
              onClick={() => save(n, note)}
              aria-label={`${n} von 5 Sternen`}
              aria-pressed={shown === n}
              className="inline-flex size-8 items-center justify-center rounded text-base leading-none text-primary disabled:opacity-50 coarse:size-11"
            >
              {shown !== null && n <= shown ? "★" : "☆"}
            </button>
          ))}
        </span>
        {!editingNote && (
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="size-6 shrink-0"
            title={note ? "Notiz bearbeiten" : "Notiz hinzufügen"}
            onClick={() => {
              setNoteValue(note ?? "");
              setEditingNote(true);
            }}
          >
            <MessageSquarePlus className="size-3.5" />
          </Button>
        )}
      </span>

      {note && !editingNote && <span className="min-w-0 break-words text-xs italic text-muted-foreground">„{note}“</span>}

      {editingNote && (
        <span className="flex items-center gap-1">
          <Input
            className="h-8 min-w-0 flex-1 text-xs"
            placeholder="Notiz des Trainers"
            value={noteValue}
            onChange={(e) => setNoteValue(e.target.value)}
            autoFocus
          />
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="size-7 shrink-0"
            title="Notiz speichern"
            // Ohne Sterne keine Notiz: der Endpunkt verlangt eine Bewertung.
            disabled={saving || shown === null}
            onClick={() => save(shown ?? 0, noteValue)}
          >
            <Check className="size-3.5" />
          </Button>
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="size-7 shrink-0"
            title="Abbrechen"
            onClick={() => {
              setNoteValue(note ?? "");
              setEditingNote(false);
            }}
          >
            <X className="size-3.5" />
          </Button>
        </span>
      )}

      {editingNote && shown === null && (
        <span className="text-xs text-muted-foreground">Bitte zuerst Sterne vergeben.</span>
      )}
    </span>
  );
}

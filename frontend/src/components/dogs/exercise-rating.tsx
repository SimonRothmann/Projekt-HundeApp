"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Pencil } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
/**
 * Bewertung und Erfolg einer bereits erfassten Übung - anzeigen und
 * nachträglich korrigieren. Auch für vergangene Tage: das Korrigieren eines
 * Vertippers ist ja gerade dann nötig, wenn das Training schon eine Weile her
 * ist. Wer bearbeiten darf, entscheidet das Backend (HasDogAccessAsync) -
 * genau wie bei der Notiz.
 *
 * Bis hierher stand hier nur Text: wer sich beim Eintragen vertippt hatte,
 * musste den ganzen Trainingstag löschen und neu erfassen. Nur die Notiz war
 * änderbar (siehe ExerciseNotes).
 *
 * Die Notiz wird unverändert mitgeschickt, weil der Endpunkt Bewertung,
 * Erfolg und Notiz gemeinsam setzt - ohne das würde eine Korrektur der
 * Sterne die Notiz löschen.
 */
export function ExerciseRating({
  exerciseId,
  rating,
  success,
  notes,
  onSaved,
}: {
  exerciseId: string;
  rating: number;
  success: boolean;
  notes: string | null;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(rating);
  const [ok, setOk] = useState(success);
  const [saving, setSaving] = useState(false);

  async function save() {
    setSaving(true);
    try {
      await api.put(`/api/trainings/exercises/${exerciseId}`, {
        rating: value,
        success: ok,
        notes,
      });
      toast.success(t("Bewertung geändert."));
      setEditing(false);
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Bewertung konnte nicht geändert werden."));
    } finally {
      setSaving(false);
    }
  }

  if (!editing) {
    return (
      <span className="flex shrink-0 items-center gap-1 text-muted-foreground">
        <span>
          {"★".repeat(rating)}
          {"☆".repeat(5 - rating)} {success ? "✓" : "✗"}
        </span>
        <Button
          type="button"
          size="icon"
          variant="ghost"
          className="size-6"
          title={t("Bewertung ändern")}
          onClick={() => {
            setValue(rating);
            setOk(success);
            setEditing(true);
          }}
        >
          <Pencil className="size-3" />
        </Button>
      </span>
    );
  }

  // Eigene Zeile statt neben dem Namen: die fünf Knöpfe brauchen auf einem
  // schmalen Telefon die volle Breite (Mobile-App-first, nie horizontal
  // scrollen).
  return (
    <span className="flex w-full min-w-0 flex-col gap-2 rounded-md border p-2">
      <span className="flex flex-wrap items-center gap-3">
        <span className="flex gap-1" role="group" aria-label={t("Bewertung, 1 bis 5")}>
          {[1, 2, 3, 4, 5].map((n) => (
            <button
              key={n}
              type="button"
              onClick={() => setValue(n)}
              aria-label={`${n} von 5`}
              aria-pressed={value === n}
              className={`flex size-8 items-center justify-center rounded-md border text-sm coarse:size-11 ${
                value >= n ? "border-accent bg-accent text-accent-foreground" : "border-input text-muted-foreground"
              }`}
            >
              {n}
            </button>
          ))}
        </span>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={ok} onChange={(e) => setOk(e.target.checked)} />
          Erfolgreich
        </label>
      </span>
      <span className="flex justify-end gap-2">
        <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
{t("Abbrechen")}
        </Button>
        <Button size="sm" onClick={save} disabled={saving}>
          {saving ? "Speichert…" : t("Speichern")}
        </Button>
      </span>
    </span>
  );
}

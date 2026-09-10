"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import type { TrainingPlanItem } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

import { useT } from "@/lib/i18n";

/**
 * Schnelleintrag für ein Wochenziel: Bewertung, Erfolg, optional ein
 * Kommentar - und eintragen. Speichert einen minimalen Tagebucheintrag mit
 * Verknüpfung zum Plan-Ziel.
 *
 * Aus GoalPlanCard herausgelöst, weil derselbe Handgriff jetzt auch auf der
 * Startseite angeboten wird ("Diese Woche"). Zwei Formulare mit je eigener
 * Speicherlogik wären die Einladung, dass sie auseinanderlaufen.
 */
export function PlanItemQuickLog({
  dogId,
  item,
  onDone,
  onCancel,
  className,
}: {
  dogId: string;
  item: TrainingPlanItem;
  onDone: () => Promise<void>;
  onCancel: () => void;
  className?: string;
}) {
  const t = useT();
  const [rating, setRating] = useState(5);
  const [success, setSuccess] = useState(true);
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);

  async function submit() {
    setSaving(true);
    try {
      const payload = {
        dogId,
        date: new Date().toISOString().slice(0, 10),
        durationMinutes: 10,
        notes: null,
        exercises: [
          {
            // Freitext-Plan-Ziele (exerciseId null) tragen ihren eigenen
            // Freitext in den Tagebucheintrag - ein früheres
            // `if (!item.exerciseId) return;` ließ den "Eintragen"-Klick
            // für solche Items kommentarlos verpuffen.
            exerciseId: item.exerciseId,
            freeTextLabel: item.exerciseId ? null : item.freeTextLabel,
            rating,
            difficulty: 0,
            success,
            notes: notes || null,
            trainingPlanItemId: item.id,
          },
        ],
      };
      try {
        await api.post("/api/trainings", payload);
        toast.success(t("Eintrag gespeichert."));
      } catch (err) {
        if (err instanceof ApiError) throw err;
        await enqueueRequest({ path: "/api/trainings", method: "POST", body: payload, label: "Schnelleintrag" });
        toast.success(t("Offline gespeichert – wird synchronisiert, sobald Internet verfügbar ist."));
      }
      await onDone();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Eintrag konnte nicht gespeichert werden."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className={cn("flex flex-col gap-2 rounded-md border bg-muted/40 p-2.5", className)}>
      <div className="flex gap-1" role="group" aria-label={t("Bewertung, 1 bis 5")}>
        {[1, 2, 3, 4, 5].map((value) => (
          <button
            key={value}
            type="button"
            onClick={() => setRating(value)}
            aria-label={`${value} von 5`}
            aria-pressed={rating === value}
            className={cn(
              "flex size-7 items-center justify-center rounded-md border text-xs coarse:size-11",
              rating >= value ? "border-accent bg-accent text-accent-foreground" : "border-input text-muted-foreground",
            )}
          >
            {value}
          </button>
        ))}
        <label className="ml-2 flex items-center gap-1.5 text-xs">
          <input type="checkbox" checked={success} onChange={(e) => setSuccess(e.target.checked)} />
          Erfolgreich
        </label>
      </div>
      <Input placeholder="Kommentar (optional)" value={notes} onChange={(e) => setNotes(e.target.value)} />
      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={saving} onClick={submit}>
          {saving ? t("Wird gespeichert…") : "Eintragen"}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

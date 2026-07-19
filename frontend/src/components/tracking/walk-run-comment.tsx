"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsWalkRun } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Check, Pencil, X } from "lucide-react";
import { toast } from "sonner";

/**
 * Zeigt den Kommentar eines Ablauf-Versuchs (GpsWalkRun) und erlaubt, ihn
 * inline zu bearbeiten (siehe Wunsch 1). Auch bei abgeschlossenen Trainings
 * nutzbar - ein Kommentar ist keine Aufzeichnung, sondern eine nachträgliche
 * Notiz zum bereits gelaufenen Versuch.
 */
export function WalkRunComment({
  trackId,
  run,
  onSaved,
}: {
  trackId: string;
  run: GpsWalkRun;
  onSaved: () => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(run.comment ?? "");
  const [saving, setSaving] = useState(false);

  async function save() {
    setSaving(true);
    try {
      await api.put(`/api/gps-tracks/${trackId}/walk-runs/${run.id}`, { comment: value.trim() || null });
      setEditing(false);
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Kommentar konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  if (editing) {
    return (
      <span className="mt-0.5 flex items-center gap-1">
        <Input
          className="h-7 text-xs"
          placeholder="Kommentar zum Ablauf"
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
            setValue(run.comment ?? "");
            setEditing(false);
          }}
          title="Abbrechen"
        >
          <X className="size-3.5" />
        </Button>
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1">
      {run.comment ? <span className="italic">„{run.comment}“</span> : <span className="text-muted-foreground/70">Kein Kommentar</span>}
      <Button
        size="icon"
        variant="ghost"
        className="size-5"
        onClick={() => {
          setValue(run.comment ?? "");
          setEditing(true);
        }}
        title="Kommentar bearbeiten"
      >
        <Pencil className="size-3" />
      </Button>
    </span>
  );
}

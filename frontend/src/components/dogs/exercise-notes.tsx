"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Check, MessageSquarePlus, Pencil } from "lucide-react";
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
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Textarea mit dem Inhalt mitwachsen lassen (bis zu einer Maximalhöhe, danach
  // scrollt sie intern) - so sieht man den kompletten getippten Text umgebrochen
  // statt einzeiliger, horizontal gequetschter Wortfetzen.
  function autoGrow() {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`;
  }
  useEffect(() => {
    if (editing) autoGrow();
  }, [editing]);

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
    // Block-Editor (flex-col) bricht im Textfluss der Plan-Log-Zeile auf eine
    // EIGENE volle Zeile um (unter die Meta). Statt eines einzeiligen, horizontal
    // gequetschten Inputs eine mehrzeilige Textarea, die mit dem Inhalt mitwächst -
    // langer Kommentar bricht um und ist ganz sichtbar (Mobile-App-first).
    return (
      <span className="flex w-full min-w-0 flex-col gap-1.5">
        <textarea
          ref={textareaRef}
          className="max-h-[200px] min-h-16 w-full min-w-0 resize-none rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
          placeholder="Notiz zur Übung"
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            autoGrow();
          }}
          rows={2}
          autoFocus
        />
        <span className="flex items-center justify-end gap-2">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setValue(notes ?? "");
              setEditing(false);
            }}
          >
            Abbrechen
          </Button>
          <Button size="sm" onClick={save} disabled={saving}>
            <Check className="size-3.5" />
            Speichern
          </Button>
        </span>
      </span>
    );
  }

  // Ohne Notiz: unaufdringlicher "Notiz hinzufügen"-Knopf.
  if (!notes) {
    return (
      <Button
        size="sm"
        variant="ghost"
        className={compact ? "h-6 px-1 align-middle text-xs text-muted-foreground" : "h-7 self-start px-2 text-xs text-muted-foreground"}
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

  // compact (Trainingsplan-Log): fließt inline im Textfluss der Zeile mit -
  // der Kommentar steht (bei kurzem Text) neben der Meta bzw. bricht (bei langem
  // Text) um, der Stift folgt direkt am Textende. Kein eigener Flex-Container,
  // damit nichts eine feste Breite erzwingt (kein horizontaler Scroll).
  if (compact) {
    return (
      <>
        <span className="italic">„{notes}“</span>{" "}
        <Button
          size="icon"
          variant="ghost"
          className="inline-flex size-5 shrink-0 align-middle"
          onClick={() => {
            setValue(notes ?? "");
            setEditing(true);
          }}
          title="Notiz bearbeiten"
        >
          <Pencil className="size-3" />
        </Button>
      </>
    );
  }

  // Nicht-compact (Tagebuch): eigener Block, langer Kommentar bricht um.
  return (
    <span className="inline-flex max-w-full items-start gap-1 text-xs text-muted-foreground">
      <span className="min-w-0 break-words italic">„{notes}“</span>
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

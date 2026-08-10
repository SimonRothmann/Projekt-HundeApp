"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { WeightableExercise } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { SlidersHorizontal } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

// 5-Stufen-Regler von "viel seltener" (−2) bis "viel öfter" (+2). 0 = normal
// (setzt die Gewichtung zurück).
const STEPS: { value: number; label: string; short: string }[] = [
  { value: -2, label: "viel seltener", short: "−−" },
  { value: -1, label: "seltener", short: "−" },
  { value: 0, label: "normal", short: "•" },
  { value: 1, label: "öfter", short: "+" },
  { value: 2, label: "viel öfter", short: "++" },
];

const masteryLabel: Record<number, string> = { 0: "neu", 1: "hängt", 2: "mittel", 3: "sitzt" };
const masteryClass: Record<number, string> = {
  0: "text-muted-foreground",
  1: "text-destructive",
  2: "text-amber-600 dark:text-amber-500",
  3: "text-emerald-600 dark:text-emerald-500",
};

/**
 * "Übungen gewichten": eigener Einstieg auf der Ziel-Karte. Der/die Besitzer:in
 * steuert je Prüfungsordnungs-Übung, wie stark der adaptive Generator sie
 * einplant (ManualPriority −2..+2). Wirkt ab dem nächsten Wochen-Neuaufbau;
 * die laufende Woche bleibt unangetastet.
 */
export function ExerciseWeightingSheet({ goalId }: { goalId: string }) {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<WeightableExercise[] | null>(null);
  const [savingId, setSavingId] = useState<string | null>(null);

  async function load() {
    try {
      setItems(await api.get<WeightableExercise[]>(`/api/goals/${goalId}/weightable-exercises`));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
    }
  }

  function onOpenChange(next: boolean) {
    setOpen(next);
    if (next && items === null) load();
  }

  async function setPriority(exerciseId: string, value: number) {
    const previous = items;
    // Optimistisch aktualisieren, dann speichern.
    setItems((list) => list?.map((e) => (e.exerciseId === exerciseId ? { ...e, manualPriority: value } : e)) ?? list);
    setSavingId(exerciseId);
    try {
      await api.put(`/api/goals/${goalId}/exercises/${exerciseId}/priority`, { value });
    } catch (err) {
      setItems(previous ?? null);
      toast.error(err instanceof ApiError ? err.message : "Konnte nicht gespeichert werden.");
    } finally {
      setSavingId(null);
    }
  }

  return (
    <>
      <Button type="button" size="sm" variant="ghost" className="h-6 px-2 text-xs" onClick={() => onOpenChange(true)}>
        <SlidersHorizontal className="size-3" />
        Übungen gewichten
      </Button>
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent side="bottom" className="max-h-[85vh] overflow-y-auto">
          <SheetHeader>
            <SheetTitle>Übungen gewichten</SheetTitle>
            <SheetDescription>
              Steuere, wie oft der Plan eine Übung wählt. Änderungen greifen ab der nächsten Woche.
            </SheetDescription>
          </SheetHeader>
          <div className="flex flex-col gap-3 p-4 pt-0">
            {items === null ? (
              <p className="text-sm text-muted-foreground">Lädt…</p>
            ) : items.length === 0 ? (
              <p className="text-sm text-muted-foreground">Für dieses Ziel gibt es keine gewichtbaren Übungen.</p>
            ) : (
              items.map((e) => (
                <div key={e.exerciseId} className="flex flex-col gap-2 rounded-md border p-3">
                  <div className="flex items-start justify-between gap-2">
                    <span className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">{e.exerciseName}</span>
                    <div className="flex shrink-0 items-center gap-1.5">
                      <span className={cn("text-xs font-medium", masteryClass[e.masteryStatus])}>{masteryLabel[e.masteryStatus]}</span>
                      {e.plannedThisWeek && <Badge variant="secondary">diese Woche</Badge>}
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    {STEPS.map((s) => {
                      const active = e.manualPriority === s.value;
                      return (
                        <button
                          key={s.value}
                          type="button"
                          disabled={savingId === e.exerciseId}
                          aria-pressed={active}
                          title={s.label}
                          onClick={() => setPriority(e.exerciseId, s.value)}
                          className={cn(
                            "flex h-9 flex-1 items-center justify-center rounded-md border text-sm font-medium transition-colors coarse:min-h-11",
                            active
                              ? s.value === 0
                                ? "border-border bg-muted text-foreground"
                                : "border-primary bg-primary/10 text-primary"
                              : "border-border text-muted-foreground hover:bg-muted",
                          )}
                        >
                          {s.short}
                        </button>
                      );
                    })}
                  </div>
                  <p className="text-center text-xs text-muted-foreground">
                    {STEPS.find((s) => s.value === e.manualPriority)?.label ?? "normal"}
                  </p>
                </div>
              ))
            )}
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}

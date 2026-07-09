"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import type { Exercise, Goal, TrainingPlanItem } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { CheckCircle2, Circle, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { difficultyLabel } from "@/lib/constants";

// Eine Woche kann mehrere Plan-Ziele haben (siehe TrainingPlanGenerator
// "ItemsPerWeek") - für die Anzeige nach Wochennummer gruppiert.
function groupByWeek(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byWeek = new Map<number, TrainingPlanItem[]>();
  for (const item of items) {
    const group = byWeek.get(item.weekNumber);
    if (group) group.push(item);
    else byWeek.set(item.weekNumber, [item]);
  }
  return [...byWeek.entries()];
}

const statusLabel: Record<Goal["status"], string> = { 0: "Aktiv", 1: "Erreicht", 2: "Abgebrochen" };
const statusVariant: Record<Goal["status"], "default" | "secondary" | "outline"> = { 0: "default", 1: "secondary", 2: "outline" };

/**
 * Ein einzelnes Ziel mit seinem Wochenplan. Jede Karte hält ihren eigenen
 * Add-/Edit-/QuickLog-State - dadurch gibt es keine goal-übergreifende
 * State-Kopplung mehr (früher lagen addItemGoalId/editItemId/quickLogItemId
 * als Einzelwerte in der Elternkomponente). onChanged lädt die Ziele der
 * Seite neu, sobald sich am Plan/Fortschritt etwas ändert.
 */
export function GoalPlanCard({
  goal,
  dogId,
  onChanged,
}: {
  goal: Goal;
  dogId: string;
  onChanged: () => Promise<void>;
}) {
  // Übungen der Ziel-Sportart, lazy für die Add-/Edit-Auswahl geladen.
  const [exercises, setExercises] = useState<Exercise[] | null>(null);

  async function ensureExercisesLoaded() {
    if (exercises !== null) return;
    try {
      const data = await api.get<Exercise[]>(`/api/sports/${goal.sportId}/exercises`);
      setExercises(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
      setExercises([]);
    }
  }

  // --- Übung hinzufügen (zentral oder inline pro Woche) ---
  const [addForm, setAddForm] = useState<{ location: "central" | "inline"; week: number } | null>(null);
  const [addExerciseId, setAddExerciseId] = useState("");
  const [addFreeText, setAddFreeText] = useState("");
  const [addUseFreeText, setAddUseFreeText] = useState(false);
  const [addWeek, setAddWeek] = useState(1);
  const [addTarget, setAddTarget] = useState(2);
  const [isAdding, setIsAdding] = useState(false);

  async function openAdd(location: "central" | "inline", week: number) {
    const isSame = addForm?.location === location && addForm.week === week;
    if (isSame) {
      setAddForm(null);
      return;
    }
    setAddForm({ location, week });
    setAddWeek(week);
    setAddExerciseId("");
    setAddFreeText("");
    setAddUseFreeText(false);
    setAddTarget(2);
    await ensureExercisesLoaded();
  }

  async function submitAdd() {
    if (addUseFreeText ? !addFreeText.trim() : !addExerciseId) {
      toast.error(addUseFreeText ? "Freitext eingeben." : "Übung auswählen.");
      return;
    }
    setIsAdding(true);
    try {
      await api.post(`/api/goals/${goal.id}/plan-items`, {
        weekNumber: addWeek,
        exerciseId: addUseFreeText ? null : addExerciseId,
        freeTextLabel: addUseFreeText ? addFreeText.trim() : null,
        repetitionsTarget: addTarget,
      });
      toast.success("Übung zum Plan hinzugefügt.");
      setAddForm(null);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übung konnte nicht hinzugefügt werden.");
    } finally {
      setIsAdding(false);
    }
  }

  async function removePlanItem(itemId: string) {
    try {
      await api.delete(`/api/goals/${goal.id}/plan-items/${itemId}`);
      toast.success("Übung aus dem Plan entfernt.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übung konnte nicht entfernt werden.");
    }
  }

  // --- Bestehendes Plan-Ziel bearbeiten ---
  const [editItemId, setEditItemId] = useState<string | null>(null);
  const [editWeek, setEditWeek] = useState(1);
  const [editTarget, setEditTarget] = useState(2);
  const [editExerciseId, setEditExerciseId] = useState("");
  const [editFreeText, setEditFreeText] = useState("");
  const [editUseFreeText, setEditUseFreeText] = useState(false);
  const [isEditing, setIsEditing] = useState(false);

  async function openEdit(item: TrainingPlanItem) {
    const isSame = editItemId === item.id;
    setEditItemId(isSame ? null : item.id);
    if (isSame) return;
    setEditWeek(item.weekNumber);
    setEditTarget(item.repetitionsTarget);
    setEditExerciseId(item.exerciseId ?? "");
    setEditFreeText(item.freeTextLabel ?? "");
    setEditUseFreeText(item.freeTextLabel !== null);
    await ensureExercisesLoaded();
  }

  async function submitEdit(itemId: string) {
    if (editUseFreeText ? !editFreeText.trim() : !editExerciseId) {
      toast.error(editUseFreeText ? "Freitext eingeben." : "Übung auswählen.");
      return;
    }
    setIsEditing(true);
    try {
      await api.put(`/api/goals/${goal.id}/plan-items/${itemId}`, {
        weekNumber: editWeek,
        exerciseId: editUseFreeText ? null : editExerciseId,
        freeTextLabel: editUseFreeText ? editFreeText.trim() : null,
        repetitionsTarget: editTarget,
      });
      toast.success("Plan-Ziel aktualisiert.");
      setEditItemId(null);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Plan-Ziel konnte nicht aktualisiert werden.");
    } finally {
      setIsEditing(false);
    }
  }

  // --- Schnelleintrag "diese Übung gemacht" pro Plan-Ziel ---
  const [quickLogItemId, setQuickLogItemId] = useState<string | null>(null);
  const [qlRating, setQlRating] = useState(5);
  const [qlSuccess, setQlSuccess] = useState(true);
  const [qlNotes, setQlNotes] = useState("");
  const [isQuickLogging, setIsQuickLogging] = useState(false);

  function openQuickLog(itemId: string) {
    setQuickLogItemId((current) => (current === itemId ? null : itemId));
    setQlRating(5);
    setQlSuccess(true);
    setQlNotes("");
  }

  async function submitQuickLog(item: TrainingPlanItem) {
    if (!item.exerciseId) return;
    setIsQuickLogging(true);
    try {
      const payload = {
        dogId,
        date: new Date().toISOString().slice(0, 10),
        durationMinutes: 10,
        notes: null,
        exercises: [
          {
            exerciseId: item.exerciseId,
            rating: qlRating,
            difficulty: 0,
            success: qlSuccess,
            notes: qlNotes || null,
            trainingPlanItemId: item.id,
          },
        ],
      };
      try {
        await api.post("/api/trainings", payload);
        toast.success("Eintrag gespeichert.");
      } catch (err) {
        if (err instanceof ApiError) throw err;
        await enqueueRequest({ path: "/api/trainings", method: "POST", body: payload, label: "Schnelleintrag" });
        toast.success("Offline gespeichert – wird synchronisiert, sobald Internet verfügbar ist.");
      }
      setQuickLogItemId(null);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Eintrag konnte nicht gespeichert werden.");
    } finally {
      setIsQuickLogging(false);
    }
  }

  async function updateStatus(status: 1 | 2) {
    try {
      await api.put<Goal>(`/api/goals/${goal.id}/status`, { status });
      toast.success(status === 1 ? "Ziel als erreicht markiert." : "Ziel abgebrochen.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Status konnte nicht aktualisiert werden.");
    }
  }

  async function deleteGoal() {
    if (!window.confirm("Ziel inkl. Trainingsplan endgültig löschen? Bereits erfasste Trainingseinträge bleiben im Tagebuch erhalten.")) {
      return;
    }
    try {
      await api.delete(`/api/goals/${goal.id}`);
      toast.success("Ziel gelöscht.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ziel konnte nicht gelöscht werden.");
    }
  }

  // Gemeinsames Add-Item-Formular (zentral mit Woche-Feld / inline ohne).
  function renderAddForm(showWeekField: boolean) {
    return (
      <div className="flex flex-col gap-3 rounded-md border bg-muted/30 p-3">
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 accent-primary"
            checked={addUseFreeText}
            onChange={(e) => setAddUseFreeText(e.target.checked)}
          />
          <span>Freitext-Übung (nicht aus dem Katalog)</span>
        </label>
        <div className={cn("grid gap-3", showWeekField ? "sm:grid-cols-3" : "sm:grid-cols-2")}>
          {showWeekField && (
            <div className="flex flex-col gap-2">
              <Label>Woche</Label>
              <Input type="number" min={1} max={12} value={addWeek} onChange={(e) => setAddWeek(Number(e.target.value))} />
            </div>
          )}
          <div className="flex flex-col gap-2">
            <Label>{addUseFreeText ? "Freitext" : "Übung"}</Label>
            {addUseFreeText ? (
              <Input
                value={addFreeText}
                onChange={(e) => setAddFreeText(e.target.value)}
                placeholder="z.B. Kopfarbeit ausprobieren"
                maxLength={150}
                autoFocus
              />
            ) : (
              <Select value={addExerciseId} onValueChange={(value) => setAddExerciseId(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder="Auswählen…" />
                </SelectTrigger>
                {/* max-h-[60vh] + touch-pan-y: Base-UI errechnet die max-height
                    aus der Trigger-Position; bei weit unten sitzendem Trigger
                    auf iOS Safari wird das zu klein und wirkt "nicht scrollbar". */}
                <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
                  {(exercises ?? []).map((ex) => (
                    <SelectItem key={ex.id} value={ex.id}>
                      {ex.name} ({difficultyLabel[ex.difficulty]})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
          <div className="flex flex-col gap-2">
            <Label>Zielwert (x diese Woche)</Label>
            <Input type="number" min={1} max={10} value={addTarget} onChange={(e) => setAddTarget(Number(e.target.value))} />
          </div>
        </div>
        <div className="flex gap-2">
          <Button type="button" size="sm" disabled={isAdding} onClick={submitAdd}>
            {isAdding ? "Wird hinzugefügt…" : "Hinzufügen"}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => setAddForm(null)}>
            Abbrechen
          </Button>
        </div>
      </div>
    );
  }

  return (
    <Card>
      <CardHeader className="flex-row flex-wrap items-start justify-between gap-2 space-y-0">
        <div className="min-w-0">
          <CardTitle className="text-base break-words">
            {goal.sportName}
            {goal.regulationName && <span className="font-normal text-muted-foreground"> · {goal.regulationName}</span>}
          </CardTitle>
          <p className="text-sm text-muted-foreground">Ziel: {new Date(goal.targetDate).toLocaleDateString("de-DE")}</p>
        </div>
        <Badge className="shrink-0" variant={statusVariant[goal.status]}>
          {statusLabel[goal.status]}
        </Badge>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {goal.notes && <p className="text-sm text-muted-foreground">{goal.notes}</p>}

        {goal.trainingPlan && (
          <div className="flex flex-col gap-3">
            {groupByWeek(goal.trainingPlan.items).map(([weekNumber, items]) => (
              <div key={weekNumber} className="flex flex-col gap-1.5 rounded-md border p-2.5">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium text-muted-foreground">KW {weekNumber}</span>
                  {goal.status === 0 && (
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      className="h-6 px-2 text-xs"
                      onClick={() => openAdd("inline", weekNumber)}
                    >
                      <Plus className="size-3" />
                      Übung
                    </Button>
                  )}
                </div>
                {items[0].isRestWeek ? (
                  <span className="text-sm text-muted-foreground">Pause</span>
                ) : (
                  items.map((item) => (
                    <div key={item.id} className="flex flex-col gap-1">
                      <div className="flex items-start gap-1">
                        <button
                          type="button"
                          onClick={() => openQuickLog(item.id)}
                          className="flex min-w-0 flex-1 items-start gap-2 text-left text-sm"
                        >
                          {item.isComplete ? (
                            <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-accent" />
                          ) : (
                            <Circle className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                          )}
                          <span className="flex min-w-0 flex-col">
                            <span className={cn("break-words", item.isComplete && "text-muted-foreground line-through")}>
                              {item.exerciseName ?? item.freeTextLabel}
                            </span>
                            <span className="text-xs text-muted-foreground">
                              {item.freeTextLabel && !item.exerciseName && (
                                <span className="mr-1 rounded bg-muted px-1 py-0.5 text-[10px] uppercase tracking-wide">
                                  Freitext
                                </span>
                              )}
                              {item.completedCount}/{item.repetitionsTarget}x erledigt
                            </span>
                          </span>
                        </button>
                        <div className="flex shrink-0 gap-0.5">
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon-xs"
                            onClick={() => openEdit(item)}
                            title="Übung, Woche oder Zielwert bearbeiten"
                          >
                            <Pencil className="size-3.5 text-muted-foreground" />
                          </Button>
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon-xs"
                            onClick={() => removePlanItem(item.id)}
                            title="Aus dem Plan entfernen"
                          >
                            <Trash2 className="size-3.5 text-muted-foreground" />
                          </Button>
                        </div>
                      </div>
                      {editItemId === item.id && (
                        <div className="ml-6 flex flex-col gap-2 rounded-md border bg-muted/40 p-2.5">
                          <label className="flex items-center gap-2 text-xs">
                            <input
                              type="checkbox"
                              className="size-3.5 accent-primary"
                              checked={editUseFreeText}
                              onChange={(e) => setEditUseFreeText(e.target.checked)}
                            />
                            <span>Freitext-Übung</span>
                          </label>
                          <div className="flex flex-col gap-1">
                            <Label className="text-xs">{editUseFreeText ? "Freitext" : "Übung"}</Label>
                            {editUseFreeText ? (
                              <Input
                                value={editFreeText}
                                onChange={(e) => setEditFreeText(e.target.value)}
                                placeholder="z.B. Kopfarbeit ausprobieren"
                                maxLength={150}
                              />
                            ) : (
                              <Select value={editExerciseId} onValueChange={(v) => setEditExerciseId(v ?? "")}>
                                <SelectTrigger>
                                  <SelectValue placeholder="Auswählen…" />
                                </SelectTrigger>
                                <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
                                  {(exercises ?? []).map((ex) => (
                                    <SelectItem key={ex.id} value={ex.id}>
                                      {ex.name} ({difficultyLabel[ex.difficulty]})
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            )}
                          </div>
                          <div className="grid grid-cols-2 gap-2">
                            <div className="flex flex-col gap-1">
                              <Label className="text-xs">Woche</Label>
                              <Input type="number" min={1} max={12} value={editWeek} onChange={(e) => setEditWeek(Number(e.target.value))} />
                            </div>
                            <div className="flex flex-col gap-1">
                              <Label className="text-xs">Zielwert (x diese Woche)</Label>
                              <Input type="number" min={1} max={10} value={editTarget} onChange={(e) => setEditTarget(Number(e.target.value))} />
                            </div>
                          </div>
                          <div className="flex gap-2">
                            <Button type="button" size="sm" disabled={isEditing} onClick={() => submitEdit(item.id)}>
                              {isEditing ? "Wird gespeichert…" : "Speichern"}
                            </Button>
                            <Button type="button" size="sm" variant="ghost" onClick={() => setEditItemId(null)}>
                              Abbrechen
                            </Button>
                          </div>
                        </div>
                      )}
                      {item.logs.length > 0 && (
                        <ul className="ml-6 flex flex-col gap-0.5 border-l pl-2.5">
                          {item.logs.map((log) => (
                            <li key={`${log.trainingSessionId}-${log.date}`} className="text-xs text-muted-foreground">
                              {new Date(log.date).toLocaleDateString("de-DE")} · {"★".repeat(log.rating)}
                              {"☆".repeat(5 - log.rating)} {log.success ? "✓" : "✗"}
                              {log.notes && <span> · {log.notes}</span>}
                            </li>
                          ))}
                        </ul>
                      )}
                      {quickLogItemId === item.id && (
                        <div className="ml-6 flex flex-col gap-2 rounded-md border bg-muted/40 p-2.5">
                          <div className="flex gap-1">
                            {[1, 2, 3, 4, 5].map((value) => (
                              <button
                                key={value}
                                type="button"
                                onClick={() => setQlRating(value)}
                                className={cn(
                                  "flex size-7 items-center justify-center rounded-md border text-xs",
                                  qlRating >= value
                                    ? "border-accent bg-accent text-accent-foreground"
                                    : "border-input text-muted-foreground",
                                )}
                              >
                                {value}
                              </button>
                            ))}
                            <label className="ml-2 flex items-center gap-1.5 text-xs">
                              <input type="checkbox" checked={qlSuccess} onChange={(e) => setQlSuccess(e.target.checked)} />
                              Erfolgreich
                            </label>
                          </div>
                          <Input placeholder="Kommentar (optional)" value={qlNotes} onChange={(e) => setQlNotes(e.target.value)} />
                          <div className="flex gap-2">
                            <Button type="button" size="sm" disabled={isQuickLogging} onClick={() => submitQuickLog(item)}>
                              {isQuickLogging ? "Wird gespeichert…" : "Eintragen"}
                            </Button>
                            <Button type="button" size="sm" variant="ghost" onClick={() => setQuickLogItemId(null)}>
                              Abbrechen
                            </Button>
                          </div>
                        </div>
                      )}
                    </div>
                  ))
                )}
                {addForm?.location === "inline" && addForm.week === weekNumber && renderAddForm(false)}
              </div>
            ))}
          </div>
        )}

        {goal.status === 0 && (
          <>
            <Button type="button" size="sm" variant="outline" className="self-start" onClick={() => openAdd("central", 1)}>
              <Plus className="size-4" />
              Übung hinzufügen (freie Woche)
            </Button>

            {addForm?.location === "central" && renderAddForm(true)}

            <div className="flex gap-2">
              <Button size="sm" variant="outline" onClick={() => updateStatus(1)}>
                Als erreicht markieren
              </Button>
              <Button size="sm" variant="ghost" onClick={() => updateStatus(2)}>
                Abbrechen
              </Button>
            </div>
          </>
        )}

        {goal.status !== 0 && (
          <Button size="sm" variant="ghost" className="self-start text-destructive hover:text-destructive" onClick={deleteGoal}>
            <Trash2 className="size-4" />
            Ziel löschen
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

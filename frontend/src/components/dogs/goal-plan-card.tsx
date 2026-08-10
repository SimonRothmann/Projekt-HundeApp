"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import type { Exercise, Goal, PlanItemReason, TrainingPlanItem } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { CheckCircle2, ChevronDown, ChevronRight, Circle, Pencil, Plus, RefreshCw, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { difficultyLabel } from "@/lib/constants";
import { ExerciseNotes } from "@/components/dogs/exercise-notes";
import { ExerciseWeightingSheet } from "@/components/dogs/exercise-weighting-sheet";

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

// Bestimmt die aktuelle Trainingswoche kalendarisch: Woche 1 startet mit der
// Plan-Erstellung (generatedAt), jede weitere angebrochene 7-Tage-Woche zählt
// eins hoch. Ergebnis wird auf die tatsächlich vorhandenen Wochennummern
// begrenzt (vor Planstart -> erste Woche, nach Planende -> letzte Woche).
// Fällt auf die erste Woche zurück, wenn kein/ungültiges Startdatum vorliegt.
function computeCurrentWeek(
  weeks: [number, TrainingPlanItem[]][],
  generatedAt: string | undefined,
): number | undefined {
  if (weeks.length === 0) return undefined;
  const weekNumbers = weeks.map(([n]) => n);
  const minWeek = Math.min(...weekNumbers);
  const maxWeek = Math.max(...weekNumbers);

  const start = generatedAt ? new Date(generatedAt).getTime() : NaN;
  if (Number.isNaN(start)) return weekNumbers[0];

  const weekMs = 7 * 24 * 60 * 60 * 1000;
  const byDate = Math.floor((Date.now() - start) / weekMs) + 1;
  const clamped = Math.min(Math.max(byDate, minWeek), maxWeek);
  // Bei (seltenen) Lücken die nächste vorhandene Wochennummer wählen.
  return weekNumbers.includes(clamped)
    ? clamped
    : (weekNumbers.filter((n) => n >= clamped).sort((a, b) => a - b)[0] ?? maxWeek);
}

// Innerhalb einer Woche nach Trainingstag gruppieren (aufsteigend). Wird nur
// als sichtbare "Tag N"-Struktur genutzt, wenn eine Woche tatsächlich mehr als
// einen Trainingstag hat (Alt-Pläne liegen alle auf Tag 1 -> flache Ansicht).
function groupByDay(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byDay = new Map<number, TrainingPlanItem[]>();
  for (const item of items) {
    const group = byDay.get(item.dayIndex);
    if (group) group.push(item);
    else byDay.set(item.dayIndex, [item]);
  }
  return [...byDay.entries()].sort(([a], [b]) => a - b);
}

const statusLabel: Record<Goal["status"], string> = { 0: "Aktiv", 1: "Erreicht", 2: "Abgebrochen" };
const statusVariant: Record<Goal["status"], "default" | "secondary" | "outline"> = { 0: "default", 1: "secondary", 2: "outline" };

// Warum der adaptive Generator eine Übung geplant hat (siehe PlanItemReason).
const reasonLabel: Record<PlanItemReason, string> = { 0: "Schwäche", 1: "Wiederholung", 2: "Neu" };

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

  // Wochen-Akkordeon: standardmäßig eingeklappt, offen ist nur die aktuelle
  // Trainingswoche (erste noch nicht vollständig erledigte, Nicht-Pause-Woche).
  const [openWeeks, setOpenWeeks] = useState<Set<number>>(new Set());
  const [regeneratingWeek, setRegeneratingWeek] = useState<number | null>(null);

  // Plan-Konfiguration (Übungen/Woche, Trainingstage) des adaptiven Generators.
  const [editingConfig, setEditingConfig] = useState(false);
  const [cfgWeekly, setCfgWeekly] = useState(goal.weeklyExerciseCount);
  const [cfgDays, setCfgDays] = useState(goal.trainingDaysPerWeek);
  const [savingConfig, setSavingConfig] = useState(false);

  // Pro-Woche abweichende Trainingstage: welche Woche gerade bearbeitet wird.
  const [editingWeekDays, setEditingWeekDays] = useState<number | null>(null);
  const [weekDaysDraft, setWeekDaysDraft] = useState(2);
  const [savingWeekDays, setSavingWeekDays] = useState(false);

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
  const [addDay, setAddDay] = useState(1);
  const [isAdding, setIsAdding] = useState(false);

  // Effektive Trainingstage einer Woche: Pro-Woche-Überschreibung, sonst
  // der Plan-Default. Bestimmt, wie viele Tage beim Hinzufügen/Bearbeiten
  // einer Übung wählbar sind.
  // Null-Guard auf weekConfigs: der Stale-While-Revalidate-Cache (IndexedDB)
  // kann beim Öffnen zuerst ältere Ziel-Daten OHNE dieses (neuere) Feld
  // liefern - ohne Guard würde .find() auf undefined die Seite crashen.
  const daysForWeek = (week: number) =>
    (goal.weekConfigs ?? []).find((w) => w.weekNumber === week)?.trainingDaysPerWeek ?? goal.trainingDaysPerWeek;

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
    setAddDay(1);
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
        dayIndex: addDay,
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

  // Adaptive Neugenerierung einer Woche (siehe docs/SMART_TRAINING_PLAN.md):
  // ersetzt nur fortschrittslose Auto-Übungen, manuelle Einträge und bereits
  // trainierte Übungen bleiben erhalten.
  async function regenerateWeek(weekNumber: number) {
    setRegeneratingWeek(weekNumber);
    try {
      await api.put(`/api/goals/${goal.id}/regenerate-week`, { weekNumber });
      toast.success(`Woche ${weekNumber} neu generiert.`);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Woche konnte nicht neu generiert werden.");
    } finally {
      setRegeneratingWeek(null);
    }
  }

  function startEditConfig() {
    setCfgWeekly(goal.weeklyExerciseCount);
    setCfgDays(goal.trainingDaysPerWeek);
    setEditingConfig(true);
  }

  function startEditWeekDays(weekNumber: number) {
    setWeekDaysDraft(daysForWeek(weekNumber));
    setEditingWeekDays(weekNumber);
  }

  async function saveWeekDays(weekNumber: number) {
    setSavingWeekDays(true);
    try {
      await api.put(`/api/goals/${goal.id}/weeks/${weekNumber}/config`, { trainingDaysPerWeek: weekDaysDraft });
      toast.success(`Trainingstage für Woche ${weekNumber} gespeichert.`);
      setEditingWeekDays(null);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Trainingstage konnten nicht gespeichert werden.");
    } finally {
      setSavingWeekDays(false);
    }
  }

  async function saveConfig() {
    setSavingConfig(true);
    try {
      await api.put(`/api/goals/${goal.id}/config`, { weeklyExerciseCount: cfgWeekly, trainingDaysPerWeek: cfgDays });
      toast.success("Plan-Einstellungen gespeichert.");
      setEditingConfig(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einstellungen konnten nicht gespeichert werden.");
    } finally {
      setSavingConfig(false);
    }
  }

  // --- Bestehendes Plan-Ziel bearbeiten ---
  const [editItemId, setEditItemId] = useState<string | null>(null);
  const [editWeek, setEditWeek] = useState(1);
  const [editTarget, setEditTarget] = useState(2);
  const [editDay, setEditDay] = useState(1);
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
    setEditDay(item.dayIndex);
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
        dayIndex: editDay,
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
    setIsQuickLogging(true);
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
          {daysForWeek(addWeek) > 1 && (
            <div className="flex flex-col gap-2">
              <Label>Trainingstag</Label>
              <Input
                type="number"
                min={1}
                max={daysForWeek(addWeek)}
                value={addDay}
                onChange={(e) => setAddDay(Number(e.target.value))}
              />
            </div>
          )}
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

  const weeks = goal.trainingPlan ? groupByWeek(goal.trainingPlan.items) : [];
  // Aktuelle Trainingswoche kalendarisch bestimmen: Woche 1 beginnt mit der
  // Plan-Erstellung (generatedAt), danach zählt jede angebrochene 7-Tage-Woche
  // hoch. Nur die aktuelle Woche ist standardmäßig aufgeklappt (nicht immer
  // Woche 1). Auf die tatsächlich vorhandenen Wochennummern begrenzt.
  const currentWeek = computeCurrentWeek(weeks, goal.trainingPlan?.generatedAt);
  const effectiveOpenWeeks =
    openWeeks.size === 0 && currentWeek != null ? new Set([currentWeek]) : openWeeks;
  function toggleWeek(week: number) {
    setOpenWeeks((prev) => {
      const next = new Set(prev.size === 0 && currentWeek != null ? [currentWeek] : prev);
      if (next.has(week)) next.delete(week);
      else next.add(week);
      return next;
    });
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

        {goal.status === 0 && !goal.isCustom && goal.trainingPlan &&
          (editingConfig ? (
            <div className="flex flex-wrap items-end gap-3 rounded-md border bg-muted/30 p-2.5">
              <div className="flex flex-col gap-1">
                <Label className="text-xs">Übungen/Woche</Label>
                <Input type="number" min={1} max={12} className="h-8 w-20" value={cfgWeekly} onChange={(e) => setCfgWeekly(Number(e.target.value))} />
              </div>
              <div className="flex flex-col gap-1">
                <Label className="text-xs">Trainingstage</Label>
                <Input type="number" min={1} max={7} className="h-8 w-20" value={cfgDays} onChange={(e) => setCfgDays(Number(e.target.value))} />
              </div>
              <div className="flex gap-2">
                <Button type="button" size="sm" disabled={savingConfig} onClick={saveConfig}>
                  {savingConfig ? "Speichert…" : "Speichern"}
                </Button>
                <Button type="button" size="sm" variant="ghost" onClick={() => setEditingConfig(false)}>
                  Abbrechen
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
              <span>
                Plan: {goal.weeklyExerciseCount} Übungen/Woche · {goal.trainingDaysPerWeek} Trainingstage
              </span>
              <Button type="button" size="sm" variant="ghost" className="h-6 px-2 text-xs" onClick={startEditConfig}>
                <Pencil className="size-3" />
                Anpassen
              </Button>
              <ExerciseWeightingSheet goalId={goal.id} />
            </div>
          ))}

        {goal.trainingPlan && (
          <div className="flex flex-col gap-3">
            {weeks.map(([weekNumber, items]) => {
              const isOpen = effectiveOpenWeeks.has(weekNumber);
              const isRest = items[0].isRestWeek;
              const doneCount = items.filter((i) => i.isComplete).length;
              return (
                <div key={weekNumber} className="rounded-md border">
                  <button
                    type="button"
                    onClick={() => toggleWeek(weekNumber)}
                    aria-expanded={isOpen}
                    className="flex w-full items-center justify-between gap-2 px-2.5 py-2 text-left coarse:min-h-11"
                  >
                    <span className="flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
                      {isOpen ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                      Woche {weekNumber}
                    </span>
                    {isRest ? (
                      <span className="text-xs text-muted-foreground">Pause</span>
                    ) : (
                      <Badge variant="secondary">
                        {doneCount}/{items.length}
                      </Badge>
                    )}
                  </button>
                  {isOpen && (
                    <div className="flex flex-col gap-1.5 border-t p-2.5">
                      {goal.status === 0 && !isRest && (
                        <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
                          {/* Pro-Woche abweichende Trainingstage (überschreibt den
                              Plan-Default nur für diese Woche). */}
                          {editingWeekDays === weekNumber ? (
                            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                              <span>Trainingstage:</span>
                              <Input
                                type="number"
                                min={1}
                                max={7}
                                className="h-6 w-14"
                                value={weekDaysDraft}
                                onChange={(e) => setWeekDaysDraft(Number(e.target.value))}
                              />
                              <Button type="button" size="sm" className="h-6 px-2 text-xs" disabled={savingWeekDays} onClick={() => saveWeekDays(weekNumber)}>
                                {savingWeekDays ? "…" : "OK"}
                              </Button>
                              <Button type="button" size="sm" variant="ghost" className="h-6 px-2 text-xs" onClick={() => setEditingWeekDays(null)}>
                                Abbrechen
                              </Button>
                            </div>
                          ) : (
                            <button
                              type="button"
                              className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
                              onClick={() => startEditWeekDays(weekNumber)}
                              title="Trainingstage dieser Woche anpassen"
                            >
                              {daysForWeek(weekNumber)} Trainingstag{daysForWeek(weekNumber) > 1 ? "e" : ""}
                              <Pencil className="size-3" />
                            </button>
                          )}
                          <div className="flex items-center gap-2">
                            {!goal.isCustom && (
                              <Button
                                type="button"
                                size="sm"
                                variant="ghost"
                                className="h-6 px-2 text-xs"
                                disabled={regeneratingWeek === weekNumber}
                                onClick={() => regenerateWeek(weekNumber)}
                                title="Diese Woche adaptiv neu generieren (erhält manuelle & bereits trainierte Übungen)"
                              >
                                <RefreshCw className={cn("size-3", regeneratingWeek === weekNumber && "animate-spin")} />
                                {regeneratingWeek === weekNumber ? "Generiere…" : "Neu generieren"}
                              </Button>
                            )}
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
                          </div>
                        </div>
                      )}
                      {isRest ? (
                        <span className="text-sm text-muted-foreground">Pause</span>
                      ) : (
                        groupByDay(items).map(([dayNumber, dayItems], _dayIdx, dayGroups) => (
                          <div key={dayNumber} className="flex flex-col gap-1">
                            {dayGroups.length > 1 && (
                              <span className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                                Tag {dayNumber}
                              </span>
                            )}
                            {dayItems.map((item) => (
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
                              {item.reason !== null && (
                                <span className="mr-1 rounded bg-muted px-1 py-0.5 text-[10px] uppercase tracking-wide">
                                  {reasonLabel[item.reason]}
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
                            {daysForWeek(editWeek) > 1 && (
                              <div className="flex flex-col gap-1">
                                <Label className="text-xs">Trainingstag</Label>
                                <Input type="number" min={1} max={daysForWeek(editWeek)} value={editDay} onChange={(e) => setEditDay(Number(e.target.value))} />
                              </div>
                            )}
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
                        <ul className="ml-6 flex flex-col gap-1 border-l pl-2.5">
                          {item.logs.map((log) => (
                            // Eine Zeile pro Log als reiner Textfluss: Meta (Datum/
                            // Sterne) nowrap, dann der Kommentar im selben Fluss -
                            // kurze Kommentare stehen neben der Meta, lange brechen um
                            // und sind voll lesbar (Stift folgt am Textende). Das
                            // overflow-wrap:anywhere deckelt die min-content (bricht
                            // notfalls überlange Wörter), sodass die nowrap-Meta das
                            // einzige Breiten-Minimum ist und nichts die Seite aufbläht
                            // (Mobile-App-first, kein horizontaler Scroll).
                            <li key={log.trainingExerciseId} className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                              <span className="whitespace-nowrap">
                                {new Date(log.date).toLocaleDateString("de-DE")} · {"★".repeat(log.rating)}
                                {"☆".repeat(5 - log.rating)} {log.success ? "✓" : "✗"}
                              </span>{" "}
                              <ExerciseNotes exerciseId={log.trainingExerciseId} notes={log.notes} onSaved={onChanged} compact />
                            </li>
                          ))}
                        </ul>
                      )}
                      {quickLogItemId === item.id && (
                        <div className="ml-6 flex flex-col gap-2 rounded-md border bg-muted/40 p-2.5">
                          <div className="flex gap-1" role="group" aria-label="Bewertung, 1 bis 5">
                            {[1, 2, 3, 4, 5].map((value) => (
                              <button
                                key={value}
                                type="button"
                                onClick={() => setQlRating(value)}
                                aria-label={`${value} von 5`}
                                aria-pressed={qlRating === value}
                                className={cn(
                                  "flex size-7 items-center justify-center rounded-md border text-xs coarse:size-11",
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
                            ))}
                          </div>
                        ))
                      )}
                      {addForm?.location === "inline" && addForm.week === weekNumber && renderAddForm(false)}
                    </div>
                  )}
                </div>
              );
            })}
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

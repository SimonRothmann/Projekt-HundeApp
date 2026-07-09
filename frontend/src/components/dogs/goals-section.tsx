"use client";

import { useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import type { Exercise, Goal, Regulation, Sport, TrainingPlanItem } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { CheckCircle2, Circle, Pencil, Plus, Target, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { difficultyLabel } from "@/lib/constants";

// Eine Woche kann jetzt mehrere Plan-Ziele haben (siehe TrainingPlanGenerator
// "ItemsPerWeek") statt wie vorher genau eines - für die Anzeige nach
// Wochennummer gruppiert, in der vom Server gelieferten Reihenfolge.
function groupByWeek(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byWeek = new Map<number, TrainingPlanItem[]>();
  for (const item of items) {
    const group = byWeek.get(item.weekNumber);
    if (group) group.push(item);
    else byWeek.set(item.weekNumber, [item]);
  }
  return [...byWeek.entries()];
}

const statusLabel: Record<Goal["status"], string> = {
  0: "Aktiv",
  1: "Erreicht",
  2: "Abgebrochen",
};

const statusVariant: Record<Goal["status"], "default" | "secondary" | "outline"> = {
  0: "default",
  1: "secondary",
  2: "outline",
};

// "goals"/"onChanged" kommen jetzt von der Eltern-Seite statt aus einem
// eigenen Fetch hier (siehe dogs/[id]/page.tsx) - die Seite braucht dieselben
// Daten ohnehin für die "Plan-Ziel"-Auswahl im Trainingstagebuch-Formular,
// und nur ein gemeinsamer State stellt sicher, dass der Fortschritt hier
// sofort sichtbar wird, sobald dort ein verknüpftes Training gespeichert wird.
export function GoalsSection({
  dogId,
  sports,
  goals,
  onChanged,
}: {
  dogId: string;
  sports: Sport[];
  goals: Goal[] | null;
  onChanged: () => Promise<void>;
}) {
  const [showForm, setShowForm] = useState(false);
  const [sportId, setSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [regulationId, setRegulationId] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [notes, setNotes] = useState("");
  const [isCustom, setIsCustom] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Übungen pro Sportart, lazy geladen für die "Übung hinzufügen"-Auswahl
  // im Plan (siehe addItemGoalId unten) - dieselbe Sportart kann für
  // mehrere Ziele/Hunde wiederverwendet werden, daher pro sportId gecacht.
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});

  async function ensureExercisesLoaded(forSportId: string) {
    if (exercisesBySport[forSportId]) return;
    try {
      const data = await api.get<Exercise[]>(`/api/sports/${forSportId}/exercises`);
      setExercisesBySport((prev) => ({ ...prev, [forSportId]: data }));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
    }
  }

  async function handleSportChange(value: string) {
    setSportId(value);
    setRegulationId("");
    setRegulations([]);
    if (!value) return;
    try {
      const data = await api.get<Regulation[]>(`/api/sports/${value}/regulations`);
      setRegulations(data);
    } catch {
      // Prüfungsauswahl ist optional - bleibt leer, Plan wird dann aus
      // allen Übungen der Sportart generiert (Fallback, siehe Backend).
    }
  }

  // Manuelles Hinzufügen einer weiteren Plan-Übung (siehe TODO.md
  // "Trainingsplan überarbeitet") - z.B. eine zweite Übungseinheit in
  // derselben Woche oder eine zusätzliche Übung in einer bestehenden.
  const [addItemGoalId, setAddItemGoalId] = useState<string | null>(null);
  const [addItemWeek, setAddItemWeek] = useState(1);
  const [addItemExerciseId, setAddItemExerciseId] = useState("");
  const [addItemFreeText, setAddItemFreeText] = useState("");
  // Toggle "Freitext statt Katalog-Übung": nützlich für spontane Wochenziele,
  // die nicht als eigene Katalog-Übung angelegt werden sollen. Analog zum
  // Trainingstagebuch-Freitext.
  const [addItemUseFreeText, setAddItemUseFreeText] = useState(false);
  const [addItemTarget, setAddItemTarget] = useState(2);
  // "inline" = Formular klappt unter der jeweiligen Woche auf, Woche-Feld
  // wird ausgeblendet (der Nutzer hat die Woche schon durch den Klick
  // ausgewählt). "central" = zentraler "+ Übung hinzufügen"-Button unter
  // dem gesamten Plan, mit freier Woche-Wahl - relevant für Wochen, die
  // noch gar keinen Eintrag haben (typisch: individueller Plan startet leer).
  const [addItemLocation, setAddItemLocation] = useState<"central" | "inline">("central");
  const [isAddingItem, setIsAddingItem] = useState(false);

  async function openAddItem(goal: Goal, location: "central" | "inline" = "central", week = 1) {
    const isSame = addItemGoalId === goal.id && addItemLocation === location && addItemWeek === week;
    if (isSame) {
      setAddItemGoalId(null);
      return;
    }
    setAddItemGoalId(goal.id);
    setAddItemLocation(location);
    setAddItemWeek(week);
    setAddItemExerciseId("");
    setAddItemFreeText("");
    setAddItemUseFreeText(false);
    setAddItemTarget(2);
    await ensureExercisesLoaded(goal.sportId);
  }

  async function submitAddItem(goal: Goal) {
    if (addItemUseFreeText) {
      if (!addItemFreeText.trim()) {
        toast.error("Freitext eingeben.");
        return;
      }
    } else if (!addItemExerciseId) {
      toast.error("Übung auswählen.");
      return;
    }
    setIsAddingItem(true);
    try {
      await api.post(`/api/goals/${goal.id}/plan-items`, {
        weekNumber: addItemWeek,
        exerciseId: addItemUseFreeText ? null : addItemExerciseId,
        freeTextLabel: addItemUseFreeText ? addItemFreeText.trim() : null,
        repetitionsTarget: addItemTarget,
      });
      toast.success("Übung zum Plan hinzugefügt.");
      setAddItemGoalId(null);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übung konnte nicht hinzugefügt werden.");
    } finally {
      setIsAddingItem(false);
    }
  }

  async function removePlanItem(goal: Goal, itemId: string) {
    try {
      await api.delete(`/api/goals/${goal.id}/plan-items/${itemId}`);
      toast.success("Übung aus dem Plan entfernt.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übung konnte nicht entfernt werden.");
    }
  }

  // Bestehendes Plan-Ziel bearbeiten: Woche, Zielwert UND Übung (bzw.
  // Freitext) sind änderbar. Bereits verknüpfte Tagebucheinträge zählen
  // dann für die neue Übung weiter - für einen "sauberen" Wechsel der
  // Übungssemantik: altes Item entfernen und neues anlegen.
  const [editItemId, setEditItemId] = useState<string | null>(null);
  const [editItemWeek, setEditItemWeek] = useState(1);
  const [editItemTarget, setEditItemTarget] = useState(2);
  const [editItemExerciseId, setEditItemExerciseId] = useState("");
  const [editItemFreeText, setEditItemFreeText] = useState("");
  const [editItemUseFreeText, setEditItemUseFreeText] = useState(false);
  const [isEditingItem, setIsEditingItem] = useState(false);

  async function openEditItem(item: TrainingPlanItem, goal: Goal) {
    const isSame = editItemId === item.id;
    setEditItemId(isSame ? null : item.id);
    if (isSame) return;
    setEditItemWeek(item.weekNumber);
    setEditItemTarget(item.repetitionsTarget);
    setEditItemExerciseId(item.exerciseId ?? "");
    setEditItemFreeText(item.freeTextLabel ?? "");
    setEditItemUseFreeText(item.freeTextLabel !== null);
    await ensureExercisesLoaded(goal.sportId);
  }

  async function submitEditItem(goal: Goal, itemId: string) {
    if (editItemUseFreeText) {
      if (!editItemFreeText.trim()) {
        toast.error("Freitext eingeben.");
        return;
      }
    } else if (!editItemExerciseId) {
      toast.error("Übung auswählen.");
      return;
    }
    setIsEditingItem(true);
    try {
      await api.put(`/api/goals/${goal.id}/plan-items/${itemId}`, {
        weekNumber: editItemWeek,
        exerciseId: editItemUseFreeText ? null : editItemExerciseId,
        freeTextLabel: editItemUseFreeText ? editItemFreeText.trim() : null,
        repetitionsTarget: editItemTarget,
      });
      toast.success("Plan-Ziel aktualisiert.");
      setEditItemId(null);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Plan-Ziel konnte nicht aktualisiert werden.");
    } finally {
      setIsEditingItem(false);
    }
  }

  // Direktes Eintragen "diese Übung gemacht" pro Plan-Ziel, ohne den Umweg
  // über das volle Trainingstagebuch-Formular - Feedback war, dass sich
  // einzelne Wochenziele nicht selbstständig auswählen ließen, nur das
  // ganze Ziel über "Als erreicht markieren".
  const [quickLogItemId, setQuickLogItemId] = useState<string | null>(null);
  const [quickLogRating, setQuickLogRating] = useState(5);
  const [quickLogSuccess, setQuickLogSuccess] = useState(true);
  const [quickLogNotes, setQuickLogNotes] = useState("");
  const [isQuickLogging, setIsQuickLogging] = useState(false);

  function openQuickLog(itemId: string) {
    setQuickLogItemId((current) => (current === itemId ? null : itemId));
    setQuickLogRating(5);
    setQuickLogSuccess(true);
    setQuickLogNotes("");
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
            rating: quickLogRating,
            difficulty: 0,
            success: quickLogSuccess,
            notes: quickLogNotes || null,
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

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await api.post<Goal>("/api/goals", {
        dogId,
        sportId,
        regulationId: isCustom ? null : (regulationId || null),
        targetDate,
        notes: notes || null,
        isCustom,
      });
      toast.success(
        isCustom
          ? "Individueller Plan angelegt - füge jetzt Wochenübungen hinzu."
          : "Ziel angelegt - Trainingsplan wurde generiert.",
      );
      setShowForm(false);
      setSportId("");
      setRegulationId("");
      setRegulations([]);
      setTargetDate("");
      setNotes("");
      setIsCustom(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ziel konnte nicht angelegt werden.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function updateStatus(goalId: string, status: 1 | 2) {
    try {
      await api.put<Goal>(`/api/goals/${goalId}/status`, { status });
      toast.success(status === 1 ? "Ziel als erreicht markiert." : "Ziel abgebrochen.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Status konnte nicht aktualisiert werden.");
    }
  }

  async function deleteGoal(goalId: string) {
    if (!window.confirm("Ziel inkl. Trainingsplan endgültig löschen? Bereits erfasste Trainingseinträge bleiben im Tagebuch erhalten.")) {
      return;
    }
    try {
      await api.delete(`/api/goals/${goalId}`);
      toast.success("Ziel gelöscht.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ziel konnte nicht gelöscht werden.");
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Ziele & Trainingsplan</h2>
        <Button size="sm" variant="outline" onClick={() => setShowForm((v) => !v)}>
          <Plus className="size-4" />
          Ziel setzen
        </Button>
      </div>

      {showForm && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Neues Ziel</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-4 accent-primary"
                  checked={isCustom}
                  onChange={(e) => setIsCustom(e.target.checked)}
                />
                <span>
                  Individueller Plan (ohne Prüfungsziel) &ndash; leere Wochen, Übungen manuell festlegen
                </span>
              </label>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="flex flex-col gap-2">
                  <Label>Sportart</Label>
                  <Select required value={sportId} onValueChange={(value) => handleSportChange(value ?? "")}>
                    <SelectTrigger>
                      <SelectValue placeholder="Auswählen…" />
                    </SelectTrigger>
                    <SelectContent>
                      {sports.map((s) => (
                        <SelectItem key={s.id} value={s.id}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="targetDate">Zieldatum</Label>
                  <Input
                    id="targetDate"
                    type="date"
                    required
                    value={targetDate}
                    onChange={(e) => setTargetDate(e.target.value)}
                  />
                </div>
              </div>
              {!isCustom && regulations.length > 0 && (
                <div className="flex flex-col gap-2">
                  <Label>Prüfung</Label>
                  <Select value={regulationId} onValueChange={(value) => setRegulationId(value ?? "")}>
                    <SelectTrigger>
                      <SelectValue placeholder="Allgemein (alle Übungen der Sportart)" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">Allgemein (alle Übungen der Sportart)</SelectItem>
                      {regulations.map((r) => (
                        <SelectItem key={r.id} value={r.id}>
                          {r.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    Legt fest, aus welcher Pflichtübungsliste der Plan generiert wird - mehrere Prüfungsordnungen
                    derselben Sportart (z.B. Fährte A/B/C) haben unterschiedliche Anforderungen.
                  </p>
                  {(() => {
                    const selected = regulations.find((r) => r.id === regulationId);
                    if (!selected?.description) return null;
                    return (
                      <div className="rounded-md bg-primary/5 px-3 py-2.5">
                        <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-primary">
                          Rahmenbedingungen
                        </p>
                        <p className="whitespace-pre-line text-sm leading-relaxed">{selected.description}</p>
                      </div>
                    );
                  })()}
                </div>
              )}
              <div className="flex flex-col gap-2">
                <Label htmlFor="goalNotes">Notizen</Label>
                <Input id="goalNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
              </div>
              <Button type="submit" className="self-start" disabled={isSubmitting}>
                {isSubmitting
                  ? isCustom
                    ? "Wird angelegt…"
                    : "Wird generiert…"
                  : isCustom
                    ? "Individuellen Plan anlegen"
                    : "Ziel anlegen & Plan generieren"}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {goals === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : goals.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-10 text-center text-muted-foreground">
            <Target className="size-8" />
            <p>Noch kein Ziel gesetzt.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {goals.map((goal) => (
            <Card key={goal.id}>
              <CardHeader className="flex-row flex-wrap items-start justify-between gap-2 space-y-0">
                <div className="min-w-0">
                  <CardTitle className="text-base break-words">
                    {goal.sportName}
                    {goal.regulationName && <span className="font-normal text-muted-foreground"> · {goal.regulationName}</span>}
                  </CardTitle>
                  <p className="text-sm text-muted-foreground">
                    Ziel: {new Date(goal.targetDate).toLocaleDateString("de-DE")}
                  </p>
                </div>
                <Badge className="shrink-0" variant={statusVariant[goal.status]}>{statusLabel[goal.status]}</Badge>
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
                              onClick={() => openAddItem(goal, "inline", weekNumber)}
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
                                    onClick={() => openEditItem(item, goal)}
                                    title="Übung, Woche oder Zielwert bearbeiten"
                                  >
                                    <Pencil className="size-3.5 text-muted-foreground" />
                                  </Button>
                                  <Button
                                    type="button"
                                    variant="ghost"
                                    size="icon-xs"
                                    onClick={() => removePlanItem(goal, item.id)}
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
                                      checked={editItemUseFreeText}
                                      onChange={(e) => setEditItemUseFreeText(e.target.checked)}
                                    />
                                    <span>Freitext-Übung</span>
                                  </label>
                                  <div className="flex flex-col gap-1">
                                    <Label className="text-xs">
                                      {editItemUseFreeText ? "Freitext" : "Übung"}
                                    </Label>
                                    {editItemUseFreeText ? (
                                      <Input
                                        value={editItemFreeText}
                                        onChange={(e) => setEditItemFreeText(e.target.value)}
                                        placeholder="z.B. Kopfarbeit ausprobieren"
                                        maxLength={150}
                                      />
                                    ) : (
                                      <Select
                                        value={editItemExerciseId}
                                        onValueChange={(v) => setEditItemExerciseId(v ?? "")}
                                      >
                                        <SelectTrigger>
                                          <SelectValue placeholder="Auswählen…" />
                                        </SelectTrigger>
                                        <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
                                          {(exercisesBySport[goal.sportId] ?? []).map((ex) => (
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
                                      <Input
                                        type="number"
                                        min={1}
                                        max={12}
                                        value={editItemWeek}
                                        onChange={(e) => setEditItemWeek(Number(e.target.value))}
                                      />
                                    </div>
                                    <div className="flex flex-col gap-1">
                                      <Label className="text-xs">Zielwert (x diese Woche)</Label>
                                      <Input
                                        type="number"
                                        min={1}
                                        max={10}
                                        value={editItemTarget}
                                        onChange={(e) => setEditItemTarget(Number(e.target.value))}
                                      />
                                    </div>
                                  </div>
                                  <div className="flex gap-2">
                                    <Button
                                      type="button"
                                      size="sm"
                                      disabled={isEditingItem}
                                      onClick={() => submitEditItem(goal, item.id)}
                                    >
                                      {isEditingItem ? "Wird gespeichert…" : "Speichern"}
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
                                      {new Date(log.date).toLocaleDateString("de-DE")} ·{" "}
                                      {"★".repeat(log.rating)}
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
                                        onClick={() => setQuickLogRating(value)}
                                        className={cn(
                                          "flex size-7 items-center justify-center rounded-md border text-xs",
                                          quickLogRating >= value
                                            ? "border-accent bg-accent text-accent-foreground"
                                            : "border-input text-muted-foreground",
                                        )}
                                      >
                                        {value}
                                      </button>
                                    ))}
                                    <label className="ml-2 flex items-center gap-1.5 text-xs">
                                      <input
                                        type="checkbox"
                                        checked={quickLogSuccess}
                                        onChange={(e) => setQuickLogSuccess(e.target.checked)}
                                      />
                                      Erfolgreich
                                    </label>
                                  </div>
                                  <Input
                                    placeholder="Kommentar (optional)"
                                    value={quickLogNotes}
                                    onChange={(e) => setQuickLogNotes(e.target.value)}
                                  />
                                  <div className="flex gap-2">
                                    <Button
                                      type="button"
                                      size="sm"
                                      disabled={isQuickLogging}
                                      onClick={() => submitQuickLog(item)}
                                    >
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
                        {addItemGoalId === goal.id &&
                          addItemLocation === "inline" &&
                          addItemWeek === weekNumber &&
                          renderAddItemForm(goal, { showWeekField: false })}
                      </div>
                    ))}
                  </div>
                )}

                {goal.status === 0 && (
                  <>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      className="self-start"
                      onClick={() => openAddItem(goal, "central", 1)}
                    >
                      <Plus className="size-4" />
                      Übung hinzufügen (freie Woche)
                    </Button>

                    {addItemGoalId === goal.id &&
                      addItemLocation === "central" &&
                      renderAddItemForm(goal, { showWeekField: true })}

                    <div className="flex gap-2">
                      <Button size="sm" variant="outline" onClick={() => updateStatus(goal.id, 1)}>
                        Als erreicht markieren
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => updateStatus(goal.id, 2)}>
                        Abbrechen
                      </Button>
                    </div>
                  </>
                )}

                {goal.status !== 0 && (
                  <Button
                    size="sm"
                    variant="ghost"
                    className="self-start text-destructive hover:text-destructive"
                    onClick={() => deleteGoal(goal.id)}
                  >
                    <Trash2 className="size-4" />
                    Ziel löschen
                  </Button>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );

  /**
   * Wiederverwendbares Formular zum Anlegen eines Plan-Items. Wird sowohl
   * zentral (mit Woche-Feld, unter dem Plan) als auch inline pro Woche
   * (ohne Woche-Feld) gerendert - genau ein Ort ist gleichzeitig sichtbar,
   * gesteuert über `addItemGoalId + addItemLocation + addItemWeek`.
   */
  function renderAddItemForm(goal: Goal, options: { showWeekField: boolean }) {
    return (
      <div className="flex flex-col gap-3 rounded-md border bg-muted/30 p-3">
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 accent-primary"
            checked={addItemUseFreeText}
            onChange={(e) => setAddItemUseFreeText(e.target.checked)}
          />
          <span>Freitext-Übung (nicht aus dem Katalog)</span>
        </label>
        <div className={cn("grid gap-3", options.showWeekField ? "sm:grid-cols-3" : "sm:grid-cols-2")}>
          {options.showWeekField && (
            <div className="flex flex-col gap-2">
              <Label>Woche</Label>
              <Input
                type="number"
                min={1}
                max={12}
                value={addItemWeek}
                onChange={(e) => setAddItemWeek(Number(e.target.value))}
              />
            </div>
          )}
          <div className="flex flex-col gap-2">
            <Label>{addItemUseFreeText ? "Freitext" : "Übung"}</Label>
            {addItemUseFreeText ? (
              <Input
                value={addItemFreeText}
                onChange={(e) => setAddItemFreeText(e.target.value)}
                placeholder="z.B. Kopfarbeit ausprobieren"
                maxLength={150}
                autoFocus
              />
            ) : (
              <Select value={addItemExerciseId} onValueChange={(value) => setAddItemExerciseId(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder="Auswählen…" />
                </SelectTrigger>
                {/* max-h-[60vh] als Sicherheitsnetz zum UI-Default
                    (max-h aus --available-height): Base-UI errechnet
                    available-height aus der Trigger-Position im Viewport;
                    bei einem weit unten sitzenden Trigger auf iOS Safari
                    wird das schnell zu klein für den Scroll-Rand, das
                    Feld wird scheinbar "nicht scrollbar". 60vh +
                    touch-pan-y ist bequem und funktioniert überall. */}
                <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
                  {(exercisesBySport[goal.sportId] ?? []).map((ex) => (
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
            <Input
              type="number"
              min={1}
              max={10}
              value={addItemTarget}
              onChange={(e) => setAddItemTarget(Number(e.target.value))}
            />
          </div>
        </div>
        <div className="flex gap-2">
          <Button type="button" size="sm" disabled={isAddingItem} onClick={() => submitAddItem(goal)}>
            {isAddingItem ? "Wird hinzugefügt…" : "Hinzufügen"}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => setAddItemGoalId(null)}>
            Abbrechen
          </Button>
        </div>
      </div>
    );
  }
}

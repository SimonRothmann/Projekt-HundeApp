"use client";

import { useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, Goal, Sport, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ListChecks, PenLine, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { difficultyLabel } from "@/lib/constants";

type ExerciseRow = {
  sportId: string;
  exerciseId: string;
  // Für spontane Spaß-/Sonstige Übungen, die nicht im Katalog stehen und
  // nicht extra dort angelegt werden sollen (siehe TrainingExercise.FreeTextLabel) -
  // schließt Sportart/Übung/Plan-Ziel-Auswahl aus, dafür freier Text.
  isFreeText: boolean;
  freeText: string;
  rating: number;
  success: boolean;
  notes: string;
  trainingPlanItemId: string;
};

function emptyRow(): ExerciseRow {
  return {
    sportId: "",
    exerciseId: "",
    isFreeText: false,
    freeText: "",
    rating: 3,
    success: true,
    notes: "",
    trainingPlanItemId: "",
  };
}

/**
 * Formular "Neues Training" für das Trainingstagebuch. Hält seinen gesamten
 * Formular-State selbst (Zeilen, Übungs-Lookup pro Sportart, Submit-Status) -
 * die Hundeseite orchestriert nur noch (siehe TODO.md Roadmap 5b, gleiches
 * Muster wie GoalCreateForm nach dem goals-section-Refactor).
 *
 * onSaved(offline): offline=true, wenn der Eintrag nur in die
 * IndexedDB-Warteschlange geschrieben wurde - der Aufrufer soll dann NICHT
 * neu vom Server laden (der Eintrag ist dort noch nicht vorhanden).
 */
export function TrainingForm({
  dogId,
  sports,
  goals,
  onSaved,
}: {
  dogId: string;
  sports: Sport[];
  goals: Goal[] | null;
  onSaved: (offline: boolean) => Promise<void>;
}) {
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [duration, setDuration] = useState(30);
  const [notes, setNotes] = useState("");
  const [rows, setRows] = useState<ExerciseRow[]>([emptyRow()]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});

  async function ensureExercisesLoaded(sportId: string) {
    if (exercisesBySport[sportId] || !sportId) return;
    const exercises = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
    setExercisesBySport((prev) => ({ ...prev, [sportId]: exercises }));
  }

  function updateRow(index: number, patch: Partial<ExerciseRow>) {
    setRows((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  async function handleSportChange(index: number, sportId: string) {
    updateRow(index, { sportId, exerciseId: "", trainingPlanItemId: "" });
    await ensureExercisesLoaded(sportId);
  }

  // Plan-Ziele (siehe GoalsSection), die zur gewählten Übung passen - nur
  // aus aktiven Zielen (Status 0) und ohne Pausenwochen, damit man einen
  // Tagebucheintrag optional einem Wochenziel zuordnen kann (siehe
  // TrainingExercise.TrainingPlanItemId). Bereits erfüllte Ziele bleiben
  // wählbar, falls man dieselbe Übung öfter als das Ziel trainieren möchte.
  function planItemOptionsFor(exerciseId: string) {
    if (!exerciseId) return [];
    return (goals ?? [])
      .filter((g) => g.status === 0)
      .flatMap((g) => g.trainingPlan?.items ?? [])
      .filter((item) => !item.isRestWeek && item.exerciseId === exerciseId);
  }

  function addRow() {
    setRows((prev) => [...prev, emptyRow()]);
  }

  function removeRow(index: number) {
    setRows((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const validRows = rows.filter((r) => (r.isFreeText ? r.freeText.trim() : r.exerciseId));
    if (validRows.length === 0) {
      toast.error("Mindestens eine Übung auswählen oder eintragen.");
      return;
    }

    const payload = {
      dogId,
      date,
      durationMinutes: duration,
      notes: notes || null,
      exercises: validRows.map((r) => ({
        exerciseId: r.isFreeText ? null : r.exerciseId,
        freeTextLabel: r.isFreeText ? r.freeText.trim() : null,
        rating: r.rating,
        difficulty: 0,
        success: r.success,
        notes: r.notes || null,
        trainingPlanItemId: r.isFreeText ? null : r.trainingPlanItemId || null,
      })),
    };

    setIsSubmitting(true);
    try {
      await api.post<TrainingSession>("/api/trainings", payload);
      toast.success("Training gespeichert.");
      setRows([emptyRow()]);
      setNotes("");
      await onSaved(false);
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        // Kein HTTP-Fehler vom Server, sondern ein Netzwerkfehler (offline) -
        // siehe PRODUCT_REQUIREMENTS.md "Offline": Training ohne Internet erfassen.
        await enqueueRequest({ path: "/api/trainings", method: "POST", body: payload, label: "Training" });
        toast.success("Offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
        setRows([emptyRow()]);
        setNotes("");
        await onSaved(true);
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Neues Training</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="date">Datum</Label>
              <Input id="date" type="date" required value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="duration">Dauer (Minuten)</Label>
              <Input
                id="duration"
                type="number"
                min={1}
                required
                value={duration}
                onChange={(e) => setDuration(Number(e.target.value))}
              />
            </div>
          </div>

          <div className="flex flex-col gap-3">
            <Label>Übungen</Label>
            {rows.map((row, index) => {
              const exercises = exercisesBySport[row.sportId] ?? [];
              const selectedExercise = exercises.find((ex) => ex.id === row.exerciseId);
              const planItemOptions = planItemOptionsFor(row.exerciseId);
              return (
                <div key={index} className="flex flex-col gap-3 rounded-md border p-3">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    title={row.isFreeText ? "Katalog-Übung auswählen" : "Sonstige/Spaß-Übung als Freitext eintragen"}
                    onClick={() =>
                      updateRow(index, {
                        isFreeText: !row.isFreeText,
                        sportId: "",
                        exerciseId: "",
                        trainingPlanItemId: "",
                        freeText: "",
                      })
                    }
                  >
                    {row.isFreeText ? <ListChecks className="size-4" /> : <PenLine className="size-4" />}
                  </Button>
                  {row.isFreeText ? (
                    <div className="flex flex-col gap-2 sm:w-72">
                      <Label>Sonstige/Spaß-Übung</Label>
                      <Input
                        placeholder="z.B. Spaziergang mit Bällchenspiel"
                        value={row.freeText}
                        onChange={(e) => updateRow(index, { freeText: e.target.value })}
                      />
                    </div>
                  ) : (
                    <>
                  <div className="flex flex-col gap-2 sm:w-48">
                    <Label>Sportart</Label>
                    <Select
                      value={row.sportId}
                      onValueChange={(value) => handleSportChange(index, value ?? "")}
                    >
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
                  <div className="flex flex-col gap-2 sm:w-56">
                    <Label>Übung</Label>
                    <Select
                      value={row.exerciseId}
                      disabled={!row.sportId}
                      onValueChange={(value) => updateRow(index, { exerciseId: value ?? "", trainingPlanItemId: "" })}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Auswählen…" />
                      </SelectTrigger>
                      <SelectContent>
                        {exercises.map((ex) => (
                          <SelectItem key={ex.id} value={ex.id}>
                            {ex.name} ({difficultyLabel[ex.difficulty]})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  {planItemOptions.length > 0 && (
                    <div className="flex flex-col gap-2 sm:w-48">
                      <Label>Plan-Ziel (optional)</Label>
                      <Select
                        value={row.trainingPlanItemId}
                        onValueChange={(value) => updateRow(index, { trainingPlanItemId: value ?? "" })}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Kein Plan-Ziel" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="">Kein Plan-Ziel</SelectItem>
                          {planItemOptions.map((item) => (
                            <SelectItem key={item.id} value={item.id}>
                              KW {item.weekNumber} ({item.completedCount}/{item.repetitionsTarget}x)
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  )}
                    </>
                  )}
                  <div className="flex flex-col gap-2">
                    <Label>Bewertung</Label>
                    <div className="flex gap-1" role="group" aria-label="Bewertung, 1 bis 5">
                      {[1, 2, 3, 4, 5].map((value) => (
                        <button
                          key={value}
                          type="button"
                          onClick={() => updateRow(index, { rating: value })}
                          aria-label={`${value} von 5`}
                          aria-pressed={row.rating === value}
                          className={`flex size-8 items-center justify-center rounded-md border text-sm coarse:size-11 ${
                            row.rating >= value
                              ? "border-accent bg-accent text-accent-foreground"
                              : "border-input text-muted-foreground"
                          }`}
                        >
                          {value}
                        </button>
                      ))}
                    </div>
                  </div>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={row.success}
                      onChange={(e) => updateRow(index, { success: e.target.checked })}
                    />
                    Erfolgreich
                  </label>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={() => removeRow(index)}
                    disabled={rows.length === 1}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
                {selectedExercise?.scoringCriteria && (
                  <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
                    <strong className="text-foreground">Bewertungskriterien:</strong>{" "}
                    {selectedExercise.scoringCriteria}
                  </p>
                )}
                </div>
              );
            })}
            <Button type="button" variant="outline" size="sm" className="self-start" onClick={addRow}>
              <Plus className="size-4" />
              Übung hinzufügen
            </Button>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="notes">Notizen</Label>
            <Input id="notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>

          <Button type="submit" className="self-start" disabled={isSubmitting}>
            {isSubmitting ? "Wird gespeichert…" : "Training speichern"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

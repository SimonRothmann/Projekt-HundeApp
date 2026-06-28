"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useParams } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import type { Dog, Exercise, Sport, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Dog as DogIcon, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { GoalsSection } from "@/components/dogs/goals-section";
import { GpsTrackSection } from "@/components/tracking/gps-track-section";
import { TrainerFeedback } from "@/components/dogs/trainer-feedback";
import { enqueueRequest } from "@/lib/offline-queue";

type ExerciseRow = {
  sportId: string;
  exerciseId: string;
  rating: number;
  success: boolean;
  notes: string;
};

const difficultyLabel: Record<Exercise["difficulty"], string> = {
  Beginner: "Einsteiger",
  Intermediate: "Fortgeschritten",
  Advanced: "Erfahren",
};

function emptyRow(): ExerciseRow {
  return { sportId: "", exerciseId: "", rating: 3, success: true, notes: "" };
}

export default function DogDetailPage() {
  const { id } = useParams<{ id: string }>();

  const [dog, setDog] = useState<Dog | null>(null);
  const [sessions, setSessions] = useState<TrainingSession[] | null>(null);
  const [sports, setSports] = useState<Sport[]>([]);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});
  const [isOwner, setIsOwner] = useState(true);

  const [showForm, setShowForm] = useState(false);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [duration, setDuration] = useState(30);
  const [notes, setNotes] = useState("");
  const [rows, setRows] = useState<ExerciseRow[]>([emptyRow()]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function loadAll() {
    try {
      const [dogData, sessionData, sportsData, myDogs] = await Promise.all([
        api.get<Dog>(`/api/dogs/${id}`),
        api.get<TrainingSession[]>(`/api/trainings?dogId=${id}`),
        api.get<Sport[]>("/api/sports"),
        api.get<Dog[]>("/api/dogs"),
      ]);
      setDog(dogData);
      setSessions(sessionData);
      setSports(sportsData);
      setIsOwner(myDogs.some((d) => d.id === id));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Daten konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount/Routenwechsel (externe Quelle: REST API).
    // loadAll() wird bei jedem Render neu erzeugt, daher absichtlich nicht in
    // den Dependencies - nur "id" soll einen erneuten Abruf auslösen.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function ensureExercisesLoaded(sportId: string) {
    if (exercisesBySport[sportId] || !sportId) return;
    const exercises = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
    setExercisesBySport((prev) => ({ ...prev, [sportId]: exercises }));
  }

  function updateRow(index: number, patch: Partial<ExerciseRow>) {
    setRows((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  async function handleSportChange(index: number, sportId: string) {
    updateRow(index, { sportId, exerciseId: "" });
    await ensureExercisesLoaded(sportId);
  }

  function addRow() {
    setRows((prev) => [...prev, emptyRow()]);
  }

  function removeRow(index: number) {
    setRows((prev) => prev.filter((_, i) => i !== index));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const validRows = rows.filter((r) => r.exerciseId);
    if (validRows.length === 0) {
      toast.error("Mindestens eine Übung auswählen.");
      return;
    }

    const payload = {
      dogId: id,
      date,
      durationMinutes: duration,
      notes: notes || null,
      exercises: validRows.map((r) => ({
        exerciseId: r.exerciseId,
        rating: r.rating,
        difficulty: 0,
        success: r.success,
        notes: r.notes || null,
      })),
    };

    setIsSubmitting(true);
    try {
      await api.post<TrainingSession>("/api/trainings", payload);
      toast.success("Training gespeichert.");
      setShowForm(false);
      setRows([emptyRow()]);
      setNotes("");
      await loadAll();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        // Kein HTTP-Fehler vom Server, sondern ein Netzwerkfehler (offline) -
        // siehe PRODUCT_REQUIREMENTS.md "Offline": Training ohne Internet erfassen.
        await enqueueRequest({ path: "/api/trainings", method: "POST", body: payload, label: "Training" });
        toast.success("Offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist.");
        setShowForm(false);
        setRows([emptyRow()]);
        setNotes("");
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!dog) return <p className="text-muted-foreground">Lädt…</p>;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center gap-3">
        <div className="flex size-12 items-center justify-center rounded-full bg-secondary">
          <DogIcon className="size-6 text-secondary-foreground" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{dog.name}</h1>
          <p className="text-muted-foreground">{dog.breed ?? "Unbekannte Rasse"}</p>
        </div>
      </div>

      <GoalsSection dogId={id} sports={sports} />

      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Trainingstagebuch</h2>
        <Button size="sm" onClick={() => setShowForm((v) => !v)}>
          <Plus className="size-4" />
          Training erfassen
        </Button>
      </div>

      {showForm && (
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
                  return (
                    <div key={index} className="flex flex-col gap-3 rounded-md border p-3">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                      <div className="flex flex-col gap-2 sm:w-48">
                        <Label>Sportart</Label>
                        <select
                          className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                          value={row.sportId}
                          onChange={(e) => handleSportChange(index, e.target.value)}
                        >
                          <option value="">Auswählen…</option>
                          {sports.map((s) => (
                            <option key={s.id} value={s.id}>
                              {s.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="flex flex-col gap-2 sm:w-56">
                        <Label>Übung</Label>
                        <select
                          className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                          value={row.exerciseId}
                          disabled={!row.sportId}
                          onChange={(e) => updateRow(index, { exerciseId: e.target.value })}
                        >
                          <option value="">Auswählen…</option>
                          {exercises.map((ex) => (
                            <option key={ex.id} value={ex.id}>
                              {ex.name} ({difficultyLabel[ex.difficulty]})
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="flex flex-col gap-2">
                        <Label>Bewertung</Label>
                        <div className="flex gap-1">
                          {[1, 2, 3, 4, 5].map((value) => (
                            <button
                              key={value}
                              type="button"
                              onClick={() => updateRow(index, { rating: value })}
                              className={`flex size-8 items-center justify-center rounded-md border text-sm ${
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
      )}

      {sessions === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : sessions.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            Noch keine Trainingseinheiten erfasst.
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {sessions.map((session) => (
            <Card key={session.id}>
              <CardHeader className="flex-row items-center justify-between space-y-0">
                <CardTitle className="text-base">
                  {new Date(session.date).toLocaleDateString("de-DE")}
                </CardTitle>
                <Badge variant="secondary">{session.durationMinutes} Min.</Badge>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                {session.notes && <p className="text-sm text-muted-foreground">{session.notes}</p>}
                <ul className="flex flex-col gap-1">
                  {session.exercises.map((ex) => (
                    <li key={ex.id} className="flex items-center justify-between text-sm">
                      <span>{ex.exerciseName}</span>
                      <span className="text-muted-foreground">
                        {"★".repeat(ex.rating)}
                        {"☆".repeat(5 - ex.rating)} {ex.success ? "✓" : "✗"}
                      </span>
                    </li>
                  ))}
                </ul>
                <GpsTrackSection trainingSessionId={session.id} />
                <TrainerFeedback session={session} isOwner={isOwner} onUpdated={loadAll} />
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

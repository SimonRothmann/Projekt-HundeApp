"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, ExerciseDifficulty, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dumbbell, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { difficultyLabel } from "@/lib/constants";

export function GlobalExercisesSection() {
  const [sports, setSports] = useState<Sport[]>([]);
  const [selectedSportId, setSelectedSportId] = useState("");
  const [exercises, setExercises] = useState<Exercise[]>([]);
  const [name, setName] = useState("");
  const [category, setCategory] = useState("");
  const [difficulty, setDifficulty] = useState<ExerciseDifficulty>(0);
  const [scoringCriteria, setScoringCriteria] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    api
      .get<Sport[]>("/api/sports")
      .then(setSports)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Sportarten konnten nicht geladen werden."));
  }, []);

  async function loadExercises(sportId: string) {
    if (!sportId) {
      setExercises([]);
      return;
    }
    try {
      const data = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
      setExercises(data.filter((e) => e.clubId === null));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
    }
  }

  async function handleSportChange(sportId: string) {
    setSelectedSportId(sportId);
    await loadExercises(sportId);
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedSportId || !name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/exercises", {
        sportId: selectedSportId,
        name,
        description: null,
        difficulty,
        category: category || null,
        scoringCriteria: scoringCriteria || null,
        clubId: null,
      });
      toast.success("Übung angelegt.");
      setName("");
      setCategory("");
      setScoringCriteria("");
      await loadExercises(selectedSportId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übung konnte nicht angelegt werden.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(exerciseId: string) {
    try {
      await api.delete(`/api/exercises/${exerciseId}`);
      toast.success("Übung gelöscht.");
      await loadExercises(selectedSportId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Dumbbell className="size-5" />
          Globale Übungen
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-2 sm:w-64">
          <Label>Sportart</Label>
          <Select value={selectedSportId} onValueChange={(value) => handleSportChange(value ?? "")}>
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

        {selectedSportId && (
          <>
            <form onSubmit={handleCreate} className="flex flex-col gap-3">
              <div className="flex flex-col gap-3 sm:flex-row">
                <div className="flex flex-col gap-2 sm:flex-1">
                  <Label htmlFor="ex-name">Name</Label>
                  <Input id="ex-name" value={name} onChange={(e) => setName(e.target.value)} required />
                </div>
                <div className="flex flex-col gap-2 sm:w-40">
                  <Label htmlFor="ex-category">Kategorie</Label>
                  <Input id="ex-category" value={category} onChange={(e) => setCategory(e.target.value)} />
                </div>
                <div className="flex flex-col gap-2 sm:w-40">
                  <Label>Schwierigkeit</Label>
                  <Select
                    value={difficulty}
                    onValueChange={(value) => setDifficulty(value as ExerciseDifficulty)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {Object.entries(difficultyLabel).map(([value, label]) => (
                        <SelectItem key={value} value={Number(value) as ExerciseDifficulty}>
                          {label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="ex-scoring">Bewertungskriterien</Label>
                <Input id="ex-scoring" value={scoringCriteria} onChange={(e) => setScoringCriteria(e.target.value)} />
              </div>
              <Button type="submit" disabled={submitting} className="self-start">
                <Plus className="size-4" />
                Übung anlegen
              </Button>
            </form>

            <ul className="flex flex-col gap-2">
              {exercises.map((ex) => (
                <li key={ex.id} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                  <div>
                    <span className="font-medium">{ex.name}</span>
                    {ex.category && <span className="ml-2 text-muted-foreground">{ex.category}</span>}
                  </div>
                  <Button type="button" size="icon-sm" variant="ghost" onClick={() => handleDelete(ex.id)}>
                    <Trash2 className="size-3.5" />
                  </Button>
                </li>
              ))}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  );
}

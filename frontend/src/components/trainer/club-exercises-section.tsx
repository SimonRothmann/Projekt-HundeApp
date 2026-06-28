"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, Exercise, ExerciseDifficulty, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dumbbell, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

const difficultyLabel: Record<ExerciseDifficulty, string> = {
  Beginner: "Einsteiger",
  Intermediate: "Fortgeschritten",
  Advanced: "Erfahren",
};

export function ClubExercisesSection({ clubs }: { clubs: Club[] }) {
  const [sports, setSports] = useState<Sport[]>([]);
  const [selectedClubId, setSelectedClubId] = useState(clubs[0]?.id ?? "");
  const [selectedSportId, setSelectedSportId] = useState("");
  const [exercises, setExercises] = useState<Exercise[]>([]);
  const [name, setName] = useState("");
  const [scoringCriteria, setScoringCriteria] = useState("");
  const [difficulty, setDifficulty] = useState<ExerciseDifficulty>("Beginner");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    api
      .get<Sport[]>("/api/sports")
      .then(setSports)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Sportarten konnten nicht geladen werden."));
  }, []);

  async function loadExercises(sportId: string, clubId: string) {
    if (!sportId) {
      setExercises([]);
      return;
    }
    try {
      const data = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
      setExercises(data.filter((e) => e.clubId === clubId));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
    }
  }

  async function handleSportChange(sportId: string) {
    setSelectedSportId(sportId);
    await loadExercises(sportId, selectedClubId);
  }

  async function handleClubChange(clubId: string) {
    setSelectedClubId(clubId);
    await loadExercises(selectedSportId, clubId);
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedSportId || !selectedClubId || !name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/exercises", {
        sportId: selectedSportId,
        name,
        description: null,
        difficulty,
        category: null,
        scoringCriteria: scoringCriteria || null,
        clubId: selectedClubId,
      });
      toast.success("Vereinsübung angelegt.");
      setName("");
      setScoringCriteria("");
      await loadExercises(selectedSportId, selectedClubId);
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
      await loadExercises(selectedSportId, selectedClubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Dumbbell className="size-5" />
          Vereinsspezifische Übungen
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-col gap-3 sm:flex-row">
          {clubs.length > 1 && (
            <div className="flex flex-col gap-2 sm:w-56">
              <Label>Verein</Label>
              <select
                className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                value={selectedClubId}
                onChange={(e) => handleClubChange(e.target.value)}
              >
                {clubs.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="flex flex-col gap-2 sm:w-56">
            <Label>Sportart</Label>
            <select
              className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
              value={selectedSportId}
              onChange={(e) => handleSportChange(e.target.value)}
            >
              <option value="">Auswählen…</option>
              {sports.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        {selectedSportId && (
          <>
            <form onSubmit={handleCreate} className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex flex-col gap-2 sm:flex-1">
                <Label htmlFor="club-ex-name">Name</Label>
                <Input id="club-ex-name" value={name} onChange={(e) => setName(e.target.value)} required />
              </div>
              <div className="flex flex-col gap-2 sm:flex-1">
                <Label htmlFor="club-ex-scoring">Bewertungskriterien</Label>
                <Input
                  id="club-ex-scoring"
                  value={scoringCriteria}
                  onChange={(e) => setScoringCriteria(e.target.value)}
                />
              </div>
              <div className="flex flex-col gap-2 sm:w-40">
                <Label>Schwierigkeit</Label>
                <select
                  className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                  value={difficulty}
                  onChange={(e) => setDifficulty(e.target.value as ExerciseDifficulty)}
                >
                  {Object.entries(difficultyLabel).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </div>
              <Button type="submit" disabled={submitting}>
                <Plus className="size-4" />
                Anlegen
              </Button>
            </form>

            <ul className="flex flex-col gap-2">
              {exercises.map((ex) => (
                <li key={ex.id} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                  <span className="font-medium">{ex.name}</span>
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

"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainerExerciseToRate } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ClipboardCheck } from "lucide-react";
import { toast } from "sonner";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

/**
 * Trainerseite: alle noch nicht bewerteten Übungen der betreuten Hunde auf
 * einen Blick - welche Übung, welcher Hund, welcher Hundeführer - mit direkter
 * Bewertung, ohne dafür ins Tagebuch des jeweiligen Hundes wechseln zu müssen.
 * Nach dem Bewerten fällt die Übung aus der Liste (Neuladen).
 */
export function TrainerExerciseRatingSection() {
  const [items, setItems] = useState<TrainerExerciseToRate[] | null>(null);

  async function load() {
    try {
      const data = await api.get<TrainerExerciseToRate[]>("/api/trainings/trainer/exercises");
      setItems(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, []);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ClipboardCheck className="size-5" />
          Übungen bewerten
        </CardTitle>
      </CardHeader>
      <CardContent>
        {items === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : items.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Keine offenen Übungen - alle Übungen der betreuten Hunde sind bewertet.
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {items.map((item) => (
              <li key={item.exerciseId} className="flex flex-col gap-1.5 rounded-md border px-3 py-2">
                <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5">
                  <span className="text-sm">
                    <span className="font-medium">{item.exerciseName}</span> · {item.dogName} ({item.handlerName})
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {new Date(item.date).toLocaleDateString("de-DE")} · Eigenbewertung{" "}
                    <span className="text-primary">
                      {"★".repeat(item.rating)}
                      {"☆".repeat(5 - item.rating)}
                    </span>{" "}
                    {item.success ? "✓" : "✗"}
                  </span>
                </div>
                <ExerciseTrainerRating exerciseId={item.exerciseId} rating={null} note={null} canEdit onSaved={load} />
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

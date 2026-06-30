"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import type { Dog, Goal, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Printer } from "lucide-react";
import { toast } from "sonner";
import { difficultyLabel } from "@/lib/constants";

const GOAL_STATUS_LABEL: Record<number, string> = { 0: "Aktiv", 1: "Erreicht", 2: "Abgebrochen" };

export default function DogPrintPage() {
  const { id } = useParams<{ id: string }>();
  const [dog, setDog] = useState<Dog | null>(null);
  const [goals, setGoals] = useState<Goal[] | null>(null);
  const [sessions, setSessions] = useState<TrainingSession[] | null>(null);

  useEffect(() => {
    Promise.all([
      api.get<Dog>(`/api/dogs/${id}`),
      api.get<Goal[]>(`/api/goals?dogId=${id}`),
      api.get<TrainingSession[]>(`/api/trainings?dogId=${id}`),
    ])
      .then(([dogData, goalData, sessionData]) => {
        setDog(dogData);
        setGoals(goalData);
        setSessions(sessionData);
      })
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Daten konnten nicht geladen werden."));
  }, [id]);

  if (!dog || !goals || !sessions) {
    return <p className="p-6 text-muted-foreground">Lädt…</p>;
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-8 p-6 print:p-0">
      <div className="flex items-center justify-between print:hidden">
        <h1 className="text-2xl font-semibold tracking-tight">Druckansicht: {dog.name}</h1>
        <Button onClick={() => window.print()}>
          <Printer className="size-4" />
          Drucken
        </Button>
      </div>

      <header className="hidden border-b pb-4 print:block">
        <h1 className="text-2xl font-semibold">{dog.name}</h1>
        <p className="text-sm text-muted-foreground">
          {dog.breed ?? "Rasse unbekannt"}
          {dog.birthday && ` · geboren ${new Date(dog.birthday).toLocaleDateString("de-DE")}`}
        </p>
      </header>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold">Trainingspläne</h2>
        {goals.length === 0 ? (
          <p className="text-sm text-muted-foreground">Keine Ziele vorhanden.</p>
        ) : (
          goals.map((goal) => (
            <div key={goal.id} className="break-inside-avoid rounded-md border p-4">
              <div className="flex items-center justify-between">
                <span className="font-medium">
                  {goal.sportName}
                  {goal.regulationName && ` - ${goal.regulationName}`}
                </span>
                <span className="text-sm text-muted-foreground">
                  {GOAL_STATUS_LABEL[goal.status]} · Ziel: {new Date(goal.targetDate).toLocaleDateString("de-DE")}
                </span>
              </div>
              {goal.notes && <p className="mt-1 text-sm text-muted-foreground">{goal.notes}</p>}
              {goal.trainingPlan && goal.trainingPlan.items.length > 0 && (
                <table className="mt-3 w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="py-1 pr-2">KW</th>
                      <th className="py-1 pr-2">Übung</th>
                      <th className="py-1 pr-2">Ziel</th>
                      <th className="py-1">Fortschritt</th>
                    </tr>
                  </thead>
                  <tbody>
                    {goal.trainingPlan.items.map((item) => (
                      <tr key={item.id} className="border-b last:border-b-0">
                        <td className="py-1 pr-2">{item.weekNumber}</td>
                        <td className="py-1 pr-2">{item.isRestWeek ? "Pausenwoche" : item.exerciseName ?? "-"}</td>
                        <td className="py-1 pr-2">{item.isRestWeek ? "-" : item.repetitionsTarget}</td>
                        <td className="py-1">
                          {item.isRestWeek ? "-" : `${item.completedCount}/${item.repetitionsTarget}${item.isComplete ? " ✓" : ""}`}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          ))
        )}
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold">Trainingshistorie</h2>
        {sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">Noch keine Trainingseinträge.</p>
        ) : (
          sessions.map((session) => (
            <div key={session.id} className="break-inside-avoid rounded-md border p-4">
              <div className="flex items-center justify-between">
                <span className="font-medium">{new Date(session.date).toLocaleDateString("de-DE")}</span>
                <span className="text-sm text-muted-foreground">{session.durationMinutes} Min.</span>
              </div>
              {session.notes && <p className="mt-1 text-sm text-muted-foreground">{session.notes}</p>}
              {session.exercises.length > 0 && (
                <ul className="mt-2 flex flex-col gap-1 text-sm">
                  {session.exercises.map((ex) => (
                    <li key={ex.id}>
                      {ex.exerciseName} - {difficultyLabel[ex.difficulty]}, Bewertung {ex.rating}/5{ex.success ? "" : " (nicht erfolgreich)"}
                      {ex.notes && ` - ${ex.notes}`}
                    </li>
                  ))}
                </ul>
              )}
              {session.trainerFeedback && (
                <p className="mt-2 text-sm">
                  <span className="font-medium">Trainer-Feedback: </span>
                  {session.trainerFeedback}
                </p>
              )}
            </div>
          ))
        )}
      </section>
    </div>
  );
}

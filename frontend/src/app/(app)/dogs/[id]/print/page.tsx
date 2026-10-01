"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import type { Dog, Goal, TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Printer } from "lucide-react";
import { toast } from "sonner";

import { useSprache, useT } from "@/lib/i18n";
import { datumKurz, leistungen, pruefungsName, punkteText } from "@/lib/pruefung";
import { ortsformat } from "@/lib/ortsformat";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { groupByWeek, istPausenwoche } from "@/lib/trainingsplan";

const GOAL_STATUS_LABEL: Record<number, string> = { 0: uebersetzbar("Aktiv"), 1: uebersetzbar("Erreicht"), 2: uebersetzbar("Abgebrochen") };

export default function DogPrintPage() {
  const t = useT();
  const sprache = useSprache();
  const ort = ortsformat(sprache);
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
      .catch((err) => toast.error(err instanceof ApiError ? err.message : t("Daten konnten nicht geladen werden.")));
    // t bewusst nicht in der Liste: Der Uebersetzer wird hier nur im
    // Fehlerfall gebraucht. Stuende er drin, liefe der ganze Abruf bei
    // jedem Sprachwechsel erneut - Daten neu laden, weil ein Toast
    // anders heissen wuerde.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  if (!dog || !goals || !sessions) {
    return <p className="p-6 text-muted-foreground">{t("Lädt…")}</p>;
  }

  const planZiele = [...goals].sort((a, b) => (a.status === 0 ? 0 : 1) - (b.status === 0 ? 0 : 1));

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-8 p-6 print:p-0">
      <div className="flex items-center justify-between print:hidden">
        <h1 className="text-2xl font-semibold tracking-tight">{t("Druckansicht: {name}", { name: dog.name })}</h1>
        <Button onClick={() => window.print()}>
          <Printer className="size-4" />
          {t("Drucken")}
        </Button>
      </div>

      <header className="hidden border-b pb-4 print:block">
        <h1 className="text-2xl font-semibold">{dog.name}</h1>
        <p className="text-sm text-muted-foreground">
          {dog.breed ?? t("Rasse unbekannt")}
          {dog.birthday && ` · ${t("geboren {datum}", { datum: new Date(dog.birthday).toLocaleDateString(ort) })}`}
        </p>
      </header>

      {leistungen(goals).length > 0 && (
        <section className="flex flex-col gap-4">
          <h2 className="text-lg font-semibold">{t("Leistungen")}</h2>
          {leistungen(goals).map((goal) => (
            <div key={goal.id} className="break-inside-avoid rounded-md border p-4">
              <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                <span className="min-w-0 font-medium [overflow-wrap:anywhere]">{pruefungsName(goal)}</span>
                <span className="text-sm text-muted-foreground">{t("bestanden")}</span>
              </div>
              <p className="mt-1 text-sm text-muted-foreground">
                {[
                  goal.examDate ? datumKurz(goal.examDate, sprache) : t("Ohne Datum"),
                  goal.examScore != null ? punkteText(t, goal.examScore, goal.maxPoints) : null,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
              {goal.examNote && <p className="mt-1 text-sm [overflow-wrap:anywhere]">{goal.examNote}</p>}
            </div>
          ))}
        </section>
      )}

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold">{t("Trainingspläne")}</h2>
        {/* Wie auf der Hundeseite bleiben auch die Pläne erreichter und
            abgebrochener Ziele erhalten (Verlauf). Das laufende Ziel steht
            zuerst, damit man den aktuellen Plan nicht zwischen alten suchen muss. */}
        {planZiele.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Keine Ziele vorhanden.")}</p>
        ) : (
          planZiele.map((goal) => (
            <div key={goal.id} className="rounded-md border p-4">
              <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                <span className="min-w-0 font-medium [overflow-wrap:anywhere]">
                  {goal.sportName}
                  {goal.regulationName && ` - ${goal.regulationName}`}
                </span>
                <span className="text-sm text-muted-foreground">
                  {t(GOAL_STATUS_LABEL[goal.status])} · {t("Ziel: {datum}", { datum: new Date(goal.targetDate).toLocaleDateString(ort) })}
                </span>
              </div>
              {goal.notes && <p className="mt-1 text-sm text-muted-foreground [overflow-wrap:anywhere]">{goal.notes}</p>}
              {goal.trainingPlan && goal.trainingPlan.items.length > 0 && (
                <table className="mt-3 w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="py-1 pr-2">{t("KW")}</th>
                      <th className="py-1 pr-2">{t("Übung")}</th>
                      <th className="py-1 pr-2">{t("Ziel")}</th>
                      <th className="py-1">{t("Fortschritt")}</th>
                    </tr>
                  </thead>
                  {/* Je Woche ein eigener Block, damit der Umbruch beim Drucken
                      zwischen den Wochen fällt und nicht mitten durch eine. */}
                  {groupByWeek(goal.trainingPlan.items).map(([woche, items]) => (
                    <tbody key={woche} className="break-inside-avoid border-b last:border-b-0">
                      {istPausenwoche(items) ? (
                        <tr>
                          <td className="py-1 pr-2">{woche}</td>
                          <td className="py-1 pr-2 text-muted-foreground" colSpan={3}>{t("Pausenwoche")}</td>
                        </tr>
                      ) : (
                        [...items]
                          .sort((a, b) => a.dayIndex - b.dayIndex)
                          .map((item, index) => (
                            <tr key={item.id}>
                              <td className="py-1 pr-2">{index === 0 ? woche : ""}</td>
                              <td className="py-1 pr-2 [overflow-wrap:anywhere]">
                                {item.exerciseName ?? item.freeTextLabel ?? t("(Übung nicht mehr verfügbar)")}
                              </td>
                              <td className="py-1 pr-2">{item.repetitionsTarget}</td>
                              <td className="py-1">
                                {item.completedCount}/{item.repetitionsTarget}
                                {item.isComplete ? " ✓" : ""}
                              </td>
                            </tr>
                          ))
                      )}
                    </tbody>
                  ))}
                </table>
              )}
            </div>
          ))
        )}
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold">{t("Trainingshistorie")}</h2>
        {sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Noch keine Trainingseinträge.")}</p>
        ) : (
          sessions.map((session) => (
            <div key={session.id} className="break-inside-avoid rounded-md border p-4">
              <div className="flex items-center justify-between">
                <span className="font-medium">{new Date(session.date).toLocaleDateString(ort)}</span>
                <span className="text-sm text-muted-foreground">{t("{n} Min.", { n: session.durationMinutes })}</span>
              </div>
              {session.notes && <p className="mt-1 text-sm text-muted-foreground">{session.notes}</p>}
              {session.exercises.length > 0 && (
                <ul className="mt-2 flex flex-col gap-1 text-sm">
                  {session.exercises.map((ex) => (
                    <li key={ex.id}>
                      {ex.exerciseName} - {t("Bewertung {n}/5", { n: ex.rating })}{ex.success ? "" : ` ${t("(nicht erfolgreich)")}`}
                      {ex.notes && ` - ${ex.notes}`}
                    </li>
                  ))}
                </ul>
              )}
              {session.trainerFeedback && (
                <p className="mt-2 text-sm">
                  <span className="font-medium">{t("Trainer-Feedback:")}</span> {session.trainerFeedback}
                </p>
              )}
            </div>
          ))
        )}
      </section>
    </div>
  );
}

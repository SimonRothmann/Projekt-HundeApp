"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, Regulation, RegulationDetail, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Trophy, ChevronDown, ChevronRight, ScrollText } from "lucide-react";
import { toast } from "sonner";

const difficultyLabel: Record<Exercise["difficulty"], string> = {
  Beginner: "Einsteiger",
  Intermediate: "Fortgeschritten",
  Advanced: "Erfahren",
};

export default function SportsPage() {
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});
  const [regulationsBySport, setRegulationsBySport] = useState<Record<string, Regulation[]>>({});
  const [expandedRegulation, setExpandedRegulation] = useState<string | null>(null);
  const [regulationDetails, setRegulationDetails] = useState<Record<string, RegulationDetail>>({});

  useEffect(() => {
    api
      .get<Sport[]>("/api/sports")
      .then(setSports)
      .catch((err) =>
        toast.error(err instanceof ApiError ? err.message : "Sportarten konnten nicht geladen werden."),
      );
  }, []);

  async function toggleExpand(sportId: string) {
    if (expanded === sportId) {
      setExpanded(null);
      return;
    }
    setExpanded(sportId);
    try {
      if (!exercisesBySport[sportId]) {
        const exercises = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
        setExercisesBySport((prev) => ({ ...prev, [sportId]: exercises }));
      }
      if (!regulationsBySport[sportId]) {
        const regulations = await api.get<Regulation[]>(`/api/sports/${sportId}/regulations`);
        setRegulationsBySport((prev) => ({ ...prev, [sportId]: regulations }));
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Daten konnten nicht geladen werden.");
    }
  }

  async function toggleRegulation(regulationId: string) {
    if (expandedRegulation === regulationId) {
      setExpandedRegulation(null);
      return;
    }
    setExpandedRegulation(regulationId);
    if (!regulationDetails[regulationId]) {
      try {
        const detail = await api.get<RegulationDetail>(`/api/sports/regulations/${regulationId}`);
        setRegulationDetails((prev) => ({ ...prev, [regulationId]: detail }));
      } catch (err) {
        toast.error(err instanceof ApiError ? err.message : "Prüfungsordnung konnte nicht geladen werden.");
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Sportarten</h1>
        <p className="text-muted-foreground">
          Datengetriebener Katalog aus Übungen und Prüfungsordnungen (siehe DATABASE.md). Übungstexte und
          Bewertungskriterien sind eigene Beschreibungen, keine Kopie offizieller Prüfungsordnungen.
        </p>
      </div>

      {sports === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : (
        <div className="flex flex-col gap-3">
          {sports.map((sport) => {
            const isOpen = expanded === sport.id;
            const exercises = exercisesBySport[sport.id];
            const regulations = regulationsBySport[sport.id];
            return (
              <Card key={sport.id}>
                <CardHeader
                  className="flex-row cursor-pointer items-center justify-between space-y-0"
                  onClick={() => toggleExpand(sport.id)}
                >
                  <div className="flex items-center gap-3">
                    <Trophy className="size-6 text-primary" />
                    <div>
                      <CardTitle className="text-base">{sport.name}</CardTitle>
                      <Badge variant="secondary">{sport.code}</Badge>
                    </div>
                  </div>
                  {isOpen ? <ChevronDown className="size-5" /> : <ChevronRight className="size-5" />}
                </CardHeader>
                {isOpen && (
                  <CardContent className="flex flex-col gap-5">
                    <div>
                      <h3 className="mb-2 text-sm font-semibold text-muted-foreground">Übungen</h3>
                      {!exercises ? (
                        <p className="text-sm text-muted-foreground">Lädt Übungen…</p>
                      ) : exercises.length === 0 ? (
                        <p className="text-sm text-muted-foreground">Noch keine Übungen hinterlegt.</p>
                      ) : (
                        <ul className="flex flex-col gap-2">
                          {exercises.map((exercise) => (
                            <li key={exercise.id} className="rounded-md border px-3 py-2">
                              <div className="flex items-center justify-between text-sm">
                                <span className="font-medium">{exercise.name}</span>
                                <Badge variant="outline">{difficultyLabel[exercise.difficulty]}</Badge>
                              </div>
                              {exercise.scoringCriteria && (
                                <p className="mt-1 text-sm text-muted-foreground">{exercise.scoringCriteria}</p>
                              )}
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>

                    <div>
                      <h3 className="mb-2 text-sm font-semibold text-muted-foreground">Prüfungsordnungen</h3>
                      {!regulations ? (
                        <p className="text-sm text-muted-foreground">Lädt…</p>
                      ) : regulations.length === 0 ? (
                        <p className="text-sm text-muted-foreground">Noch keine Prüfungsordnung hinterlegt.</p>
                      ) : (
                        <div className="flex flex-col gap-2">
                          {regulations.map((regulation) => {
                            const detail = regulationDetails[regulation.id];
                            const regOpen = expandedRegulation === regulation.id;
                            return (
                              <div key={regulation.id} className="rounded-md border">
                                <button
                                  type="button"
                                  className="flex w-full items-center justify-between px-3 py-2 text-sm"
                                  onClick={() => toggleRegulation(regulation.id)}
                                >
                                  <span className="flex items-center gap-2 font-medium">
                                    <ScrollText className="size-4" />
                                    {regulation.name}
                                  </span>
                                  {regOpen ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                                </button>
                                {regOpen && (
                                  <div className="border-t px-3 py-2">
                                    {!detail ? (
                                      <p className="text-sm text-muted-foreground">Lädt…</p>
                                    ) : (
                                      <>
                                        <p className="mb-2 text-xs text-muted-foreground">
                                          Version {detail.currentVersion.versionLabel} · gültig ab{" "}
                                          {new Date(detail.currentVersion.validFrom).toLocaleDateString("de-DE")}
                                        </p>
                                        <ul className="flex flex-col gap-2">
                                          {detail.exercises.map((re) => (
                                            <li key={re.exerciseId} className="text-sm">
                                              <div className="flex items-center justify-between">
                                                <span className="font-medium">{re.exerciseName}</span>
                                                {re.maxPoints > 0 && (
                                                  <Badge variant="outline">{re.maxPoints} Punkte</Badge>
                                                )}
                                              </div>
                                              {re.scoringNotes && (
                                                <p className="text-muted-foreground">{re.scoringNotes}</p>
                                              )}
                                            </li>
                                          ))}
                                        </ul>
                                      </>
                                    )}
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  </CardContent>
                )}
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

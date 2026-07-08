"use client";

import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import type { Exercise, Regulation, RegulationDetail, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Trophy,
  ChevronDown,
  ChevronRight,
  ScrollText,
  Sparkles,
  Building2,
} from "lucide-react";
import { toast } from "sonner";
import { difficultyLabel } from "@/lib/constants";

// GET mit Offline-Fallback: frische Daten laden und cachen; schlägt das
// Netz fehl (offline auf dem Hundeplatz), die zuletzt gesehene Version aus
// dem Read-Cache liefern. Wirft nur, wenn beides fehlschlägt.
async function getWithCache<T>(path: string): Promise<T> {
  try {
    const fresh = await api.get<T>(path);
    await setCachedData(path, fresh);
    return fresh;
  } catch (err) {
    const cached = await getCachedData<T>(path);
    if (cached) return cached;
    throw err;
  }
}

export default function SportsPage() {
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [uncategorized, setUncategorized] = useState<Exercise[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [showUncategorized, setShowUncategorized] = useState(false);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});
  const [regulationsBySport, setRegulationsBySport] = useState<Record<string, Regulation[]>>({});
  const [expandedRegulation, setExpandedRegulation] = useState<string | null>(null);
  const [regulationDetails, setRegulationDetails] = useState<Record<string, RegulationDetail>>({});

  const globalSports = useMemo(() => sports?.filter((s) => !s.clubId) ?? [], [sports]);
  const clubSports = useMemo(() => sports?.filter((s) => s.clubId) ?? [], [sports]);

  useEffect(() => {
    getWithCache<Sport[]>("/api/sports")
      .then(setSports)
      .catch((err) =>
        toast.error(err instanceof ApiError ? err.message : "Sportarten konnten nicht geladen werden."),
      );
    getWithCache<Exercise[]>("/api/exercises/uncategorized")
      .then(setUncategorized)
      .catch(() => {
        // Kein Toast: erwartet fehlend für Nutzer ohne Zugriff.
      });
  }, []);

  async function toggleExpand(sportId: string) {
    if (expanded === sportId) {
      setExpanded(null);
      return;
    }
    setExpanded(sportId);
    try {
      if (!exercisesBySport[sportId]) {
        const exercises = await getWithCache<Exercise[]>(`/api/sports/${sportId}/exercises`);
        setExercisesBySport((prev) => ({ ...prev, [sportId]: exercises }));
      }
      if (!regulationsBySport[sportId]) {
        const regulations = await getWithCache<Regulation[]>(`/api/sports/${sportId}/regulations`);
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
        const detail = await getWithCache<RegulationDetail>(`/api/sports/regulations/${regulationId}`);
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
          Informativer Katalog nach VDH-Prüfungsordnungen. Übungstexte und Bewertungskriterien sind
          eigene Beschreibungen, keine Kopie offizieller Prüfungsordnungen. Globale Sportarten pflegt
          der Admin; jeder Verein kann zusätzlich eigene Übungen und Sportarten anlegen (siehe
          Trainer-Bereich).
        </p>
      </div>

      {sports === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : (
        <div className="flex flex-col gap-3">
          {/* Sportartlose Übungen als eigene Karte - direkt oben, weil sie
              kontextlos existieren und sonst leicht übersehen werden. */}
          {uncategorized.length > 0 && (
            <Card className="border-dashed">
              <CardHeader
                className="flex-row cursor-pointer items-center justify-between space-y-0"
                onClick={() => setShowUncategorized((prev) => !prev)}
              >
                <div className="flex items-center gap-3">
                  <Sparkles className="size-6 text-muted-foreground" />
                  <div>
                    <CardTitle className="text-base">Ohne Sportart</CardTitle>
                    <p className="text-xs text-muted-foreground">
                      Übergreifende Übungen ohne feste Sport-Zuordnung
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary">{uncategorized.length}</Badge>
                  {showUncategorized ? (
                    <ChevronDown className="size-5" />
                  ) : (
                    <ChevronRight className="size-5" />
                  )}
                </div>
              </CardHeader>
              {showUncategorized && (
                <CardContent className="flex flex-col gap-3">
                  {uncategorized.length === 0 ? (
                    <p className="text-sm text-muted-foreground">
                      Noch keine sportartlosen Übungen. Neue Übung anlegen und &bdquo;Ohne Sportart&ldquo; wählen.
                    </p>
                  ) : (
                    <ul className="flex flex-col gap-2">
                      {uncategorized.map((exercise) => (
                        <ExerciseRow key={exercise.id} exercise={exercise} />
                      ))}
                    </ul>
                  )}
                </CardContent>
              )}
            </Card>
          )}

          {/* Globale Sportarten */}
          {globalSports.map((sport) => (
            <SportCard
              key={sport.id}
              sport={sport}
              isOpen={expanded === sport.id}
              exercises={exercisesBySport[sport.id]}
              regulations={regulationsBySport[sport.id]}
              regulationDetails={regulationDetails}
              expandedRegulation={expandedRegulation}
              onToggle={() => toggleExpand(sport.id)}
              onToggleRegulation={toggleRegulation}
            />
          ))}

          {/* Vereinsspezifische Sportarten, klar abgesetzt */}
          {clubSports.length > 0 && (
            <div className="flex items-center gap-2 pt-2 text-sm text-muted-foreground">
              <Building2 className="size-4" />
              Vereinsspezifische Sportarten
            </div>
          )}
          {clubSports.map((sport) => (
            <SportCard
              key={sport.id}
              sport={sport}
              isOpen={expanded === sport.id}
              exercises={exercisesBySport[sport.id]}
              regulations={regulationsBySport[sport.id]}
              regulationDetails={regulationDetails}
              expandedRegulation={expandedRegulation}
              onToggle={() => toggleExpand(sport.id)}
              onToggleRegulation={toggleRegulation}
            />
          ))}
        </div>
      )}

    </div>
  );
}

function SportCard({
  sport,
  isOpen,
  exercises,
  regulations,
  regulationDetails,
  expandedRegulation,
  onToggle,
  onToggleRegulation,
}: {
  sport: Sport;
  isOpen: boolean;
  exercises: Exercise[] | undefined;
  regulations: Regulation[] | undefined;
  regulationDetails: Record<string, RegulationDetail>;
  expandedRegulation: string | null;
  onToggle: () => void;
  onToggleRegulation: (id: string) => void;
}) {
  return (
    <Card>
      <CardHeader
        className="flex-row cursor-pointer items-center justify-between space-y-0"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <Trophy className={sport.clubId ? "size-6 text-amber-500" : "size-6 text-primary"} />
          <div>
            <div className="flex items-center gap-2">
              <CardTitle className="text-base">{sport.name}</CardTitle>
              {sport.clubId && (
                <Badge variant="outline" className="border-amber-500/40 text-amber-700">
                  Verein
                </Badge>
              )}
            </div>
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
                  <ExerciseRow key={exercise.id} exercise={exercise} />
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
                        onClick={() => onToggleRegulation(regulation.id)}
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
}

function ExerciseRow({ exercise }: { exercise: Exercise }) {
  return (
    <li className="rounded-md border px-3 py-2">
      <div className="flex items-center justify-between text-sm">
        <div className="flex items-center gap-2">
          <span className="font-medium">{exercise.name}</span>
          {exercise.clubId && (
            <Badge variant="outline" className="border-amber-500/40 text-amber-700">
              Verein
            </Badge>
          )}
        </div>
        <Badge variant="outline">{difficultyLabel[exercise.difficulty]}</Badge>
      </div>
      {exercise.category && (
        <p className="mt-0.5 text-xs text-muted-foreground">{exercise.category}</p>
      )}
      {exercise.scoringCriteria && (
        <p className="mt-1 text-sm text-muted-foreground">{exercise.scoringCriteria}</p>
      )}
    </li>
  );
}

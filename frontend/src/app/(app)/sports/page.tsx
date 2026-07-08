"use client";

import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import type { Club, Exercise, Regulation, RegulationDetail, Sport } from "@/lib/types";
import { useAuth } from "@/lib/auth-context";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Trophy,
  ChevronDown,
  ChevronRight,
  ScrollText,
  Plus,
  Sparkles,
  Building2,
} from "lucide-react";
import { toast } from "sonner";
import { difficultyLabel } from "@/lib/constants";
import { SportEditorSheet } from "@/components/sports/sport-editor-sheet";
import { ExerciseEditorSheet } from "@/components/sports/exercise-editor-sheet";

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
  const { user, isTrainer } = useAuth();
  const isAdmin = !!user?.roles.includes("ADMIN");
  const canManage = isAdmin || !!isTrainer;

  const [sports, setSports] = useState<Sport[] | null>(null);
  const [uncategorized, setUncategorized] = useState<Exercise[]>([]);
  const [myClubs, setMyClubs] = useState<Club[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [showUncategorized, setShowUncategorized] = useState(false);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});
  const [regulationsBySport, setRegulationsBySport] = useState<Record<string, Regulation[]>>({});
  const [expandedRegulation, setExpandedRegulation] = useState<string | null>(null);
  const [regulationDetails, setRegulationDetails] = useState<Record<string, RegulationDetail>>({});

  // Editor-State: welche Sportart bzw. welcher Verein wird beim Öffnen des
  // Übungseditors vorbelegt (z. B. beim "+ Übung"-Klick auf einer Karte).
  const [sportEditorOpen, setSportEditorOpen] = useState(false);
  const [exerciseEditor, setExerciseEditor] = useState<{
    open: boolean;
    sportId: string | null;
    clubId: string | null;
  }>({ open: false, sportId: null, clubId: null });

  const globalSports = useMemo(() => sports?.filter((s) => !s.clubId) ?? [], [sports]);
  const clubSports = useMemo(() => sports?.filter((s) => s.clubId) ?? [], [sports]);

  useEffect(() => {
    getWithCache<Sport[]>("/api/sports")
      .then(setSports)
      .catch((err) =>
        toast.error(err instanceof ApiError ? err.message : "Sportarten konnten nicht geladen werden."),
      );
    // Sportartlose Übungen (getrennt geladen, damit sie als eigene Karte
    // erscheinen können).
    getWithCache<Exercise[]>("/api/exercises/uncategorized")
      .then(setUncategorized)
      .catch(() => {
        // Kein Toast: für Nutzer ohne Zugriff (unauthenticated) erwartet fehlend.
      });
    if (canManage) {
      // Vereine, in denen der Nutzer als Trainer geführt wird - Basis für die
      // Sichtbarkeits-Auswahl beim Anlegen. Admins sehen alle Vereine als
      // Auswahl (nicht "meine").
      const clubsPath = isAdmin ? "/api/admin/clubs" : "/api/groups/my-clubs";
      api
        .get<Club[]>(clubsPath)
        .then(setMyClubs)
        .catch(() => {
          // Stille: Auswahl bleibt leer, User bekommt Fehlermeldung erst
          // beim Anlegen (Fall unwahrscheinlich für Admin/Trainer).
        });
    }
  }, [canManage, isAdmin]);

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

  function handleSportCreated(sport: Sport) {
    setSports((prev) => (prev ? [...prev, sport].sort((a, b) => a.name.localeCompare(b.name, "de")) : [sport]));
    // Übungsliste dieser Sportart als "leer" vormerken, damit beim Aufklappen
    // sofort "Keine Übungen" statt "Lädt…" erscheint.
    setExercisesBySport((prev) => ({ ...prev, [sport.id]: [] }));
    setRegulationsBySport((prev) => ({ ...prev, [sport.id]: [] }));
    setExpanded(sport.id);
  }

  function handleExerciseCreated(exercise: Exercise) {
    if (exercise.sportId === null) {
      setUncategorized((prev) => [...prev, exercise].sort((a, b) => a.name.localeCompare(b.name, "de")));
      setShowUncategorized(true);
    } else {
      setExercisesBySport((prev) => {
        const list = prev[exercise.sportId!] ?? [];
        return {
          ...prev,
          [exercise.sportId!]: [...list, exercise].sort((a, b) => a.name.localeCompare(b.name, "de")),
        };
      });
      setExpanded(exercise.sportId);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Sportarten</h1>
          <p className="text-muted-foreground">
            Datengetriebener Katalog aus Übungen und Prüfungsordnungen. Übungstexte und Bewertungs-
            kriterien sind eigene Beschreibungen, keine Kopie offizieller Prüfungsordnungen.
          </p>
        </div>
        {canManage && (
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setSportEditorOpen(true)}>
              <Plus className="size-4" />
              Sportart
            </Button>
            <Button onClick={() => setExerciseEditor({ open: true, sportId: null, clubId: null })}>
              <Plus className="size-4" />
              Übung
            </Button>
          </div>
        )}
      </div>

      {sports === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : (
        <div className="flex flex-col gap-3">
          {/* Sportartlose Übungen als eigene Karte - direkt oben, weil sie
              kontextlos existieren und sonst leicht übersehen werden. */}
          {(uncategorized.length > 0 || canManage) && (
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
              onAddExercise={
                canManage
                  ? () => setExerciseEditor({ open: true, sportId: sport.id, clubId: sport.clubId })
                  : null
              }
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
              onAddExercise={
                canManage
                  ? () => setExerciseEditor({ open: true, sportId: sport.id, clubId: sport.clubId })
                  : null
              }
            />
          ))}
        </div>
      )}

      <SportEditorSheet
        open={sportEditorOpen}
        onOpenChange={setSportEditorOpen}
        isAdmin={isAdmin}
        clubs={myClubs}
        onCreated={handleSportCreated}
      />
      <ExerciseEditorSheet
        open={exerciseEditor.open}
        onOpenChange={(open) => setExerciseEditor((prev) => ({ ...prev, open }))}
        isAdmin={isAdmin}
        sports={sports ?? []}
        clubs={myClubs}
        presetSportId={exerciseEditor.sportId}
        presetClubId={exerciseEditor.clubId}
        onCreated={handleExerciseCreated}
      />
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
  onAddExercise,
}: {
  sport: Sport;
  isOpen: boolean;
  exercises: Exercise[] | undefined;
  regulations: Regulation[] | undefined;
  regulationDetails: Record<string, RegulationDetail>;
  expandedRegulation: string | null;
  onToggle: () => void;
  onToggleRegulation: (id: string) => void;
  onAddExercise: (() => void) | null;
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
            <div className="mb-2 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-muted-foreground">Übungen</h3>
              {onAddExercise && (
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={(e) => {
                    e.stopPropagation();
                    onAddExercise();
                  }}
                >
                  <Plus className="size-4" />
                  Übung
                </Button>
              )}
            </div>
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

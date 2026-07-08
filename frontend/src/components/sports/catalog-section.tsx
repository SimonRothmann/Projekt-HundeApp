"use client";

import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { BookOpen, ChevronDown, ChevronRight, Plus, Sparkles, Trash2, Trophy } from "lucide-react";
import { toast } from "sonner";
import { difficultyLabel } from "@/lib/constants";
import { SportEditorSheet, type SportScope } from "@/components/sports/sport-editor-sheet";
import { ExerciseEditorSheet, type ExerciseScope } from "@/components/sports/exercise-editor-sheet";

/**
 * Karte "Sportarten & Übungen verwalten". Wird von zwei Stellen genutzt:
 * - Admin-Bereich mit scope="global": pflegt den globalen VDH-Katalog.
 * - Trainer-Bereich pro Verein: pflegt die vereinseigenen Sportarten und
 *   Übungen. Zusätzlich werden globale Sportarten als "Übungen anlegen"-
 *   Ziel angeboten (der Trainer kann eine Vereinsübung an einer globalen
 *   Sportart hängen, ohne die Sportart selbst zu duplizieren).
 *
 * Angezeigt werden nur die zum Scope passenden Sportarten (Global-Katalog
 * ohne Vereinsartige, Vereins-Katalog nur Sportarten dieses Vereins).
 * "Ohne Sportart" fasst sportartlose Übungen desselben Scopes zusammen.
 */
export function CatalogSection({
  scope,
  title,
  description,
}: {
  scope: SportScope;
  title: string;
  description: string;
}) {
  const [allSports, setAllSports] = useState<Sport[] | null>(null);
  const [uncategorized, setUncategorized] = useState<Exercise[]>([]);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});
  const [expanded, setExpanded] = useState<string | null>(null);
  const [showUncategorized, setShowUncategorized] = useState(false);

  const [sportEditorOpen, setSportEditorOpen] = useState(false);
  const [exerciseEditor, setExerciseEditor] = useState<{ open: boolean; sportId: string | null }>({
    open: false,
    sportId: null,
  });

  // Sportarten dieses Scopes: für Admin nur globale, für Trainer nur die
  // seines Vereins. Der Übungs-Editor darf zusätzlich globale Sportarten als
  // Ziel anbieten, wenn wir im Vereinsscope sind - deshalb `allSports` roh
  // laden und lokal filtern.
  const sportsInScope = useMemo(() => {
    if (!allSports) return [];
    return allSports.filter((s) =>
      scope.kind === "global" ? s.clubId === null : s.clubId === scope.clubId,
    );
  }, [allSports, scope]);

  // Auswahl im Übungs-Editor: eigene Scope-Sportarten + globale (Vereins-
  // trainer darf eine Vereinsübung an eine globale Sportart hängen).
  const sportsForExerciseSelect = useMemo(() => {
    if (!allSports) return [];
    return scope.kind === "global"
      ? allSports.filter((s) => s.clubId === null)
      : allSports;
  }, [allSports, scope]);

  const scopeUncategorized = useMemo(
    () =>
      uncategorized.filter((e) =>
        scope.kind === "global" ? e.clubId === null : e.clubId === scope.clubId,
      ),
    [uncategorized, scope],
  );

  async function loadSports() {
    try {
      const data = await api.get<Sport[]>("/api/sports");
      setAllSports(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Sportarten konnten nicht geladen werden.");
    }
  }

  async function loadUncategorized() {
    try {
      const data = await api.get<Exercise[]>("/api/exercises/uncategorized");
      setUncategorized(data);
    } catch {
      // stille Ignoranz - Ohne-Sportart-Karte bleibt einfach leer
    }
  }

  async function loadExercisesFor(sportId: string) {
    try {
      const data = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
      // Nur die Übungen dieses Scopes anzeigen: Admin-Global sieht keine
      // Vereinsübungen, Trainer sieht keine anderer Vereine (Backend filtert
      // ohnehin, aber im Vereinsscope kommen globale Übungen mit - die
      // filtern wir hier raus, weil die "Übungsliste im Vereinskatalog" nur
      // die vereinseigenen zeigen soll).
      setExercisesBySport((prev) => ({
        ...prev,
        [sportId]: data.filter((e) =>
          scope.kind === "global" ? e.clubId === null : e.clubId === scope.clubId,
        ),
      }));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übungen konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Beim Mount / Scope-Wechsel Katalog neu laden - dies löst Fetch-basierte
    // setStates aus, die ESLint als "in-Effect-setState" markiert. Das ist
    // hier gewollt (externe Datenquelle) und darum bewusst gedämpft.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadSports();
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadUncategorized();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scope.kind, scope.kind === "club" ? scope.clubId : null]);

  async function toggleSport(sportId: string) {
    if (expanded === sportId) {
      setExpanded(null);
      return;
    }
    setExpanded(sportId);
    if (!exercisesBySport[sportId]) await loadExercisesFor(sportId);
  }

  function handleSportCreated(sport: Sport) {
    setAllSports((prev) =>
      prev ? [...prev, sport].sort((a, b) => a.name.localeCompare(b.name, "de")) : [sport],
    );
    setExercisesBySport((prev) => ({ ...prev, [sport.id]: [] }));
    setExpanded(sport.id);
  }

  function handleExerciseCreated(exercise: Exercise) {
    if (exercise.sportId === null) {
      setUncategorized((prev) => [...prev, exercise].sort((a, b) => a.name.localeCompare(b.name, "de")));
      setShowUncategorized(true);
    } else {
      const sportId = exercise.sportId;
      setExercisesBySport((prev) => {
        const list = prev[sportId] ?? [];
        return {
          ...prev,
          [sportId]: [...list, exercise].sort((a, b) => a.name.localeCompare(b.name, "de")),
        };
      });
      setExpanded(sportId);
    }
  }

  async function handleDeleteExercise(exercise: Exercise) {
    if (!confirm(`Übung „${exercise.name}“ wirklich löschen?`)) return;
    try {
      await api.delete(`/api/exercises/${exercise.id}`);
      toast.success("Übung gelöscht.");
      if (exercise.sportId === null) {
        setUncategorized((prev) => prev.filter((e) => e.id !== exercise.id));
      } else {
        setExercisesBySport((prev) => ({
          ...prev,
          [exercise.sportId!]: (prev[exercise.sportId!] ?? []).filter((e) => e.id !== exercise.id),
        }));
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  const exerciseScope: ExerciseScope = scope;

  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0">
        <div className="flex items-start gap-3">
          <BookOpen className="mt-0.5 size-5 text-primary" />
          <div>
            <CardTitle className="text-base">{title}</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">{description}</p>
          </div>
        </div>
        <div className="flex flex-shrink-0 flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={() => setSportEditorOpen(true)}>
            <Plus className="size-4" />
            Sportart
          </Button>
          <Button size="sm" onClick={() => setExerciseEditor({ open: true, sportId: null })}>
            <Plus className="size-4" />
            Übung
          </Button>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {allSports === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : (
          <>
            {/* Ohne-Sportart-Karte für sportartlose Übungen desselben Scopes */}
            <div className="rounded-md border border-dashed">
              <button
                type="button"
                className="flex w-full items-center justify-between px-3 py-2 text-left"
                onClick={() => setShowUncategorized((v) => !v)}
              >
                <span className="flex items-center gap-2">
                  <Sparkles className="size-4 text-muted-foreground" />
                  <span className="font-medium">Ohne Sportart</span>
                  <span className="text-xs text-muted-foreground">übergreifend</span>
                </span>
                <span className="flex items-center gap-2">
                  <Badge variant="secondary">{scopeUncategorized.length}</Badge>
                  {showUncategorized ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                </span>
              </button>
              {showUncategorized && (
                <div className="border-t px-3 py-3">
                  {scopeUncategorized.length === 0 ? (
                    <p className="text-sm text-muted-foreground">
                      Noch keine sportartlosen Übungen. Über &bdquo;+ Übung&ldquo; anlegen und &bdquo;Ohne Sportart&ldquo; wählen.
                    </p>
                  ) : (
                    <ul className="flex flex-col gap-2">
                      {scopeUncategorized.map((ex) => (
                        <ExerciseListRow key={ex.id} exercise={ex} onDelete={() => handleDeleteExercise(ex)} />
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>

            {/* Sportarten dieses Scopes */}
            {sportsInScope.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {scope.kind === "global"
                  ? "Noch keine globalen Sportarten. Über „+ Sportart“ anlegen."
                  : "Noch keine vereinseigenen Sportarten. Der Verein kann jederzeit welche anlegen."}
              </p>
            ) : (
              sportsInScope.map((sport) => {
                const isOpen = expanded === sport.id;
                const exercises = exercisesBySport[sport.id];
                return (
                  <div key={sport.id} className="rounded-md border">
                    <button
                      type="button"
                      className="flex w-full items-center justify-between px-3 py-2 text-left"
                      onClick={() => toggleSport(sport.id)}
                    >
                      <span className="flex items-center gap-3">
                        <Trophy className="size-4 text-primary" />
                        <span className="font-medium">{sport.name}</span>
                        <Badge variant="secondary">{sport.code}</Badge>
                      </span>
                      <span className="flex items-center gap-2">
                        {exercises && <Badge variant="outline">{exercises.length}</Badge>}
                        {isOpen ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                      </span>
                    </button>
                    {isOpen && (
                      <div className="flex flex-col gap-3 border-t px-3 py-3">
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-semibold text-muted-foreground">Übungen</span>
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => setExerciseEditor({ open: true, sportId: sport.id })}
                          >
                            <Plus className="size-4" />
                            Übung
                          </Button>
                        </div>
                        {!exercises ? (
                          <p className="text-sm text-muted-foreground">Lädt…</p>
                        ) : exercises.length === 0 ? (
                          <p className="text-sm text-muted-foreground">Noch keine Übungen für diese Sportart.</p>
                        ) : (
                          <ul className="flex flex-col gap-2">
                            {exercises.map((ex) => (
                              <ExerciseListRow key={ex.id} exercise={ex} onDelete={() => handleDeleteExercise(ex)} />
                            ))}
                          </ul>
                        )}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </>
        )}
      </CardContent>

      <SportEditorSheet
        open={sportEditorOpen}
        onOpenChange={setSportEditorOpen}
        scope={scope}
        onCreated={handleSportCreated}
      />
      <ExerciseEditorSheet
        open={exerciseEditor.open}
        onOpenChange={(open) => setExerciseEditor((prev) => ({ ...prev, open }))}
        scope={exerciseScope}
        sports={sportsForExerciseSelect}
        presetSportId={exerciseEditor.sportId}
        onCreated={handleExerciseCreated}
      />
    </Card>
  );
}

function ExerciseListRow({ exercise, onDelete }: { exercise: Exercise; onDelete: () => void }) {
  return (
    <li className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
      <div className="flex flex-col gap-0.5">
        <span className="font-medium">{exercise.name}</span>
        <span className="flex items-center gap-2 text-xs text-muted-foreground">
          <Badge variant="outline" className="h-5">
            {difficultyLabel[exercise.difficulty]}
          </Badge>
          {exercise.category}
        </span>
      </div>
      <Button type="button" size="icon-sm" variant="ghost" onClick={onDelete} title="Übung löschen">
        <Trash2 className="size-3.5" />
      </Button>
    </li>
  );
}

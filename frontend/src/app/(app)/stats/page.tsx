"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import type { DashboardStats, DogExerciseStat } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { BarChart, ChevronDown, ChevronRight, Dog, TrendingDown, TrendingUp } from "lucide-react";
import { toast } from "sonner";

// Bewertungstrend als Pfeil: steigend (grün) / fallend (rot) / stabil.
// null (zu wenige Durchgänge) rendert nichts.
function TrendBadge({ trend }: { trend: number | null }) {
  if (trend === null || Math.abs(trend) < 0.25) return <span className="text-muted-foreground">→ stabil</span>;
  if (trend > 0)
    return (
      <span className="flex items-center gap-0.5 text-emerald-600 dark:text-emerald-400">
        <TrendingUp className="size-3.5" /> +{trend.toFixed(1)}
      </span>
    );
  return (
    <span className="flex items-center gap-0.5 text-destructive">
      <TrendingDown className="size-3.5" /> {trend.toFixed(1)}
    </span>
  );
}

/**
 * Übungs-Aufschlüsselung eines Hundes: lädt bei Aufklappen die Kennzahlen pro
 * Übung (schwächste zuerst) und zeigt eine rein regelbasierte, lokale
 * Fokus-Empfehlung - ganz ohne externe KI. Die schwächste Übung (niedrigste
 * Ø-Bewertung) ist die Empfehlung, worauf als Nächstes hingearbeitet werden
 * sollte.
 */
function DogExercises({ dogId }: { dogId: string }) {
  const [rows, setRows] = useState<DogExerciseStat[] | null>(null);

  useEffect(() => {
    let active = true;
    api
      .get<DogExerciseStat[]>(`/api/stats/dogs/${dogId}/exercises`)
      .then((data) => {
        if (active) setRows(data);
      })
      .catch((err) => {
        if (active) {
          setRows([]);
          toast.error(err instanceof ApiError ? err.message : "Übungs-Statistik konnte nicht geladen werden.");
        }
      });
    return () => {
      active = false;
    };
  }, [dogId]);

  if (rows === null) return <p className="text-xs text-muted-foreground">Lädt…</p>;
  if (rows.length === 0) return <p className="text-xs text-muted-foreground">Noch keine Übungen erfasst.</p>;

  const focus = rows[0];

  return (
    <div className="flex flex-col gap-2">
      <div className="rounded-md bg-muted/60 px-2 py-1.5 text-xs">
        <span className="font-medium">Fokus-Empfehlung: </span>
        <span>
          {focus.exerciseName} (Ø {focus.avgRating.toFixed(1)} ★, {Math.round(focus.successRate * 100)} % erfolgreich)
        </span>
      </div>
      <ul className="flex flex-col divide-y text-xs">
        {rows.map((ex) => (
          <li key={ex.exerciseName} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5 py-1.5">
            <span className="font-medium">{ex.exerciseName}</span>
            <span className="flex items-center gap-2 text-muted-foreground">
              <span className="text-primary" title={`Ø ${ex.avgRating.toFixed(1)} von 5`}>
                {"★".repeat(Math.round(ex.avgRating))}
                {"☆".repeat(5 - Math.round(ex.avgRating))}
              </span>
              <span>{Math.round(ex.successRate * 100)} %</span>
              <TrendBadge trend={ex.ratingTrend} />
              <span className="tabular-nums">×{ex.count}</span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default function StatsPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [openDogs, setOpenDogs] = useState<Set<string>>(new Set());

  function toggleDog(dogId: string) {
    setOpenDogs((prev) => {
      const next = new Set(prev);
      if (next.has(dogId)) next.delete(dogId);
      else next.add(dogId);
      return next;
    });
  }

  useEffect(() => {
    // Stale-While-Revalidate: zuletzt gesehene Statistiken sofort anzeigen
    // (auch offline), frische Daten im Hintergrund nachladen.
    async function load() {
      const cached = await getCachedData<DashboardStats>("stats-dashboard");
      if (cached) setStats(cached);
      try {
        const fresh = await api.get<DashboardStats>("/api/stats/dashboard");
        setStats(fresh);
        await setCachedData("stats-dashboard", fresh);
      } catch (err) {
        if (!cached) toast.error(err instanceof ApiError ? err.message : "Statistiken konnten nicht geladen werden.");
      }
    }
    load();
  }, []);

  const maxWeekCount = stats ? Math.max(...stats.weeklyActivity.map((w) => w.count), 1) : 1;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Statistiken</h1>
        <p className="text-muted-foreground">Trainingsfortschritt im Überblick.</p>
      </div>

      {stats === null ? (
        <p className="text-sm text-muted-foreground">Lädt…</p>
      ) : (
        <>
          <Card>
            <CardHeader className="flex-row items-center gap-2 space-y-0">
              <BarChart className="size-5 text-primary" />
              <CardTitle className="text-base">Trainings der letzten 12 Wochen</CardTitle>
            </CardHeader>
            <CardContent>
              {stats.weeklyActivity.every((w) => w.count === 0) ? (
                <p className="text-sm text-muted-foreground">Noch keine Trainings erfasst.</p>
              ) : (
                <div className="flex items-end gap-1 h-32">
                  {stats.weeklyActivity.map((w) => (
                    <div key={w.week} className="flex flex-col items-center flex-1 gap-1 h-full justify-end">
                      <span className="text-[10px] text-muted-foreground">{w.count > 0 ? w.count : ""}</span>
                      <div
                        className="w-full bg-primary rounded-t transition-all"
                        style={{ height: `${(w.count / maxWeekCount) * 100}%`, minHeight: w.count > 0 ? "4px" : "0" }}
                        title={`${w.week}: ${w.count}`}
                      />
                      <span className="text-[9px] text-muted-foreground rotate-45 origin-left mt-1 hidden sm:block">
                        {w.week.slice(-5)}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {stats.perDog.length === 0 ? (
            <Card>
              <CardContent className="py-10 text-center text-muted-foreground">
                Noch keine Hunde vorhanden.
              </CardContent>
            </Card>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
              {stats.perDog.map((dog) => (
                <Card key={dog.dogId}>
                  <CardHeader className="flex-row items-center gap-3 space-y-0">
                    <Dog className="size-6 text-primary" />
                    <CardTitle className="text-base">{dog.dogName}</CardTitle>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-3">
                    <div className="grid grid-cols-2 gap-2 text-sm">
                      <div>
                        <p className="text-muted-foreground text-xs">Trainings gesamt</p>
                        <p className="font-medium">{dog.sessionCount}</p>
                      </div>
                      <div>
                        <p className="text-muted-foreground text-xs">Letzte 30 Tage</p>
                        <p className="font-medium">{dog.sessionsLast30d}</p>
                      </div>
                      <div>
                        <p className="text-muted-foreground text-xs">Aktive Ziele</p>
                        <p className="font-medium">{dog.activeGoals}</p>
                      </div>
                      <div>
                        <p className="text-muted-foreground text-xs">Ø Bewertung (30d)</p>
                        <p className="font-medium">{dog.avgRating30d !== null ? `${dog.avgRating30d} / 5` : "–"}</p>
                      </div>
                    </div>
                    {dog.planItemsTotal > 0 && (
                      <div>
                        <div className="flex justify-between text-xs text-muted-foreground mb-1">
                          <span>Planziele</span>
                          <span>
                            {dog.planItemsCompleted} / {dog.planItemsTotal}
                          </span>
                        </div>
                        <div className="h-2 rounded-full bg-muted overflow-hidden">
                          <div
                            className="h-full bg-primary transition-all"
                            style={{ width: `${(dog.planItemsCompleted / dog.planItemsTotal) * 100}%` }}
                          />
                        </div>
                      </div>
                    )}
                    {dog.sessionCount > 0 && (
                      <div className="border-t pt-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 self-start px-2 text-xs text-muted-foreground"
                          onClick={() => toggleDog(dog.dogId)}
                        >
                          {openDogs.has(dog.dogId) ? (
                            <ChevronDown className="size-3.5" />
                          ) : (
                            <ChevronRight className="size-3.5" />
                          )}
                          Übungen &amp; Schwerpunkte
                        </Button>
                        {openDogs.has(dog.dogId) && (
                          <div className="mt-2">
                            <DogExercises dogId={dog.dogId} />
                          </div>
                        )}
                      </div>
                    )}
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

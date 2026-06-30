"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { DashboardStats } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { BarChart, Dog } from "lucide-react";
import { toast } from "sonner";

export default function StatsPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);

  useEffect(() => {
    api
      .get<DashboardStats>("/api/stats/dashboard")
      .then(setStats)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Statistiken konnten nicht geladen werden."));
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

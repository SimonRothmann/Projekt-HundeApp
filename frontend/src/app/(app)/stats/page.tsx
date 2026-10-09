"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import type { DashboardStats, DogExerciseStat } from "@/lib/types";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { BarChart, ChevronDown, ChevronRight, Dog } from "lucide-react";
import { toast } from "sonner";
import { ConditionStats } from "@/components/dogs/condition-stats";
import { FaehrtenTrend, TrendBadge } from "@/components/tracking/faehrten-trend";

import { useSprache, useT } from "@/lib/i18n";
import { zahlText } from "@/lib/ortsformat";

/**
 * Übungs-Aufschlüsselung eines Hundes: lädt bei Aufklappen die Kennzahlen pro
 * Übung (schwächste zuerst) und zeigt eine rein regelbasierte, lokale
 * Fokus-Empfehlung - ganz ohne externe KI. Die schwächste Übung (niedrigste
 * Ø-Bewertung) ist die Empfehlung, worauf als Nächstes hingearbeitet werden
 * sollte.
 */
function DogExercises({ dogId }: { dogId: string }) {
  const t = useT();
  const sprache = useSprache();
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
          toast.error(err instanceof ApiError ? err.message : t("Übungs-Statistik konnte nicht geladen werden."));
        }
      });
    return () => {
      active = false;
    };
    // t bewusst nicht in der Liste: Der Uebersetzer wird hier nur im
    // Fehlerfall gebraucht. Stuende er drin, liefe der ganze Abruf bei
    // jedem Sprachwechsel erneut - Daten neu laden, weil ein Toast
    // anders heissen wuerde.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dogId]);

  if (rows === null) return <p className="text-xs text-muted-foreground">{t("Lädt…")}</p>;
  if (rows.length === 0) return <p className="text-xs text-muted-foreground">{t("Noch keine Übungen erfasst.")}</p>;

  const focus = rows[0];

  return (
    <div className="flex flex-col gap-2">
      <div className="rounded-md bg-muted/60 px-2 py-1.5 text-xs">
        <span className="font-medium">{t("Fokus-Empfehlung:")} </span>
        <span>
          {focus.exerciseName} (Ø {zahlText(focus.avgRating, sprache)} ★, {t("{prozent} % erfolgreich", { prozent: Math.round(focus.successRate * 100) })})
        </span>
      </div>
      <ul className="flex flex-col divide-y text-xs">
        {rows.map((ex) => (
          <li key={ex.exerciseName} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5 py-1.5">
            <span className="font-medium">{ex.exerciseName}</span>
            <span className="flex items-center gap-2 text-muted-foreground">
              <span className="text-primary-text" title={t("Ø {wert} von 5", { wert: zahlText(ex.avgRating, sprache) })}>
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
  const t = useT();
  const sprache = useSprache();
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
        if (!cached) toast.error(err instanceof ApiError ? err.message : t("Statistiken konnten nicht geladen werden."));
      }
    }
    load();
    // t bewusst nicht in der Liste: Der Uebersetzer wird hier nur im
    // Fehlerfall gebraucht. Stuende er drin, liefe der ganze Abruf bei
    // jedem Sprachwechsel erneut - Daten neu laden, weil ein Toast
    // anders heissen wuerde.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Weder Hund noch Training: Die Wochen- und Hundekarten wären nur Nullen.
  const ohneTraining = stats !== null && stats.perDog.every((dog) => dog.sessionCount === 0);
  const maxWeekCount = stats ? Math.max(...stats.weeklyActivity.map((w) => w.count), 1) : 1;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("Statistiken")}</h1>
        <p className="text-muted-foreground">{t("Trainingsfortschritt im Überblick.")}</p>
      </div>

      {stats === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : ohneTraining ? (
        // Ohne Hund oder ohne ein einziges Training gibt es nichts zu zeigen
        // (auch die Fährten-Entwicklung hängt an Trainings) - statt leerer
        // Karten und Nullen ein Satz und der Weg dorthin: Hund und erstes
        // Training entstehen auf der Startseite.
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-10 text-center">
            <BarChart className="size-10 text-muted-foreground" aria-hidden />
            <p className="font-medium">{t("Hier erscheint dein Trainingsfortschritt.")}</p>
            <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
              {stats.perDog.length === 0
                ? t("Leg auf der Startseite deinen Hund an und trag das erste Training ein – dann siehst du hier, wie ihr vorankommt.")
                : t("Trag auf der Startseite das erste Training ein – dann siehst du hier, wie ihr vorankommt.")}
            </p>
            <Link href="/dashboard" className={cn(buttonVariants(), "h-11 px-4 text-base")}>
              {t("Zur Startseite")}
            </Link>
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader className="flex flex-row items-center gap-2 space-y-0">
              <BarChart className="size-5 shrink-0 text-primary-text" />
              <CardTitle className="min-w-0 text-base">{t("Trainings der letzten 12 Wochen")}</CardTitle>
            </CardHeader>
            <CardContent>
              {stats.weeklyActivity.every((w) => w.count === 0) ? (
                <p className="text-sm text-muted-foreground">{t("Noch keine Trainings erfasst.")}</p>
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

          <div className="grid gap-4 sm:grid-cols-2">
            {stats.perDog.map((dog) => (
              <Card key={dog.dogId}>
                <CardHeader className="flex flex-row items-center gap-3 space-y-0">
                  <Dog className="size-6 shrink-0 text-primary-text" />
                  <CardTitle className="min-w-0 text-base [overflow-wrap:anywhere]">{dog.dogName}</CardTitle>
                </CardHeader>
                <CardContent className="flex flex-col gap-3">
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    <div>
                      <p className="text-muted-foreground text-xs">{t("Trainings gesamt")}</p>
                      <p className="font-medium">{dog.sessionCount}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground text-xs">{t("Letzte 30 Tage")}</p>
                      <p className="font-medium">{dog.sessionsLast30d}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground text-xs">{t("Aktive Ziele")}</p>
                      <p className="font-medium">{dog.activeGoals}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground text-xs">{t("Ø Bewertung (30d)")}</p>
                      <p className="font-medium">{dog.avgRating30d !== null ? `${zahlText(dog.avgRating30d, sprache)} / 5` : "–"}</p>
                    </div>
                  </div>
                  {dog.planItemsTotal > 0 && (
                    <div>
                      <div className="flex justify-between text-xs text-muted-foreground mb-1">
                        <span>{t("Planziele")}</span>
                        <span>
                          {dog.planItemsCompleted} / {dog.planItemsTotal}
                        </span>
                      </div>
                      <div className="h-2 rounded-full bg-primary/15 overflow-hidden">
                        <div
                          className="h-full bg-primary transition-all"
                          style={{ width: `${(dog.planItemsCompleted / dog.planItemsTotal) * 100}%` }}
                        />
                      </div>
                    </div>
                  )}
                  <FaehrtenTrend dogId={dog.dogId} />
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
                        <div className="mt-2 flex flex-col gap-4">
                          <DogExercises dogId={dog.dogId} />
                          <ConditionStats dogId={dog.dogId} />
                        </div>
                      )}
                    </div>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

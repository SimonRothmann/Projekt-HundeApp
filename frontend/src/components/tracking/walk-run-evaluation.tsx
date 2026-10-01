"use client";

import type { GpsWalkRun } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
// Schwellen identisch zum Backend (GpsTrackEvaluator) - bewusst großzügig,
// weil der GPS-Fehler selbst 3-8 m beträgt. Exportiert, weil Kartenlinie und
// Fährtenbild (lib/faehrten-bild.ts) dieselben Schwellen brauchen - eine Zahl,
// ein Ort.
export const GREEN_MAX_M = 3;
export const AMBER_MAX_M = 6;

function ampelClass(avgMeters: number): string {
  if (avgMeters <= GREEN_MAX_M) return "text-emerald-600 dark:text-emerald-500";
  if (avgMeters <= AMBER_MAX_M) return "text-amber-600 dark:text-amber-500";
  return "text-destructive";
}

export function ampelLabel(avgMeters: number): string {
  if (avgMeters <= GREEN_MAX_M) return uebersetzbar("eng an der Fährte");
  if (avgMeters <= AMBER_MAX_M) return uebersetzbar("mittlere Abweichung");
  return uebersetzbar("deutlich abgekommen");
}

const stopLabel: Record<number, string> = { 0: uebersetzbar("unerklärt"), 1: uebersetzbar("Verweisen"), 2: uebersetzbar("erklärt") };
const stopClass: Record<number, string> = {
  0: "border-destructive/40 text-destructive",
  1: "border-emerald-500/40 text-emerald-600 dark:text-emerald-500",
  2: "border-border text-muted-foreground",
};

/**
 * Auswertung eines Ablauf-Versuchs: Abweichung, Gegenstände, Stockungen.
 *
 * Wichtig für die Deutung (und deshalb auch in der UI benannt): Gemessen wird
 * die Linie des HUNDEFÜHRERS. Der Hund kann im Radius der Fährtenleine
 * ausscheren und zurückkommen, ohne dass sich das Gerät bewegt - solche
 * Ausschläge sind hier unsichtbar. Sichtbar werden sie über die Stockungen:
 * sucht oder kreist der Hund, bleibt der Hundeführer stehen.
 */
export function WalkRunEvaluation({ run }: { run: GpsWalkRun }) {
  const t = useT();
  // Lose Prüfungen (== null statt === null, ?? []): aus einem älteren
  // Read-Cache oder von einem älteren Backend-Stand fehlen diese Felder ganz
  // (undefined) - ohne diese Toleranz stürzt die Fährtenansicht ab, wie
  // seinerzeit bei goal.weekConfigs.
  if (run.evaluatedAt == null || run.avgDeviationMeters == null) return null;

  // Anzeige in ganzen Metern, Ampel aber auf dem ungerundeten Wert: dieselben
  // Schwellen wie Backend (GpsTrackEvaluator) und Kartenlinie - sonst wäre
  // dieselbe Fährte hier grün und auf der Karte gelb.
  const avg = run.avgDeviationMeters;
  const stops = run.stops ?? [];
  const unexplained = stops.filter((s) => s.kind === 0).length;

  return (
    <div className="flex flex-col gap-1.5 rounded-md border bg-muted/30 p-2.5">
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <span className={cn("text-sm font-semibold", ampelClass(avg))}>
          {t("Ø {meter} m", { meter: Math.round(avg) })}
        </span>
        <span className={cn("text-xs", ampelClass(avg))}>{t(ampelLabel(avg))}</span>
        {run.maxDeviationMeters != null && (
          <span className="text-xs">{t("max {meter} m", { meter: Math.round(run.maxDeviationMeters) })}</span>
        )}
        {run.onTrackPercent != null && <span className="text-xs">{t("{prozent} % auf Fährte", { prozent: Math.round(run.onTrackPercent) })}</span>}
        {run.articlesTotal != null && run.articlesTotal > 0 && (
          <span className="text-xs">
            {t("{gefunden}/{gesamt} Gegenstände", { gefunden: run.articlesFound ?? 0, gesamt: run.articlesTotal })}
          </span>
        )}
      </div>

      {stops.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          {stops.slice(0, 6).map((stop, i) => (
            <Badge key={i} variant="outline" className={stopClass[stop.kind]}>
              {t(stopLabel[stop.kind])} {stop.durationSeconds}s
              {stop.markerLabel ? ` · ${stop.markerLabel}` : ""}
            </Badge>
          ))}
          {stops.length > 6 && <span className="text-xs">{t("+{n} weitere", { n: stops.length - 6 })}</span>}
        </div>
      )}

      <p className="text-[0.7rem] leading-snug">
        {t("Gemessen wird die Linie des Hundeführers – der Hund kann bis Leinenlänge abweichen, ohne dass es hier sichtbar wird.")}
        {unexplained > 0 &&
          ` ${unexplained === 1 ? t("1 unerklärte Stockung deutet auf Suchen hin.") : t("{n} unerklärte Stockungen deuten auf Suchen hin.", { n: unexplained })}`}
      </p>
    </div>
  );
}

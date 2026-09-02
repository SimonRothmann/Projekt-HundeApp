"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { DogConditionStats } from "@/lib/types";
import { conditionLabel } from "@/components/dogs/condition-picker";
import { toast } from "sonner";

/**
 * Was die Verfassung des Hundes mit seinen Bewertungen zu tun hat.
 *
 * Zwei Blickwinkel, weil sie zwei verschiedene Fragen beantworten:
 * - "Wie fällt die Bewertung aus, wenn er abgelenkt war?" - das ahnt man,
 *   aber man hat es nicht in Zahlen.
 * - "Was macht es, wenn schon zwei Tage hintereinander trainiert wurde?" -
 *   das sieht im Alltag niemand, weil niemand seine Trainingstage zusammenzählt.
 *
 * Ohne Angaben gibt es hier nichts zu zeigen; die Aufforderung nennt dann den
 * Weg dorthin, statt eine leere Tabelle zu zeigen.
 */
export function ConditionStats({ dogId }: { dogId: string }) {
  const [stats, setStats] = useState<DogConditionStats | null>(null);

  useEffect(() => {
    let active = true;
    api
      .get<DogConditionStats>(`/api/stats/dogs/${dogId}/condition`)
      .then((data) => {
        if (active) setStats(data);
      })
      .catch((err) => {
        if (active) {
          setStats(null);
          toast.error(err instanceof ApiError ? err.message : "Verfassung konnte nicht geladen werden.");
        }
      });
    return () => {
      active = false;
    };
  }, [dogId]);

  if (stats === null) return <p className="text-xs text-muted-foreground">Lädt…</p>;

  const dichte = stats.byPrecedingDays.filter((d) => d.sessionCount > 0);

  if (stats.sessionsWithCondition === 0 && dichte.length === 0) {
    return <p className="text-xs text-muted-foreground">Noch keine Trainings erfasst.</p>;
  }

  return (
    <div className="flex flex-col gap-3">
      {stats.sessionsWithCondition === 0 ? (
        <p className="rounded-md bg-muted/60 px-2 py-1.5 text-xs text-muted-foreground">
          Noch keine Verfassung eingetragen. Ein Tipp beim Erfassen eines Trainings genügt – danach steht hier,
          wie sich motiviert, abgelenkt, müde und gestresst auf die Bewertungen auswirken.
        </p>
      ) : (
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium">Bewertung nach Verfassung</p>
          <ul className="flex flex-col divide-y text-xs">
            {stats.byCondition.map((row) => (
              <li key={row.condition} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5 py-1.5">
                <span className="font-medium">{conditionLabel(row.condition) ?? "unbekannt"}</span>
                <span className="flex items-center gap-2 text-muted-foreground">
                  {row.avgRating !== null && (
                    <span className="text-primary tabular-nums" title={`Ø ${row.avgRating.toFixed(1)} von 5`}>
                      Ø {row.avgRating.toFixed(1)} ★
                    </span>
                  )}
                  {row.successRate !== null && (
                    <span className="tabular-nums">{Math.round(row.successRate * 100)} %</span>
                  )}
                  <span className="tabular-nums">×{row.sessionCount}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {dichte.length > 1 && (
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium">Nach Trainingstagen am Stück</p>
          <ul className="flex flex-col divide-y text-xs">
            {dichte.map((row) => (
              <li
                key={row.precedingTrainingDays}
                className="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5 py-1.5"
              >
                <span className="font-medium">{dichteName(row.precedingTrainingDays)}</span>
                <span className="flex items-center gap-2 text-muted-foreground">
                  {row.avgRating !== null && (
                    <span className="text-primary tabular-nums">Ø {row.avgRating.toFixed(1)} ★</span>
                  )}
                  {row.tiredOrStressedShare !== null && row.tiredOrStressedShare > 0 && (
                    <span className="tabular-nums" title="Anteil müde oder gestresst">
                      {Math.round(row.tiredOrStressedShare * 100)} % müde/gestresst
                    </span>
                  )}
                  <span className="tabular-nums">×{row.sessionCount}</span>
                </span>
              </li>
            ))}
          </ul>
          {hinweis(dichte)}
        </div>
      )}

      {stats.sessionsWithCondition > 0 && (
        <p className="text-xs text-muted-foreground tabular-nums">
          Verfassung bei {stats.sessionsWithCondition} von {stats.sessionsTotal} Trainings angegeben.
        </p>
      )}
    </div>
  );
}

function dichteName(tage: number): string {
  if (tage === 0) return "Nach einer Pause";
  if (tage === 1) return "Zweiter Tag in Folge";
  return "Dritter Tag oder später";
}

/**
 * Der eigentliche Sinn der Tabelle in einem Satz - aber nur, wenn die Zahlen
 * ihn hergeben. Ein Hinweis auf einen Einbruch, den es nicht gibt, wäre
 * schlimmer als gar keiner.
 */
function hinweis(dichte: { precedingTrainingDays: number; sessionCount: number; avgRating: number | null }[]) {
  const ohnePause = dichte.find((d) => d.precedingTrainingDays === 0);
  const amStueck = dichte.filter((d) => d.precedingTrainingDays > 0 && d.avgRating !== null);
  if (!ohnePause?.avgRating || amStueck.length === 0) return null;

  const schwaechste = amStueck.reduce((a, b) => (a.avgRating! < b.avgRating! ? a : b));
  const abstand = ohnePause.avgRating - schwaechste.avgRating!;

  // Unter einem halben Stern und unter drei Einheiten ist es Rauschen.
  if (abstand < 0.5 || schwaechste.sessionCount < 3) return null;

  return (
    <p className="rounded-md bg-muted/60 px-2 py-1.5 text-xs">
      {dichteName(schwaechste.precedingTrainingDays)} fällt die Bewertung im Schnitt um{" "}
      <span className="font-medium tabular-nums">{abstand.toFixed(1)} ★</span> niedriger aus als nach einer Pause.
    </p>
  );
}

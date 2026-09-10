"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Target } from "lucide-react";
import { api } from "@/lib/api";
import type { Dog, Goal, TrainingPlanItem } from "@/lib/types";
import { offeneWochenziele } from "@/lib/trainingsplan";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { PlanItemQuickLog } from "@/components/dogs/plan-item-quick-log";

import { useT } from "@/lib/i18n";

type Eintrag = { hund: Dog; goal: Goal; woche: number; items: TrainingPlanItem[] };

/**
 * "Diese Woche": die offenen Wochenziele aus den Trainingsplänen, direkt auf
 * der Startseite eintragbar.
 *
 * Vorher: Hunde, Hund, scrollen, Wochenziel, Bewertung, Eintragen. Hier bleiben
 * die letzten drei. Nebenbei wird der Plan sichtbar, ohne dass man die
 * Hundeseite öffnet. Zeigt nichts, solange nichts offen ist.
 */
export function DieseWocheSection({ hunde }: { hunde: Dog[] }) {
  const t = useT();
  const [eintraege, setEintraege] = useState<Eintrag[] | null>(null);
  const [offenesItem, setOffenesItem] = useState<string | null>(null);
  const hundeSchluessel = hunde.map((h) => h.id).join(",");

  async function laden() {
    const listen = await Promise.all(
      hunde.map(async (hund) => {
        try {
          const goals = await api.get<Goal[]>(`/api/goals?dogId=${hund.id}`);
          return goals.flatMap((goal) => {
            const offen = offeneWochenziele(goal);
            return offen ? [{ hund, goal, ...offen }] : [];
          });
        } catch {
          // Offline oder Serverfehler: der Abschnitt ist ein Angebot, kein
          // Muss - dann steht er eben nicht da.
          return [];
        }
      }),
    );
    setEintraege(listen.flat());
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hundeSchluessel]);

  if (!eintraege || eintraege.length === 0) return null;

  return (
    <section className="flex flex-col gap-3">
      <SectionHeading icon={Target} title={t("Diese Woche")} />
      {eintraege.map((eintrag) => (
        <Card key={eintrag.goal.id}>
          <CardHeader className="border-b">
            <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
              <CardTitle className="text-base [overflow-wrap:anywhere]">
                {hunde.length > 1 ? `${eintrag.hund.name} · ` : ""}
                {eintrag.goal.sportName}
              </CardTitle>
              <span className="text-xs text-muted-foreground">
                {t("Woche {nummer} · {anzahl} offen", { nummer: eintrag.woche, anzahl: eintrag.items.length })}
              </span>
            </div>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            {eintrag.items.map((item) => (
              <div key={item.id} className="flex flex-col gap-1.5">
                <button
                  type="button"
                  onClick={() => setOffenesItem((aktuell) => (aktuell === item.id ? null : item.id))}
                  aria-expanded={offenesItem === item.id}
                  className="flex w-full items-center justify-between gap-3 rounded-lg border border-surface-border bg-surface px-3 py-2 text-left transition-colors hover:border-primary/40 coarse:min-h-11"
                >
                  <span className="min-w-0">
                    <span className="block font-medium [overflow-wrap:anywhere]">{item.exerciseName ?? item.freeTextLabel}</span>
                    <span className="text-xs text-muted-foreground">
                      {t("{erledigt}/{ziel}× erledigt", { erledigt: item.completedCount, ziel: item.repetitionsTarget })}
                    </span>
                  </span>
                  <span className="shrink-0 text-sm font-medium text-primary-text">{t("Eintragen")}</span>
                </button>
                {offenesItem === item.id && (
                  <PlanItemQuickLog
                    dogId={eintrag.hund.id}
                    item={item}
                    onDone={async () => {
                      setOffenesItem(null);
                      await laden();
                    }}
                    onCancel={() => setOffenesItem(null)}
                  />
                )}
              </div>
            ))}
            <Link href={`/dogs/${eintrag.hund.id}#trainingsplan`} className="self-start text-sm text-primary-text">
              {t("Zum Trainingsplan")}
            </Link>
          </CardContent>
        </Card>
      ))}
    </section>
  );
}

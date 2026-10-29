"use client";

import { Trophy } from "lucide-react";
import { katalogName } from "@/lib/katalog-name";
import { balkenProzent } from "@/lib/kennzahlen";
import { groessterWert } from "@/lib/nutzung";
import { useSprache, useT } from "@/lib/i18n";
import type { AdminSportUsage } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * Sportarten: Trainings der letzten 90 Tage (über die Übungen aus dem
 * Katalog) und Hunde, die die Sportart gewählt haben. Auch ungenutzte stehen
 * drin - sie zeigen, was bisher gar nicht verwendet wird.
 */
export function NutzungSportartenSection({ sports }: { sports: AdminSportUsage[] }) {
  const t = useT();
  const sprache = useSprache();
  const hoechster = groessterWert(sports.map((s) => s.trainings));
  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <Trophy className="size-5 shrink-0" />
          {t("Sportarten")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Trainings der letzten 90 Tage mit mindestens einer Übung aus der Sportart, und Hunde, die die Sportart ausgewählt haben.")}
        </p>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-3">
          {sports.map((s, i) => (
            <li key={`${s.name}-${i}`} className="min-w-0">
              <div className="flex items-baseline justify-between gap-3 text-sm">
                <span className="min-w-0 [overflow-wrap:anywhere]">{katalogName(s, sprache)}</span>
                <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
                  <span className="text-sm font-semibold text-foreground">{s.trainings}</span> {t("Trainings")} · <span className="font-semibold text-foreground">{s.dogs}</span> {t("Hunde")}
                </span>
              </div>
              <div aria-hidden className="mt-1 h-2 overflow-hidden rounded-full bg-primary/15">
                <div className="h-full rounded-full bg-primary" style={{ width: `${balkenProzent(s.trainings, hoechster)}%` }} />
              </div>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

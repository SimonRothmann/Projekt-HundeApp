"use client";

import { Clock } from "lucide-react";
import { wochentagKurz } from "@/lib/nutzung";
import { useSprache, useT } from "@/lib/i18n";
import type { AdminUsageTiming } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Saeulen } from "@/components/admin/nutzung-balken";

/** Wann eingetragen wird: Wochentag und Stunde (deutsche Zeit) der letzten 90 Tage. */
export function NutzungWannSection({ timing }: { timing: AdminUsageTiming }) {
  const t = useT();
  const sprache = useSprache();
  const gesamt = timing.byWeekday.reduce((summe, wert) => summe + wert, 0);
  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <Clock className="size-5 shrink-0" />
          {t("Wann wird eingetragen")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Trainings, Fährten und Abläufe der letzten 90 Tage nach dem Zeitpunkt des Eintragens, in deutscher Zeit.")}
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        {gesamt === 0 ? (
          <p className="text-sm text-muted-foreground">{t("In den letzten 90 Tagen wurde nichts eingetragen.")}</p>
        ) : (
          <>
            <div>
              <h3 className="mb-2 text-sm font-medium">{t("Nach Wochentag")}</h3>
              <Saeulen
                werte={timing.byWeekday}
                labels={timing.byWeekday.map((_, i) => wochentagKurz(i, sprache))}
                beschreibung={t("Einträge nach Wochentag")}
              />
            </div>
            <div className="border-t pt-4">
              <h3 className="mb-2 text-sm font-medium">{t("Nach Uhrzeit")}</h3>
              <Saeulen
                werte={timing.byHour}
                labels={timing.byHour.map((_, i) => String(i))}
                beschriftenJede={3}
                beschreibung={t("Einträge nach Stunde")}
              />
              <p className="mt-1 text-xs text-muted-foreground">{t("Stunde des Tages (0 bis 23 Uhr)")}</p>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

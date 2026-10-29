"use client";

import { UserCheck } from "lucide-react";
import { useT } from "@/lib/i18n";
import type { AdminRetention } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Balkenzeile } from "@/components/admin/nutzung-balken";

/** Bindung: bleiben Konten dabei, und wer hat nie etwas angelegt? */
export function NutzungBindungSection({ retention, gesamt }: { retention: AdminRetention; gesamt: number }) {
  const t = useT();
  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserCheck className="size-5 shrink-0" />
          {t("Bindung")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Ob Konten nach den ersten Wochen dabeibleiben. Aktiv heißt: mindestens etwas angelegt.")}
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        <ul className="flex flex-col gap-3">
          <Balkenzeile label={t("Konten älter als 30 Tage")} wert={retention.accountsOlderThan30Days} bezug={gesamt} hauptzeile />
          <Balkenzeile
            label={t("davon in den letzten 30 Tagen aktiv")}
            wert={retention.olderActiveLast30Days}
            bezug={retention.accountsOlderThan30Days}
          />
        </ul>
        <div className="border-t pt-4">
          <p className="mb-3 text-sm font-medium">{t("Nie etwas angelegt")}</p>
          <ul className="flex flex-col gap-3">
            <Balkenzeile label={t("ohne Hund")} wert={retention.neverCreatedWithoutDog} bezug={gesamt} />
            <Balkenzeile label={t("mit Hund, aber ohne Eintrag")} wert={retention.neverCreatedWithDog} bezug={gesamt} />
          </ul>
        </div>
      </CardContent>
    </Card>
  );
}

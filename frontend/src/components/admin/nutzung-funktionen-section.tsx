"use client";

import { LayoutList } from "lucide-react";
import { funktionsName, nutzungStatus, sortiereFunktionen } from "@/lib/nutzung";
import { useSprache, useT } from "@/lib/i18n";
import { datumKurz } from "@/lib/pruefung";
import type { AdminFeatureUsage } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * Nutzung je Funktion, die meistgenutzten zuerst. Funktionen ohne Nutzung in
 * den letzten 30 Tagen sind markiert, nie genutzte noch deutlicher - das ist
 * die Antwort auf "was wird gar nicht verwendet". Karten statt Tabelle: Bei
 * 375 px gäbe es sonst seitliches Scrollen.
 */
export function NutzungFunktionenSection({ features }: { features: AdminFeatureUsage[] }) {
  const t = useT();
  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <LayoutList className="size-5 shrink-0" />
          {t("Funktionen")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Sortiert nach den Konten der letzten 30 Tage. Gezählt wird, wer etwas angelegt hat - bei Push, wer es eingeschaltet hat.")}
        </p>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-2">
          {sortiereFunktionen(features).map((f) => (
            <Funktionszeile key={f.key} f={f} />
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function Funktionszeile({ f }: { f: AdminFeatureUsage }) {
  const t = useT();
  const sprache = useSprache();
  const status = nutzungStatus(f);
  const nurKonten = f.entriesLast30Days === null;
  return (
    <li className="min-w-0 rounded-lg border p-3">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span className="min-w-0 font-medium [overflow-wrap:anywhere]">{t(funktionsName(f.key))}</span>
        {status === "ruhend" && <Badge variant="secondary">{t("in 30 Tagen nicht genutzt")}</Badge>}
        {status === "nie" && <Badge variant="destructive">{t("noch nie genutzt")}</Badge>}
      </div>
      <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-2 text-xs sm:grid-cols-4">
        <Wert label={nurKonten ? t("Neu eingeschaltet, 30 Tage") : t("Konten, 30 Tage")} wert={f.accountsLast30Days} />
        {!nurKonten && <Wert label={t("Einträge, 30 Tage")} wert={f.entriesLast30Days} />}
        <Wert label={nurKonten ? t("Konten mit Push") : t("Konten jemals")} wert={f.accountsEver} />
        <Wert label={t("Zuletzt genutzt")} wert={f.lastUsed ? datumKurz(f.lastUsed, sprache) : "-"} />
      </dl>
    </li>
  );
}

function Wert({ label, wert }: { label: string; wert: number | string | null }) {
  return (
    <div className="min-w-0">
      <dt className="text-muted-foreground [overflow-wrap:anywhere]">{label}</dt>
      <dd className="text-sm font-semibold tabular-nums">{wert}</dd>
    </div>
  );
}

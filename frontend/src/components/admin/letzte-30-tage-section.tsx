"use client";

import { BarChart3 } from "lucide-react";
import { anteilProzent, balkenProzent } from "@/lib/kennzahlen";
import { useT } from "@/lib/i18n";
import type { AdminStats } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * "Die letzten 30 Tage": was aus den neuen Konten geworden ist - als schlichte
 * Balken, ohne Diagramm-Paket.
 *
 * Nur Zählungen. Es gibt hier keine Namen und keine Liste von Personen, und
 * der Server liefert auch keine (siehe AdminService.GetRecentStatsAsync).
 *
 * Gemessen wird, was ohnehin in der Datenbank steht - ohne Tracking im
 * Browser, ohne Cookies, ohne Dritte. Jede Zeile ist eine Frage an die eigenen
 * Daten ("wie viele der neuen Konten haben einen Hund?"), nie ein Protokoll
 * dessen, was jemand angeklickt hat.
 */
export function Letzte30TageSection({ stats }: { stats: AdminStats }) {
  const t = useT();
  const z = stats.last30Days;

  const zeilen = [
    { label: t("Neue Konten"), wert: z.newAccounts, bezug: z.newAccounts, hauptzeile: true, mitAnteil: false },
    { label: t("davon mit Hund"), wert: z.withDog, bezug: z.newAccounts },
    { label: t("davon im Verein"), wert: z.inClub, bezug: z.newAccounts },
    { label: t("davon mit Prüfungsziel"), wert: z.withGoal, bezug: z.newAccounts },
    { label: t("davon über Vereinslink gekommen"), wert: z.viaClubLink, bezug: z.newAccounts },
  ];

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <BarChart3 className="size-5 shrink-0" />
          {t("Die letzten 30 Tage")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Nur Zählungen, keine Personen. Gemessen wird nichts im Browser, sondern nur, was in den Daten steht.")}
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        <ul className="flex flex-col gap-3">
          {zeilen.map((zeile) => (
            <Balkenzeile key={zeile.label} {...zeile} />
          ))}
        </ul>

        <div className="border-t pt-4">
          <ul>
            <Balkenzeile
              label={t("Aktive Konten")}
              wert={z.activeAccounts}
              bezug={stats.userCount}
              bezugText={t("von {gesamt} Konten insgesamt", { gesamt: stats.userCount })}
              hauptzeile
            />
          </ul>
          <p className="mt-2 text-xs text-muted-foreground">
            {t("Verschiedene Personen, die in den letzten 30 Tagen mindestens ein Training oder eine Fährte angelegt haben - egal, wann das Konto entstand.")}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

function Balkenzeile({
  label,
  wert,
  bezug,
  bezugText,
  hauptzeile = false,
  mitAnteil = true,
}: {
  label: string;
  wert: number;
  bezug: number;
  bezugText?: string;
  hauptzeile?: boolean;
  /** Bei den neuen Konten wäre "100 %" Rauschen - sie sind der Bezug der Zeilen darunter. */
  mitAnteil?: boolean;
}) {
  const anteil = anteilProzent(wert, bezug);
  return (
    <li className="min-w-0">
      <div className="flex items-baseline justify-between gap-3 text-sm">
        <span className={hauptzeile ? "min-w-0 font-medium" : "min-w-0"}>{label}</span>
        <span className="shrink-0 tabular-nums">
          <span className="font-semibold">{wert}</span>
          {mitAnteil && anteil !== null && (
            <span className="ml-1.5 text-xs text-muted-foreground">{anteil} %</span>
          )}
        </span>
      </div>
      {/* Reiner Schmuck: die Zahl steht daneben im Text. */}
      <div aria-hidden className="mt-1 h-2 overflow-hidden rounded-full bg-primary/15">
        <div className="h-full rounded-full bg-primary" style={{ width: `${balkenProzent(wert, bezug)}%` }} />
      </div>
      {bezugText && <p className="mt-1 text-xs text-muted-foreground">{bezugText}</p>}
    </li>
  );
}

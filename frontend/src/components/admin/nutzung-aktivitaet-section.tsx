"use client";

import { Activity } from "lucide-react";
import { groessterWert } from "@/lib/nutzung";
import { balkenProzent } from "@/lib/kennzahlen";
import { useSprache, useT } from "@/lib/i18n";
import { datumTagMonat } from "@/lib/pausen";
import type { AdminUsageActivity } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Balkenzeile } from "@/components/admin/nutzung-balken";

/**
 * Aktivität: wie viele Konten die App geöffnet haben und wie viele etwas
 * angelegt haben, dazu der Verlauf der letzten 12 Wochen. Nur Zählungen
 * (siehe AdminUsageService).
 */
export function NutzungAktivitaetSection({ activity }: { activity: AdminUsageActivity }) {
  const t = useT();
  const sprache = useSprache();
  const gesamt = activity.totalAccounts;
  const bezugText = t("von {gesamt} Konten insgesamt", { gesamt });
  const hoechsteWoche = groessterWert(activity.weeks.map((w) => w.activeAccounts));

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <Activity className="size-5 shrink-0" />
          {t("Aktivität")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Nur Zählungen aus vorhandenen Daten, keine Personen und keine Seitenaufrufe.")}
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        <div>
          <ul className="flex flex-col gap-3">
            <Balkenzeile label={t("App geöffnet, letzte 7 Tage")} wert={activity.openedLast7Days} bezug={gesamt} zusatz={bezugText} hauptzeile />
            <Balkenzeile label={t("App geöffnet, letzte 30 Tage")} wert={activity.openedLast30Days} bezug={gesamt} zusatz={bezugText} hauptzeile />
          </ul>
          <p className="mt-2 text-xs text-muted-foreground">
            {t("Ein Richtwert: Gezählt werden Konten, deren Anmeldung in dem Zeitraum erneuert wurde. Wer die App kurz nach der letzten Erneuerung noch einmal öffnet, wird nicht erneut gezählt - die Zahl liegt eher zu niedrig.")}
          </p>
        </div>

        <div className="border-t pt-4">
          <ul className="flex flex-col gap-3">
            <Balkenzeile label={t("Aktiv, letzte 7 Tage")} wert={activity.activeLast7Days} bezug={gesamt} zusatz={bezugText} hauptzeile />
            <Balkenzeile label={t("Aktiv, letzte 30 Tage")} wert={activity.activeLast30Days} bezug={gesamt} zusatz={bezugText} hauptzeile />
            <Balkenzeile label={t("Aktiv, letzte 90 Tage")} wert={activity.activeLast90Days} bezug={gesamt} zusatz={bezugText} hauptzeile />
          </ul>
          <p className="mt-2 text-xs text-muted-foreground">
            {t("Aktiv heißt: in dem Zeitraum mindestens etwas angelegt (Training, Fährte, Ziel, Lernantwort, Termin ...). Push einschalten zählt nicht.")}
          </p>
        </div>

        <div className="border-t pt-4">
          <h3 className="text-sm font-medium">{t("Die letzten 12 Wochen")}</h3>
          <p className="mt-1 text-xs text-muted-foreground">
            {t("Je Woche (ab Montag): Balken und erste Zahl sind die aktiven Konten, die zweite Zahl die angelegten Trainings, Fährten und Abläufe.")}
          </p>
          <ol className="mt-3 flex flex-col gap-1.5">
            {activity.weeks.map((woche) => (
              <li key={woche.weekStart} className="flex items-center gap-2 text-sm">
                <span className="w-12 shrink-0 text-xs text-muted-foreground tabular-nums">{datumTagMonat(woche.weekStart, sprache)}</span>
                <div aria-hidden className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-primary/15">
                  <div className="h-full rounded-full bg-primary" style={{ width: `${balkenProzent(woche.activeAccounts, hoechsteWoche)}%` }} />
                </div>
                <span className="min-w-20 shrink-0 text-right tabular-nums">
                  <span className="font-semibold">{woche.activeAccounts}</span>
                  <span className="text-muted-foreground"> / {woche.entries}</span>
                </span>
              </li>
            ))}
          </ol>
        </div>
      </CardContent>
    </Card>
  );
}

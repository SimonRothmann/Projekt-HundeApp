"use client";

import Link from "next/link";
import { ListChecks } from "lucide-react";
import type { TrainerOpenCounts } from "@/lib/types";
import { zaehlerBeschriftung, zuErledigen } from "@/lib/trainer-offen";
import { useT } from "@/lib/i18n";

/**
 * Für Trainer:innen: was auf sie wartet (Beitrittsanfragen, Trainings zu
 * bewerten) als kleine Chips mit Link - dieselben Zahlen wie "Zu erledigen" auf
 * der Trainer-Übersicht. Nur, was größer als 0 ist; ohne Offenes entfällt die
 * Zeile ganz.
 */
export function ZuErledigenZeile({ zahlen }: { zahlen: TrainerOpenCounts | null }) {
  const t = useT();
  const offen = zuErledigen(zahlen);
  if (offen.length === 0) return null;

  return (
    <section aria-label={t("Zu erledigen")} className="flex flex-wrap items-center gap-2">
      <span className="flex items-center gap-1.5 text-sm font-medium text-muted-foreground">
        <ListChecks className="size-4" aria-hidden />
        {t("Zu erledigen")}
      </span>
      {offen.map((z) => (
        <Link
          key={z.art}
          href={z.href}
          className="inline-flex min-h-11 min-w-0 items-center gap-1.5 rounded-full bg-primary/8 px-3.5 text-sm font-medium ring-1 ring-primary/20 transition-colors hover:bg-primary/12"
        >
          <span className="font-semibold tabular-nums text-primary-text">{z.anzahl}</span>
          <span className="min-w-0 [overflow-wrap:anywhere]">{zaehlerBeschriftung(t, z)}</span>
        </Link>
      ))}
    </section>
  );
}

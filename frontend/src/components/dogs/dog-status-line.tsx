"use client";

import Link from "next/link";
import { BarChart } from "lucide-react";
import type { ZielStatus } from "@/lib/hundeseite";
import { useT } from "@/lib/i18n";

/**
 * Eine Zeile unter den Aktionen, nur wenn es etwas zu sagen gibt: wo der Hund
 * im laufenden Ziel steht ("BH · Woche 9 · 1/4") und ob neues Trainer-Feedback
 * wartet. Beides ist antippbar - das Ziel führt zum Plan, das Feedback zum
 * Eintrag im Tagebuch. Rechts ein ruhiger Link zur Statistik des Hundes.
 */
export function DogStatusLine({
  ziel,
  neuesFeedback,
  statistikHref,
  onZiel,
  onFeedback,
}: {
  ziel: ZielStatus | null;
  neuesFeedback: boolean;
  /** Adresse der Statistik dieses Hundes; null = kein Link (fremder Hund, noch kein Training). */
  statistikHref: string | null;
  onZiel: () => void;
  onFeedback: () => void;
}) {
  const t = useT();
  if (!ziel && !neuesFeedback && !statistikHref) return null;

  const stand = ziel
    ? ziel.woche
      ? t("Woche {woche} · {erledigt}/{geplant}", ziel.woche)
      : ziel.tage === 0
        ? t("heute")
        : ziel.tage === 1
          ? t("noch 1 Tag")
          : t("noch {n} Tage", { n: ziel.tage })
    : "";

  return (
    <div className="flex items-center gap-3 text-sm">
      {ziel && (
        <button
          type="button"
          onClick={onZiel}
          className="min-h-11 min-w-0 flex-1 truncate text-left text-muted-foreground transition-colors hover:text-foreground"
        >
          {ziel.name} · {stand}
        </button>
      )}
      {neuesFeedback && (
        <button
          type="button"
          onClick={onFeedback}
          className="flex min-h-11 shrink-0 items-center gap-1.5 font-medium text-primary-text"
        >
          <span aria-hidden className="size-2 rounded-full bg-primary" />
          {t("Neues Feedback")}
        </button>
      )}
      {statistikHref && (
        <Link
          href={statistikHref}
          className="ml-auto flex min-h-11 shrink-0 items-center gap-1.5 text-muted-foreground transition-colors hover:text-foreground"
        >
          <BarChart className="size-4" aria-hidden />
          {t("Statistik")}
        </Link>
      )}
    </div>
  );
}

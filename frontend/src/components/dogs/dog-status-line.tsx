"use client";

import type { ZielStatus } from "@/lib/hundeseite";
import { useT } from "@/lib/i18n";

/**
 * Eine Zeile unter den Aktionen, nur wenn es etwas zu sagen gibt: wo der Hund
 * im laufenden Ziel steht ("BH · Woche 9 · 1/4") und ob neues Trainer-Feedback
 * wartet. Beides ist antippbar - das Ziel führt zum Plan, das Feedback zum
 * Eintrag im Tagebuch.
 */
export function DogStatusLine({
  ziel,
  neuesFeedback,
  onZiel,
  onFeedback,
}: {
  ziel: ZielStatus | null;
  neuesFeedback: boolean;
  onZiel: () => void;
  onFeedback: () => void;
}) {
  const t = useT();
  if (!ziel && !neuesFeedback) return null;

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
    </div>
  );
}

"use client";

import type { GroupTrainingSession } from "@/lib/types";
import { useT } from "@/lib/i18n";

/**
 * Wer zum Termin kommt, in einer Zeile: Zusagen, Absagen, noch offen - je mit
 * farbigem Punkt. Die Farbe steht nie allein: Zu jedem Punkt gehört die Zahl
 * mit Wort, damit die Zeile auch ohne Farbensehen lesbar bleibt.
 *
 * Umbrechen darf sie: Auf 375 px Breite passen die drei Angaben nicht
 * nebeneinander.
 */
export function SessionCounts({
  termin,
  className,
}: {
  termin: Pick<GroupTrainingSession, "attendingCount" | "decliningCount" | "openCount">;
  className?: string;
}) {
  const t = useT();
  const teile = [
    // Singular bei 1: "1 kommen" liest sich falsch. Ein Plural-Helfer im i18n
    // gibt es nicht; wie sonst im Code zwei Sätze nebeneinander.
    {
      punkt: "bg-primary",
      text: termin.attendingCount === 1 ? t("1 kommt") : t("{n} kommen", { n: termin.attendingCount }),
    },
    {
      punkt: "bg-destructive",
      text: termin.decliningCount === 1 ? t("1 kann nicht") : t("{n} können nicht", { n: termin.decliningCount }),
    },
    { punkt: "bg-muted-foreground/60", text: t("{n} offen", { n: termin.openCount }) },
  ];
  return (
    <p className={["flex flex-wrap items-center gap-x-2 gap-y-0.5", className ?? "text-xs text-muted-foreground"].join(" ")}>
      {teile.map((teil, i) => (
        <span key={teil.text} className="inline-flex items-center gap-1">
          {i > 0 && <span aria-hidden className="mr-1">·</span>}
          <span aria-hidden className={`inline-block size-2 shrink-0 rounded-full ${teil.punkt}`} />
          {teil.text}
        </span>
      ))}
    </p>
  );
}

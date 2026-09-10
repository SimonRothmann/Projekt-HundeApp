"use client";

import type { GroupTrainingCategory, GroupTrainingSession } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { CalendarDays, MapPin } from "lucide-react";
import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";

const categoryLabel: Record<GroupTrainingCategory, string> = { 0: uebersetzbar("Welpen"), 1: uebersetzbar("Junghunde"), 2: uebersetzbar("Basis") };
const fmt = (iso: string) =>
  new Date(iso).toLocaleString("de-DE", { weekday: "short", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/**
 * Read-only-Sicht für Mitglieder: die nächsten Gruppentrainings der eigenen
 * Gruppen (siehe docs/GROUP_TRAINING_SCHEDULE.md). Rendert nichts, wenn es
 * keine kommenden Termine gibt – dann bleibt das Dashboard unverändert.
 *
 * Die Termine lädt die Startseite zusammen mit ihren übrigen Daten, damit sie
 * in einem Zug erscheint statt Abschnitt für Abschnitt.
 */
export function UpcomingTrainingsSection({ sessions }: { sessions: GroupTrainingSession[] }) {
  const t = useT();

  if (sessions.length === 0) return null;

  return (
    <Card>
      <CardHeader className="p-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <CalendarDays className="size-5 text-primary-text" />
          {t("Nächste Gruppentrainings")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2 p-3 pt-0">
        {sessions.slice(0, 5).map((s) => (
          <div key={s.id} className={s.status === 1 ? "rounded-md border p-2.5 opacity-60" : "rounded-md border p-2.5"}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm font-medium [overflow-wrap:anywhere]">
                {fmt(s.startsAt)} · {s.groupName}
              </span>
              <span className="flex shrink-0 items-center gap-1">
                <Badge variant="secondary">{t(categoryLabel[s.category])}</Badge>
                {s.status === 1 && <Badge variant="outline">{t("Abgesagt")}</Badge>}
              </span>
            </div>
            {s.location && (
              <p className="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
                <MapPin className="size-3" />
                {s.location}
              </p>
            )}
            {s.items.length > 0 && (
              <p className="mt-0.5 text-xs text-muted-foreground [overflow-wrap:anywhere]">
                {s.items.map((i) => (i.exercise ? i.exercise.title : i.freeText)).join(" · ")}
              </p>
            )}
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

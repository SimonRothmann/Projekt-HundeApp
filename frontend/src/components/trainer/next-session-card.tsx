"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { CalendarClock, MapPin } from "lucide-react";
import { api } from "@/lib/api";
import type { Club, GroupTrainingSession } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { buttonVariants } from "@/components/ui/button";
import { KalenderKnopf } from "@/components/schedule/kalender-knopf";
import { SessionCounts } from "@/components/schedule/session-counts";
import { naechsterTermin, zusageNamen } from "@/lib/naechster-termin";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/** So weit voraus wird gesucht - ein Termin jenseits davon ist noch nicht "der nächste". */
const SUCHE_TAGE = 90;

const wann = (iso: string) =>
  new Date(iso).toLocaleString("de-DE", { weekday: "long", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/**
 * Ganz oben auf der Trainer-Seite: der nächste geplante Termin über alle
 * Vereine, die die Person als Trainer:in betreut - mit der Zählung, wer kommt.
 * Es ist die Frage, die vor jedem Training gestellt wird ("Wie viele sind
 * heute?"), und stand vorher erst auf einer eigenen Seite.
 *
 * Gibt es keinen kommenden Termin (oder ließ er sich nicht laden), steht hier
 * nichts: Eine Karte "Kein Termin" ganz oben wäre Lärm.
 */
export function NextSessionCard({ clubs }: { clubs: Club[] }) {
  const t = useT();
  const [termin, setTermin] = useState<GroupTrainingSession | null>(null);
  const vereineSchluessel = clubs.map((c) => c.id).join(",");

  useEffect(() => {
    const ids = vereineSchluessel.split(",").filter(Boolean);
    if (ids.length === 0) return;
    let abgebrochen = false;

    const heute = new Date();
    const bis = new Date(heute.getTime() + SUCHE_TAGE * 24 * 60 * 60 * 1000);
    const params = new URLSearchParams({ from: heute.toISOString().slice(0, 10), to: bis.toISOString().slice(0, 10) });

    Promise.all(
      ids.map((id) =>
        // Ein Verein, der nicht antwortet, nimmt den anderen nichts weg.
        api.get<GroupTrainingSession[]>(`/api/group-training/schedule/clubs/${id}?${params}`).catch(() => [] as GroupTrainingSession[]),
      ),
    ).then((listen) => {
      if (!abgebrochen) setTermin(naechsterTermin(listen));
    });
    return () => {
      abgebrochen = true;
    };
  }, [vereineSchluessel]);

  if (!termin) return null;

  const { namen, weitere } = zusageNamen(termin);

  return (
    <Card>
      <CardHeader className="p-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <CalendarClock className="size-5 shrink-0 text-primary-text" />
          {t("Nächster Termin")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2.5 p-3 pt-0">
        <div className="min-w-0">
          <p className="font-medium [overflow-wrap:anywhere]">{termin.groupName}</p>
          <p className="text-sm text-muted-foreground">
            {wann(termin.startsAt)} · {t("{n} Min.", { n: termin.durationMinutes })}
          </p>
          {termin.location && (
            <p className="mt-0.5 flex items-start gap-1 text-sm text-muted-foreground">
              <MapPin className="mt-0.5 size-3.5 shrink-0" />
              <span className="min-w-0 [overflow-wrap:anywhere]">{termin.location}</span>
            </p>
          )}
        </div>

        <SessionCounts termin={termin} className="text-sm" />

        {namen.length > 0 && (
          <ul className="flex flex-wrap gap-1.5" aria-label={t("Zusagen")}>
            {namen.map((name, i) => (
              <li key={`${name}-${i}`} className="max-w-full min-w-0 truncate rounded-full border border-border/60 bg-primary/8 px-2.5 py-0.5 text-xs">
                {name}
              </li>
            ))}
            {weitere > 0 && (
              <li className="rounded-full border border-border/60 px-2.5 py-0.5 text-xs text-muted-foreground">
                {t("+{n}", { n: weitere })}
              </li>
            )}
          </ul>
        )}

        <div className="flex flex-wrap gap-2">
          <Link
            href={`/trainer/schedule?verein=${termin.clubId}`}
            className={cn(buttonVariants({ size: "sm" }))}
          >
            {t("Vorbereiten")}
          </Link>
          <KalenderKnopf termin={termin} />
        </div>
      </CardContent>
    </Card>
  );
}

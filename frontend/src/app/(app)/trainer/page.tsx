"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { CalendarDays, ChevronRight, ClipboardList, ListChecks, Building2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { anfragenJeGruppe, zuErledigen, type ZuErledigen } from "@/lib/trainer-offen";
import type { Club, TrainerOpenCounts } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { MyGroupsSection } from "@/components/trainer/my-groups-section";
import { NextSessionCard } from "@/components/trainer/next-session-card";
import { SupervisedDogsSection } from "@/components/trainer/supervised-dogs-section";

/** So viele betreute Hunde stehen auf der Übersicht, der Rest klappt auf. */
const HUNDE_VORSCHAU = 3;

/**
 * Trainer-Übersicht: oben, was auf die Person wartet, dann die Dinge, die an
 * einem Trainingsabend gebraucht werden (Termin, Gruppen, Hunde), unten die
 * Werkzeuge. Alles Lange liegt auf eigenen Seiten (/trainer/anfragen,
 * /trainer/bewerten, /trainer/verein) - die Übersicht war vorher am Telefon
 * sieben Bildschirme lang, und "Meine Gruppen" erst ab dem sechsten zu finden.
 */
export default function TrainerPage() {
  const t = useT();
  const [myClubs, setMyClubs] = useState<Club[] | null>(null);
  const [clubsFehler, setClubsFehler] = useState(false);
  const [counts, setCounts] = useState<TrainerOpenCounts | null>(null);

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    api
      .get<Club[]>("/api/groups/my-clubs")
      .then(setMyClubs)
      .catch((err) => {
        // Kein [] setzen: Ein Aussetzer ist nicht "kein Verein", sonst
        // verschwänden Werkzeuge und Nächster Termin ohne Hinweis.
        setClubsFehler(true);
        toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
      });
    api
      .get<TrainerOpenCounts>("/api/groups/open-counts")
      .then(setCounts)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : t("Offenes konnte nicht geladen werden.")));
    // t bewusst nicht in der Liste: nur für Fehlermeldungen, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const clubs = myClubs ?? [];
  const offen = zuErledigen(counts);

  const zaehlerText = (z: ZuErledigen) =>
    z.art === "anfragen"
      ? z.anzahl === 1 ? t("Beitrittsanfrage") : t("Beitrittsanfragen")
      : z.anzahl === 1 ? t("Training bewerten") : t("Trainings bewerten");

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("Trainer-Übersicht")}</h1>
        <p className="text-muted-foreground">{t("Gruppen, betreute Hunde und Offenes auf einen Blick.")}</p>
      </div>

      {offen.length > 0 && (
        <section className="flex flex-col gap-3">
          <SectionHeading icon={ListChecks} title={t("Zu erledigen")} />
          <div className="grid grid-cols-[repeat(auto-fit,minmax(9.5rem,1fr))] gap-3">
            {offen.map((z) => (
              <Link
                key={z.art}
                href={z.href}
                className="flex min-w-0 flex-col gap-0.5 rounded-xl bg-primary/8 px-3 py-2.5 ring-1 ring-primary/20 transition-colors hover:bg-primary/12 coarse:min-h-14"
              >
                <span className="flex items-center justify-between gap-2">
                  <span className="font-heading text-2xl font-semibold tabular-nums text-primary-text">{z.anzahl}</span>
                  <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
                </span>
                <span className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">{zaehlerText(z)}</span>
              </Link>
            ))}
          </div>
        </section>
      )}

      {clubs.length > 0 && <NextSessionCard clubs={clubs} />}

      <MyGroupsSection clubs={myClubs} anfragenJeGruppe={anfragenJeGruppe(counts)} />

      <SupervisedDogsSection vorschau={HUNDE_VORSCHAU} />

      {clubsFehler && (
        <Card>
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
            <p className="min-w-0 text-sm text-muted-foreground">{t("Vereine konnten nicht geladen werden.")}</p>
            <Button variant="outline" size="sm" onClick={() => window.location.reload()}>
              {t("Erneut versuchen")}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Gruppentraining, Terminplanung und Verein setzen einen Verein voraus -
          ohne ihn führten die Kacheln auf Seiten, die nur "kein Verein" sagen. */}
      {clubs.length > 0 && (
        <section className="flex flex-col gap-3">
          <SectionHeading icon={ClipboardList} title={t("Werkzeuge")} />
          <Card className="gap-0 py-0">
            <ul className="divide-y">
              <Werkzeug href="/trainer/group-training" icon={ClipboardList} titel={t("Gruppentraining")} zeile={t("Einheiten und Bausteine")} />
              <Werkzeug href="/trainer/schedule" icon={CalendarDays} titel={t("Terminplanung")} zeile={t("Termine und Serien")} />
              <Werkzeug href="/trainer/verein" icon={Building2} titel={t("Verein")} zeile={t("Einladung, Mitglieder, eigene Sportarten")} />
            </ul>
          </Card>
        </section>
      )}
    </div>
  );
}

function Werkzeug({
  href,
  icon: Icon,
  titel,
  zeile,
}: {
  href: string;
  icon: typeof ClipboardList;
  titel: string;
  zeile: string;
}) {
  return (
    <li>
      <Link
        href={href}
        className="flex min-w-0 items-center gap-3 px-4 py-3 transition-colors hover:bg-accent/30 coarse:min-h-14"
      >
        <Icon className="size-5 shrink-0 text-primary-text" />
        <span className="flex min-w-0 flex-1 flex-col">
          <span className="text-sm font-medium">{titel}</span>
          <span className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{zeile}</span>
        </span>
        <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
      </Link>
    </li>
  );
}

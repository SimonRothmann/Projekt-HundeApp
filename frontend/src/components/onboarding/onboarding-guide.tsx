"use client";

import { useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { OnboardingStatus } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button, buttonVariants } from "@/components/ui/button";
import { Check, ChevronRight, Clock, Compass } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Der geführte Erststart auf dem Dashboard.
 *
 * Ein leeres Dashboard ist die härteste Hürde der App: Man sieht, dass etwas
 * fehlt, aber nicht, was als Erstes zu tun wäre.
 *
 * Nach dem Hund gabelt sich der Weg, und zwar sichtbar nebeneinander:
 * - **auf eigene Faust**: Ziel setzen, erstes Training eintragen;
 * - **über den Verein**: beitreten, Trainingsgruppe wählen.
 *
 * Beides führt ans Ziel, keiner der Wege ist Pflicht. Deshalb steht "oder"
 * dazwischen und nicht "danach", und der Erststart gilt als erledigt, sobald
 * EINER der beiden gegangen ist.
 *
 * Verschwindet von selbst, sobald das der Fall ist - und lässt sich vorher
 * wegklicken, für alle, die sich erst einmal umsehen wollen.
 */

type Schritt = {
  titel: string;
  erledigt: boolean;
  /** Angefragt und wartet auf Freigabe - kein offener Schritt mehr. */
  wartet?: boolean;
  ziel: string;
  hinweis: string;
};

/**
 * Ob der Erststart überhaupt etwas zu zeigen hat. Das Dashboard fragt damit
 * ab, ob es seine eigene "Tritt einem Verein bei"-Karte zurückhalten soll -
 * zwei Kacheln mit derselben Botschaft wären Lärm.
 */
export function zeigtErststart(status: OnboardingStatus | null): status is OnboardingStatus {
  return status !== null && !status.isComplete && !status.isDismissed;
}

export function OnboardingGuide({
  status,
  onDismissed,
}: {
  status: OnboardingStatus | null;
  onDismissed: () => void;
}) {
  const [versteckt, setVersteckt] = useState(false);
  const t = useT();

  async function wegklicken() {
    setVersteckt(true);
    try {
      await api.post("/api/onboarding/dismiss");
      onDismissed();
    } catch (err) {
      setVersteckt(false);
      toast.error(err instanceof ApiError ? err.message : t("Konnte nicht ausgeblendet werden."));
    }
  }

  // Typwächter: danach ist status garantiert vorhanden.
  if (!zeigtErststart(status) || versteckt) return null;

  const hundZiel = status.firstDogId ? `/dogs/${status.firstDogId}` : "/dogs";

  const eigenerWeg: Schritt[] = [
    {
      titel: t("Ziel setzen"),
      erledigt: status.hasGoal,
      ziel: `${hundZiel}#trainingsplan`,
      hinweis: t("Prüfung und Termin wählen – daraus entsteht der Trainingsplan."),
    },
    {
      titel: t("Erstes Training eintragen"),
      erledigt: status.hasTraining,
      ziel: hundZiel,
      hinweis: t("Übung, Bewertung, fertig. Der Rest ist optional."),
    },
  ];

  const vereinsWeg: Schritt[] = [
    {
      titel: t("Verein beitreten"),
      erledigt: status.hasClubMembership,
      wartet: status.hasPendingClubRequest,
      ziel: "/clubs",
      hinweis: t("Anfrage stellen – der Verein gibt sie frei."),
    },
    {
      titel: t("Trainingsgruppe beitreten"),
      erledigt: status.hasGroupMembership,
      wartet: status.hasPendingGroupRequest,
      ziel: "/clubs",
      hinweis: t("Dein Trainer sieht dann eure Trainings und kann sie bewerten."),
    },
  ];

  return (
    <Card className="border-primary/40 bg-primary/5">
      <CardHeader className="items-center">
        <CardTitle className="flex items-center gap-2 text-base">
          <Compass className="size-5" />
          {t("Erste Schritte")}
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-4">
        {!status.hasDog ? (
          <div className="flex flex-col gap-3">
            <p className="text-sm text-muted-foreground">
              {t("Leg zuerst deinen Hund an – alles Weitere hängt daran.")}
            </p>
            <Link href="/dogs" className={cn(buttonVariants({ size: "sm" }), "self-start")}>
              {t("Hund anlegen")}
              <ChevronRight className="size-4" />
            </Link>
          </div>
        ) : (
          <>
            <p className="flex items-center gap-2 text-sm">
              <Erledigt />
              <span>
                <span className="font-medium">{status.firstDogName}</span>{" "}
                {t("ist angelegt. Weiter geht es auf einem von zwei Wegen – einer genügt.")}
              </span>
            </p>

            <div className="grid gap-3 sm:grid-cols-2">
              <Weg titel={t("Selbst loslegen")} schritte={eigenerWeg} />
              <Weg titel={t("Über den Verein")} schritte={vereinsWeg} />
            </div>
          </>
        )}

        <Button
          size="sm"
          variant="ghost"
          className="h-7 self-start px-2 text-xs text-muted-foreground"
          onClick={wegklicken}
        >
          {t("Ausblenden")}
        </Button>
      </CardContent>
    </Card>
  );
}

function Erledigt() {
  return (
    <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-primary/15 text-primary">
      <Check className="size-3.5" />
    </span>
  );
}

function Weg({ titel, schritte }: { titel: string; schritte: Schritt[] }) {
  const t = useT();

  return (
    <div className="flex min-w-0 flex-col gap-2 rounded-lg border border-border/60 bg-card p-3">
      <p className="text-sm font-medium">{titel}</p>
      <ul className="flex flex-col gap-2">
        {schritte.map((schritt) => (
          <li key={schritt.titel} className="min-w-0">
            {schritt.erledigt ? (
              <span className="flex items-start gap-2 text-sm text-muted-foreground">
                <Erledigt />
                <span className="line-through">{schritt.titel}</span>
              </span>
            ) : schritt.wartet ? (
              <span className="flex items-start gap-2 text-sm text-muted-foreground">
                <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-muted">
                  <Clock className="size-3.5" />
                </span>
                <span className="min-w-0">
                  {schritt.titel}
                  <span className="block text-xs">{t("Anfrage gestellt – warte auf Freigabe.")}</span>
                </span>
              </span>
            ) : (
              <Link
                href={schritt.ziel}
                // scroll={false}: Der Anker #trainingsplan entsteht erst, wenn
                // die Ziele geladen sind - Next würde sonst nach oben springen.
                scroll={false}
                className="flex min-w-0 items-start gap-2 rounded-md py-1 text-sm transition-colors hover:text-primary coarse:min-h-11"
              >
                <span className="flex size-5 shrink-0 items-center justify-center rounded-full border border-border" />
                <span className="min-w-0">
                  {schritt.titel}
                  <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">
                    {schritt.hinweis}
                  </span>
                </span>
              </Link>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

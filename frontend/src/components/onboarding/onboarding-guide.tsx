"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";
import { Building2, ChevronRight, CircleCheck, Flag } from "lucide-react";
import type { OnboardingStatus } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { AusblendenKnopf } from "@/components/onboarding/ausblenden-knopf";
import { useT } from "@/lib/i18n";

/**
 * "Gut gemacht! Wie geht es weiter?" - die Gabelung nach dem ersten Training.
 *
 * Der Erststart davor ist die Karte "Wie heißt dein Hund?" (ErsterHundKarte),
 * danach geht es direkt ins Eintragen-Fenster. Erst wenn das erste Training
 * steht, lohnt sich die Frage, wohin die Reise geht - sichtbar nebeneinander:
 * - **auf eigene Faust**: ein Prüfungsziel setzen (daraus entsteht der Plan);
 * - **über den Verein**: beitreten, Trainingsgruppe wählen.
 *
 * Beides führt ans Ziel, keiner der Wege ist Pflicht. Wann die Karte steht und
 * wann nicht, entscheidet erststartZustand (lib/erststart.ts): kurz nach dem
 * ersten Training, bis einer der Wege gegangen oder die Karte weggeklickt ist.
 */
export function OnboardingGuide({
  status,
  onDismissed,
}: {
  status: OnboardingStatus;
  onDismissed: () => void;
}) {
  const t = useT();
  // Ohne Hund-Id (kann nach einem Training nicht vorkommen) wenigstens die Liste.
  const hundZiel = status.firstDogId ? `/dogs/${status.firstDogId}` : "/dogs";

  return (
    <Card className="border-primary/40 bg-primary/5">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <CircleCheck className="size-5 shrink-0" aria-hidden />
          {t("Gut gemacht! Wie geht es weiter?")}
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">
          {t("Dein erstes Training ist eingetragen. Zwei Wege führen weiter – einer genügt.")}
        </p>

        <Weg
          icon={Flag}
          titel={t("Prüfungsziel setzen")}
          hinweis={t("Prüfung und Termin wählen – daraus entsteht der Trainingsplan.")}
          ziel={`${hundZiel}#trainingsplan`}
          // scroll={false}: Der Anker #trainingsplan entsteht erst, wenn die
          // Ziele geladen sind - Next würde sonst nach oben springen.
          scroll={false}
        />
        <Weg
          icon={Building2}
          titel={t("Verein beitreten")}
          hinweis={t("Anfrage stellen – der Verein gibt sie frei.")}
          ziel="/clubs"
        />

        <AusblendenKnopf onDismissed={onDismissed} />
      </CardContent>
    </Card>
  );
}

function Weg({
  icon: Icon,
  titel,
  hinweis,
  ziel,
  scroll,
}: {
  icon: LucideIcon;
  titel: string;
  hinweis: string;
  ziel: string;
  scroll?: boolean;
}) {
  return (
    <Link
      href={ziel}
      scroll={scroll}
      className="group flex min-h-14 min-w-0 items-center gap-3 rounded-lg border border-border/60 bg-card px-3 py-2 transition-colors hover:border-primary/40"
    >
      <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary-text ring-1 ring-primary/20">
        <Icon className="size-5" aria-hidden />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-medium [overflow-wrap:anywhere]">{titel}</span>
        <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">{hinweis}</span>
      </span>
      <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden />
    </Link>
  );
}

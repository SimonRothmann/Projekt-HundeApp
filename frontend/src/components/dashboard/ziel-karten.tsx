"use client";

import Link from "next/link";
import { Check, Target } from "lucide-react";
import type { Dog, Goal } from "@/lib/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { datumMitWochentag, pruefungsName, tageBisPruefung } from "@/lib/pruefung";
import { useSprache, useT } from "@/lib/i18n";

export type ZielKarteEintrag = {
  hund: Dog;
  goal: Goal;
  // Stand der laufenden Woche (siehe wochenFortschritt); null ohne geplante Übung.
  fortschritt: { woche: number; geplant: number; erledigt: number } | null;
};

/**
 * Je Hund mit aktivem Ziel eine kompakte Karte direkt unter der Begrüßung:
 * Wann ist die Prüfung, und wie weit ist die Woche?
 *
 * Bisher zeigte die Startseite den Plan nur über die offenen Wochenübungen -
 * wer alles erledigt hatte, sah von seinem Ziel gar nichts mehr.
 *
 * Bei überschrittenem Datum steht statt der Tage ein Link zur Hundeseite: Dort
 * sitzt "Ziel abschließen", und ein Datum, das verstrichen ist, wartet auf sein
 * Ergebnis.
 */
export function ZielKartenSection({ eintraege }: { eintraege: ZielKarteEintrag[] }) {
  if (eintraege.length === 0) return null;

  return (
    <section className="flex flex-col gap-3">
      {eintraege.map((eintrag) => (
        <ZielKarte key={eintrag.goal.id} eintrag={eintrag} />
      ))}
    </section>
  );
}

function ZielKarte({ eintrag }: { eintrag: ZielKarteEintrag }) {
  const t = useT();
  const sprache = useSprache();
  const { hund, goal, fortschritt } = eintrag;
  const tage = tageBisPruefung(goal.targetDate);
  const planLink = `/dogs/${hund.id}#trainingsplan`;
  const alles = fortschritt !== null && fortschritt.erledigt >= fortschritt.geplant;

  return (
    <Card size="sm">
      <CardContent className="flex flex-col gap-2.5">
        <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
          <div className="min-w-0">
            <p className="flex items-center gap-1.5 font-semibold [overflow-wrap:anywhere]">
              <Target className="size-4 shrink-0 text-primary-text" />
              <span className="min-w-0">
                {hund.name} · {pruefungsName(goal)}
              </span>
            </p>
            <p className="text-sm text-muted-foreground">
              {t("am {datum}", { datum: datumMitWochentag(goal.targetDate, sprache) })}
            </p>
          </div>
          {tage < 0 ? (
            <Link
              href={planLink}
              className="shrink-0 text-sm font-medium text-primary-text underline-offset-4 hover:underline coarse:flex coarse:min-h-11 coarse:items-center"
            >
              {t("Ergebnis eintragen")}
            </Link>
          ) : (
            <span className="shrink-0 text-sm font-medium">
              {tage === 0 ? t("heute") : tage === 1 ? t("noch 1 Tag") : t("noch {n} Tage", { n: tage })}
            </span>
          )}
        </div>

        {fortschritt && (
          <div className="flex flex-col gap-1.5">
            <div className="flex gap-1" aria-hidden>
              {Array.from({ length: fortschritt.geplant }, (_, i) => (
                <span
                  key={i}
                  className={cn("h-1.5 min-w-0 flex-1 rounded-full", i < fortschritt.erledigt ? "bg-primary" : "bg-muted")}
                />
              ))}
            </div>
            <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
              {alles ? (
                <>
                  <Check className="size-4 shrink-0 text-emerald-600 dark:text-emerald-500" />
                  {t("Diese Woche geschafft")}
                </>
              ) : fortschritt.geplant === 1 ? (
                t("Diese Woche: {erledigt} von 1 Übung", { erledigt: fortschritt.erledigt })
              ) : (
                t("Diese Woche: {erledigt} von {geplant} Übungen", {
                  erledigt: fortschritt.erledigt,
                  geplant: fortschritt.geplant,
                })
              )}
            </p>
          </div>
        )}

        <Link
          href={planLink}
          className="self-start text-sm text-primary-text underline-offset-4 hover:underline coarse:flex coarse:min-h-11 coarse:items-center"
        >
          {t("Zum Plan")}
        </Link>
      </CardContent>
    </Card>
  );
}

/**
 * Eine einzige Karte für alle Hunde ohne aktives Ziel - bei drei Hunden keine
 * drei gleichlautenden Aufforderungen. Der Knopf führt zum ersten der genannten
 * Hunde, dorthin, wo das Ziel angelegt wird.
 */
export function KeinZielKarte({ hunde }: { hunde: Dog[] }) {
  const t = useT();
  if (hunde.length === 0) return null;

  const [erster, ...weitere] = hunde;
  const untertitel =
    weitere.length === 0
      ? erster.name
      : weitere.length === 1
        ? t("Für {name} und einen weiteren", { name: erster.name })
        : t("Für {name} und {n} weitere", { name: erster.name, n: weitere.length });

  return (
    <Card size="sm" className="border-primary/40 bg-primary/5">
      <CardHeader>
        <CardTitle className="text-base">{t("Noch kein Prüfungsziel")}</CardTitle>
        <CardDescription className="[overflow-wrap:anywhere]">{untertitel}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-sm">{t("Mit einem Ziel plant Dogity jede Woche passende Übungen bis zur Prüfung.")}</p>
        <Link
          href={`/dogs/${erster.id}#trainingsplan`}
          className={cn(buttonVariants({ size: "sm" }), "self-start coarse:min-h-11")}
        >
          {t("Prüfungsziel setzen")}
        </Link>
      </CardContent>
    </Card>
  );
}

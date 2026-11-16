"use client";

import { Footprints } from "lucide-react";
import type { Dog } from "@/lib/types";
import { tageSeitLegen, type OffeneFaehrte } from "@/lib/faehrte";
import { Card, CardContent } from "@/components/ui/card";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";

import { useSprache, useT } from "@/lib/i18n";
import { alterMinutenText } from "@/lib/alter-minuten";
import { istMantrailing } from "@/lib/mantrailing";
import { ortsformat } from "@/lib/ortsformat";
import { datumKurz } from "@/lib/pruefung";
import { heuteIso } from "@/lib/kalendertag";
import { useSchritte } from "@/lib/use-schritte";

export type FaehrteMitHund = OffeneFaehrte & { hund: Dog };

/**
 * Gelegte Fährten und Trails, die noch nicht abgelaufen sind und sich noch
 * ablaufen lassen (Fährte 24, Trail 48 Stunden nach dem Legen) - mit Fährtenalter
 * und dem Knopf zum Ablaufen. Es ist die Hauptkarte der Startseite: primärfarben
 * getönt, mit dem Fährtenalter groß und einem gefüllten Knopf. Steht etwas von
 * gestern dabei (Übernachtfährte, Trail), heißt die Kopfzeile "Zuletzt gelegt"
 * und die Karte sagt, wann.
 *
 * Vorher musste man die Fährte zum Suchen im Tagebuch des Hundes wiederfinden:
 * Hunde, Hund, gut zwei Bildschirme scrollen. Das Fährtenalter steht dabei,
 * weil es genau die Größe ist, auf die man zwischen Legen und Suchen wartet.
 *
 * Die Fährten kommen mit dem Rest der Startseite in einem Aufruf (siehe
 * dashboard/page.tsx). Früher holte der Abschnitt sie selbst, je Hund und je
 * Einheit mit Fährte eine eigene Anfrage.
 */
export function HeuteGelegtSection({
  faehrten,
  mehrereHunde,
  onChanged,
}: {
  faehrten: FaehrteMitHund[];
  mehrereHunde: boolean;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();
  const ort = ortsformat(sprache);
  const schritte = useSchritte();

  if (faehrten.length === 0) return null;

  const alleHeute = faehrten.every((f) => tageSeitLegen(f.gelegtBis) === 0);

  return (
    <section className="flex flex-col gap-3">
      {faehrten.map((f) => {
        const uhrzeit = f.gelegtBis.toLocaleTimeString(ort, { hour: "2-digit", minute: "2-digit" });
        const tage = tageSeitLegen(f.gelegtBis);
        return (
          <Card
            key={f.track.id}
            className="border-l-4 border-l-primary bg-primary/8 ring-primary/30 dark:bg-primary/15 dark:ring-primary/40"
          >
            <CardContent className="flex flex-col gap-3">
              <p className="flex min-w-0 items-start gap-2 text-sm font-medium">
                <Footprints className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
                <span className="min-w-0 [overflow-wrap:anywhere]">
                  {alleHeute ? t("Heute gelegt") : t("Zuletzt gelegt")}
                  {mehrereHunde ? ` · ${f.hund.name}` : ""}
                  <span className="font-normal text-muted-foreground">
                    {" · "}
                    {tage === 0
                      ? t("Gelegt bis {uhrzeit}", { uhrzeit })
                      : tage === 1
                        ? t("Gestern gelegt bis {uhrzeit}", { uhrzeit })
                        : t("Gelegt am {datum} bis {uhrzeit}", { datum: datumKurz(heuteIso(f.gelegtBis.getTime()), sprache), uhrzeit })}
                  </span>
                </span>
              </p>
              <div className="min-w-0">
                <p className="flex flex-wrap items-baseline gap-x-2">
                  <span className="text-2xl font-bold tabular-nums">{alterMinutenText(f.alterMinuten, t)}</span>
                  <span className="text-sm text-muted-foreground">{istMantrailing(f.track) ? t("Trailalter") : t("Fährtenalter")}</span>
                </p>
                {(f.track.lengthMeters || f.track.surface) && (
                  <p className="text-sm text-muted-foreground">
                    {[
                      f.track.lengthMeters
                        ? `${Math.round(f.track.lengthMeters)} m${!istMantrailing(f.track) && schritte(f.track.lengthMeters) ? ` ${schritte(f.track.lengthMeters)}` : ""}`
                        : null,
                      f.track.surface ? f.track.surface.split(", ").map((teil) => t(teil)).join(", ") : null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </p>
                )}
              </div>
              <WalkRunRecorder
                trackId={f.track.id}
                laidTrackPoints={f.track.points}
                track={f.track}
                label={istMantrailing(f.track) ? t("Jetzt suchen") : t("Jetzt ablaufen")}
                hervorgehoben
                onSaved={onChanged}
              />
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

"use client";

import { Footprints } from "lucide-react";
import type { Dog } from "@/lib/types";
import { tageSeitLegen, type OffeneFaehrte } from "@/lib/faehrte";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";

import { useSprache, useT } from "@/lib/i18n";
import { istMantrailing } from "@/lib/mantrailing";
import { ortsformat } from "@/lib/ortsformat";
import { datumKurz } from "@/lib/pruefung";
import { heuteIso } from "@/lib/kalendertag";
import { useSchritte } from "@/lib/use-schritte";

export type FaehrteMitHund = OffeneFaehrte & { hund: Dog };

/**
 * Gelegte Fährten und Trails, die noch nicht abgelaufen sind und sich noch
 * ablaufen lassen (Fährte 24, Trail 48 Stunden nach dem Legen) - mit Fährtenalter
 * und dem Knopf zum Ablaufen. Steht etwas von gestern dabei (Übernachtfährte,
 * Trail), heißt der Abschnitt "Zuletzt gelegt" und die Karte sagt, wann.
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
      <SectionHeading icon={Footprints} title={alleHeute ? t("Heute gelegt") : t("Zuletzt gelegt")} />
      {faehrten.map((f) => {
        const uhrzeit = f.gelegtBis.toLocaleTimeString(ort, { hour: "2-digit", minute: "2-digit" });
        const tage = tageSeitLegen(f.gelegtBis);
        return (
          <Card key={f.track.id}>
            <CardContent className="flex flex-wrap items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="font-medium">
                  {mehrereHunde ? `${f.hund.name} · ` : ""}
                  {tage === 0
                    ? t("Gelegt bis {uhrzeit}", { uhrzeit })
                    : tage === 1
                      ? t("Gestern gelegt bis {uhrzeit}", { uhrzeit })
                      : t("Gelegt am {datum} bis {uhrzeit}", { datum: datumKurz(heuteIso(f.gelegtBis.getTime()), sprache), uhrzeit })}
                </p>
                <p className="text-sm text-muted-foreground">
                  {istMantrailing(f.track)
                    ? t("Trailalter {minuten} Min.", { minuten: f.alterMinuten })
                    : t("Fährtenalter {minuten} Min.", { minuten: f.alterMinuten })}
                  {f.track.lengthMeters ? ` · ${Math.round(f.track.lengthMeters)} m` : ""}
                  {!istMantrailing(f.track) && schritte(f.track.lengthMeters) ? ` ${schritte(f.track.lengthMeters)}` : ""}
                  {f.track.surface ? ` · ${f.track.surface.split(", ").map((teil) => t(teil)).join(", ")}` : ""}
                </p>
              </div>
              <WalkRunRecorder
                trackId={f.track.id}
                laidTrackPoints={f.track.points}
                track={f.track}
                label={istMantrailing(f.track) ? t("Jetzt suchen") : t("Jetzt ablaufen")}
                onSaved={onChanged}
              />
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

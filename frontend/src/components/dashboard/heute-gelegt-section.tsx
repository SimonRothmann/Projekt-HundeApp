"use client";

import { Footprints } from "lucide-react";
import type { Dog } from "@/lib/types";
import type { OffeneFaehrte } from "@/lib/faehrte";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";

import { useT } from "@/lib/i18n";

export type FaehrteMitHund = OffeneFaehrte & { hund: Dog };

/**
 * Heute gelegte Fährten, die noch nicht abgelaufen sind - mit Fährtenalter
 * und dem Knopf zum Ablaufen.
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

  if (faehrten.length === 0) return null;

  return (
    <section className="flex flex-col gap-3">
      <SectionHeading icon={Footprints} title={t("Heute gelegt")} />
      {faehrten.map((f) => {
        const uhrzeit = f.gelegtBis.toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" });
        return (
          <Card key={f.track.id}>
            <CardContent className="flex flex-wrap items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="font-medium">
                  {mehrereHunde ? `${f.hund.name} · ` : ""}
                  {t("Gelegt bis {uhrzeit}", { uhrzeit })}
                </p>
                <p className="text-sm text-muted-foreground">
                  {t("Fährtenalter {minuten} Min.", { minuten: f.alterMinuten })}
                  {f.track.lengthMeters ? ` · ${Math.round(f.track.lengthMeters)} m` : ""}
                  {f.track.surface ? ` · ${f.track.surface.split(", ").map((teil) => t(teil)).join(", ")}` : ""}
                </p>
              </div>
              <WalkRunRecorder
                trackId={f.track.id}
                laidTrackPoints={f.track.points}
                label={t("Jetzt ablaufen")}
                onSaved={onChanged}
              />
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

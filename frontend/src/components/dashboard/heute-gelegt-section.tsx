"use client";

import { useEffect, useState } from "react";
import { Footprints } from "lucide-react";
import { api } from "@/lib/api";
import type { Dog, GpsTrack, TrainingSession } from "@/lib/types";
import { nichtAbgelaufeneFaehrten, type OffeneFaehrte } from "@/lib/faehrte";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";

import { useT } from "@/lib/i18n";

type FaehrteMitHund = OffeneFaehrte & { hund: Dog };

/**
 * Heute gelegte Fährten, die noch nicht abgelaufen sind - mit Fährtenalter
 * und dem Knopf zum Ablaufen.
 *
 * Vorher musste man die Fährte zum Suchen im Tagebuch des Hundes wiederfinden:
 * Hunde, Hund, gut zwei Bildschirme scrollen. Das Fährtenalter steht dabei,
 * weil es genau die Größe ist, auf die man zwischen Legen und Suchen wartet.
 */
export function HeuteGelegtSection({ hunde }: { hunde: Dog[] }) {
  const t = useT();
  const [faehrten, setFaehrten] = useState<FaehrteMitHund[] | null>(null);
  const hundeSchluessel = hunde.map((h) => h.id).join(",");

  async function laden() {
    // Dasselbe Datum, mit dem der Recorder speichert (siehe fahrte-recorder).
    const heute = new Date().toISOString().slice(0, 10);
    const listen = await Promise.all(
      hunde.map(async (hund) => {
        try {
          const einheiten = await api.get<TrainingSession[]>(`/api/trainings?dogId=${hund.id}&from=${heute}`);
          const mitFaehrte = einheiten.filter((e) => e.hasGpsTrack);
          const tracks = (
            await Promise.all(mitFaehrte.map((e) => api.get<GpsTrack[]>(`/api/gps-tracks?trainingSessionId=${e.id}`)))
          ).flat();
          return nichtAbgelaufeneFaehrten(tracks).map((f) => ({ ...f, hund }));
        } catch {
          return [];
        }
      }),
    );
    setFaehrten(listen.flat());
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hundeSchluessel]);

  if (!faehrten || faehrten.length === 0) return null;

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
                  {hunde.length > 1 ? `${f.hund.name} · ` : ""}
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
                onSaved={laden}
              />
            </CardContent>
          </Card>
        );
      })}
    </section>
  );
}

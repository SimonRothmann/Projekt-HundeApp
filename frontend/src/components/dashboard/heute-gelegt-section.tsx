"use client";

import { useEffect, useState } from "react";
import { Check, Footprints } from "lucide-react";
import type { Dog } from "@/lib/types";
import { ablaufFristStunden, tageSeitLegen, type OffeneFaehrte } from "@/lib/faehrte";
import { spurArt } from "@/lib/spurart";
import { stehzeitErreichtAb } from "@/lib/schweiss";
import { Card, CardContent } from "@/components/ui/card";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";

import { useSprache, useT } from "@/lib/i18n";
import { alterMinutenText } from "@/lib/alter-minuten";
import { ortsformat } from "@/lib/ortsformat";
import { datumKurz } from "@/lib/pruefung";
import { heuteIso } from "@/lib/kalendertag";
import { useSchritte } from "@/lib/use-schritte";
import { leitfadenGemerkt, leitfadenNachId, liegezeitRest } from "@/lib/faehrte-leitfaden";

export type FaehrteMitHund = OffeneFaehrte & { hund: Dog };

/**
 * Gelegte Fährten und Trails, die noch nicht abgelaufen sind und sich noch
 * ablaufen lassen (Fährte 4, Trail 48, Schweißfährte 72 Stunden nach dem Legen) - mit Fährtenalter
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
        const art = spurArt(f.track);
        // Tag und Uhrzeit eines Zeitpunkts ("23.11.2026, 14:00") für die Stehzeit der Schweißfährte.
        const tagUndUhr = (zeitpunkt: Date) =>
          `${datumKurz(heuteIso(zeitpunkt.getTime()), sprache)}, ${zeitpunkt.toLocaleTimeString(ort, { hour: "2-digit", minute: "2-digit" })}`;
        const stehzeitAb = art === "schweiss" ? stehzeitErreichtAb(f.gelegtBis, f.track.stehzeitStunden) : null;
        const stehzeitErreicht = stehzeitAb !== null && f.alterMinuten >= (f.track.stehzeitStunden ?? 0) * 60;
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
                  <span className="text-sm text-muted-foreground">
                    {art === "trail" ? t("Trailalter") : art === "schweiss" ? t("Schweißfährtenalter") : t("Fährtenalter")}
                  </span>
                </p>
                {art === "faehrte" && <LiegezeitZeile dogId={f.hund.id} alterMinuten={f.alterMinuten} />}
                {art === "schweiss" && (
                  <>
                    {stehzeitAb && (
                      <p className={stehzeitErreicht ? "flex items-center gap-1 text-sm font-medium text-erfolg-text" : "text-sm text-muted-foreground"}>
                        {stehzeitErreicht ? (
                          <>
                            <Check className="size-4 shrink-0" aria-hidden />
                            {t("Stehzeit erreicht")}
                          </>
                        ) : (
                          t("Stehzeit erreicht ab {zeit}", { zeit: tagUndUhr(stehzeitAb) })
                        )}
                      </p>
                    )}
                    <p className="text-sm text-muted-foreground">
                      {t("Arbeiten bis {zeit}", { zeit: tagUndUhr(new Date(f.gelegtBis.getTime() + ablaufFristStunden(f.track) * 3600_000)) })}
                    </p>
                  </>
                )}
                {(f.track.lengthMeters || f.track.surface) && (
                  <p className="text-sm text-muted-foreground">
                    {[
                      f.track.lengthMeters
                        ? `${Math.round(f.track.lengthMeters)} m${art === "faehrte" && schritte(f.track.lengthMeters) ? ` ${schritte(f.track.lengthMeters)}` : ""}`
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
                label={art === "trail" ? t("Jetzt suchen") : art === "schweiss" ? t("Jetzt arbeiten") : t("Jetzt ablaufen")}
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

/** Zur beim Legen gewählten Stufe (nur auf diesem Gerät gemerkt): ist die Mindest-Liegezeit schon um? */
function LiegezeitZeile({ dogId, alterMinuten }: { dogId: string; alterMinuten: number }) {
  const t = useT();
  const [stufe, setStufe] = useState<string | null>(null);
  useEffect(() => {
    // Gemerkte Stufe aus dem localStorage (externe Quelle).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setStufe(leitfadenGemerkt(dogId));
  }, [dogId]);
  const leitfaden = leitfadenNachId(stufe);
  if (!leitfaden) return null;
  const rest = liegezeitRest(leitfaden, alterMinuten);
  return (
    <p className={rest === 0 ? "flex items-center gap-1 text-sm font-medium text-erfolg-text" : "text-sm text-muted-foreground"}>
      {rest === 0 ? (
        <>
          <Check className="size-4 shrink-0" aria-hidden />
          {t("{stufe}: Liegezeit erreicht", { stufe: leitfaden.name })}
        </>
      ) : (
        t("{stufe}: noch {rest}", { stufe: leitfaden.name, rest: alterMinutenText(rest, t) })
      )}
    </p>
  );
}

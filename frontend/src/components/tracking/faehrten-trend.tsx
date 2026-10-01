"use client";

import { useEffect, useState } from "react";
import { TrendingDown, TrendingUp } from "lucide-react";
import { api } from "@/lib/api";
import type { DogTrackStats } from "@/lib/types";
import { ampelStufe, type Ampel } from "@/lib/faehrten-bild";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat, zahlText } from "@/lib/ortsformat";
import { cn } from "@/lib/utils";

// Bewertungstrend als Pfeil: steigend (grün) / fallend (rot) / stabil.
// null (zu wenige Durchgänge) rendert nichts.
export function TrendBadge({ trend }: { trend: number | null }) {
  const t = useT();
  const sprache = useSprache();
  if (trend === null || Math.abs(trend) < 0.25) return <span className="text-muted-foreground">{t("→ stabil")}</span>;
  if (trend > 0)
    return (
      <span className="flex items-center gap-0.5 text-emerald-600 dark:text-emerald-400">
        <TrendingUp className="size-3.5" /> +{zahlText(trend, sprache)}
      </span>
    );
  return (
    <span className="flex items-center gap-0.5 text-destructive">
      <TrendingDown className="size-3.5" /> {zahlText(trend, sprache)}
    </span>
  );
}

const AMPEL_BALKEN: Record<Ampel, string> = {
  gruen: "bg-emerald-500",
  gelb: "bg-amber-500",
  rot: "bg-destructive",
};

/**
 * Fährten-Entwicklung eines Hundes: die jüngsten ausgewerteten Abläufe als
 * kleiner Balkenverlauf (Anteil "auf Fährte") plus Trend. Rendert nichts,
 * wenn der Hund zu wenige ausgewertete Fährten hat - so bleibt die Karte für
 * Hunde ohne Fährtenarbeit unverändert.
 *
 * Eine Komponente für zwei Orte: die Statistikseite (ab dem ersten Ablauf,
 * einfarbige Balken) und die Hundeseite (ab drei Abläufen, Balken nach
 * Abweichung gefärbt und beschriftet). Die Unterschiede sind Eigenschaften,
 * keine zweite Fassung - Abruf, Trend-Pfeil und Fehlerverhalten sind dieselben.
 *
 * Gemessen wird die Linie des HUNDEFÜHRERS (siehe GpsTrackEvaluator) - daher
 * die bewusst zurückhaltende Beschriftung.
 */
export function FaehrtenTrend({
  dogId,
  mindestens = 1,
  hoechstens,
  nachAbweichung = false,
  className = "border-t pt-2",
  aktualisiert,
}: {
  dogId: string;
  /** Wie viele ausgewertete Abläufe es geben muss, damit etwas erscheint. */
  mindestens?: number;
  /** Nur die jüngsten so viele Abläufe zeigen; ohne Angabe alle, die der Server liefert. */
  hoechstens?: number;
  /** Balken nach mittlerer Abweichung färben (Ampel) und mit ganzen Prozent beschriften. */
  nachAbweichung?: boolean;
  className?: string;
  /** Ändert sich der Wert, wird neu geladen - z. B. nach einem neuen Ablauf. */
  aktualisiert?: unknown;
}) {
  const t = useT();
  const sprache = useSprache();
  const [stats, setStats] = useState<DogTrackStats | null>(null);

  useEffect(() => {
    let active = true;
    api
      .get<DogTrackStats>(`/api/stats/dogs/${dogId}/tracks`)
      .then((data) => {
        if (active) setStats(data);
      })
      .catch(() => {
        // Still: der Block ist optional, ein Fehler soll die Karte nicht stören.
        if (active) setStats({ runs: [], deviationTrend: null, onTrackTrend: null });
      });
    return () => {
      active = false;
    };
  }, [dogId, aktualisiert]);

  if (stats === null || stats.runs.length === 0 || stats.runs.length < mindestens) return null;

  const runs = hoechstens ? stats.runs.slice(-hoechstens) : stats.runs;
  const last = runs[runs.length - 1];
  const datumFormat = ortsformat(sprache);
  // Ein Satz für beide Darstellungen, damit Tooltips nicht auseinanderlaufen.
  const tooltip = (run: (typeof runs)[number]) =>
    t("{datum}: Ø {meter} m · {prozent} % auf der Fährte", {
      datum: new Date(run.date).toLocaleDateString(datumFormat),
      meter: Math.round(run.avgDeviationMeters),
      prozent: Math.round(run.onTrackPercent),
    });

  return (
    <div className={className}>
      <div className="mb-1 flex items-center justify-between gap-2">
        <span className="min-w-0 text-xs font-medium text-muted-foreground">
          {t("Fährte · {n} Abläufe", { n: runs.length })}
        </span>
        {/* Sinkende Abweichung = Verbesserung, daher invertiert übergeben. */}
        {stats.deviationTrend !== null && <TrendBadge trend={-stats.deviationTrend} />}
      </div>
      {nachAbweichung ? (
        <>
          <div className="flex items-end gap-1" title={t("Anteil auf der Fährte je Ablauf")}>
            {runs.map((run, i) => {
              const prozent = Math.round(run.onTrackPercent);
              return (
                <div
                  key={i}
                  className="flex h-20 min-w-0 flex-1 flex-col items-center justify-end gap-0.5"
                  title={tooltip(run)}
                >
                  <span className="text-[10px] leading-none tabular-nums text-muted-foreground">{prozent}%</span>
                  <div
                    className={cn("w-full rounded-sm", AMPEL_BALKEN[ampelStufe(run.avgDeviationMeters)])}
                    style={{ height: `${Math.max(4, Math.round(prozent * 0.56))}px` }}
                  />
                </div>
              );
            })}
          </div>
          <p className="mt-1 text-[0.7rem] leading-snug text-muted-foreground">
            {t("Höhe: Anteil auf der Fährte · Farbe: mittlere Abweichung")}
          </p>
        </>
      ) : (
        <div className="flex items-end gap-1" title={t("Anteil auf der Fährte je Ablauf")}>
          {runs.map((run, i) => (
            <div
              key={i}
              className="flex-1 rounded-sm bg-primary/70"
              style={{ height: `${Math.max(4, Math.round(run.onTrackPercent * 0.28))}px` }}
              title={tooltip(run)}
            />
          ))}
        </div>
      )}
      <p className="mt-1 text-xs text-muted-foreground">
        {t("Zuletzt: Ø {meter} m · {prozent} % auf der Fährte", {
          meter: Math.round(last.avgDeviationMeters),
          prozent: Math.round(last.onTrackPercent),
        })}
        {last.articlesTotal > 0 &&
          ` · ${t("{gefunden}/{gesamt} Gegenstände", { gefunden: last.articlesFound, gesamt: last.articlesTotal })}`}
      </p>
    </div>
  );
}

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

/**
 * Trend der Fährten in Worten statt als nacktes "+1,3": Ein Plus bei der
 * Abweichung hieß "weniger Abweichung, also besser" - das konnte niemand
 * ablesen. `aenderung` ist die Änderung der mittleren Abweichung in Metern
 * (jüngere Hälfte der Abläufe minus ältere): negativ = näher an der Fährte.
 */
export function AbweichungsTrend({ aenderung }: { aenderung: number }) {
  const t = useT();
  const sprache = useSprache();
  const titel = t("Mittlere Abweichung der jüngeren Hälfte der Abläufe im Vergleich zur älteren");
  const meter = zahlText(Math.abs(aenderung), sprache);
  if (Math.abs(aenderung) < 0.25)
    return (
      <span className="text-muted-foreground" title={titel}>
        {t("Abweichung gleich geblieben")}
      </span>
    );
  if (aenderung < 0)
    return (
      <span className="flex items-center gap-0.5 text-emerald-600 dark:text-emerald-400" title={titel}>
        <TrendingUp className="size-3.5 shrink-0" /> {t("{meter} m näher an der Fährte", { meter })}
      </span>
    );
  return (
    <span className="flex items-center gap-0.5 text-destructive" title={titel}>
      <TrendingDown className="size-3.5 shrink-0" /> {t("{meter} m weiter weg von der Fährte", { meter })}
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
  stats: vorgegeben,
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
  /**
   * Schon geladene Auswertung des Hundes. Die Hundeseite braucht dieselben
   * Zahlen für die Tageszeilen des Tagebuchs und lädt sie einmal für beide;
   * ohne Angabe holt sich der Verlauf seine Daten selbst. null = noch nicht da.
   */
  stats?: DogTrackStats | null;
}) {
  const t = useT();
  const sprache = useSprache();
  const [eigene, setEigene] = useState<DogTrackStats | null>(null);
  const stats = vorgegeben !== undefined ? vorgegeben : eigene;

  useEffect(() => {
    if (vorgegeben !== undefined) return;
    let active = true;
    api
      .get<DogTrackStats>(`/api/stats/dogs/${dogId}/tracks`)
      .then((data) => {
        if (active) setEigene(data);
      })
      .catch(() => {
        // Still: der Block ist optional, ein Fehler soll die Karte nicht stören.
        if (active) setEigene({ runs: [], deviationTrend: null, onTrackTrend: null });
      });
    return () => {
      active = false;
    };
  }, [dogId, aktualisiert, vorgegeben]);

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
      {/* flex-wrap: Der Trend steht jetzt in Worten und passt bei 375 px nicht immer daneben. */}
      <div className="mb-1 flex flex-wrap items-center justify-between gap-x-2 gap-y-0.5 text-xs">
        <span className="min-w-0 font-medium text-muted-foreground">
          {t("Fährte · {n} Abläufe", { n: runs.length })}
        </span>
        {stats.deviationTrend !== null && <AbweichungsTrend aenderung={stats.deviationTrend} />}
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

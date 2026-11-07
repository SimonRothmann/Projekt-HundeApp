"use client";

import { ChevronRight } from "lucide-react";
import type { GpsWalkRun } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { aufschluesselung, bestaetigteZahlen } from "@/lib/gegenstaende";
import { istMantrailing, spurGrenzen } from "@/lib/mantrailing";
import { ampelLabel, ampelStufe, type Ampel } from "@/lib/spur-ampel";
import {
  abschnittsZeilen,
  dauerText,
  hatWinkelUndSchenkel,
  winkelBeschriftung,
  zustandBeschriftung,
} from "@/lib/winkel-schenkel";
type Spur = { kind?: number | null };

// Die Grenzen je Art stehen in lib/mantrailing.ts, die Einstufung in lib/spur-ampel.ts.
const AMPEL_KLASSE: Record<Ampel, string> = {
  gruen: "text-emerald-600 dark:text-emerald-500",
  gelb: "text-amber-600 dark:text-amber-500",
  rot: "text-destructive",
};

function ampelClass(avgMeters: number, art?: Spur): string {
  return AMPEL_KLASSE[ampelStufe(avgMeters, spurGrenzen(art))];
}

const stopLabel: Record<number, string> = {
  0: uebersetzbar("unerklärt"),
  1: uebersetzbar("Verweisen"),
  2: uebersetzbar("erklärt"),
  3: uebersetzbar("am Winkel"),
};
const stopClass: Record<number, string> = {
  0: "border-destructive/40 text-destructive",
  1: "border-emerald-500/40 text-emerald-600 dark:text-emerald-500",
  2: "border-border text-muted-foreground",
  3: "border-border text-muted-foreground",
};

/**
 * Winkel und Schenkel der Fährte, in Laufreihenfolge, aufklappbar (im Ergebnis-Fenster offen).
 * Nur wenn es etwas zu unterscheiden gibt: einen Winkel oder mehr als einen Schenkel. Keine Note -
 * die Zeilen sagen, WO es gehakt hat; die Deutung bleibt bei der Hundeführer:in.
 */
function WinkelUndSchenkel({ run, gross, art }: { run: GpsWalkRun; gross: boolean; art?: Spur }) {
  const t = useT();
  if (!hatWinkelUndSchenkel(run)) return null;

  const grenzen = spurGrenzen(art);
  return (
    <details open={gross} className={cn("group min-w-0 rounded-md border border-input px-2.5", gross ? "text-sm" : "text-xs")}>
      <summary className="flex min-h-10 cursor-pointer list-none items-center justify-between gap-2 font-medium [&::-webkit-details-marker]:hidden">
        {t("Winkel und Schenkel")}
        <ChevronRight className="size-4 shrink-0 transition-transform group-open:rotate-90" aria-hidden />
      </summary>
      <ul className="flex flex-col divide-y pb-1">
        {abschnittsZeilen(run).map((zeile) => {
          const platz = "flex min-w-0 flex-wrap items-baseline gap-x-1.5 gap-y-0.5 py-1.5 [overflow-wrap:anywhere]";
          if (zeile.art === "schenkel") {
            const { leg } = zeile;
            return (
              <li key={`s${leg.number}`} className={platz}>
                <span className="font-medium">{t("Schenkel {nr}", { nr: leg.number })}</span>
                <span>· {t("{meter} m", { meter: Math.round(leg.lengthMeters) })}</span>
                {leg.durationSeconds != null && <span>· {t("{dauer} min", { dauer: dauerText(leg.durationSeconds) })}</span>}
                {leg.avgDeviationMeters != null && (
                  <span className={ampelClass(leg.avgDeviationMeters, art)}>
                    · {t("Ø {meter} m", { meter: Math.round(leg.avgDeviationMeters) })}
                  </span>
                )}
              </li>
            );
          }

          const { corner, zustand } = zeile;
          const zustandKlasse =
            zustand === "sauber"
              ? AMPEL_KLASSE.gruen
              : zustand === "ueberschossen"
                ? AMPEL_KLASSE[ampelStufe(corner.overshootMeters ?? Infinity, grenzen)]
                : "text-muted-foreground";
          return (
            <li key={`w${corner.number}`} className={platz}>
              <span className="font-medium">{winkelBeschriftung(corner, t)}</span>
              <span className={zustandKlasse}>· {zustandBeschriftung(corner, t)}</span>
              {corner.holdSeconds > 0 && <span>· {t("Halt {sekunden} s", { sekunden: corner.holdSeconds })}</span>}
            </li>
          );
        })}
      </ul>
    </details>
  );
}

/**
 * Auswertung eines Ablauf-Versuchs: Abweichung, Gegenstände, Winkel und
 * Schenkel (nur Fährte), Stockungen.
 *
 * Wichtig für die Deutung (und deshalb auch in der UI benannt): Gemessen wird
 * die Linie des HUNDEFÜHRERS. Der Hund kann im Radius der Fährtenleine
 * ausscheren und zurückkommen, ohne dass sich das Gerät bewegt - solche
 * Ausschläge sind hier unsichtbar. Sichtbar werden sie über die Stockungen:
 * sucht oder kreist der Hund, bleibt der Hundeführer stehen.
 *
 * gross: für das Ergebnis-Fenster direkt nach dem Ablaufen (ablauf-ergebnis-
 * sheet.tsx) - dieselben Werte, aber die Abweichung als Überschrift und die
 * übrigen Zahlen als Felder, die man im Stehen am Platz ohne Brille liest.
 *
 * art: die Spur des Laufs. Beim Mantrailing gelten andere Grenzen und Begriffe
 * (siehe lib/mantrailing.ts); ohne Angabe gilt die Fährte.
 */
export function WalkRunEvaluation({ run, gross = false, art }: { run: GpsWalkRun; gross?: boolean; art?: Spur }) {
  const t = useT();
  // Lose Prüfungen (== null statt === null, ?? []): aus einem älteren
  // Read-Cache oder von einem älteren Backend-Stand fehlen diese Felder ganz
  // (undefined) - ohne diese Toleranz stürzt die Fährtenansicht ab, wie
  // seinerzeit bei goal.weekConfigs.
  if (run.evaluatedAt == null || run.avgDeviationMeters == null) return null;

  // Anzeige in ganzen Metern, Ampel aber auf dem ungerundeten Wert: dieselben
  // Schwellen wie Backend (GpsTrackEvaluator) und Kartenlinie - sonst wäre
  // dieselbe Fährte hier grün und auf der Karte gelb.
  const avg = run.avgDeviationMeters;
  const stops = run.stops ?? [];
  const unexplained = stops.filter((s) => s.kind === 0).length;
  const trail = istMantrailing(art);
  const bestaetigt = bestaetigteZahlen(run);
  const zahl = gross ? "rounded-md border border-input px-2.5 py-1.5 text-sm font-medium text-foreground" : "text-xs";
  const abweichung = t("Ø {meter} m", { meter: Math.round(avg) });
  const kennzahlen = (
    <>
      {run.maxDeviationMeters != null && (
        <span className={zahl}>{t("max {meter} m", { meter: Math.round(run.maxDeviationMeters) })}</span>
      )}
      {run.onTrackPercent != null && (
        <span className={zahl}>
          {trail
            ? t("{prozent} % auf der Spur", { prozent: Math.round(run.onTrackPercent) })
            : t("{prozent} % auf Fährte", { prozent: Math.round(run.onTrackPercent) })}
        </span>
      )}
      {run.articlesTotal != null && run.articlesTotal > 0 && (
        // Mit Angaben der Hundeführer:in zählen die bestätigten Gegenstände, sonst die GPS-Zahl wie bisher.
        <>
          <span className={zahl}>
            {t("{gefunden}/{gesamt} Gegenstände", {
              gefunden: bestaetigt?.gefunden ?? run.articlesFound ?? 0,
              gesamt: run.articlesTotal,
            })}
          </span>
          {bestaetigt && (
            <span className="min-w-0 text-xs text-muted-foreground [overflow-wrap:anywhere]">{aufschluesselung(bestaetigt, t)}</span>
          )}
        </>
      )}
    </>
  );

  return (
    // Kein eigener Rand/Hintergrund: Der Ablauf-Kasten (gps-track-section.tsx)
    // trägt die Werte schon - ein dritter Kasten darin machte die Zeile eng.
    <div className={cn("flex flex-col pt-1", gross ? "gap-3" : "gap-1.5")}>
      {gross ? (
        <>
          <div className="flex flex-col gap-1">
            <span className={cn("text-3xl font-semibold", ampelClass(avg, art))}>{abweichung}</span>
            <span className={cn("text-sm", ampelClass(avg, art))}>{t(ampelLabel(avg, art))}</span>
          </div>
          <div className="flex flex-wrap gap-2">{kennzahlen}</div>
        </>
      ) : (
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <span className={cn("text-sm font-semibold", ampelClass(avg, art))}>{abweichung}</span>
          <span className={cn("text-xs", ampelClass(avg, art))}>{t(ampelLabel(avg, art))}</span>
          {kennzahlen}
        </div>
      )}

      <WinkelUndSchenkel run={run} gross={gross} art={art} />

      {stops.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          {stops.slice(0, 6).map((stop, i) => (
            <Badge key={i} variant="outline" className={stopClass[stop.kind]}>
              {t(stopLabel[stop.kind])} {stop.durationSeconds}s
              {stop.markerLabel ? ` · ${stop.markerLabel}` : ""}
            </Badge>
          ))}
          {stops.length > 6 && <span className="text-xs">{t("+{n} weitere", { n: stops.length - 6 })}</span>}
        </div>
      )}

      <p className="text-[0.7rem] leading-snug">
        {trail
          ? t("Gemessen wird die Linie des Hundeführers. Beim Mantrailing arbeitet der Hund an langer Leine auch neben der Spur – deshalb gelten hier {gruen} m als nah und {gelb} m als mittlere Abweichung.", {
              gruen: spurGrenzen(art).gruenBis,
              gelb: spurGrenzen(art).gelbBis,
            })
          : t("Gemessen wird die Linie des Hundeführers – der Hund kann bis Leinenlänge abweichen, ohne dass es hier sichtbar wird.")}
        {unexplained > 0 &&
          ` ${unexplained === 1 ? t("1 unerklärte Stockung deutet auf Suchen hin.") : t("{n} unerklärte Stockungen deuten auf Suchen hin.", { n: unexplained })}`}
      </p>
    </div>
  );
}

"use client";

import type { ReactNode } from "react";
import { ChevronDown } from "lucide-react";
import type { DogTrackRun, TrainingSession } from "@/lib/types";
import { useSprache, useT } from "@/lib/i18n";
import { hatFeedback, laufAmTag, tagKurz, tagesKurzbild } from "@/lib/tagebuch";
import { cn } from "@/lib/utils";

/** Id der aufklappbaren Fläche eines Tages - die Zeile verweist per aria-controls darauf. */
const inhaltId = (datum: string) => `tag-inhalt-${datum}`;

/**
 * Ein Trainingstag im Tagebuch: standardmäßig EINE Zeile, die den Tag in einem
 * Blick sagt (Datum, Dauer, Kurzbild, Markierung "Feedback"), und beim Antippen
 * die volle Tageskarte darunter (`children`, siehe SessionDayCard).
 *
 * Der Inhalt kommt als Kind und wird nur gerendert, wenn der Tag offen ist -
 * Karte und GPS-Abruf gehören damit zum Aufklappen und nicht zum Anzeigen der
 * Liste. Die Zeile bleibt als Kopf der Karte stehen und klappt wieder zu.
 *
 * `neu`: Feedback, das auf diesem Gerät noch nicht gesehen wurde. Die Zeile
 * markiert es nur; als gesehen gilt es erst, wenn der Tag aufgeklappt ist.
 */
export function SessionDayRow({
  tag,
  offen,
  neu,
  laeufe,
  onToggle,
  children,
}: {
  tag: TrainingSession[];
  offen: boolean;
  neu: boolean;
  /** Ausgewertete Fährten-Abläufe des Hundes - für das Kurzbild einer Fährte. */
  laeufe: readonly DogTrackRun[] | null;
  onToggle: () => void;
  /** Die aufgeklappte Tageskarte. */
  children?: ReactNode;
}) {
  const t = useT();
  const sprache = useSprache();
  const datum = tag[0].date;
  const minuten = tag.reduce((summe, s) => summe + s.durationMinutes, 0);
  const feedback = hatFeedback(tag);

  return (
    // Optik der Karten von früher (ruhiger Ring statt Rahmen); die ID bleibt
    // für den Sprung zu einem Eintrag, der keinen Feedback-Block hat.
    <div
      id={`tag-${datum}`}
      className="overflow-hidden rounded-xl bg-card text-card-foreground ring-1 ring-foreground/10 dark:ring-white/15"
    >
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={offen}
        // Nur bei offenem Tag: Zu, gibt es das Element nicht, und der Verweis
        // ginge ins Leere (wie bei den Reitern, siehe group-view-switch).
        aria-controls={offen ? inhaltId(datum) : undefined}
        className={cn(
          "flex min-h-16 w-full items-center gap-3 px-3 py-2 text-left transition-colors hover:bg-muted/50",
          offen && "border-b bg-primary/6 dark:bg-white/6",
        )}
      >
        <span className="flex w-20 shrink-0 flex-col">
          <span className="text-sm font-semibold tabular-nums">{tagKurz(datum, sprache)}</span>
          <span className="text-xs text-muted-foreground">{t("{n} Min.", { n: minuten })}</span>
        </span>
        <span className="line-clamp-2 min-w-0 flex-1 text-sm [overflow-wrap:anywhere]">
          {tagesKurzbild(tag, laufAmTag(laeufe, datum), t)}
        </span>
        {feedback && (
          <span
            className={cn(
              "flex shrink-0 items-center gap-1.5 text-xs",
              neu ? "font-semibold text-primary-text" : "text-muted-foreground",
            )}
          >
            <span aria-hidden className={cn("size-2 rounded-full", neu ? "bg-primary" : "bg-muted-foreground/40")} />
            {t("Feedback")}
            {neu && <span className="sr-only"> ({t("Neu")})</span>}
          </span>
        )}
        <ChevronDown
          aria-hidden
          className={cn("size-4 shrink-0 text-muted-foreground transition-transform", offen && "rotate-180")}
        />
      </button>
      {offen && <div id={inhaltId(datum)}>{children}</div>}
    </div>
  );
}

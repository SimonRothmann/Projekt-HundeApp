"use client";

import { useT } from "@/lib/i18n";
import type { HundeReiter } from "@/lib/hundeseite";
import { SegmentUmschalter } from "@/components/ui/segment-umschalter";

/** Id des Reiters bzw. seines Inhalts - das Panel verweist per aria-labelledby auf seinen Reiter. */
export const reiterTabId = (reiter: HundeReiter) => `hund-tab-${reiter}`;
export const reiterPanelId = (reiter: HundeReiter) => `hund-panel-${reiter}`;

/**
 * Umschalter "Plan / Tagebuch (n Tage)" über die ganze Breite. Für
 * Besitzer:innen mit eingeschaltetem Modul kommt "Gesundheit" als drittes
 * Segment dazu; dann bekommt das Tagebuch mit seiner längeren Beschriftung
 * ("Tagebuch (12 Tage)") den meisten Platz, "Plan" den wenigsten.
 *
 * Der Reiter gehört der Hundeseite (er steht in der Adresse); hier wird nur
 * angezeigt und gemeldet.
 */
export function DogTabs({
  reiter,
  tage,
  zeigtGesundheit,
  onChange,
}: {
  reiter: HundeReiter;
  /** Anzahl der Trainingstage im geladenen Tagebuch. */
  tage: number;
  /** Ob das Segment "Gesundheit" dabei ist (Besitzer:innen, Modul an). */
  zeigtGesundheit: boolean;
  onChange: (reiter: HundeReiter) => void;
}) {
  const t = useT();
  const segmente: [HundeReiter, string][] = [
    ["plan", t("Plan")],
    [
      "tagebuch",
      tage === 0 ? t("Tagebuch") : tage === 1 ? t("Tagebuch (1 Tag)") : t("Tagebuch ({n} Tage)", { n: tage }),
    ],
    ...(zeigtGesundheit ? [["gesundheit", t("Gesundheit")] as [HundeReiter, string]] : []),
  ];

  return (
    <SegmentUmschalter
      ariaLabel={t("Ansicht des Hundes")}
      wert={reiter}
      segmente={segmente}
      tabId={reiterTabId}
      onChange={onChange}
      className={zeigtGesundheit ? "grid-cols-[minmax(0,0.65fr)_minmax(0,1.75fr)_minmax(0,1.1fr)]" : undefined}
    />
  );
}

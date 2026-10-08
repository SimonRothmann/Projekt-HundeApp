"use client";

import { useT } from "@/lib/i18n";
import type { HundeReiter } from "@/lib/hundeseite";
import { cn } from "@/lib/utils";

/** Id des Reiters bzw. seines Inhalts - das Panel verweist per aria-labelledby auf seinen Reiter. */
export const reiterTabId = (reiter: HundeReiter) => `hund-tab-${reiter}`;
export const reiterPanelId = (reiter: HundeReiter) => `hund-panel-${reiter}`;

/**
 * Umschalter "Plan / Tagebuch (n Tage)" über die ganze Breite: zwei gleich
 * breite Segmente, am Telefon mit dem Daumen gut zu treffen - Optik und
 * Bedienung wie GroupViewSwitch der Gruppenseite.
 *
 * Der Reiter gehört der Hundeseite (er steht in der Adresse); hier wird nur
 * angezeigt und gemeldet. min-w-0 + truncate, damit die englische Beschriftung
 * bei 375 px nichts aufspreizt.
 */
export function DogTabs({
  reiter,
  tage,
  onChange,
}: {
  reiter: HundeReiter;
  /** Anzahl der Trainingstage im geladenen Tagebuch. */
  tage: number;
  onChange: (reiter: HundeReiter) => void;
}) {
  const t = useT();
  const segmente: [HundeReiter, string][] = [
    ["plan", t("Plan")],
    [
      "tagebuch",
      tage === 0 ? t("Tagebuch") : tage === 1 ? t("Tagebuch (1 Tag)") : t("Tagebuch ({n} Tage)", { n: tage }),
    ],
  ];

  return (
    <div role="tablist" aria-label={t("Ansicht des Hundes")} className="grid grid-cols-2 gap-1 rounded-xl bg-muted p-1">
      {segmente.map(([wert, label]) => {
        const aktiv = reiter === wert;
        return (
          <button
            key={wert}
            id={reiterTabId(wert)}
            type="button"
            role="tab"
            aria-selected={aktiv}
            onClick={() => onChange(wert)}
            className={cn(
              "min-h-11 min-w-0 rounded-lg px-2 text-sm font-medium transition-colors",
              aktiv ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground",
            )}
          >
            <span className="block truncate">{label}</span>
          </button>
        );
      })}
    </div>
  );
}

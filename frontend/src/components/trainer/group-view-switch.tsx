"use client";

import { useT } from "@/lib/i18n";
import type { GruppenAnsicht } from "@/lib/gruppen-ansicht";
import { cn } from "@/lib/utils";

/** Id des Tabs bzw. des Inhalts einer Ansicht - das Panel verweist per aria-labelledby auf seinen Tab.
 * Kein aria-controls am Tab: Es gibt immer nur das Panel der offenen Ansicht, der andere Verweis ginge ins Leere. */
export const ansichtTabId = (ansicht: GruppenAnsicht) => `gruppe-tab-${ansicht}`;
export const ansichtPanelId = (ansicht: GruppenAnsicht) => `gruppe-panel-${ansicht}`;

/**
 * Umschalter "Mitglieder (n) / Anmeldungen (m)" über die ganze Breite: zwei
 * gleich breite Segmente, am Telefon mit dem Daumen gut zu treffen.
 *
 * Die Ansicht gehört der Gruppenseite (sie steht in der Adresse); hier wird nur
 * angezeigt und gemeldet. min-w-0 + truncate, damit lange Zahlen oder die
 * englische Beschriftung bei 375 px nichts aufspreizen.
 */
export function GroupViewSwitch({
  ansicht,
  mitglieder,
  anmeldungen,
  onChange,
}: {
  ansicht: GruppenAnsicht;
  mitglieder: number;
  anmeldungen: number;
  onChange: (ansicht: GruppenAnsicht) => void;
}) {
  const t = useT();
  const segmente: [GruppenAnsicht, string][] = [
    ["mitglieder", t("Mitglieder ({n})", { n: mitglieder })],
    ["anmeldungen", t("Anmeldungen ({n})", { n: anmeldungen })],
  ];

  return (
    <div role="tablist" aria-label={t("Ansicht der Gruppe")} className="grid grid-cols-2 gap-1 rounded-xl bg-muted p-1">
      {segmente.map(([wert, label]) => {
        const aktiv = ansicht === wert;
        return (
          <button
            key={wert}
            id={ansichtTabId(wert)}
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

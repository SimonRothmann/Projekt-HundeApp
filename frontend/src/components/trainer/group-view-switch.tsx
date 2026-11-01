"use client";

import { useT } from "@/lib/i18n";
import type { GruppenAnsicht } from "@/lib/gruppen-ansicht";
import { SegmentUmschalter } from "@/components/ui/segment-umschalter";

/** Id des Tabs bzw. des Inhalts einer Ansicht - das Panel verweist per aria-labelledby auf seinen Tab.
 * Kein aria-controls am Tab: Es gibt immer nur das Panel der offenen Ansicht, der andere Verweis ginge ins Leere. */
export const ansichtTabId = (ansicht: GruppenAnsicht) => `gruppe-tab-${ansicht}`;
export const ansichtPanelId = (ansicht: GruppenAnsicht) => `gruppe-panel-${ansicht}`;

/**
 * Umschalter "Mitglieder (n) / Anmeldungen (m)" über die ganze Breite. Die
 * Ansicht gehört der Gruppenseite (sie steht in der Adresse); hier wird nur
 * angezeigt und gemeldet.
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
    <SegmentUmschalter
      ariaLabel={t("Ansicht der Gruppe")}
      wert={ansicht}
      segmente={segmente}
      tabId={ansichtTabId}
      onChange={onChange}
    />
  );
}

"use client";

import { ArrowUp } from "lucide-react";
import { useT } from "@/lib/i18n";
import { himmelsrichtung, normalisiere } from "@/lib/wind";

/**
 * Die Windrichtung als Himmelsrichtung mit kleinem Pfeil: "aus W". Es gilt die Konvention des
 * Wetterdienstes (Wind AUS Westen); der Pfeil zeigt dorthin, wohin der Wind weht, wie bei Wetterkarten.
 * `beimSuchen`: der Wert gehört zum Suchen und nicht zum Legen.
 */
export function WindRichtung({ grad, beimSuchen = false }: { grad: number; beimSuchen?: boolean }) {
  const t = useT();
  const richtung = t(himmelsrichtung(grad));
  return (
    <span
      className="inline-flex items-center gap-1"
      title={t("Wind aus {richtung} – der Pfeil zeigt, wohin er weht", { richtung })}
    >
      <ArrowUp aria-hidden className="size-3 shrink-0" style={{ transform: `rotate(${normalisiere(grad + 180)}deg)` }} />
      {beimSuchen ? t("beim Suchen aus {richtung}", { richtung }) : t("aus {richtung}", { richtung })}
    </span>
  );
}

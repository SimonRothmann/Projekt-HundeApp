"use client";

import { ArrowUp } from "lucide-react";
import { useT } from "@/lib/i18n";
import type { AnsatzHinweis as Hinweis } from "@/lib/ansatz";
import { pfeilWinkel } from "@/lib/ansatz";
import { himmelsrichtung } from "@/lib/wind";

/**
 * Beim verdeckten Absuchen das Einzige, was die Aufzeichnung vom Verlauf zeigt, bis der Ansatz gefunden
 * ist: wie weit und in welcher Richtung. Der Pfeil zeigt relativ zur Laufrichtung (oben = weiter geradeaus),
 * solange diese bekannt ist, sonst nach Norden; die Himmelsrichtung steht zur Sicherheit dabei. Liegt als
 * Schicht über der Karte (über Maßstab und Quellenangabe) und fängt keine Berührungen ab.
 */
export function AnsatzHinweis({ hinweis, laufrichtung }: { hinweis: Hinweis; laufrichtung: number | null }) {
  const t = useT();
  return (
    <div className="pointer-events-none absolute inset-x-3 bottom-12 z-10 flex justify-center">
      <div
        role="status"
        className="flex min-w-0 items-center gap-3 rounded-xl border border-border bg-background/95 px-4 py-2.5 shadow-md"
      >
        <ArrowUp
          aria-hidden
          className="size-7 shrink-0 text-primary-text"
          style={{ transform: `rotate(${pfeilWinkel(hinweis.peilung, laufrichtung)}deg)` }}
        />
        <span className="min-w-0 text-base font-medium [overflow-wrap:anywhere]">
          {t("Ansatz in {meter} m", { meter: hinweis.meter })}
          <span className="ml-2 text-sm font-normal text-muted-foreground">{t(himmelsrichtung(hinweis.peilung))}</span>
        </span>
      </div>
    </div>
  );
}

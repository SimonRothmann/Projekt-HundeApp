"use client";

import { ChevronRight, TriangleAlert } from "lucide-react";

/**
 * Rotes Band oben auf der Hundeseite, wenn etwas Gesundheitliches dieses Hundes überfällig ist.
 * Stil der roten Gesundheitskarte der Startseite (GesundheitsHinweise); das Symbol und der Satz
 * tragen die Aussage, nicht die Farbe. Ein Tipp öffnet den Reiter "Gesundheit".
 */
export function GesundheitBanner({ text, onOeffnen }: { text: string; onOeffnen: () => void }) {
  return (
    <button
      type="button"
      onClick={onOeffnen}
      className="flex min-h-11 w-full min-w-0 items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-left text-sm font-medium transition-colors hover:bg-destructive/15 active:bg-destructive/20"
    >
      <TriangleAlert className="size-4 shrink-0 text-destructive" aria-hidden />
      <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{text}</span>
      <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
    </button>
  );
}

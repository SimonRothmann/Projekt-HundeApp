"use client";

import Link from "next/link";
import { CalendarClock, ChevronRight, Clock, TriangleAlert } from "lucide-react";
import type { Dog } from "@/lib/types";
import { hinweiseFuerHund, type Ton } from "@/lib/gesundheit";
import { heuteIso } from "@/lib/kalendertag";
import { useSprache, useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

const ZEILE =
  "flex min-h-11 min-w-0 items-center gap-2 px-3 py-2 text-sm font-medium transition-colors hover:bg-muted/60 active:bg-muted";

/**
 * Rahmen der Karte nach dem dringendsten Hinweis darin; das Symbol und der Satz selbst
 * ("abgelaufen", "fällig") tragen die Aussage, nicht die Farbe.
 */
const TON_KLASSEN: Record<Ton, string> = {
  rot: "border-destructive/40 bg-destructive/10",
  gelb: "border-warnung/40 bg-warnung/10",
  neutral: "border-surface-border bg-surface",
};

/**
 * Dezente Hinweise auf der Startseite, je Hund höchstens zwei Zeilen: etwas ist überfällig oder in
 * höchstens 14 Tagen fällig ("Bello: Tollwut gültig bis 03.11."), oder ein Tierarzttermin steht in
 * höchstens zwei Tagen an. Sonst bleibt die Startseite ruhig. Ein Tipp führt in den Bereich
 * "Gesundheit" des Hundes. Die Hunde bringen ihren Stand nur mit, wenn die Person Besitzer:in ist;
 * archivierte Hunde melden nichts mehr.
 */
export function GesundheitsHinweise({ hunde }: { hunde: Dog[] }) {
  const t = useT();
  const sprache = useSprache();
  const heute = heuteIso();

  const zeilen = hunde
    .filter((hund) => !hund.archivedAt)
    .flatMap((hund) =>
      hinweiseFuerHund(t, hund, heute, sprache).map((hinweis, i) => ({ schluessel: `${hund.id}-${i}`, hundId: hund.id, ...hinweis })),
    );
  if (zeilen.length === 0) return null;
  // Alle Hinweise in EINER Karte, je Hinweis eine Zeile: Einzelne Kästen
  // untereinander machten die Startseite unruhig.
  const ton: Ton = zeilen.some((z) => z.ton === "rot") ? "rot" : zeilen.some((z) => z.ton === "gelb") ? "gelb" : "neutral";

  return (
    <div className={cn("flex min-w-0 flex-col divide-y overflow-hidden rounded-lg border", TON_KLASSEN[ton])}>
      {zeilen.map((zeile) => {
        const Zeichen = zeile.ton === "rot" ? TriangleAlert : zeile.ton === "gelb" ? Clock : CalendarClock;
        return (
          <Link key={zeile.schluessel} href={`/dogs/${zeile.hundId}?tab=gesundheit`} className={ZEILE}>
            <Zeichen
              className={cn(
                "size-4 shrink-0",
                zeile.ton === "rot" && "text-destructive",
                zeile.ton === "gelb" && "text-warnung-text",
                zeile.ton === "neutral" && "text-primary-text",
              )}
              aria-hidden
            />
            <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{zeile.text}</span>
            <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
          </Link>
        );
      })}
    </div>
  );
}

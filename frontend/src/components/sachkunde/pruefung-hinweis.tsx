"use client";

import { useT } from "@/lib/i18n";
import { PUNKTE_JE_ANTWORT } from "@/lib/sachkunde-pruefung";

/**
 * Der Hinweis, dass die Übungsprüfung keine offizielle ist. Steht vor dem
 * Start und auf dem Ergebnis: Die Wertungsregel stammt von Vereinsseiten und
 * nicht aus den PDFs des Verbands - ohne diesen Satz hielte man "Übung
 * bestanden" leicht für ein Urteil über die echte Prüfung. Die Bestehensgrenze
 * kommt vom Server (passPercent), die Punkte je Antwort aus lib/sachkunde-pruefung.ts.
 */
export function PruefungHinweis({ prozent }: { prozent: number }) {
  const t = useT();
  return (
    <p className="rounded-md border border-border/60 bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
      {t(
        "Übungsprüfung – keine offizielle Prüfung. Gewertet nach der verbreiteten Regel: {punkte} Punkte je richtige Antwort, {punkte} Punkte Abzug je falsch angekreuzte, bestanden ab {prozent} %. Dein Verband oder Verein kann anders werten.",
        { punkte: PUNKTE_JE_ANTWORT, prozent },
      )}
    </p>
  );
}

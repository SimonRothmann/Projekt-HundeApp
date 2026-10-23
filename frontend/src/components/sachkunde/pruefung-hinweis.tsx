"use client";

import { useT } from "@/lib/i18n";

/**
 * Der Hinweis, dass die Übungsprüfung keine offizielle ist. Steht vor dem
 * Start und auf dem Ergebnis: Die Wertungsregel stammt von Vereinsseiten und
 * nicht aus den PDFs des Verbands - ohne diesen Satz hielte man "Übung
 * bestanden" leicht für ein Urteil über die echte Prüfung.
 */
export function PruefungHinweis() {
  const t = useT();
  return (
    <p className="rounded-md border border-border/60 bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
      {t(
        "Übungsprüfung – keine offizielle Prüfung. Gewertet nach der verbreiteten Regel: 2 Punkte je richtige Antwort, 2 Punkte Abzug je falsch angekreuzte, bestanden ab 70 %. Dein Verband oder Verein kann anders werten.",
      )}
    </p>
  );
}

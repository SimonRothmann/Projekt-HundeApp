"use client";

import { cn } from "@/lib/utils";

/**
 * Umschalter über die ganze Breite: gleich breite Segmente, am Telefon mit dem
 * Daumen gut zu treffen (Hundeseite: Plan / Tagebuch / Gesundheit, Gruppenseite:
 * Mitglieder / Anmeldungen). Die Auswahl gehört der Seite (sie steht in der
 * Adresse); hier wird nur angezeigt und gemeldet. min-w-0 + truncate, damit
 * lange Beschriftungen bei 375 px nichts aufspreizen.
 *
 * `tabId` liefert die Id jedes Reiters, auf die das Panel per aria-labelledby
 * verweist. `className` setzt die Spalten, wenn sie nicht gleich breit sein sollen.
 */
export function SegmentUmschalter<T extends string>({
  ariaLabel,
  wert,
  segmente,
  tabId,
  onChange,
  className,
}: {
  ariaLabel: string;
  wert: T;
  segmente: [T, string][];
  tabId: (wert: T) => string;
  onChange: (wert: T) => void;
  className?: string;
}) {
  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      className={cn("grid gap-1 rounded-xl bg-muted p-1", className ?? "grid-cols-2")}
    >
      {segmente.map(([segment, label]) => {
        const aktiv = wert === segment;
        return (
          <button
            key={segment}
            id={tabId(segment)}
            type="button"
            role="tab"
            aria-selected={aktiv}
            onClick={() => onChange(segment)}
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

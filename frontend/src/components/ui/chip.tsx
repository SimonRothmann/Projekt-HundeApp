"use client";

import type { ButtonHTMLAttributes } from "react";
import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Farbe und Rand einer Auswahl: gewählt kräftig im Primärton, sonst ein
 * schlichter Rand, der beim Darüberfahren aufleuchtet. Einzige Stelle für
 * dieses Aussehen - der Chip und die großen Antwortzeilen der Sachkunde
 * (die keine Pillen sind) nutzen sie gemeinsam.
 */
export function auswahlRahmen(gewaehlt: boolean): string {
  return gewaehlt
    ? "border-primary bg-primary/15"
    : "border-input hover:border-primary/50 hover:bg-accent/30";
}

/**
 * Der Auswahl-Chip: ein Knopf in Pillenform. Mit `gewaehlt` ist er ein
 * Schalter (aria-pressed), ohne ein einfacher Knopf. Lange Namen brechen um,
 * statt die Seite breiter zu machen.
 *
 * Die Antippfläche ist mindestens 44 px hoch - bei `kompakt` erst auf
 * Touch-Geräten (grobe Zeiger), für dichte Reihen und Filterleisten, die am
 * Rechner nicht so hoch sein müssen.
 *
 * Mit `haken` zeigt ein gewählter Chip zusätzlich ein Häkchen: für Auswahlen,
 * deren Zustand nicht nur an der Farbe hängen soll (farbsichere Darstellung).
 */
export function Chip({
  gewaehlt,
  kompakt,
  haken,
  className,
  children,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { gewaehlt?: boolean; kompakt?: boolean; haken?: boolean }) {
  return (
    <button
      type="button"
      aria-pressed={gewaehlt}
      className={cn(
        "inline-flex max-w-full min-w-0 items-center gap-1.5 rounded-full border py-1.5 text-left text-sm transition-colors [overflow-wrap:anywhere] disabled:opacity-50",
        kompakt ? "px-3 coarse:min-h-11" : "min-h-11 px-3.5",
        auswahlRahmen(!!gewaehlt),
        gewaehlt ? "text-primary-text" : "text-foreground",
        className,
      )}
      {...rest}
    >
      {haken && gewaehlt && <Check className="size-3.5 shrink-0" aria-hidden />}
      {children}
    </button>
  );
}

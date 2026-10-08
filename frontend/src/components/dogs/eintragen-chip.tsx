"use client";

import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

/**
 * Der Chip des Eintragen-Fensters: eine Antippfläche von mindestens 44 px Höhe
 * (nicht erst auf Touch-Geräten - das Fenster ist für den Hundeplatz gebaut).
 * Mit `gewaehlt` ist er ein Schalter (aria-pressed), ohne ein einfacher Knopf.
 * Lange Namen brechen um, statt die Seite breiter zu machen.
 */
export function Chip({
  gewaehlt,
  className,
  children,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { gewaehlt?: boolean }) {
  return (
    <button
      type="button"
      aria-pressed={gewaehlt}
      className={cn(
        "inline-flex min-h-11 max-w-full min-w-0 items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-left text-sm transition-colors [overflow-wrap:anywhere] disabled:opacity-50",
        gewaehlt
          ? "border-primary bg-primary/15 text-primary-text"
          : "border-input text-foreground hover:border-primary/50 hover:bg-accent/30",
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}

/** Kleine Überschrift eines Abschnitts im Fenster. */
export function Abschnittsname({ children, id }: { children: React.ReactNode; id?: string }) {
  return (
    <p id={id} className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
      {children}
    </p>
  );
}

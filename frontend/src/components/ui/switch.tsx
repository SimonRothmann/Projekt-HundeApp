"use client";

import { cn } from "@/lib/utils";

/**
 * Schalter in der Art der iOS-Einstellungen: ein Zeilenknopf mit Titel,
 * Erklärung und dem Schieber rechts.
 *
 * Die ganze Zeile ist das Ziel - ein 51 px breiter Schieber allein wäre
 * zwar groß genug, aber der Finger trifft ohnehin zuerst den Text. Als
 * Rolle "switch" statt eines Kontrollkästchens, damit Screenreader "an/aus"
 * ansagen und nicht "angehakt".
 */
export function SchalterZeile({
  an,
  disabled,
  onChange,
  titel,
  beschreibung,
}: {
  an: boolean;
  disabled?: boolean;
  onChange: (an: boolean) => void;
  titel: string;
  beschreibung?: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={an}
      disabled={disabled}
      onClick={() => onChange(!an)}
      className="flex min-h-[3.25rem] w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/50 active:bg-muted disabled:opacity-60"
    >
      <span className="min-w-0 flex-1">
        <span className="block font-medium [overflow-wrap:anywhere]">{titel}</span>
        {beschreibung && (
          <span className="block text-xs text-muted-foreground [overflow-wrap:anywhere]">{beschreibung}</span>
        )}
      </span>
      <span
        aria-hidden
        className={cn(
          "relative h-[1.9375rem] w-[3.1875rem] shrink-0 rounded-full transition-colors",
          an ? "bg-primary" : "bg-muted-foreground/35",
        )}
      >
        <span
          className={cn(
            "absolute top-0.5 left-0.5 size-[1.6875rem] rounded-full bg-white shadow transition-transform",
            an && "translate-x-[1.25rem]",
          )}
        />
      </span>
    </button>
  );
}

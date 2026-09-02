"use client";

import { DOG_CONDITION, type DogCondition } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * Verfassung des Hundes an diesem Trainingstag - ein Tipp, mehr nicht.
 *
 * Bewusst optional und ohne Vorauswahl: Ein Pflichtfeld mehr würde die Hürde
 * beim Eintragen wieder anheben, die zuletzt mühsam gesenkt wurde. Wer nichts
 * antippt, trägt eben nichts ein; die Auswertung rechnet solche Einheiten
 * heraus, statt sie als "ausgeglichen" zu zählen.
 *
 * Erneutes Antippen hebt die Auswahl auf - ein Fehlgriff soll nicht bedeuten,
 * dass man mit einer falschen Angabe leben muss.
 */

export const CONDITIONS: { key: DogCondition; label: string; hint: string }[] = [
  { key: DOG_CONDITION.Motivated, label: "motiviert", hint: "zieht mit, arbeitet freudig" },
  { key: DOG_CONDITION.Settled, label: "ausgeglichen", hint: "unauffällig, wie üblich" },
  { key: DOG_CONDITION.Distracted, label: "abgelenkt", hint: "bei der Umwelt statt beim Hundeführer" },
  { key: DOG_CONDITION.Tired, label: "müde", hint: "kraftlos, langsam, wenig Ausdauer" },
  { key: DOG_CONDITION.Stressed, label: "gestresst", hint: "überdreht, unruhig, kann nicht abschalten" },
];

export function conditionLabel(condition: DogCondition | null | undefined): string | null {
  return CONDITIONS.find((c) => c.key === condition)?.label ?? null;
}

export function ConditionPicker({
  value,
  onChange,
  disabled,
}: {
  value: DogCondition | null;
  onChange: (value: DogCondition | null) => void;
  disabled?: boolean;
}) {
  return (
    <div role="group" aria-label="Verfassung des Hundes" className="flex flex-wrap gap-1.5">
      {CONDITIONS.map((c) => {
        const aktiv = value === c.key;
        return (
          <button
            key={c.key}
            type="button"
            disabled={disabled}
            aria-pressed={aktiv}
            title={c.hint}
            onClick={() => onChange(aktiv ? null : c.key)}
            className={cn(
              "rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-10 disabled:opacity-50",
              aktiv
                ? "border-primary bg-primary/15 text-primary"
                : "border-border/60 text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
            )}
          >
            {c.label}
          </button>
        );
      })}
    </div>
  );
}

"use client";

import type { DogGender } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Zweierwahl "Rüde" | "Hündin", beide Seiten sichtbar.
 *
 * Bewusst ohne Vorbelegung (`wert` ist null, bis jemand tippt): Das Backend
 * kennt für das Geschlecht keine "keine Angabe" und nimmt bei Fehlen die 0 -
 * "Rüde". Ein Auswahlfeld, das schon auf Rüde steht, legte so jede Hündin
 * unbemerkt falsch an. Pflicht ist die Wahl deshalb im Formular, nicht hier.
 *
 * Zwei große Flächen statt eines Auswahlmenüs: Auf dem Handy ist das ein Tipp
 * statt zwei, und beide Möglichkeiten stehen im Blick.
 */
export function GeschlechtWahl({
  wert,
  onChange,
  labelId,
  ungueltig = false,
}: {
  wert: DogGender | null;
  onChange: (wert: DogGender) => void;
  /** Id des Beschriftungselements (Label oberhalb) für die Gruppe. */
  labelId: string;
  ungueltig?: boolean;
}) {
  const t = useT();
  const optionen: { wert: DogGender; text: string }[] = [
    { wert: 0, text: t("Rüde") },
    { wert: 1, text: t("Hündin") },
  ];

  return (
    <div role="group" aria-labelledby={labelId} className="grid grid-cols-2 gap-2">
      {optionen.map((option) => (
        <button
          key={option.wert}
          type="button"
          aria-pressed={wert === option.wert}
          onClick={() => onChange(option.wert)}
          className={cn(
            "min-h-12 min-w-0 rounded-xl border px-2 text-base font-medium transition-colors",
            wert === option.wert
              ? "border-primary bg-primary text-primary-foreground"
              : ungueltig
                ? "border-destructive text-foreground hover:bg-accent/30"
                : "border-input text-foreground hover:border-primary/50 hover:bg-accent/30",
          )}
        >
          {option.text}
        </button>
      ))}
    </div>
  );
}

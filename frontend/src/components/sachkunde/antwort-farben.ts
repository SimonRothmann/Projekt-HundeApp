import { auswahlRahmen } from "@/components/ui/chip";

/**
 * Farbe einer Antwort im Lernmodus und in der Zuordnung. Vor dem Auflösen ist
 * sie nur gewählt oder nicht. Danach: die richtige Antwort immer grün, eine
 * falsch gewählte rot, der Rest gedämpft - so sieht man in einem Blick, was
 * man verwechselt hat.
 */
export function antwortFarben({
  gewaehlt,
  aufgeloest,
  richtig,
  falsch,
}: {
  gewaehlt: boolean;
  aufgeloest: boolean;
  richtig: boolean;
  falsch: boolean;
}): string {
  if (!aufgeloest) return auswahlRahmen(gewaehlt);
  if (richtig) return "border-emerald-500/60 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300";
  if (falsch) return "border-destructive/60 bg-destructive/10 text-destructive";
  return "border-border/40 opacity-60";
}

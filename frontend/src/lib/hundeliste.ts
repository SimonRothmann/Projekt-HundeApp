import type { Dog } from "@/lib/types";

/**
 * Die Hundeliste, auch wenn sie nur einen Hund enthält. Ziel des Zurück-Knopfs
 * auf der Hundeseite, wenn der Reiter "Hunde" direkt dorthin geführt hat -
 * sonst führte "Zurück" auf /dogs und von dort gleich wieder auf den Hund.
 */
export const HUNDELISTE_IMMER = "/dogs?liste=1";

/**
 * Wohin der Reiter "Hunde" führt: bei genau einem aktiven Hund gleich auf
 * dessen Seite, sonst null (die Liste bleibt).
 *
 * Die meisten haben einen Hund - die Liste mit einer einzigen Karte war dann
 * nur ein Tipp mehr bei jedem Öffnen. Die Liste bleibt, wenn eine Einladung
 * zum Mitverwalten wartet (die steht nur dort), oder wenn sie ausdrücklich
 * verlangt ist (?liste=1, etwa über "Zurück" von der Hundeseite, um einen
 * zweiten Hund anzulegen oder Archivierte zu sehen).
 */
export function direktZumHund(
  dogs: readonly Dog[],
  { einladungen, listeGewuenscht }: { einladungen: number; listeGewuenscht: boolean },
): string | null {
  if (listeGewuenscht || einladungen > 0) return null;
  const aktive = dogs.filter((d) => !d.archivedAt);
  return aktive.length === 1 ? aktive[0].id : null;
}

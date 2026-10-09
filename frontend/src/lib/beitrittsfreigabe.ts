import type { Group } from "@/lib/types";

/**
 * Beitrittsfreigabe mit Gruppe (Trainer-Seite "Beitrittsanfragen") - als reine
 * Funktionen, damit die Vorauswahl ohne Browser prüfbar ist.
 */

/** Gruppen, in die die Trainer:in einladen darf: die sie verwaltet (myRelation 3 = Trainer:in). */
export function einladbareGruppen(gruppen: readonly Group[]): Group[] {
  return gruppen.filter((g) => g.myRelation === 3);
}

/**
 * Was beim Annehmen vorausgewählt ist: die einzige Gruppe, wenn es genau eine
 * gibt - sonst "Keine" (null). Bei mehreren soll die Trainer:in bewusst
 * wählen; eine geratene Gruppe wäre eine Einladung an die falsche Stelle.
 */
export function vorauswahlGruppe(gruppen: readonly { id: string }[]): string | null {
  return gruppen.length === 1 ? gruppen[0].id : null;
}

/**
 * Die Wahl zu einer Anfrage: eine ausdrücklich getroffene (auch "Keine" =
 * null) gewinnt, sonst gilt die Vorauswahl. Eine Wahl, die es nicht mehr gibt
 * (Gruppe zwischenzeitlich gelöscht), fällt auf "Keine" zurück - der Server
 * würde sie abweisen und die Anfrage bliebe liegen.
 */
export function gewaehlteGruppe(
  wahl: string | null | undefined,
  gruppen: readonly { id: string }[],
): string | null {
  if (wahl === undefined) return vorauswahlGruppe(gruppen);
  return wahl !== null && gruppen.some((g) => g.id === wahl) ? wahl : null;
}

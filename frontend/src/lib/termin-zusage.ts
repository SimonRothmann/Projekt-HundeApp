import type { GroupTrainingSession } from "@/lib/types";

type Zaehlung = Pick<GroupTrainingSession, "myResponse" | "attendingCount" | "decliningCount" | "openCount">;

/**
 * Der Stand eines Termins, wie er nach einer Antwort aussehen wird - für die
 * sofortige Anzeige, bevor der Server geantwortet hat.
 *
 * Die eigene Stimme wechselt nur die Spalte: Wer von "kommt" auf "kann nicht"
 * umschaltet, wandert aus den Zusagen in die Absagen, und die Zahl der offenen
 * ändert sich nicht. Wer zum ersten Mal antwortet, war vorher offen.
 *
 * Die Zahlen stimmen, weil nur aktive Mitglieder antworten dürfen und der
 * Server auch nur deren Antworten zählt (GroupTrainingScheduleService.MapAsync).
 * Die endgültigen Werte liefert ohnehin die Antwort des Servers nach.
 */
export function mitAntwort<T extends Zaehlung>(termin: T, kommt: boolean): T {
  if (termin.myResponse === kommt) return termin;

  let zusagen = termin.attendingCount;
  let absagen = termin.decliningCount;
  let offen = termin.openCount;
  if (termin.myResponse === true) zusagen -= 1;
  else if (termin.myResponse === false) absagen -= 1;
  else offen -= 1;
  if (kommt) zusagen += 1;
  else absagen += 1;

  return {
    ...termin,
    myResponse: kommt,
    attendingCount: Math.max(0, zusagen),
    decliningCount: Math.max(0, absagen),
    openCount: Math.max(0, offen),
  };
}

/** Ob man zu diesem Termin noch antworten kann: geplant und noch nicht begonnen. */
export function kannAntworten(termin: Pick<GroupTrainingSession, "status" | "startsAt">, jetzt: number = Date.now()): boolean {
  return termin.status === 0 && new Date(termin.startsAt).getTime() > jetzt;
}

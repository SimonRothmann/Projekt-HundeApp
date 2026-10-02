import type { TrainerOpenCounts } from "@/lib/types";

/** Ein Zähler unter "Zu erledigen" auf der Trainer-Übersicht. */
export type ZuErledigen = {
  art: "anfragen" | "bewerten";
  /** Wohin der Zähler führt. */
  href: string;
  anzahl: number;
};

/** Zahlen vom Server sind Zähler: nie negativ, nie gebrochen, bei Unsinn 0. */
const zaehler = (n: number | null | undefined) => (Number.isFinite(n) && (n as number) > 0 ? Math.floor(n as number) : 0);

/**
 * Beitrittsanfragen an Gruppen UND Vereine zusammen: Für die Trainer:in ist
 * es dieselbe Frage ("wer wartet auf mich?"), und beide liegen auf derselben
 * Seite.
 */
export function offeneAnfragen(counts: TrainerOpenCounts): number {
  return zaehler(counts.groupJoinRequests) + zaehler(counts.clubJoinRequests);
}

/**
 * Die Zähler, die "Zu erledigen" zeigt - nur die mit etwas darin. Ist alles
 * 0 (oder noch nichts geladen), ist die Liste leer und der Block entfällt:
 * eine Zeile voller Nullen wäre Lärm ganz oben.
 */
export function zuErledigen(counts: TrainerOpenCounts | null): ZuErledigen[] {
  if (!counts) return [];
  const liste: ZuErledigen[] = [
    { art: "anfragen", href: "/trainer/anfragen", anzahl: offeneAnfragen(counts) },
    { art: "bewerten", href: "/trainer/bewerten", anzahl: zaehler(counts.sessionsToRate) },
  ];
  return liste.filter((z) => z.anzahl > 0);
}

/** Offene Beitrittsanfragen je Gruppe, zum Nachschlagen in der Gruppenliste. */
export function anfragenJeGruppe(counts: TrainerOpenCounts | null): Record<string, number> {
  const nachGruppe: Record<string, number> = {};
  for (const g of counts?.groups ?? []) {
    const n = zaehler(g.joinRequests);
    if (n > 0) nachGruppe[g.groupId] = n;
  }
  return nachGruppe;
}

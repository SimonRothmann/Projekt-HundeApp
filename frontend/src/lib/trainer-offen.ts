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

/**
 * Die Zahlen für "Zu erledigen" auf der Startseite. Gruppenanfragen und
 * Trainings zu bewerten gehören zum Trainer-Bereich (Modul Gruppentraining,
 * `trainerBereichAn`); die Beitrittsanfragen des Vereins nicht: Wer Mitglieder
 * verwaltet, sieht sie auch ohne Trainer-Rolle und bei abgeschaltetem Modul.
 */
export function fuerStartseite(counts: TrainerOpenCounts | null, trainerBereichAn: boolean): TrainerOpenCounts | null {
  if (!counts || trainerBereichAn) return counts;
  return { ...counts, groupJoinRequests: 0, sessionsToRate: 0, groups: [] };
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

type Uebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/**
 * Beschriftung eines Zählers ("Beitrittsanfragen", "Training bewerten") -
 * gemeinsam für die Trainer-Übersicht und die Zeile "Zu erledigen" auf der
 * Startseite, damit beide dasselbe sagen.
 */
export function zaehlerBeschriftung(t: Uebersetzer, z: ZuErledigen): string {
  return z.art === "anfragen"
    ? z.anzahl === 1 ? t("Beitrittsanfrage") : t("Beitrittsanfragen")
    : z.anzahl === 1 ? t("Training bewerten") : t("Trainings bewerten");
}

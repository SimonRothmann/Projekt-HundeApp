import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Die Rollen einer Person in einem Verein - als Bitmenge, so wie der Server sie
 * liefert und erwartet (ClubRole im Backend; die Werte dürfen sich nie ändern).
 * Eine Person kann mehrere haben, die Rechte addieren sich.
 *
 * Was jemand DARF, entscheidet der Server und liefert es mit dem Verein mit
 * (canManage, canManageMembers, canAppointTrainers, isTrainer). Hier steht nur,
 * was die Oberfläche braucht, um Rollen zu zeigen und die Auswahl anzubieten,
 * die der Server dem Gegenüber auch erlaubt.
 */
export const ROLLE = {
  training: 1,
  verwaltung: 2,
  ausbildungswart: 4,
  mitgliederverwaltung: 8,
  vorstand: 16,
} as const;

export type Rollenbit = (typeof ROLLE)[keyof typeof ROLLE];

export type RollenInfo = {
  bit: Rollenbit;
  name: string;
  /** Ein Satz: was die Rolle darf. */
  erklaerung: string;
};

/** In der Reihenfolge, in der sie angezeigt werden: von der Alltagsrolle bis zur Leitung. */
export const ROLLEN: readonly RollenInfo[] = [
  {
    bit: ROLLE.training,
    name: uebersetzbar("Training"),
    erklaerung: uebersetzbar("Leitet die eigenen Gruppen, plant deren Termine und pflegt den Vereinskatalog."),
  },
  {
    bit: ROLLE.ausbildungswart,
    name: uebersetzbar("Ausbildungswart:in"),
    erklaerung: uebersetzbar("Verwaltet alle Gruppen und Termine des Vereins, beruft Trainer:innen und weist ihnen Gruppen zu."),
  },
  {
    bit: ROLLE.mitgliederverwaltung,
    name: uebersetzbar("Mitgliederverwaltung"),
    erklaerung: uebersetzbar("Entscheidet Beitrittsanfragen, lädt ein und führt Einladungslink und Mitgliederliste - ohne Trainerrechte."),
  },
  {
    bit: ROLLE.vorstand,
    name: uebersetzbar("Vorstand"),
    erklaerung: uebersetzbar("Darf lesen, ändert nichts. Die Vereinsübersicht für den Vorstand folgt."),
  },
  {
    bit: ROLLE.verwaltung,
    name: uebersetzbar("Verwaltung"),
    erklaerung: uebersetzbar("Darf alles im Verein: Stammdaten ändern, alle Rollen vergeben und entziehen."),
  },
];

export function hatRolle(rollen: number, bit: Rollenbit): boolean {
  return (rollen & bit) !== 0;
}

/** Die Rollen einer Menge in Anzeigereihenfolge. */
export function rollenVon(rollen: number): RollenInfo[] {
  return ROLLEN.filter((r) => hatRolle(rollen, r.bit));
}

/** Die Menge mit der Rolle (an = true) bzw. ohne sie. */
export function mitRolle(rollen: number, bit: Rollenbit, an: boolean): number {
  return an ? rollen | bit : rollen & ~bit;
}

type Rechte = { canManage?: boolean; canAppointTrainers?: boolean };

/**
 * Welche Rollen das Gegenüber vergeben und entziehen darf: die Verwaltung
 * (und der Admin) alle, eine Ausbildungswart:in nur Training - wer sonst
 * nichts darf, keine. Spiegelt die Regel des Servers, damit die Oberfläche
 * nichts anbietet, was dort abgewiesen würde; entschieden wird dort.
 */
export function aenderbareRollen(rechte: Rechte, istAdmin = false): Rollenbit[] {
  if (istAdmin || rechte.canManage) return ROLLEN.map((r) => r.bit);
  return rechte.canAppointTrainers ? [ROLLE.training] : [];
}

/**
 * Ob eine Person ganz abberufen werden darf: Wer alle Rollen ändern darf,
 * immer; sonst nur, wenn ihr kein anderes Recht als Training genommen würde.
 */
export function darfAbberufen(person: number, aenderbar: readonly Rollenbit[]): boolean {
  return person !== 0 && ROLLEN.every((r) => !hatRolle(person, r.bit) || aenderbar.includes(r.bit));
}

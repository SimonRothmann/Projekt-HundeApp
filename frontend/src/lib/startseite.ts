import type { Dog, Goal, GroupTrainingSession, OffenesFeedback, TrainingPlanItem } from "@/lib/types";
import { tageBisPruefung } from "@/lib/pruefung";
import { offeneWochenziele, wochenFortschritt } from "@/lib/trainingsplan";

/**
 * Was die Startseite aus ihren Daten auswählt - als reine Funktionen, damit
 * die Regeln ("welcher Termin steht oben?", "welches Feedback wartet?") ohne
 * Browser prüfbar sind.
 */

// ---- Gruppentermine ----

/** So weit im Voraus bekommt ein Termin eine eigene Karte; spätere stehen höchstens unter "Weitere Termine". */
export const TERMIN_TAGE_VORAUS = 7;

/** Höchstens so viele weitere Termine zeigt die einklappbare Liste (wie der frühere Block "Nächste Gruppentrainings"). */
export const MAX_WEITERE_TERMINE = 5;

const TAG_MS = 24 * 60 * 60 * 1000;

/**
 * Der Termin für die Karte und die Liste dahinter.
 *
 * `naechster` ist der erste nicht abgesagte Termin, der noch nicht begonnen hat
 * und innerhalb von {@link TERMIN_TAGE_VORAUS} Tagen liegt. Gibt es keinen,
 * bleibt die Startseite ganz ohne Terminkarte: Viele Vereine regeln Termine
 * anders, eine leere oder werbende Karte wäre dort nur Platzverschwendung -
 * und `weitere` bleibt dann leer, es hängt an der Karte.
 *
 * `weitere` sind die übrigen kommenden Termine (abgesagte eingeschlossen, die
 * Liste kennzeichnet sie), in zeitlicher Reihenfolge und gedeckelt.
 */
export function terminUebersicht(
  termine: readonly GroupTrainingSession[],
  jetzt: number = Date.now(),
): { naechster: GroupTrainingSession | null; weitere: GroupTrainingSession[] } {
  const kommende = termine
    .filter((t) => new Date(t.startsAt).getTime() > jetzt)
    .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime());

  const grenze = jetzt + TERMIN_TAGE_VORAUS * TAG_MS;
  const naechster = kommende.find((t) => t.status === 0 && new Date(t.startsAt).getTime() <= grenze) ?? null;
  if (!naechster) return { naechster: null, weitere: [] };

  return { naechster, weitere: kommende.filter((t) => t !== naechster).slice(0, MAX_WEITERE_TERMINE) };
}

// ---- Trainer-Feedback ----

/**
 * Das neueste unbeantwortete Feedback und die Zahl der übrigen.
 *
 * Der Server liefert die Liste schon sortiert und gefiltert; sortiert wird
 * hier trotzdem noch einmal (neuestes zuerst, ohne Zeitpunkt zuletzt), weil
 * die Karte nicht davon abhängen soll, was ein zwischengespeicherter älterer
 * Stand mitbringt. Ohne Text gibt es nichts zu beantworten.
 */
export function feedbackKarte(liste: readonly OffenesFeedback[] | null | undefined): {
  aktuell: OffenesFeedback | null;
  weitere: OffenesFeedback[];
} {
  const offen = (liste ?? [])
    .filter((f) => f.feedback.trim() !== "")
    .sort((a, b) => (b.feedbackAt ? Date.parse(b.feedbackAt) : -Infinity) - (a.feedbackAt ? Date.parse(a.feedbackAt) : -Infinity));
  return { aktuell: offen[0] ?? null, weitere: offen.slice(1) };
}

/** Ab so vielen Zeichen bietet die Karte "mehr" an (drei Zeilen bei 390 px sind etwa 120 Zeichen). */
const FEEDBACK_KURZ = 120;

/** Ob ein Feedback mehr als die drei Vorschauzeilen füllen kann - dann gehört ein "mehr" dazu. */
export function feedbackKuerzbar(text: string): boolean {
  return text.length > FEEDBACK_KURZ || (text.match(/\n/g)?.length ?? 0) >= 3;
}

// ---- Ziele ----

export type ZielKartenEintrag = {
  hund: Dog;
  goal: Goal;
  /** Tage bis zur Prüfung (0 = heute); nie negativ, denn abgelaufene Ziele bekommen keine Karte. */
  tage: number;
  /** Stand der laufenden Woche; null ohne geplante Übung (Pause, leerer Plan). */
  fortschritt: { woche: number; geplant: number; erledigt: number } | null;
  /** Die noch offenen Übungen der laufenden Woche; leer, wenn alles erledigt ist. */
  offen: TrainingPlanItem[];
  /** Wie viele weitere laufende Ziele der Hund hat (die Karte zeigt nur das nächste). */
  weitereZiele: number;
  /** Ob es eine Begleithundeprüfung ist - dann gehört "Sachkunde üben" in die Karte. */
  bh: boolean;
};

export type AbgelaufenesZiel = { hund: Dog; goal: Goal };

/**
 * Die Hunde der Startseite, aufgeteilt nach ihren aktiven Zielen:
 *
 * - `karten`: je Hund EINE Karte, für das Ziel mit dem nächsten noch nicht
 *   verstrichenen Datum. Hat ein Hund mehrere, weist die Karte auf den
 *   Rest hin (`weitereZiele`), die Pläne stehen auf der Hundeseite.
 * - `abgelaufen`: aktive Ziele, deren Prüfungstag vorbei ist - sie warten auf
 *   ihr Ergebnis, auch mehrere je Hund, ältestes zuerst.
 * - `ohneZiel`: Hunde ganz ohne aktives Ziel. Wer nur ein abgelaufenes hat,
 *   gehört nicht dazu - er hat ein Ziel, nur noch kein Ergebnis.
 *
 * Archivierte Hunde zählen nicht (das Backend liefert sie ohnehin nicht, die
 * Regel gilt hier trotzdem).
 */
export function zielAufteilung(
  eintraege: readonly { dog: Dog; activeGoals: Goal[] }[],
  bhSportIds: ReadonlySet<string>,
  jetzt: number = Date.now(),
): { karten: ZielKartenEintrag[]; abgelaufen: AbgelaufenesZiel[]; ohneZiel: Dog[] } {
  const karten: ZielKartenEintrag[] = [];
  const abgelaufen: AbgelaufenesZiel[] = [];
  const ohneZiel: Dog[] = [];

  for (const { dog, activeGoals } of eintraege) {
    if (dog.archivedAt) continue;
    const aktive = activeGoals.filter((g) => g.status === 0);
    if (aktive.length === 0) {
      ohneZiel.push(dog);
      continue;
    }

    const laufende = aktive.filter((g) => tageBisPruefung(g.targetDate, jetzt) >= 0);
    const naechstes = laufende.reduce<Goal | null>((bestes, g) => (bestes === null || g.targetDate < bestes.targetDate ? g : bestes), null);
    if (naechstes) {
      karten.push({
        hund: dog,
        goal: naechstes,
        tage: tageBisPruefung(naechstes.targetDate, jetzt),
        fortschritt: wochenFortschritt(naechstes, jetzt),
        offen: offeneWochenziele(naechstes, jetzt)?.items ?? [],
        weitereZiele: laufende.length - 1,
        bh: bhSportIds.has(naechstes.sportId),
      });
    }

    abgelaufen.push(
      ...aktive
        .filter((g) => tageBisPruefung(g.targetDate, jetzt) < 0)
        .sort((a, b) => a.targetDate.localeCompare(b.targetDate))
        .map((goal) => ({ hund: dog, goal })),
    );
  }

  return { karten, abgelaufen, ohneZiel };
}

/**
 * Ob die kleine Zeile "Sachkunde üben" am Ende der Startseite steht.
 *
 * Die Sachkunde hat keinen anderen Platz in der Handy-Navigation - sie bleibt
 * über diese Zeile erreichbar. Zeigt schon eine BH-Karte den Link, entfällt
 * die Zeile, damit er nicht zweimal auf dem Bildschirm steht.
 */
export function zeigeSachkundeZeile(karten: readonly Pick<ZielKartenEintrag, "bh">[], sachkundeAn: boolean): boolean {
  return sachkundeAn && !karten.some((k) => k.bh);
}

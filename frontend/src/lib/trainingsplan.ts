import type { Goal, PauseZeitraum, TrainingPlanItem } from "@/lib/types";
import type { Sprache } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { tageBisPruefung } from "@/lib/pruefung";
import { heuteIso, tageBis, vereinszeitTag } from "@/lib/kalendertag";
import { pausentageZwischen } from "@/lib/pausen";

/**
 * Welche Wochen eines Trainingsplans die Hundeseite zeigt.
 *
 * Der Plan stand mit allen Wochen untereinander über dem Tagebuch - bei einem
 * Zwölf-Wochen-Plan sind das zwölf Zeilen, mal Anzahl der Ziele, bevor man
 * überhaupt beim Tagebuch ankommt. Gebraucht wird beim Öffnen der Seite aber
 * genau eine: die laufende.
 *
 * Zwei Ausnahmen, in denen nichts verborgen wird: wenn sich die laufende Woche
 * nicht bestimmen lässt (abgeschlossener Plan), und wenn es ohnehin nur zwei
 * Wochen gibt - dann kostet das Ausblenden mehr Aufmerksamkeit, als es Platz
 * spart.
 */
export const OHNE_VERKUERZUNG_BIS = 2;

export function sichtbareWochen<T>(
  wochen: [number, T][],
  // null wie undefined heißt "keine laufende Woche" - der Aufrufer bestimmt
  // sie aus den Wochennummern und kommt dabei ohne Treffer aus.
  aktuelleWoche: number | null | undefined,
  alleZeigen: boolean,
): [number, T][] {
  if (alleZeigen || aktuelleWoche == null || wochen.length <= OHNE_VERKUERZUNG_BIS) return wochen;
  return wochen.filter(([nummer]) => nummer === aktuelleWoche);
}

/**
 * Entfernt Pausen-Platzhalter aus Wochen, die echte Übungen haben.
 *
 * Eine Pausenwoche besteht aus genau einem Platzhalter ohne Übung und mit
 * Zielwert 0. Neben echten Übungen derselben Woche ist er ein Altbestand (die
 * Neugenerierung ließ ihn früher stehen) und erschien als namenlose Zeile
 * "0/0x erledigt", die auch in "0/6" statt "0/5" mitgezählt wurde. Der Server
 * gibt ihn inzwischen nicht mehr aus - der Lesecache des Geräts kann ihn aber
 * noch tragen.
 */
export function ohneUeberfluessigePause(items: TrainingPlanItem[]): TrainingPlanItem[] {
  const wochenMitUebung = new Set(items.filter((item) => !item.isRestWeek).map((item) => item.weekNumber));
  return items.filter((item) => !item.isRestWeek || !wochenMitUebung.has(item.weekNumber));
}

/** Ob eine Woche (nach {@link groupByWeek}) nur aus der Pause besteht. */
export function istPausenwoche(items: TrainingPlanItem[]): boolean {
  return items.length > 0 && items.every((item) => item.isRestWeek);
}

// Eine Woche kann mehrere Plan-Ziele haben (siehe TrainingPlanGenerator
// "ItemsPerWeek") - für die Anzeige nach Wochennummer gruppiert.
export function groupByWeek(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byWeek = new Map<number, TrainingPlanItem[]>();
  for (const item of ohneUeberfluessigePause(items)) {
    const group = byWeek.get(item.weekNumber);
    if (group) group.push(item);
    else byWeek.set(item.weekNumber, [item]);
  }
  return [...byWeek.entries()];
}

/**
 * Die Planwoche (ab 1) an einem Kalendertag - dieselbe Regel wie im Server
 * (Trainingspausen.Planwoche): ganze Kalendertage seit dem Planbeginn, ohne die Tage, an denen der
 * Hund pausiert, durch 7, abgerundet. Vor dem Planbeginn die erste.
 */
function planwocheAmTag(planBeginn: string, tag: string, pausen: readonly PauseZeitraum[] | undefined): number {
  const vergangen = tageBis(tag, planBeginn);
  if (vergangen <= 0) return 1;
  const aktiv = vergangen - pausentageZwischen(pausen ?? [], planBeginn, tag, tag);
  return Math.floor(Math.max(0, aktiv) / 7) + 1;
}

// Bestimmt die Trainingswoche an einem Kalendertag: Woche 1 beginnt mit dem Tag der
// Plan-Erstellung (generatedAt, als Tag in Vereinszeit), jede weitere angebrochene 7-Tage-Woche
// zählt eins hoch. Ergebnis wird auf die tatsächlich vorhandenen Wochennummern begrenzt (vor
// Planstart -> erste Woche, nach Planende -> letzte Woche). Fällt auf die erste Woche zurück,
// wenn kein/ungültiges Startdatum vorliegt.
//
// Trainingspausen halten den Plan an: Die Tage, an denen der Hund pausiert, zählen nicht mit
// (dieselbe Regel wie im Server, Trainingspausen.Planwoche; siehe docs/SMART_TRAINING_PLAN.md,
// "Pausen"). Während einer Pause steht die Woche still, danach geht es dort weiter, wo der Plan
// stand.
function planwocheAmKalendertag(
  weeks: [number, TrainingPlanItem[]][],
  generatedAt: string | undefined,
  tag: string,
  pausen?: readonly PauseZeitraum[],
): number | undefined {
  if (weeks.length === 0) return undefined;
  const weekNumbers = weeks.map(([n]) => n);
  const minWeek = Math.min(...weekNumbers);
  const maxWeek = Math.max(...weekNumbers);

  const start = generatedAt ? Date.parse(generatedAt) : NaN;
  if (Number.isNaN(start)) return weekNumbers[0];

  const byDate = planwocheAmTag(vereinszeitTag(start), tag, pausen);
  const clamped = Math.min(Math.max(byDate, minWeek), maxWeek);
  // Bei (seltenen) Lücken die nächste vorhandene Wochennummer wählen.
  return weekNumbers.includes(clamped)
    ? clamped
    : (weekNumbers.filter((n) => n >= clamped).sort((a, b) => a - b)[0] ?? maxWeek);
}

// Die laufende Woche: die Planwoche am heutigen Kalendertag (Vereinszeit, wie im Server).
// Aus GoalPlanCard hierher gezogen, weil die Startseite ("Diese Woche") dieselbe Woche meinen
// muss wie der Plan auf der Hundeseite.
export function computeCurrentWeek(
  weeks: [number, TrainingPlanItem[]][],
  generatedAt: string | undefined,
  jetzt: number = Date.now(),
  pausen?: readonly PauseZeitraum[],
): number | undefined {
  return planwocheAmKalendertag(weeks, generatedAt, vereinszeitTag(jetzt), pausen);
}

/**
 * Die Übungen der laufenden Woche eines aktiven Ziels (ohne Pausen-Platzhalter).
 *
 * Die eine Stelle, die "laufende Woche" für die Startseite bestimmt: "Diese
 * Woche" (offene Übungen) und die Zielkarte (Fortschritt) rechnen beide damit
 * und können deshalb nicht auseinanderlaufen. Ist das Zieldatum vorbei, gibt
 * es keine laufende Woche mehr (null).
 */
function uebungenDerLaufendenWoche(
  goal: Goal,
  jetzt: number,
): { woche: number; wochen: [number, TrainingPlanItem[]][]; uebungen: TrainingPlanItem[] } | null {
  if (goal.status !== 0 || !goal.trainingPlan) return null;
  // Nach dem Zieldatum läuft keine Woche mehr: computeCurrentWeek hielte sonst
  // die letzte Planwoche fest, und die Startseite zeigte monatelang "Diese
  // Woche: 0 von 3" für eine Prüfung, die längst vorbei ist. Der Prüfungstag
  // selbst zählt noch (tageBisPruefung == 0); gerechnet wird mit dem
  // Kalendertag des Geräts, wie überall bei Prüfungsdaten.
  if (tageBisPruefung(goal.targetDate, jetzt) < 0) return null;
  const wochen = groupByWeek(goal.trainingPlan.items);
  const woche = computeCurrentWeek(wochen, goal.trainingPlan.generatedAt, jetzt, goal.planPausen);
  if (woche == null) return null;
  const uebungen = (wochen.find(([nummer]) => nummer === woche)?.[1] ?? []).filter((item) => !item.isRestWeek);
  return { woche, wochen, uebungen };
}

/**
 * Ob ein Trainingsdatum zählen darf: ein gültiger Kalendertag, der nicht in der Zukunft liegt.
 *
 * Das Formular belegt das Datum mit dem UTC-Tag vor, deshalb zählen beide Kalendertage als heute.
 * Ein Datum in der Zukunft zählt noch nicht, auch wenn es in dieselbe Planwoche fällt; das deckt
 * nebenbei Tage nach dem Zieldatum ab, die die letzte Planwoche sonst zuschlüge.
 */
function istZaehlbaresDatum(datum: string, jetzt: number): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(datum)) return false;
  const heute = heuteIso(jetzt);
  const utcHeute = new Date(jetzt).toISOString().slice(0, 10);
  return datum <= (heute > utcHeute ? heute : utcHeute);
}

/**
 * Ob der Kalendertag in der laufenden Planwoche des Ziels liegt - die Prüfung,
 * die ein Training erfüllen muss, um für die Woche zu zählen.
 *
 * Vor dem Planstart gibt es noch keine Woche; die Planwoche gäbe dort die
 * erste zurück und ein Training von vor dem Plan würde dieser zugerechnet.
 *
 * Die Planwoche rechnet in Vereinszeit (wie der Server), das Trainingsdatum und "heute"
 * (istZaehlbaresDatum) mit dem Gerätetag. Außerhalb von Mitteleuropa kann ein Training "von
 * heute" deshalb rund um den Wochenwechsel der Nachbarwoche zugerechnet werden; das gilt auch
 * für passendesPlanItem und gewaehltesPlanItem und ist so gewollt (Serverregel zählt).
 */
function tagInLaufenderWoche(
  goal: Goal,
  laufend: { woche: number; wochen: [number, TrainingPlanItem[]][] },
  tag: string,
): boolean {
  const generatedAt = goal.trainingPlan?.generatedAt;
  const start = generatedAt ? Date.parse(generatedAt) : NaN;
  if (!Number.isNaN(start) && tag < vereinszeitTag(start)) return false;
  return planwocheAmKalendertag(laufend.wochen, generatedAt, tag, goal.planPausen) === laufend.woche;
}

/** Die Antwort des Servers, wenn ein Plan-Ziel nicht (mehr) existiert (TrainingService). */
const PLAN_ZIEL_FEHLT = "Ein oder mehrere Plan-Ziele wurden nicht gefunden.";

export function istPlanZielFehler(err: unknown): boolean {
  if (!(err instanceof Error)) return false;
  // ApiError.message ist für die Anzeige in die Oberflächensprache übersetzt
  // (lib/api.ts) - verglichen wird deshalb mit den Originaltexten des Servers.
  // Dieselbe Form wie dort, ohne ApiError zu importieren (offline-queue und
  // api würden sich sonst im Kreis importieren).
  const original = (err as { errors?: unknown }).errors;
  if (Array.isArray(original)) return original.some((e) => typeof e === "string" && e.includes(PLAN_ZIEL_FEHLT));
  return err.message.includes(PLAN_ZIEL_FEHLT);
}

/**
 * Das Training ohne Plan-Verknüpfungen - oder null, wenn es keine hat.
 *
 * Die Verknüpfung setzt das Formular von selbst, aus den zuletzt geladenen
 * Zielen. Wurde der Plan inzwischen woanders neu erzeugt, gibt es das Ziel
 * nicht mehr, und der Server lehnt das ganze Training ab - ein Training, das
 * man nie bewusst verknüpft hat, darf deshalb nicht verloren gehen. Dann lieber
 * einmal ohne Plan-Zählung speichern.
 */
export function ohnePlanVerknuepfung(body: unknown): unknown | null {
  if (typeof body !== "object" || body === null) return null;
  const exercises = (body as { exercises?: unknown }).exercises;
  if (!Array.isArray(exercises)) return null;
  const verknuepft = exercises.some(
    (e) => typeof e === "object" && e !== null && (e as { trainingPlanItemId?: unknown }).trainingPlanItemId,
  );
  if (!verknuepft) return null;
  return {
    ...(body as object),
    exercises: exercises.map((e) =>
      typeof e === "object" && e !== null ? { ...(e as object), trainingPlanItemId: null } : e,
    ),
  };
}

/**
 * Das offene Plan-Ziel, das ein Training dieser Übung an diesem Tag zählen
 * soll - damit niemand beim Eintragen von Hand "Plan-Ziel" wählen muss.
 *
 * Gesucht wird nur dort, wo die Zuordnung eindeutig und gewollt ist:
 * - dieselbe Katalog-Übung (Freitext-Übungen haben keine Kennung, an der man
 *   sie sicher wiedererkennt - dort verknüpft nur, wer es selbst wählt),
 * - im aktiven, noch nicht abgelaufenen Ziel (dieselbe Regel wie bei
 *   "Diese Woche" auf der Startseite),
 * - in der laufenden Woche, und das Trainingsdatum liegt in ihr. Wer ein altes
 *   Training nachträgt, würde sonst die Wochenzahl von heute aufbessern.
 * - noch nicht erfüllt. Eine erfüllte Übung weiter zu verknüpfen hieße, das
 *   Wochenziel zu überfüllen, ohne dass die Karte das je anzeigt.
 *
 * Hat der Hund mehrere passende Ziele, gewinnt das mit dem früheren
 * Zieldatum - dessen Prüfung steht zuerst an.
 *
 * vergeben: wie viele Einträge für ein Plan-Ziel derselben Eingabe schon
 * unterwegs sind (Schlüssel: Item-Id). Zwei Zeilen mit derselben Übung dürfen
 * ein einmal offenes Ziel nicht beide füllen.
 */
export function passendesPlanItem(
  goals: readonly Goal[] | null | undefined,
  exerciseId: string | null | undefined,
  datum: string,
  jetzt: number = Date.now(),
  vergeben?: ReadonlyMap<string, number>,
): TrainingPlanItem | null {
  if (!exerciseId) return null;
  // Ein Training ist etwas Geschehenes (siehe istZaehlbaresDatum).
  if (!istZaehlbaresDatum(datum, jetzt)) return null;

  let bestes: { item: TrainingPlanItem; zieldatum: string } | null = null;
  for (const goal of goals ?? []) {
    const laufend = uebungenDerLaufendenWoche(goal, jetzt);
    if (!laufend || !goal.trainingPlan) continue;

    if (!tagInLaufenderWoche(goal, laufend, datum)) continue;

    const item = laufend.uebungen.find(
      (kandidat) =>
        kandidat.exerciseId === exerciseId &&
        kandidat.completedCount + (vergeben?.get(kandidat.id) ?? 0) < kandidat.repetitionsTarget,
    );
    if (item && (bestes === null || goal.targetDate < bestes.zieldatum)) {
      bestes = { item, zieldatum: goal.targetDate };
    }
  }
  return bestes?.item ?? null;
}

/**
 * Die ausdrücklich gewählte Planübung (Chip, Planzeile), soweit sie für das
 * Trainingsdatum zählt - dieselben Regeln wie bei passendesPlanItem: laufende
 * Woche, nicht vor dem Planstart, nicht in der Zukunft. Wer erst die Übung
 * antippt und dann "Gestern" wählt, trägt sonst ein Training der Vorwoche in
 * die laufende ein.
 *
 * Erfüllte Übungen bleiben wählbar (der Nutzer wollte sie ausdrücklich); nur
 * das Datum entscheidet. Ist die Übung in den geladenen Zielen nicht zu finden:
 * gibt es Ziele, wurde der Plan inzwischen ersetzt (null); gibt es noch keine
 * (nicht geladen), gilt die Wahl nur für heute, weil sich das Datum nicht
 * prüfen lässt.
 */
export function gewaehltesPlanItem(
  goals: readonly Goal[] | null | undefined,
  gewaehlt: TrainingPlanItem,
  datum: string,
  jetzt: number = Date.now(),
): TrainingPlanItem | null {
  if (!istZaehlbaresDatum(datum, jetzt)) return null;
  for (const goal of goals ?? []) {
    const laufend = uebungenDerLaufendenWoche(goal, jetzt);
    const item = laufend?.uebungen.find((kandidat) => kandidat.id === gewaehlt.id);
    if (laufend && item) return tagInLaufenderWoche(goal, laufend, datum) ? item : null;
  }
  return (goals ?? []).length === 0 && datum === heuteIso(jetzt) ? gewaehlt : null;
}

/**
 * Die noch offenen Wochenziele der laufenden Woche - für "Diese Woche" auf
 * der Startseite.
 *
 * Nur aktive Ziele, keine Pausenwoche, nichts schon Erledigtes. null statt
 * einer leeren Liste, wenn es nichts zu tun gibt: dann soll das Ziel auf der
 * Startseite gar nicht erst auftauchen.
 */
export function offeneWochenziele(
  goal: Goal,
  jetzt: number = Date.now(),
): { woche: number; items: TrainingPlanItem[] } | null {
  const laufend = uebungenDerLaufendenWoche(goal, jetzt);
  if (!laufend) return null;
  const items = laufend.uebungen.filter((item) => !item.isComplete);
  return items.length > 0 ? { woche: laufend.woche, items } : null;
}

/**
 * Wie viele der geplanten Übungen der laufenden Woche schon erledigt sind - für
 * die Zielkarte auf der Startseite. null ohne aktiven Plan und in Wochen ohne
 * geplante Übung (Pause, leerer individueller Plan): dann gibt es nichts zu
 * zeigen.
 */
export function wochenFortschritt(
  goal: Goal,
  jetzt: number = Date.now(),
): { woche: number; geplant: number; erledigt: number } | null {
  const laufend = uebungenDerLaufendenWoche(goal, jetzt);
  if (!laufend || laufend.uebungen.length === 0) return null;
  return {
    woche: laufend.woche,
    geplant: laufend.uebungen.length,
    erledigt: laufend.uebungen.filter((item) => item.isComplete).length,
  };
}

/**
 * Wie viele Übungen einer Woche erledigt sind - für "Woche 9 · 1/4" und den
 * Fortschrittsbalken der Plankarte. Eine Pausenwoche hat nichts zu erledigen
 * (geplant 0), damit die Karte dort "Pause" statt "0/0" schreibt.
 */
export function fortschrittDerWoche(items: TrainingPlanItem[]): { geplant: number; erledigt: number } {
  const uebungen = items.filter((item) => !item.isRestWeek);
  return { geplant: uebungen.length, erledigt: uebungen.filter((item) => item.isComplete).length };
}

/**
 * Der Anzeigename einer Planübung: Katalog-Übung, sonst Freitext, sonst null
 * (Übung nicht mehr im Katalog). Mit Sprache auf Englisch der englische
 * Katalogname, falls es einen gibt - Freitext bleibt, wie er eingegeben wurde.
 */
export function planItemName(
  item: Pick<TrainingPlanItem, "exerciseName" | "freeTextLabel"> & Partial<Pick<TrainingPlanItem, "exerciseNameEn">>,
  sprache: Sprache = "de",
): string | null {
  if (item.exerciseName != null) return katalogName({ name: item.exerciseName, nameEn: item.exerciseNameEn }, sprache);
  return item.freeTextLabel ?? null;
}

/**
 * Effektive Trainingstage einer Woche: Überschreibung für genau diese Woche,
 * sonst der Plan-Standard. Bestimmt, wie viele Tage beim Hinzufügen oder
 * Bearbeiten einer Übung wählbar sind.
 *
 * weekConfigs darf fehlen: Der Lesecache des Geräts (IndexedDB) kann beim
 * Öffnen zuerst ältere Ziel-Daten ohne dieses (neuere) Feld liefern - ohne
 * den Schutz stürzte die Seite beim Zugriff ab.
 */
export function trainingstageDerWoche(
  goal: Pick<Goal, "weekConfigs" | "trainingDaysPerWeek">,
  woche: number,
): number {
  return (goal.weekConfigs ?? []).find((w) => w.weekNumber === woche)?.trainingDaysPerWeek ?? goal.trainingDaysPerWeek;
}

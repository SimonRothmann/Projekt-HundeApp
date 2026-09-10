import type { Goal, TrainingPlanItem } from "@/lib/types";

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

// Eine Woche kann mehrere Plan-Ziele haben (siehe TrainingPlanGenerator
// "ItemsPerWeek") - für die Anzeige nach Wochennummer gruppiert.
export function groupByWeek(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byWeek = new Map<number, TrainingPlanItem[]>();
  for (const item of items) {
    const group = byWeek.get(item.weekNumber);
    if (group) group.push(item);
    else byWeek.set(item.weekNumber, [item]);
  }
  return [...byWeek.entries()];
}

// Bestimmt die aktuelle Trainingswoche kalendarisch: Woche 1 startet mit der
// Plan-Erstellung (generatedAt), jede weitere angebrochene 7-Tage-Woche zählt
// eins hoch. Ergebnis wird auf die tatsächlich vorhandenen Wochennummern
// begrenzt (vor Planstart -> erste Woche, nach Planende -> letzte Woche).
// Fällt auf die erste Woche zurück, wenn kein/ungültiges Startdatum vorliegt.
// Aus GoalPlanCard hierher gezogen, weil die Startseite ("Diese Woche")
// dieselbe Woche meinen muss wie der Plan auf der Hundeseite.
export function computeCurrentWeek(
  weeks: [number, TrainingPlanItem[]][],
  generatedAt: string | undefined,
  jetzt: number = Date.now(),
): number | undefined {
  if (weeks.length === 0) return undefined;
  const weekNumbers = weeks.map(([n]) => n);
  const minWeek = Math.min(...weekNumbers);
  const maxWeek = Math.max(...weekNumbers);

  const start = generatedAt ? new Date(generatedAt).getTime() : NaN;
  if (Number.isNaN(start)) return weekNumbers[0];

  const weekMs = 7 * 24 * 60 * 60 * 1000;
  const byDate = Math.floor((jetzt - start) / weekMs) + 1;
  const clamped = Math.min(Math.max(byDate, minWeek), maxWeek);
  // Bei (seltenen) Lücken die nächste vorhandene Wochennummer wählen.
  return weekNumbers.includes(clamped)
    ? clamped
    : (weekNumbers.filter((n) => n >= clamped).sort((a, b) => a - b)[0] ?? maxWeek);
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
  if (goal.status !== 0 || !goal.trainingPlan) return null;
  const wochen = groupByWeek(goal.trainingPlan.items);
  const woche = computeCurrentWeek(wochen, goal.trainingPlan.generatedAt, jetzt);
  if (woche == null) return null;
  const items = (wochen.find(([nummer]) => nummer === woche)?.[1] ?? []).filter(
    (item) => !item.isRestWeek && !item.isComplete,
  );
  return items.length > 0 ? { woche, items } : null;
}

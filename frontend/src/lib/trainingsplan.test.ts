import { describe, expect, it } from "vitest";
import { fortschrittDerWoche, planItemName, sichtbareWochen, trainingstageDerWoche } from "./trainingsplan";
import type { TrainingPlanItem } from "./types";

const wochen: [number, string][] = [
  [1, "a"],
  [2, "b"],
  [3, "c"],
  [4, "d"],
];

describe("sichtbareWochen", () => {
  it("zeigt nur die laufende Woche", () => {
    expect(sichtbareWochen(wochen, 3, false)).toEqual([[3, "c"]]);
  });

  it("zeigt alle Wochen, sobald man es verlangt", () => {
    expect(sichtbareWochen(wochen, 3, true)).toEqual(wochen);
  });

  it("verbirgt nichts, wenn sich die laufende Woche nicht bestimmen lässt", () => {
    // Abgeschlossener Plan: dann ist keine Woche die richtige, und eine
    // willkürlich gewählte wäre schlechter als die vollständige Liste.
    expect(sichtbareWochen(wochen, null, false)).toEqual(wochen);
    // computeCurrentWeek liefert undefined statt null, wenn es keine Wochen
    // gibt - beides muss dasselbe bedeuten.
    expect(sichtbareWochen(wochen, undefined, false)).toEqual(wochen);
  });

  it("verbirgt nichts bei zwei Wochen - das spart keinen Platz, kostet aber einen Knopf", () => {
    const kurz: [number, string][] = [
      [1, "a"],
      [2, "b"],
    ];

    expect(sichtbareWochen(kurz, 1, false)).toEqual(kurz);
  });
});

function uebung(teil: Partial<TrainingPlanItem>): TrainingPlanItem {
  return {
    id: "i",
    weekNumber: 1,
    exerciseId: "e",
    exerciseName: "Sitz",
    freeTextLabel: null,
    repetitionsTarget: 2,
    isRestWeek: false,
    completedCount: 0,
    isComplete: false,
    logs: [],
    reason: null,
    dayIndex: 1,
    ...teil,
  };
}

describe("fortschrittDerWoche", () => {
  it("zählt erledigte gegen geplante Übungen", () => {
    const items = [uebung({ isComplete: true }), uebung({}), uebung({}), uebung({ isComplete: true })];

    expect(fortschrittDerWoche(items)).toEqual({ geplant: 4, erledigt: 2 });
  });

  it("hat in einer Pausenwoche nichts zu erledigen", () => {
    const pause = [uebung({ isRestWeek: true, exerciseId: null, exerciseName: null, repetitionsTarget: 0 })];

    expect(fortschrittDerWoche(pause)).toEqual({ geplant: 0, erledigt: 0 });
  });
});

describe("planItemName", () => {
  it("nimmt die Katalog-Übung, sonst den Freitext", () => {
    expect(planItemName(uebung({}))).toBe("Sitz");
    expect(planItemName(uebung({ exerciseName: null, freeTextLabel: "Kopfarbeit" }))).toBe("Kopfarbeit");
  });

  it("liefert null, wenn die Übung nicht mehr im Katalog steht", () => {
    expect(planItemName(uebung({ exerciseName: null }))).toBeNull();
  });
});

describe("trainingstageDerWoche", () => {
  const ziel = { trainingDaysPerWeek: 3, weekConfigs: [{ weekNumber: 2, trainingDaysPerWeek: 1 }] };

  it("nimmt die Überschreibung der Woche, sonst den Plan-Standard", () => {
    expect(trainingstageDerWoche(ziel, 2)).toBe(1);
    expect(trainingstageDerWoche(ziel, 5)).toBe(3);
  });

  it("übersteht ältere Daten aus dem Lesecache ohne weekConfigs", () => {
    const alt = { trainingDaysPerWeek: 2 } as unknown as Parameters<typeof trainingstageDerWoche>[0];

    expect(trainingstageDerWoche(alt, 1)).toBe(2);
  });
});

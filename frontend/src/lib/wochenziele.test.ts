import { describe, expect, it } from "vitest";
import { computeCurrentWeek, groupByWeek, offeneWochenziele } from "./trainingsplan";
import type { Goal, TrainingPlanItem } from "./types";

function item(teil: Partial<TrainingPlanItem>): TrainingPlanItem {
  return {
    id: "i",
    weekNumber: 1,
    exerciseId: "ex",
    exerciseName: "Fußarbeit",
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

function ziel(items: TrainingPlanItem[], teil: Partial<Goal> = {}): Goal {
  return {
    id: "g",
    dogId: "d",
    sportId: "s",
    sportName: "BH",
    regulationId: null,
    regulationName: null,
    targetDate: "2026-12-01",
    status: 0,
    notes: null,
    isCustom: false,
    weeklyExerciseCount: 3,
    trainingDaysPerWeek: 2,
    weekConfigs: [],
    trainingPlan: { id: "p", generatedAt: "2026-09-01T00:00:00Z", items },
    planManagedByTrainer: false,
    ...teil,
  };
}

const TAG = 24 * 60 * 60 * 1000;
const START = Date.parse("2026-09-01T00:00:00Z");

describe("computeCurrentWeek", () => {
  const wochen = groupByWeek([item({ id: "a", weekNumber: 1 }), item({ id: "b", weekNumber: 2 }), item({ id: "c", weekNumber: 3 })]);

  it("zählt ab der Planerstellung je angebrochene sieben Tage eine Woche weiter", () => {
    expect(computeCurrentWeek(wochen, "2026-09-01T00:00:00Z", START + 8 * TAG)).toBe(2);
  });

  it("bleibt nach dem Planende bei der letzten Woche", () => {
    expect(computeCurrentWeek(wochen, "2026-09-01T00:00:00Z", START + 60 * TAG)).toBe(3);
  });
});

describe("offeneWochenziele", () => {
  it("liefert nur die offenen Ziele der laufenden Woche", () => {
    const g = ziel([
      item({ id: "a", weekNumber: 1 }),
      item({ id: "b", weekNumber: 2 }),
      item({ id: "c", weekNumber: 2, completedCount: 2, isComplete: true }),
    ]);

    const offen = offeneWochenziele(g, START + 8 * TAG);

    expect(offen?.woche).toBe(2);
    expect(offen?.items.map((i) => i.id)).toEqual(["b"]);
  });

  it("liefert nichts für eine Pausenwoche", () => {
    const g = ziel([item({ id: "a", weekNumber: 1, isRestWeek: true })]);

    expect(offeneWochenziele(g, START)).toBeNull();
  });

  it("liefert nichts, wenn in der Woche schon alles erledigt ist", () => {
    const g = ziel([item({ id: "a", weekNumber: 1, completedCount: 2, isComplete: true })]);

    expect(offeneWochenziele(g, START)).toBeNull();
  });

  it("liefert nichts für erreichte oder abgebrochene Ziele", () => {
    expect(offeneWochenziele(ziel([item({})], { status: 1 }), START)).toBeNull();
    expect(offeneWochenziele(ziel([item({})], { status: 2 }), START)).toBeNull();
  });
});

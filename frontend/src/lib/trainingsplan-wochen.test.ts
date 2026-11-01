import { describe, expect, it } from "vitest";
import { computeCurrentWeek, groupByWeek, istPausenwoche, offeneWochenziele, wochenFortschritt } from "./trainingsplan";
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

describe("wochenFortschritt", () => {
  it("zählt die geplanten und die erledigten Übungen der laufenden Woche", () => {
    const g = ziel([
      item({ id: "a", weekNumber: 1 }),
      item({ id: "b", weekNumber: 2 }),
      item({ id: "c", weekNumber: 2, completedCount: 2, isComplete: true }),
      item({ id: "d", weekNumber: 2, completedCount: 2, isComplete: true }),
    ]);

    expect(wochenFortschritt(g, START + 8 * TAG)).toEqual({ woche: 2, geplant: 3, erledigt: 2 });
  });

  it("meldet alles erledigt, wenn keine Übung mehr offen ist - dort, wo 'Diese Woche' nichts mehr zeigt", () => {
    const g = ziel([item({ id: "a", weekNumber: 1, completedCount: 2, isComplete: true })]);

    expect(offeneWochenziele(g, START)).toBeNull();
    expect(wochenFortschritt(g, START)).toEqual({ woche: 1, geplant: 1, erledigt: 1 });
  });

  it("meint dieselbe Woche wie 'Diese Woche'", () => {
    const g = ziel([
      item({ id: "a", weekNumber: 1 }),
      item({ id: "b", weekNumber: 2 }),
      item({ id: "c", weekNumber: 3 }),
    ]);

    for (const tage of [0, 6, 7, 13, 14, 40]) {
      const jetzt = START + tage * TAG;
      expect(wochenFortschritt(g, jetzt)?.woche).toBe(offeneWochenziele(g, jetzt)?.woche);
    }
  });

  it("liefert nichts ohne geplante Übung: Pausenwoche, leerer Plan", () => {
    expect(wochenFortschritt(ziel([item({ id: "a", weekNumber: 1, isRestWeek: true })]), START)).toBeNull();
    expect(wochenFortschritt(ziel([]), START)).toBeNull();
  });

  it("liefert nichts für erreichte oder abgebrochene Ziele und Ziele ohne Plan", () => {
    expect(wochenFortschritt(ziel([item({})], { status: 1 }), START)).toBeNull();
    expect(wochenFortschritt(ziel([item({})], { status: 2 }), START)).toBeNull();
    expect(wochenFortschritt(ziel([item({})], { trainingPlan: null }), START)).toBeNull();
  });
});

describe("Pausen-Platzhalter neben echten Übungen", () => {
  const platzhalter = item({ id: "p", weekNumber: 12, exerciseId: null, exerciseName: null, repetitionsTarget: 0, isRestWeek: true });

  it("blendet den Platzhalter aus, wenn die Woche echte Übungen hat", () => {
    const wochen = groupByWeek([platzhalter, item({ id: "a", weekNumber: 12 }), item({ id: "b", weekNumber: 12 })]);

    expect(wochen).toHaveLength(1);
    expect(wochen[0][1].map((i) => i.id)).toEqual(["a", "b"]);
    expect(istPausenwoche(wochen[0][1])).toBe(false);
  });

  it("behält den Platzhalter einer reinen Pausenwoche", () => {
    const wochen = groupByWeek([item({ id: "a", weekNumber: 1 }), { ...platzhalter, weekNumber: 4 }]);

    const pause = wochen.find(([nummer]) => nummer === 4)!;
    expect(istPausenwoche(pause[1])).toBe(true);
  });

  it("zählt die Wochenübungen ohne den Platzhalter", () => {
    const ziel12 = ziel([platzhalter, item({ id: "a", weekNumber: 12 })], {
      trainingPlan: { id: "tp", generatedAt: "2020-01-01T00:00:00Z", items: [platzhalter, item({ id: "a", weekNumber: 12 })] },
    });

    // Woche 12 ist die einzige und damit die laufende.
    expect(wochenFortschritt(ziel12)).toMatchObject({ woche: 12, geplant: 1 });
  });
});

describe("abgelaufene Ziele", () => {
  // Zieldatum 2026-09-20; lokale Mittagszeit, damit die Zeitzone des Rechners
  // den Kalendertag nicht verschiebt.
  const g = ziel([item({ id: "a", weekNumber: 1 }), item({ id: "b", weekNumber: 2 })], { targetDate: "2026-09-20" });
  const mittag = (tag: string) => new Date(`${tag}T12:00:00`).getTime();

  it("liefert nach dem Zieldatum weder Wochenübungen noch Fortschritt", () => {
    expect(offeneWochenziele(g, mittag("2026-09-21"))).toBeNull();
    expect(wochenFortschritt(g, mittag("2026-09-21"))).toBeNull();
    expect(wochenFortschritt(g, mittag("2026-12-01"))).toBeNull();
  });

  it("zählt den Prüfungstag selbst noch als laufend", () => {
    expect(offeneWochenziele(g, mittag("2026-09-20"))).not.toBeNull();
    expect(wochenFortschritt(g, mittag("2026-09-20"))).not.toBeNull();
  });

  it("gilt bis zur letzten Minute des Prüfungstags, nicht nur bis zum Mittag", () => {
    expect(wochenFortschritt(g, new Date("2026-09-20T23:59:00").getTime())).not.toBeNull();
    expect(wochenFortschritt(g, new Date("2026-09-21T00:01:00").getTime())).toBeNull();
  });

  it("liefert vor dem Zieldatum wie bisher die laufende Woche", () => {
    expect(wochenFortschritt(g, mittag("2026-09-10"))).toMatchObject({ woche: 2, geplant: 1 });
  });

  it("berücksichtigt ein Zieldatum mit Zeitanteil wie ein reines Datum", () => {
    const mitZeit = ziel([item({})], { targetDate: "2026-09-20T00:00:00Z" });

    expect(wochenFortschritt(mitZeit, mittag("2026-09-20"))).not.toBeNull();
    expect(wochenFortschritt(mitZeit, mittag("2026-09-21"))).toBeNull();
  });
});

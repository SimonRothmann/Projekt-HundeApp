import { describe, expect, it } from "vitest";
import {
  computeCurrentWeek,
  groupByWeek,
  passendesPlanItem,
  wochenFortschritt,
} from "./trainingsplan";
import { plusTage } from "./kalendertag";
import { pausentageZwischen } from "./pausen";
import type { Goal, PauseZeitraum, TrainingPlanItem } from "./types";

// Die Planwoche rechnet in ganzen Kalendertagen der Vereinszeit (wie der Server) - der Test legt
// die Zeitzone des Geräts selbst fest, damit "0 Uhr Ortszeit" und "0 Uhr Vereinszeit" dasselbe sind.
process.env.TZ = "Europe/Berlin";

/**
 * Die Planwoche mit Trainingspausen - dieselben Fälle wie im Server
 * (backend/tests/.../TrainingspausenTests.cs): Die Tage, an denen der Hund
 * pausiert, zählen nicht; die Woche steht still und läuft danach dort weiter.
 *
 * Gerechnet wird mit dem Kalender des Geräts: Der Plan beginnt am 1.9. um
 * 0 Uhr Ortszeit, "Tag n" ist dessen Mittag (so bleibt jede Zeitzone und die
 * Sommerzeit außen vor, solange n vor dem 25.10. liegt).
 */
const BEGINN_TAG = "2026-09-01";
const BEGINN = new Date(2026, 8, 1).getTime();
const GENERIERT = new Date(BEGINN).toISOString();

const mittag = (tag: number) => new Date(2026, 8, 1 + tag, 12).getTime();
const pause = (vonTag: number, bisTag: number | null): PauseZeitraum => ({
  start: plusTage(BEGINN_TAG, vonTag),
  end: bisTag === null ? null : plusTage(BEGINN_TAG, bisTag),
});

function item(teil: Partial<TrainingPlanItem>): TrainingPlanItem {
  return {
    id: "i", weekNumber: 1, exerciseId: "ex", exerciseName: "Sitz", freeTextLabel: null, repetitionsTarget: 2,
    isRestWeek: false, completedCount: 0, isComplete: false, logs: [], reason: null, dayIndex: 1, ...teil,
  };
}

const wochen = groupByWeek([1, 2, 3, 4, 5].map((w) => item({ id: `w${w}`, weekNumber: w })));
const woche = (tag: number, pausen?: PauseZeitraum[]) => computeCurrentWeek(wochen, GENERIERT, mittag(tag), pausen);

describe("computeCurrentWeek mit Pausen", () => {
  it("zählt ohne Pause Kalenderwochen", () => {
    expect(woche(6)).toBe(1);
    expect(woche(7)).toBe(2);
    expect(woche(20)).toBe(3);
  });

  it("hält die Woche während einer Pause an", () => {
    const pausen = [pause(9, 22)];
    expect(woche(8, pausen)).toBe(2);
    expect(woche(9, pausen)).toBe(2);
    expect(woche(15, pausen)).toBe(2);
    expect(woche(22, pausen)).toBe(2);
  });

  it("macht danach dort weiter, wo der Plan stand - auch mitten in der Woche", () => {
    const pausen = [pause(9, 22)];
    // Neun Tage Plan sind gelaufen (Tag 3 der Woche 2); fünf weitere bis zu Woche 3.
    expect(woche(23, pausen)).toBe(2);
    expect(woche(27, pausen)).toBe(2);
    expect(woche(28, pausen)).toBe(3);
  });

  it("lässt eine offene Pause bis heute anhalten", () => {
    const pausen = [pause(3, null)];
    expect(woche(4, pausen)).toBe(1);
    expect(woche(40, pausen)).toBe(1);
  });

  it("ignoriert eine Pause vor dem Planbeginn", () => {
    const pausen: PauseZeitraum[] = [{ start: plusTage(BEGINN_TAG, -30), end: plusTage(BEGINN_TAG, -2) }];
    expect(woche(7, pausen)).toBe(2);
  });

  it("ignoriert eine Pause, die erst nach heute beginnt", () => {
    const pausen = [pause(10, 20)];
    expect(woche(6, pausen)).toBe(1);
    expect(woche(7, pausen)).toBe(2);
  });

  it("zählt von einer Pause, die vor dem Planbeginn anfängt, nur den Teil im Plan", () => {
    const pausen: PauseZeitraum[] = [{ start: plusTage(BEGINN_TAG, -2), end: plusTage(BEGINN_TAG, 3) }];
    expect(woche(10, pausen)).toBe(1);
    expect(woche(11, pausen)).toBe(2);
  });

  it("bleibt ohne Pausenangabe wie bisher (älterer Lesecache)", () => {
    expect(woche(15, undefined)).toBe(3);
    expect(woche(15, [])).toBe(3);
  });
});

describe("Pausentage der Planrechnung", () => {
  const tag = (n: number) => plusTage(BEGINN_TAG, n);

  it("zählt überschneidende Pausen nur einmal", () => {
    expect(pausentageZwischen([pause(2, 8), pause(6, 12)], BEGINN_TAG, tag(30), tag(30))).toBe(11);
  });

  it("zählt nichts, wenn das Fenster leer ist", () => {
    expect(pausentageZwischen([pause(2, 8)], tag(5), tag(5), tag(5))).toBe(0);
    expect(pausentageZwischen([], BEGINN_TAG, tag(5), tag(5))).toBe(0);
  });
});

describe("Planwoche in ganzen Tagen der Vereinszeit (Regel des Servers)", () => {
  // Plan am Nachmittag erstellt: 1.9. um 15:00 Uhr Berlin (13:00 UTC).
  const NACHMITTAGS = "2026-09-01T13:00:00Z";
  const um = (iso: string) => Date.parse(iso);
  const woche_ = (jetzt: string, pausen?: PauseZeitraum[]) => computeCurrentWeek(wochen, NACHMITTAGS, um(jetzt), pausen);

  it("die neue Woche beginnt um 0 Uhr des siebten Tages, nicht erst zur Uhrzeit der Erstellung", () => {
    // 8.9. um 08:00 Uhr: sieben Kalendertage seit dem Planbeginn - der Server sagt Woche 2.
    expect(woche_("2026-09-08T06:00:00Z")).toBe(2);
    expect(woche_("2026-09-07T21:59:00Z")).toBe(1); // 7.9., 23:59 Uhr
    expect(woche_("2026-09-07T22:00:00Z")).toBe(2); // 8.9., 0:00 Uhr
  });

  it("der Tag wechselt um Mitternacht Vereinszeit, nicht um Mitternacht UTC", () => {
    // 2.9., 00:30 Uhr Berlin = 1.9., 22:30 UTC: für den Server schon der 2.9. als Planbeginn.
    const spaet = (jetzt: string) => computeCurrentWeek(wochen, "2026-09-01T22:30:00Z", Date.parse(jetzt), undefined);
    expect(spaet("2026-09-08T21:30:00Z")).toBe(1); // 8.9., 23:30 Uhr: erst 6 Tage seit dem 2.9.
    expect(spaet("2026-09-08T22:30:00Z")).toBe(2); // 9.9., 00:30 Uhr: 7 Tage
  });

  it("Pausentage sind ganze Kalendertage, beide Grenzen einschließlich", () => {
    // Pause 3.9. bis 9.9. (7 Tage): am 15.9. (14 Tage seit Beginn) sind 7 aktive Tage vergangen.
    const pausen: PauseZeitraum[] = [{ start: "2026-09-03", end: "2026-09-09" }];
    expect(woche_("2026-09-14T10:00:00Z", pausen)).toBe(1); // 13 Tage - 7 = 6
    expect(woche_("2026-09-15T10:00:00Z", pausen)).toBe(2); // 14 Tage - 7 = 7
  });

  it("ein Training am Tag des Planbeginns zählt, eines davor nicht", () => {
    const g = ziel([]);
    g.trainingPlan = { id: "p", generatedAt: NACHMITTAGS, items: [1, 2].map((w) => item({ id: `sitz-w${w}`, weekNumber: w, exerciseId: "sitz" })) };
    const jetzt = um("2026-09-03T10:00:00Z");
    expect(passendesPlanItem([g], "sitz", "2026-09-01", jetzt)?.id).toBe("sitz-w1");
    expect(passendesPlanItem([g], "sitz", "2026-08-31", jetzt)).toBeNull();
  });

  function ziel(planPausen: PauseZeitraum[]): Goal {
    return {
      id: "g", dogId: "d", sportId: "s", sportName: "BH", regulationId: null, regulationName: null,
      targetDate: "2026-12-01", status: 0, notes: null, isCustom: false, weeklyExerciseCount: 1, trainingDaysPerWeek: 1,
      weekConfigs: [], trainingPlan: null, planManagedByTrainer: false, planPausen,
    };
  }
});

describe("Plan und Pause im Zusammenspiel", () => {
  function ziel(planPausen: PauseZeitraum[]): Goal {
    return {
      id: "g", dogId: "d", sportId: "s", sportName: "BH", regulationId: null, regulationName: null,
      targetDate: "2026-12-01", status: 0, notes: null, isCustom: false, weeklyExerciseCount: 1, trainingDaysPerWeek: 1,
      weekConfigs: [],
      trainingPlan: {
        id: "p", generatedAt: GENERIERT,
        items: [1, 2, 3].map((w) => item({ id: `sitz-w${w}`, weekNumber: w, exerciseId: "sitz" })),
      },
      planManagedByTrainer: false,
      planPausen,
    };
  }

  it("zeigt in der Pause den Stand der stehengebliebenen Woche", () => {
    const stand = wochenFortschritt(ziel([pause(9, 22)]), mittag(15));
    expect(stand?.woche).toBe(2);
  });

  it("zählt ein Training in der Pause für die Woche, in der der Plan steht", () => {
    const heute = new Date(mittag(15));
    const datum = `${heute.getFullYear()}-${String(heute.getMonth() + 1).padStart(2, "0")}-${String(heute.getDate()).padStart(2, "0")}`;

    const mitPause = passendesPlanItem([ziel([pause(9, 22)])], "sitz", datum, mittag(15));
    const ohnePause = passendesPlanItem([ziel([])], "sitz", datum, mittag(15));

    expect(mitPause?.id).toBe("sitz-w2");
    expect(ohnePause?.id).toBe("sitz-w3");
  });
});

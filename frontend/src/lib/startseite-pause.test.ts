import { describe, expect, it } from "vitest";
import { pausenOhneKarte, zielAufteilung } from "./startseite";
import { PAUSE_GRUND, type Dog, type DogPause, type Goal } from "./types";

// Mittwoch, 4. November 2026, Mittag (Ortszeit des Geräts).
const JETZT = new Date(2026, 10, 4, 12).getTime();

function pause(teil: Partial<DogPause> = {}): DogPause {
  return { id: "p", startDate: "2026-11-01", endDate: "2026-11-10", reason: PAUSE_GRUND.Laeufig, note: null, ...teil };
}

const hund = (id: string, teil: Partial<Dog> = {}): Dog => ({
  id, name: id, breed: null, birthday: null, gender: 1, imageUrl: null, notes: null, archivedAt: null, hasImage: false, ...teil,
});

function ziel(): Goal {
  return {
    id: "g", dogId: "d", sportId: "s", sportName: "Sport", regulationId: null, regulationName: null, targetDate: "2027-01-10",
    status: 0, notes: null, isCustom: false, weeklyExerciseCount: 2, trainingDaysPerWeek: 2, weekConfigs: [],
    trainingPlan: { id: "p", generatedAt: "2026-10-05T00:00:00", items: [] }, planManagedByTrainer: false,
  };
}

describe("zielAufteilung mit Pause", () => {
  it("gibt der Karte die Pause, die der Hund heute hat", () => {
    const p = pause();
    const { karten } = zielAufteilung([{ dog: hund("Flitzer", { pause: p }), activeGoals: [ziel()] }], new Set(), JETZT);
    expect(karten[0].pause).toBe(p);
  });

  it("gibt ihr keine Pause, die vorbei ist oder erst kommt", () => {
    const vorbei = pause({ startDate: "2026-10-01", endDate: "2026-10-20" });
    const spaeter = pause({ startDate: "2026-11-20", endDate: null });
    expect(zielAufteilung([{ dog: hund("A", { pause: vorbei }), activeGoals: [ziel()] }], new Set(), JETZT).karten[0].pause).toBeNull();
    expect(zielAufteilung([{ dog: hund("B", { pause: spaeter }), activeGoals: [ziel()] }], new Set(), JETZT).karten[0].pause).toBeNull();
  });

  it("verträgt Hunde ohne Pausenfeld", () => {
    expect(zielAufteilung([{ dog: hund("C"), activeGoals: [ziel()] }], new Set(), JETZT).karten[0].pause).toBeNull();
  });
});

describe("pausenOhneKarte", () => {
  it("nennt pausierende Hunde, die keine Zielkarte haben", () => {
    const mitKarte = hund("Karte", { pause: pause() });
    const ohneKarte = hund("Ohne", { pause: pause() });
    const gesund = hund("Gesund");

    const ergebnis = pausenOhneKarte([mitKarte, ohneKarte, gesund], [{ hund: mitKarte }], JETZT);

    expect(ergebnis.map((e) => e.hund.id)).toEqual(["Ohne"]);
  });

  it("überspringt archivierte Hunde und vorbeigegangene Pausen", () => {
    const archiviert = hund("Alt", { archivedAt: "2026-01-01", pause: pause() });
    const vorbei = hund("Vorbei", { pause: pause({ endDate: "2026-11-02" }) });

    expect(pausenOhneKarte([archiviert, vorbei], [], JETZT)).toEqual([]);
  });
});

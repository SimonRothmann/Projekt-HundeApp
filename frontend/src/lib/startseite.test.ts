import { describe, expect, it } from "vitest";
import {
  feedbackKarte,
  feedbackKuerzbar,
  MAX_WEITERE_TERMINE,
  terminUebersicht,
  zeigeSachkundeZeile,
  zielAufteilung,
} from "./startseite";
import type { Dog, Goal, GroupTrainingSession, OffenesFeedback, TrainingPlanItem } from "./types";

const JETZT = Date.parse("2026-10-08T10:00:00");

function termin(id: string, startsAt: string, status: 0 | 1 = 0): GroupTrainingSession {
  return {
    id, clubId: "c", groupId: "g", groupName: "Gruppe", category: 0, startsAt, durationMinutes: 60,
    location: null, notes: null, status, plannedMinutes: 0, items: [], trainers: [],
    myResponse: null, attendingCount: 0, decliningCount: 0, openCount: 0, responses: [], registrationCount: 0,
  };
}

describe("terminUebersicht", () => {
  it("nimmt den frühesten nicht abgesagten Termin der nächsten 7 Tage", () => {
    const liste = [termin("spaeter", "2026-10-12T18:00:00"), termin("frueh", "2026-10-09T18:00:00")];
    const { naechster, weitere } = terminUebersicht(liste, JETZT);
    expect(naechster?.id).toBe("frueh");
    expect(weitere.map((t) => t.id)).toEqual(["spaeter"]);
  });

  it("zeigt ohne Termin in 7 Tagen gar nichts - auch keine späteren", () => {
    const liste = [termin("fern", "2026-10-20T18:00:00")];
    expect(terminUebersicht(liste, JETZT)).toEqual({ naechster: null, weitere: [] });
    expect(terminUebersicht([], JETZT)).toEqual({ naechster: null, weitere: [] });
  });

  it("überspringt abgesagte Termine für die Karte, führt sie aber unter weitere (zeitlich einsortiert)", () => {
    const liste = [termin("abgesagt", "2026-10-09T18:00:00", 1), termin("geplant", "2026-10-10T18:00:00")];
    const { naechster, weitere } = terminUebersicht(liste, JETZT);
    expect(naechster?.id).toBe("geplant");
    expect(weitere.map((t) => t.id)).toEqual(["abgesagt"]);
    const andersrum = terminUebersicht([termin("geplant", "2026-10-09T18:00:00"), termin("abgesagt", "2026-10-10T18:00:00", 1)], JETZT);
    expect(andersrum.weitere.map((t) => t.id)).toEqual(["abgesagt"]);
  });

  it("zeigt nichts, wenn in 7 Tagen nur abgesagte Termine liegen", () => {
    expect(terminUebersicht([termin("a", "2026-10-09T18:00:00", 1), termin("b", "2026-10-20T18:00:00")], JETZT).naechster).toBeNull();
  });

  it("ignoriert Termine, die schon begonnen haben", () => {
    const liste = [termin("vorbei", "2026-10-08T08:00:00"), termin("heute", "2026-10-08T18:00:00")];
    const { naechster, weitere } = terminUebersicht(liste, JETZT);
    expect(naechster?.id).toBe("heute");
    expect(weitere).toEqual([]);
  });

  it("deckelt die weiteren Termine", () => {
    const liste = Array.from({ length: 10 }, (_, i) => termin(`t${i}`, `2026-10-${String(9 + i).padStart(2, "0")}T18:00:00`));
    expect(terminUebersicht(liste, JETZT).weitere).toHaveLength(MAX_WEITERE_TERMINE);
  });
});

function feedback(sessionId: string, feedbackAt: string | null, text = "Gut gemacht"): OffenesFeedback {
  return { sessionId, dogId: "d", dogName: "Bello", trainerName: "Tina", feedback: text, feedbackAt };
}

describe("feedbackKarte", () => {
  it("nimmt das neueste und zählt die übrigen", () => {
    const { aktuell, weitere } = feedbackKarte([
      feedback("alt", "2026-10-01T10:00:00Z"),
      feedback("neu", "2026-10-07T10:00:00Z"),
      feedback("mittel", "2026-10-04T10:00:00Z"),
    ]);
    expect(aktuell?.sessionId).toBe("neu");
    expect(weitere.map((f) => f.sessionId)).toEqual(["mittel", "alt"]);
  });

  it("stellt Feedback ohne Zeitpunkt hinter datiertes", () => {
    expect(feedbackKarte([feedback("ohne", null), feedback("mit", "2026-10-01T10:00:00Z")]).aktuell?.sessionId).toBe("mit");
  });

  it("überspringt Feedback ohne Text", () => {
    expect(feedbackKarte([feedback("leer", "2026-10-07T10:00:00Z", "  "), feedback("da", "2026-10-01T10:00:00Z")]).aktuell?.sessionId).toBe("da");
  });

  it("kommt mit fehlender oder leerer Liste zurecht", () => {
    expect(feedbackKarte(undefined)).toEqual({ aktuell: null, weitere: [] });
    expect(feedbackKarte([])).toEqual({ aktuell: null, weitere: [] });
  });
});

describe("feedbackKuerzbar", () => {
  it("bietet bei kurzem Text kein 'mehr' an", () => {
    expect(feedbackKuerzbar("Schön gearbeitet!")).toBe(false);
  });

  it("bietet es bei langem Text oder vielen Zeilenumbrüchen an", () => {
    expect(feedbackKuerzbar("x".repeat(200))).toBe(true);
    expect(feedbackKuerzbar("a\nb\nc\nd")).toBe(true);
  });
});

const hund = (id: string, archiviert = false): Dog => ({
  id, name: id, breed: null, birthday: null, gender: 0, imageUrl: null, notes: null,
  archivedAt: archiviert ? "2026-01-01" : null, hasImage: false,
});

function item(id: string, woche: number, erledigt: boolean): TrainingPlanItem {
  return {
    id, weekNumber: woche, exerciseId: id, exerciseName: id, freeTextLabel: null, repetitionsTarget: 2,
    isRestWeek: false, completedCount: erledigt ? 2 : 0, isComplete: erledigt, logs: [], reason: null, dayIndex: 0,
  };
}

function ziel(id: string, sportId: string, targetDate: string, items: TrainingPlanItem[] = []): Goal {
  return {
    id, dogId: "d", sportId, sportName: "Sport", regulationId: null, regulationName: null, targetDate, status: 0,
    notes: null, isCustom: false, weeklyExerciseCount: 2, trainingDaysPerWeek: 2, weekConfigs: [],
    trainingPlan: { id: "p", generatedAt: "2026-10-05T00:00:00", items },
    planManagedByTrainer: false,
  };
}

describe("zielAufteilung", () => {
  const keinBh = new Set<string>();

  it("macht je Hund eine Karte mit offenen Übungen und Fortschritt der laufenden Woche", () => {
    const g = ziel("g1", "s", "2027-01-10", [item("a", 1, true), item("b", 1, false), item("c", 1, false)]);
    const { karten, abgelaufen, ohneZiel } = zielAufteilung([{ dog: hund("Bello"), activeGoals: [g] }], keinBh, JETZT);
    expect(abgelaufen).toEqual([]);
    expect(ohneZiel).toEqual([]);
    expect(karten).toHaveLength(1);
    expect(karten[0].tage).toBe(94);
    expect(karten[0].fortschritt).toEqual({ woche: 1, geplant: 3, erledigt: 1 });
    expect(karten[0].offen.map((i) => i.id)).toEqual(["b", "c"]);
    expect(karten[0].bh).toBe(false);
    expect(karten[0].weitereZiele).toBe(0);
  });

  it("wählt bei mehreren laufenden Zielen eines Hundes das nächste", () => {
    const fern = ziel("fern", "s", "2027-03-01");
    const nah = ziel("nah", "s", "2026-12-01");
    const { karten } = zielAufteilung([{ dog: hund("Bello"), activeGoals: [fern, nah] }], keinBh, JETZT);
    expect(karten.map((k) => k.goal.id)).toEqual(["nah"]);
    expect(karten[0].weitereZiele).toBe(1);
  });

  it("zählt den Prüfungstag selbst noch als laufend", () => {
    const { karten, abgelaufen } = zielAufteilung([{ dog: hund("Bello"), activeGoals: [ziel("heute", "s", "2026-10-08")] }], keinBh, JETZT);
    expect(karten).toHaveLength(1);
    expect(karten[0].tage).toBe(0);
    expect(abgelaufen).toEqual([]);
  });

  it("führt verstrichene Ziele getrennt als abgelaufen - ohne Karte, ohne Wochenübungen", () => {
    const vorbei = ziel("vorbei", "s", "2026-10-03", [item("a", 1, false)]);
    const { karten, abgelaufen, ohneZiel } = zielAufteilung([{ dog: hund("Emma"), activeGoals: [vorbei] }], keinBh, JETZT);
    expect(karten).toEqual([]);
    expect(abgelaufen.map((a) => [a.hund.id, a.goal.id])).toEqual([["Emma", "vorbei"]]);
    // Ein abgelaufenes Ziel ist ein Ziel: kein "Prüfungsziel setzen".
    expect(ohneZiel).toEqual([]);
  });

  it("gibt einem Hund mit laufendem UND abgelaufenem Ziel beides", () => {
    const { karten, abgelaufen } = zielAufteilung(
      [{ dog: hund("Bello"), activeGoals: [ziel("vorbei", "s", "2026-09-01"), ziel("laeuft", "s", "2026-12-01")] }],
      keinBh,
      JETZT,
    );
    expect(karten.map((k) => k.goal.id)).toEqual(["laeuft"]);
    expect(abgelaufen.map((a) => a.goal.id)).toEqual(["vorbei"]);
  });

  it("sammelt Hunde ohne aktives Ziel und überspringt archivierte", () => {
    const erledigt = { ...ziel("fertig", "s", "2027-01-01"), status: 1 as const };
    const { ohneZiel } = zielAufteilung(
      [
        { dog: hund("Ohne"), activeGoals: [] },
        { dog: hund("Fertig"), activeGoals: [erledigt] },
        { dog: hund("Rentner", true), activeGoals: [] },
      ],
      keinBh,
      JETZT,
    );
    expect(ohneZiel.map((h) => h.id)).toEqual(["Ohne", "Fertig"]);
  });

  it("erkennt eine Begleithundeprüfung an der Sportart", () => {
    const { karten } = zielAufteilung([{ dog: hund("Bello"), activeGoals: [ziel("bh", "sport-bh", "2027-01-10")] }], new Set(["sport-bh"]), JETZT);
    expect(karten[0].bh).toBe(true);
  });
});

describe("zeigeSachkundeZeile", () => {
  it("steht am Ende, wenn das Modul an ist und keine BH-Karte den Link trägt", () => {
    expect(zeigeSachkundeZeile([{ bh: false }], true)).toBe(true);
    expect(zeigeSachkundeZeile([], true)).toBe(true);
  });

  it("entfällt, wenn eine BH-Karte den Link zeigt oder das Modul aus ist", () => {
    expect(zeigeSachkundeZeile([{ bh: true }], true)).toBe(false);
    expect(zeigeSachkundeZeile([], false)).toBe(false);
  });
});

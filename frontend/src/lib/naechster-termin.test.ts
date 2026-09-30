import { describe, expect, it } from "vitest";
import { naechsterTermin, zusageNamen } from "./naechster-termin";
import type { GroupTrainingSession, SessionResponse } from "./types";

const JETZT = Date.parse("2026-10-01T08:00:00Z");

function termin(id: string, startsAt: string, status: 0 | 1 = 0): GroupTrainingSession {
  return {
    id, clubId: "c", groupId: "g", groupName: "Gruppe", category: 0, startsAt, durationMinutes: 60,
    location: null, notes: null, status, plannedMinutes: 0, items: [], trainers: [],
    myResponse: null, attendingCount: 0, decliningCount: 0, openCount: 0, responses: [],
  };
}

describe("naechsterTermin", () => {
  it("nimmt über alle Listen den frühesten kommenden Termin", () => {
    const a = [termin("a1", "2026-10-09T16:00:00Z"), termin("a2", "2026-10-03T16:00:00Z")];
    const b = [termin("b1", "2026-10-02T16:00:00Z")];
    expect(naechsterTermin([a, b], JETZT)?.id).toBe("b1");
  });

  it("überspringt abgesagte Termine", () => {
    const liste = [termin("abgesagt", "2026-10-02T16:00:00Z", 1), termin("geplant", "2026-10-05T16:00:00Z")];
    expect(naechsterTermin([liste], JETZT)?.id).toBe("geplant");
  });

  it("überspringt Termine, die schon begonnen haben", () => {
    const liste = [termin("vorbei", "2026-10-01T06:00:00Z"), termin("kommt", "2026-10-01T18:00:00Z")];
    expect(naechsterTermin([liste], JETZT)?.id).toBe("kommt");
  });

  it("liefert null ohne kommenden Termin", () => {
    expect(naechsterTermin([], JETZT)).toBeNull();
    expect(naechsterTermin([[termin("x", "2026-09-01T16:00:00Z")], []], JETZT)).toBeNull();
  });
});

describe("zusageNamen", () => {
  const antwort = (vorname: string, isAttending: boolean): SessionResponse => ({ userId: vorname, firstName: vorname, lastName: "Muster", isAttending });

  it("zeigt nur Zusagen und fasst den Rest zu einer Zahl zusammen", () => {
    const t = {
      responses: [
        antwort("Anna", true), antwort("Ben", false), antwort("Carla", true), antwort("Dora", true),
        antwort("Emil", true), antwort("Fritz", true), antwort("Gina", true),
      ],
    };
    const { namen, weitere } = zusageNamen(t);
    expect(namen).toEqual(["Anna Muster", "Carla Muster", "Dora Muster", "Emil Muster"]);
    expect(weitere).toBe(2);
  });

  it("hat keinen Rest, wenn alle Namen passen", () => {
    expect(zusageNamen({ responses: [antwort("Anna", true)] })).toEqual({ namen: ["Anna Muster"], weitere: 0 });
  });

  it("ersetzt einen fehlenden Namen durch ein Fragezeichen", () => {
    const t = { responses: [{ userId: "u", firstName: "", lastName: "", isAttending: true }] };
    expect(zusageNamen(t).namen).toEqual(["?"]);
  });
});

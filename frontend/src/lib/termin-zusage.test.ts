import { describe, expect, it } from "vitest";
import { kannAntworten, mitAntwort } from "./termin-zusage";

const stand = { myResponse: null as boolean | null, attendingCount: 2, decliningCount: 1, openCount: 4 };

describe("mitAntwort", () => {
  it("zählt eine erste Zusage aus den Offenen heraus", () => {
    expect(mitAntwort(stand, true)).toEqual({ myResponse: true, attendingCount: 3, decliningCount: 1, openCount: 3 });
  });

  it("zählt eine erste Absage aus den Offenen heraus", () => {
    expect(mitAntwort(stand, false)).toEqual({ myResponse: false, attendingCount: 2, decliningCount: 2, openCount: 3 });
  });

  it("verschiebt beim Umschalten von Zusage auf Absage nur zwischen den beiden Spalten", () => {
    const zugesagt = { ...stand, myResponse: true };
    expect(mitAntwort(zugesagt, false)).toEqual({ myResponse: false, attendingCount: 1, decliningCount: 2, openCount: 4 });
  });

  it("verschiebt beim Umschalten von Absage auf Zusage ebenso", () => {
    const abgesagt = { ...stand, myResponse: false };
    expect(mitAntwort(abgesagt, true)).toEqual({ myResponse: true, attendingCount: 3, decliningCount: 0, openCount: 4 });
  });

  it("ändert nichts, wenn die Antwort schon so lautet", () => {
    const zugesagt = { ...stand, myResponse: true };
    expect(mitAntwort(zugesagt, true)).toBe(zugesagt);
  });

  it("wird nie negativ", () => {
    expect(mitAntwort({ myResponse: null, attendingCount: 0, decliningCount: 0, openCount: 0 }, true).openCount).toBe(0);
  });

  it("lässt übrige Felder des Termins unberührt", () => {
    const termin = { ...stand, id: "x", groupName: "Gruppe" };
    expect(mitAntwort(termin, true)).toMatchObject({ id: "x", groupName: "Gruppe" });
  });
});

describe("kannAntworten", () => {
  const jetzt = Date.parse("2026-10-01T08:00:00Z");
  it("erlaubt geplante Termine vor dem Beginn", () => {
    expect(kannAntworten({ status: 0, startsAt: "2026-10-05T16:00:00Z" }, jetzt)).toBe(true);
  });
  it("erlaubt nichts bei abgesagten Terminen", () => {
    expect(kannAntworten({ status: 1, startsAt: "2026-10-05T16:00:00Z" }, jetzt)).toBe(false);
  });
  it("erlaubt nichts nach dem Beginn", () => {
    expect(kannAntworten({ status: 0, startsAt: "2026-10-01T07:59:00Z" }, jetzt)).toBe(false);
  });
});

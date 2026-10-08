import { describe, expect, it } from "vitest";
import { istDurchBewertungErledigt, kannSelbsteinschaetzungUebernehmen } from "./trainer-bewertung";

const uebung = (rating: number, trainerRating: number | null) => ({
  exerciseId: "u",
  exerciseName: "Sitz",
  rating,
  success: true,
  trainerRating,
  trainerNote: null,
});

describe("kannSelbsteinschaetzungUebernehmen", () => {
  it("ist wahr, wenn eine unbewertete Übung eine Selbsteinschätzung hat", () => {
    expect(kannSelbsteinschaetzungUebernehmen({ exercises: [uebung(4, null)] })).toBe(true);
  });

  it("ist wahr, wenn nur eine von mehreren Übungen noch offen ist", () => {
    expect(kannSelbsteinschaetzungUebernehmen({ exercises: [uebung(3, 5), uebung(2, null)] })).toBe(true);
  });

  it("ist falsch, wenn alle Übungen schon bewertet sind", () => {
    expect(kannSelbsteinschaetzungUebernehmen({ exercises: [uebung(3, 5), uebung(2, 2)] })).toBe(false);
  });

  it("ist falsch ohne Übungen (zum Beispiel nur eine Fährte)", () => {
    expect(kannSelbsteinschaetzungUebernehmen({ exercises: [] })).toBe(false);
  });

  it("ist falsch, wenn es keine Selbsteinschätzung gibt", () => {
    expect(kannSelbsteinschaetzungUebernehmen({ exercises: [uebung(0, null)] })).toBe(false);
  });
});

describe("istDurchBewertungErledigt", () => {
  it("ist wahr, wenn ein Feedback-Text vorliegt", () => {
    expect(istDurchBewertungErledigt({ trainerFeedback: "Schön", exercises: [uebung(3, null)] })).toBe(true);
  });

  it("ist wahr, wenn alle Übungen bewertet sind", () => {
    expect(istDurchBewertungErledigt({ trainerFeedback: null, exercises: [uebung(3, 5), uebung(2, 2)] })).toBe(true);
  });

  it("ist falsch, solange eine Übung ohne Trainer-Bewertung bleibt", () => {
    expect(istDurchBewertungErledigt({ trainerFeedback: null, exercises: [uebung(3, 5), uebung(2, null)] })).toBe(false);
  });

  it("ist falsch ohne Übungen und ohne Feedback (Fährte-only)", () => {
    expect(istDurchBewertungErledigt({ trainerFeedback: null, exercises: [] })).toBe(false);
  });
});

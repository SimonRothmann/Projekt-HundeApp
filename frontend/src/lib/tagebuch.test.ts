import { describe, expect, it } from "vitest";
import {
  geltenderFilter,
  laufAmTag,
  nachTagen,
  tageAnzahl,
  tagKurz,
  tagPasstZuFilter,
  tagesKurzbild,
  verfuegbareFilter,
} from "./tagebuch";
import type { DogTrackRun, TrainingExercise, TrainingSession } from "./types";

// Der Übersetzer des Tests setzt nur Platzhalter ein - die Wörter bleiben deutsch.
const t = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (_, name: string) => String(werte?.[name] ?? `{${name}}`));

function uebung(name: string, rating: number): TrainingExercise {
  return { exerciseName: name, rating } as TrainingExercise;
}

function einheit(teil: Partial<TrainingSession> = {}): TrainingSession {
  return {
    id: "s", date: "2026-09-25", durationMinutes: 30, notes: null, exercises: [], trainerFeedback: null,
    feedbackAt: null, locationName: null, hasGpsTrack: false, ...teil,
  } as TrainingSession;
}

function lauf(datum: string, abweichung: number, prozent: number): DogTrackRun {
  return { date: datum, avgDeviationMeters: abweichung, onTrackPercent: prozent, articlesFound: 0, articlesTotal: 0, unexplainedStops: 0 };
}

describe("nachTagen", () => {
  const liste = [
    einheit({ id: "a", date: "2026-09-25" }),
    einheit({ id: "b", date: "2026-09-25" }),
    einheit({ id: "c", date: "2026-09-20" }),
  ];

  it("fasst Einheiten desselben Tages zusammen und behält die Reihenfolge", () => {
    const tage = nachTagen(liste);
    expect([...tage.keys()]).toEqual(["2026-09-25", "2026-09-20"]);
    expect(tage.get("2026-09-25")?.map((s) => s.id)).toEqual(["a", "b"]);
  });

  it("zählt Tage statt Einheiten", () => {
    expect(tageAnzahl(liste)).toBe(2);
    expect(tageAnzahl([])).toBe(0);
    expect(tageAnzahl(null)).toBe(0);
  });
});

describe("tagKurz", () => {
  it("schreibt Wochentag, Tag und Monat ohne Jahr", () => {
    expect(tagKurz("2026-09-25", "de")).toBe("Fr 25.9.");
    expect(tagKurz("2026-01-05", "de")).toBe("Mo 5.1.");
  });

  it("richtet sich nach der Sprache", () => {
    expect(tagKurz("2026-09-25", "en")).toBe("Fri 25/09");
  });

  it("rechnet mit dem Kalendertag und nicht mit UTC-Mitternacht", () => {
    // Mit new Date("2026-03-01") fiele das westlich von Greenwich auf den 28.2.
    expect(tagKurz("2026-03-01T00:00:00", "de")).toBe("So 1.3.");
  });

  it("gibt unlesbare Eingaben unverändert zurück statt NaN", () => {
    expect(tagKurz("gestern", "de")).toBe("gestern");
  });
});

describe("laufAmTag", () => {
  const laeufe = [lauf("2026-09-24", 2, 80), lauf("2026-09-25", 1, 98), lauf("2026-09-25", 3, 70)];

  it("nimmt den jüngsten Ablauf des Tages", () => {
    expect(laufAmTag(laeufe, "2026-09-25")?.onTrackPercent).toBe(70);
  });

  it("findet nichts für Tage ohne ausgewerteten Ablauf", () => {
    expect(laufAmTag(laeufe, "2026-09-20")).toBeNull();
    expect(laufAmTag(null, "2026-09-25")).toBeNull();
  });

  it("vergleicht nur den Kalendertag", () => {
    expect(laufAmTag(laeufe, "2026-09-24T00:00:00")?.avgDeviationMeters).toBe(2);
  });
});

describe("tagesKurzbild", () => {
  it("zeigt bei einer ausgewerteten Fährte Abweichung und Anteil auf der Fährte", () => {
    const bild = tagesKurzbild([einheit({ hasGpsTrack: true })], lauf("2026-09-25", 1.2, 97.6), t);
    expect(bild).toBe("Fährte · Ø 1 m · 98 %");
  });

  it("zeigt eine Fährte ohne Angaben nur als Fährte", () => {
    expect(tagesKurzbild([einheit({ hasGpsTrack: true })], null, t)).toBe("Fährte");
  });

  it("stellt die Länge der Fährte voran", () => {
    const tag = [einheit({ hasGpsTrack: true, trackLengthMeters: 399.6 })];
    expect(tagesKurzbild(tag, lauf("2026-09-25", 1.2, 97.6), t)).toBe("400 m · Ø 1 m · 98 %");
  });

  it("zeigt ohne Auswertung den Untergrund, mit Auswertung nicht", () => {
    const tag = [einheit({ hasGpsTrack: true, trackLengthMeters: 312, trackSurface: "Wiese, Wald" })];
    expect(tagesKurzbild(tag, null, t)).toBe("312 m · Wiese, Wald");
    expect(tagesKurzbild(tag, lauf("2026-09-25", 1, 90), t)).toBe("312 m · Ø 1 m · 90 %");
  });

  it("nimmt Länge und Untergrund von der Einheit des Tages, die sie trägt", () => {
    const tag = [einheit({ id: "a" }), einheit({ id: "b", hasGpsTrack: true, trackLengthMeters: 250, trackSurface: "Sand" })];
    expect(tagesKurzbild(tag, null, t)).toBe("250 m · Sand");
  });

  it("lässt eine Fährte ohne bekannte Länge beim Wort Fährte", () => {
    expect(tagesKurzbild([einheit({ hasGpsTrack: true, trackLengthMeters: null, trackSurface: " " })], null, t)).toBe("Fährte");
  });

  it("zeigt die erste Übung mit Sternen und die Zahl der übrigen", () => {
    const tag = [einheit({ exercises: [uebung("Fußarbeit", 4), uebung("Sitz", 3), uebung("Platz", 5)] })];
    expect(tagesKurzbild(tag, null, t)).toBe("Fußarbeit ★★★★ · +2");
  });

  it("lässt die Sterne weg, wenn nichts bewertet ist, und zählt über Einheiten hinweg", () => {
    const tag = [einheit({ exercises: [uebung("Sitz", 0)] }), einheit({ exercises: [uebung("Platz", 2)] })];
    expect(tagesKurzbild(tag, null, t)).toBe("Sitz · +1");
  });

  it("hält die Sterne in 0 bis 5", () => {
    expect(tagesKurzbild([einheit({ exercises: [uebung("Sitz", 9)] })], null, t)).toBe("Sitz ★★★★★");
    expect(tagesKurzbild([einheit({ exercises: [uebung("Sitz", -1)] })], null, t)).toBe("Sitz");
  });

  it("verbindet Fährte und Übungen", () => {
    const tag = [einheit({ hasGpsTrack: true, exercises: [uebung("Sitz", 3)] })];
    expect(tagesKurzbild(tag, lauf("2026-09-25", 0.4, 100), t)).toBe("Fährte · Ø 0 m · 100 % · Sitz ★★★");
  });

  it("nimmt ohne Übung und Fährte die erste Zeile der Notiz, gekürzt", () => {
    expect(tagesKurzbild([einheit({ notes: "Schöner Spaziergang\nzweite Zeile" })], null, t)).toBe("Schöner Spaziergang");
    const lang = tagesKurzbild([einheit({ notes: "x".repeat(100) })], null, t);
    expect(lang).toHaveLength(60);
    expect(lang.endsWith("…")).toBe(true);
  });

  it("nimmt sonst den Ort, und sonst 'Training'", () => {
    expect(tagesKurzbild([einheit({ locationName: "Hundeplatz" })], null, t)).toBe("Hundeplatz");
    expect(tagesKurzbild([einheit()], null, t)).toBe("Training");
    expect(tagesKurzbild([einheit({ notes: "   " })], null, t)).toBe("Training");
  });
});

describe("Filter", () => {
  const faehrte = [einheit({ hasGpsTrack: true })];
  const feedback = [einheit({ trainerFeedback: "Gut" })];
  const nichts = [einheit()];

  it("passt zu Tagen der gesuchten Art", () => {
    expect(tagPasstZuFilter("faehrten", faehrte)).toBe(true);
    expect(tagPasstZuFilter("faehrten", feedback)).toBe(false);
    expect(tagPasstZuFilter("feedback", feedback)).toBe(true);
    expect(tagPasstZuFilter("feedback", nichts)).toBe(false);
    expect(tagPasstZuFilter("alle", nichts)).toBe(true);
  });

  it("genügt, wenn eine Einheit des Tages passt", () => {
    expect(tagPasstZuFilter("faehrten", [einheit(), einheit({ hasGpsTrack: true })])).toBe(true);
  });

  it("bietet nur Filter an, zu denen es Tage gibt", () => {
    expect(verfuegbareFilter([faehrte, nichts])).toEqual(["alle", "faehrten"]);
    expect(verfuegbareFilter([feedback])).toEqual(["alle", "feedback"]);
    expect(verfuegbareFilter([faehrte, feedback])).toEqual(["alle", "faehrten", "feedback"]);
  });

  it("zeigt ohne Fährte und Feedback gar keine Leiste", () => {
    expect(verfuegbareFilter([nichts, nichts])).toEqual([]);
    expect(verfuegbareFilter([])).toEqual([]);
  });

  it("fällt auf 'alle' zurück, wenn der gewählte Filter nicht mehr angeboten wird", () => {
    expect(geltenderFilter("faehrten", ["alle", "faehrten"])).toBe("faehrten");
    expect(geltenderFilter("faehrten", ["alle", "feedback"])).toBe("alle");
    expect(geltenderFilter("feedback", [])).toBe("alle");
  });
});

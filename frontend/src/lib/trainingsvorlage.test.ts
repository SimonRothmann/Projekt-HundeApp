import { describe, expect, it } from "vitest";
import { emptyRow, letzteSportart, zeilenAusEinheit, VORGABE_BEWERTUNG } from "./trainingsvorlage";
import type { TrainingExercise, TrainingSession } from "@/lib/types";

function uebung(teil: Partial<TrainingExercise>): TrainingExercise {
  return {
    id: "te-1",
    exerciseId: "ex-1",
    exerciseName: "Fußarbeit",
    rating: 5,
    difficulty: 0,
    success: true,
    notes: "lief gut",
    trainingPlanItemId: "plan-1",
    trainerRating: null,
    trainerNote: null,
    ...teil,
  };
}

function einheit(uebungen: TrainingExercise[]): TrainingSession {
  return {
    id: "s-1",
    dogId: "d-1",
    date: "2026-09-01",
    durationMinutes: 45,
    notes: "Platz war nass",
    exercises: uebungen,
    trainerFeedback: null,
    feedbackAt: null,
    startTime: null,
    latitude: null,
    longitude: null,
    locationName: null,
    temperatureC: null,
    relativeHumidity: null,
    windSpeedKmh: null,
    weatherCode: null,
    condition: null,
    hasGpsTrack: false,
  };
}

describe("letzteSportart", () => {
  it("nimmt die Sportart der letzten Zeile", () => {
    expect(letzteSportart([emptyRow("igp"), emptyRow("agility")])).toBe("agility");
  });

  it("übergeht Freitext-Zeilen, die gar keine Sportart tragen", () => {
    // Sonst stünde die Sportart nach einem eingetragenen Spaziergang wieder
    // leer da - genau der Tipper, den das Übernehmen sparen soll.
    const zeilen = [emptyRow("igp"), { ...emptyRow(), isFreeText: true, freeText: "Spaziergang" }];

    expect(letzteSportart(zeilen)).toBe("igp");
  });

  it("liefert leer, wenn noch keine Sportart gewählt wurde", () => {
    expect(letzteSportart([emptyRow()])).toBe("");
    expect(letzteSportart([])).toBe("");
  });
});

describe("zeilenAusEinheit", () => {
  const sportVonUebung = { "ex-1": "igp", "ex-2": "igp" };

  it("übernimmt den Übungssatz samt Sportart", () => {
    const zeilen = zeilenAusEinheit(
      einheit([uebung({}), uebung({ id: "te-2", exerciseId: "ex-2", exerciseName: "Voraus" })]),
      sportVonUebung,
    );

    expect(zeilen.map((z) => z.exerciseId)).toEqual(["ex-1", "ex-2"]);
    expect(zeilen.map((z) => z.sportId)).toEqual(["igp", "igp"]);
  });

  it("übernimmt die Bewertung NICHT, sondern setzt sie zurück", () => {
    // Der wichtigste Punkt der ganzen Vorlage: die Bewertung ist der eine
    // Wert, der jedes Mal ein anderer ist, und der adaptive Plangenerator
    // erkennt an ihr die Schwächen. Mitgeschleppt wäre sie eine Falschangabe.
    const zeilen = zeilenAusEinheit(einheit([uebung({ rating: 5 })]), sportVonUebung);

    expect(zeilen[0].rating).toBe(VORGABE_BEWERTUNG);
    expect(zeilen[0].rating).not.toBe(5);
  });

  it("übernimmt weder Notiz noch Plan-Zuordnung der alten Einheit", () => {
    const zeilen = zeilenAusEinheit(einheit([uebung({ notes: "lief gut", trainingPlanItemId: "plan-1" })]), sportVonUebung);

    expect(zeilen[0].notes).toBe("");
    expect(zeilen[0].trainingPlanItemId).toBe("");
  });

  it("macht aus einer Freitext-Übung wieder eine Freitext-Zeile", () => {
    const zeilen = zeilenAusEinheit(
      einheit([uebung({ exerciseId: null, exerciseName: "Spaziergang mit Bällchenspiel" })]),
      sportVonUebung,
    );

    expect(zeilen[0].isFreeText).toBe(true);
    expect(zeilen[0].freeText).toBe("Spaziergang mit Bällchenspiel");
    expect(zeilen[0].exerciseId).toBe("");
  });

  it("lässt die Sportart leer, wenn die Übung keiner zugeordnet werden kann", () => {
    // Kommt vor, wenn der Hund die Sportart inzwischen nicht mehr führt. Die
    // Zeile bleibt dann von Hand ausfüllbar, statt zu verschwinden.
    const zeilen = zeilenAusEinheit(einheit([uebung({ exerciseId: "ex-fremd" })]), sportVonUebung);

    expect(zeilen[0].sportId).toBe("");
    expect(zeilen[0].exerciseId).toBe("ex-fremd");
  });

  it("liefert eine leere Zeile statt gar keiner", () => {
    expect(zeilenAusEinheit(einheit([]), sportVonUebung)).toEqual([emptyRow()]);
  });
});

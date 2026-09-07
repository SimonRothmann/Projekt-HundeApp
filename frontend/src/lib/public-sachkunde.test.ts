import { describe, expect, it } from "vitest";
import { nachAbschnitten } from "./public-sachkunde";
import type { QuizQuestion } from "@/lib/types";

function frage(number: string, section: string, sectionName: string): QuizQuestion {
  return {
    id: `${section}-${number}`,
    number,
    section,
    sectionName,
    kind: "SingleChoice",
    text: "Welche Aussage ist richtig?",
    imageName: null,
    sampleSolution: null,
    options: [],
    terms: [],
    keys: [],
    state: null,
  };
}

describe("nachAbschnitten", () => {
  it("bündelt die Fragen eines Abschnitts unter einer Überschrift", () => {
    const abschnitte = nachAbschnitten([
      frage("A1", "A", "Verhalten und Umgang mit dem Hund"),
      frage("B1", "B", "Zucht, Aufzucht und Gesundheit"),
      frage("A2", "A", "Verhalten und Umgang mit dem Hund"),
    ]);

    expect(abschnitte.map((a) => a.key)).toEqual(["A", "B"]);
    expect(abschnitte[0].fragen.map((f) => f.number)).toEqual(["A1", "A2"]);
    expect(abschnitte[0].name).toBe("Verhalten und Umgang mit dem Hund");
  });

  it("behält die Reihenfolge des Katalogs bei, statt alphabetisch zu sortieren", () => {
    // Der Jugendkatalog führt nur einen Abschnitt, der Erwachsenenkatalog fünf
    // in der Reihenfolge der Prüfungsvorlage - wer eine Frage im Original
    // nachschlägt, sucht sie an derselben Stelle.
    const abschnitte = nachAbschnitten([
      frage("D1", "D", "Kynologie, Verbände und Ausbildung"),
      frage("A1", "A", "Verhalten und Umgang mit dem Hund"),
    ]);

    expect(abschnitte.map((a) => a.key)).toEqual(["D", "A"]);
  });

  it("liefert nichts zurück, wenn es keine Fragen gibt", () => {
    expect(nachAbschnitten([])).toEqual([]);
  });
});

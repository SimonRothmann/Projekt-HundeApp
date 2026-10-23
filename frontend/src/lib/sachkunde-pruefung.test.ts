import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  alsAbgabe,
  antwortUmschalten,
  anzahlOffen,
  balkenBreite,
  dauerText,
  falscheFragen,
  istBeantwortet,
  istGueltigerStand,
  leereAntwort,
  leseStand,
  loescheStand,
  schreibeStand,
  zuordnungUmschalten,
  type PruefungsStand,
} from "./sachkunde-pruefung";
import type { QuizExam, QuizExamQuestion, QuizExamResult } from "./types";

function frage(id: string, teil: Partial<QuizExamQuestion> = {}): QuizExamQuestion {
  return {
    id,
    number: id.toUpperCase(),
    section: "A",
    sectionName: "Verhalten",
    kind: "SingleChoice",
    text: `Frage ${id}?`,
    imageName: null,
    options: [
      { id: `${id}-a`, text: "a", imageName: null },
      { id: `${id}-b`, text: "b", imageName: null },
    ],
    terms: [],
    keys: [],
    ...teil,
  };
}

const EXAM: QuizExam = {
  catalogCode: "SWHV-BHVT-ERW",
  questionCount: 3,
  maxPoints: 6,
  passPoints: 5,
  passPercent: 70,
  questions: [
    frage("f1"),
    frage("f2"),
    frage("f3", {
      kind: "Assignment",
      options: [],
      terms: [
        { id: "t1", text: "Boxer" },
        { id: "t2", text: "Basset" },
      ],
      keys: [
        { key: "C", label: null },
        { key: "E", label: null },
      ],
    }),
  ],
};

describe("istBeantwortet / anzahlOffen", () => {
  it("eine Frage ohne Eintrag oder mit leerer Auswahl ist offen", () => {
    expect(istBeantwortet(undefined)).toBe(false);
    expect(istBeantwortet(leereAntwort("f1"))).toBe(false);
    expect(anzahlOffen(EXAM, {})).toBe(3);
  });

  it("ein Kreuz oder eine einzige Zuordnung genügt", () => {
    const antworten = {
      f1: { ...leereAntwort("f1"), selectedOptionIds: ["f1-a"] },
      f3: { ...leereAntwort("f3"), assignments: { t1: "E" } },
    };
    expect(istBeantwortet(antworten.f1)).toBe(true);
    expect(istBeantwortet(antworten.f3)).toBe(true);
    expect(anzahlOffen(EXAM, antworten)).toBe(1);
  });
});

describe("antwortUmschalten", () => {
  it("Erwachsene können mehreres ankreuzen", () => {
    let antwort = leereAntwort("f1");
    antwort = antwortUmschalten(antwort, "f1-a", false);
    antwort = antwortUmschalten(antwort, "f1-b", false);
    expect(antwort.selectedOptionIds).toEqual(["f1-a", "f1-b"]);
  });

  it("Jugend: ein neuer Tipp ersetzt die Auswahl", () => {
    let antwort = leereAntwort("f1");
    antwort = antwortUmschalten(antwort, "f1-a", true);
    antwort = antwortUmschalten(antwort, "f1-b", true);
    expect(antwort.selectedOptionIds).toEqual(["f1-b"]);
  });

  it("derselbe Tipp noch einmal nimmt das Kreuz weg - einzeln wie mehrfach", () => {
    const einzeln = antwortUmschalten(antwortUmschalten(leereAntwort("f1"), "f1-a", true), "f1-a", true);
    const mehrfach = antwortUmschalten(antwortUmschalten(leereAntwort("f1"), "f1-a", false), "f1-a", false);
    expect(einzeln.selectedOptionIds).toEqual([]);
    expect(mehrfach.selectedOptionIds).toEqual([]);
  });

  it("verändert die übergebene Antwort nicht", () => {
    const vorher = leereAntwort("f1");
    antwortUmschalten(vorher, "f1-a", false);
    expect(vorher.selectedOptionIds).toEqual([]);
  });
});

describe("zuordnungUmschalten", () => {
  it("setzt, ändert und nimmt zurück", () => {
    let antwort = leereAntwort("f3");
    antwort = zuordnungUmschalten(antwort, "t1", "E");
    expect(antwort.assignments).toEqual({ t1: "E" });
    antwort = zuordnungUmschalten(antwort, "t1", "C");
    expect(antwort.assignments).toEqual({ t1: "C" });
    antwort = zuordnungUmschalten(antwort, "t1", "C");
    expect(antwort.assignments).toEqual({});
  });
});

describe("alsAbgabe", () => {
  it("schickt eine Antwort je Frage in Reihenfolge der Prüfung, unbeantwortete leer", () => {
    const abgabe = alsAbgabe(EXAM, {
      f3: { ...leereAntwort("f3"), assignments: { t1: "E" } },
      f1: { ...leereAntwort("f1"), selectedOptionIds: ["f1-b"] },
    });

    expect(abgabe.answers.map((a) => a.questionId)).toEqual(["f1", "f2", "f3"]);
    expect(abgabe.answers[0].selectedOptionIds).toEqual(["f1-b"]);
    expect(abgabe.answers[1]).toEqual({ questionId: "f2", selectedOptionIds: [], assignments: {} });
    expect(abgabe.answers[2].assignments).toEqual({ t1: "E" });
  });
});

describe("dauerText", () => {
  it("zeigt Minuten und Sekunden, ab einer Stunde auch Stunden", () => {
    expect(dauerText(0)).toBe("0:00");
    expect(dauerText(247_000)).toBe("4:07");
    expect(dauerText(3_750_000)).toBe("1:02:30");
  });

  it("rundet Bruchteile ab und verträgt eine zurückgestellte Uhr", () => {
    expect(dauerText(59_999)).toBe("0:59");
    expect(dauerText(-5_000)).toBe("0:00");
  });
});

describe("falscheFragen", () => {
  const ergebnis: QuizExamResult = {
    points: 2,
    maxPoints: 6,
    passPoints: 5,
    passPercent: 70,
    percent: 33,
    passed: false,
    questionCount: 3,
    correctCount: 1,
    saved: false,
    sections: [],
    questions: [
      { questionId: "f1", correct: true, points: 2, maxPoints: 2, correctOptionIds: ["f1-a"], correctAssignments: {} },
      { questionId: "f2", correct: false, points: 0, maxPoints: 2, correctOptionIds: ["f2-a"], correctAssignments: {} },
      { questionId: "f3", correct: false, points: 0, maxPoints: 2, correctOptionIds: [], correctAssignments: { t1: "E", t2: "C" } },
    ],
  };

  it("liefert nur die Fragen ohne volle Punktzahl, mit eigener Antwort", () => {
    const antworten = { f2: { ...leereAntwort("f2"), selectedOptionIds: ["f2-b"] } };

    const falsch = falscheFragen(EXAM, antworten, ergebnis);

    expect(falsch.map((f) => f.frage.id)).toEqual(["f2", "f3"]);
    expect(falsch[0].antwort.selectedOptionIds).toEqual(["f2-b"]);
    // Unbeantwortet bekommt eine leere Antwort statt undefined.
    expect(falsch[1].antwort).toEqual(leereAntwort("f3"));
  });
});

describe("balkenBreite", () => {
  it("rechnet Prozent und hält sich in 0 bis 100", () => {
    expect(balkenBreite(8, 16)).toBe(50);
    expect(balkenBreite(0, 0)).toBe(0);
    expect(balkenBreite(20, 16)).toBe(100);
    expect(balkenBreite(-2, 16)).toBe(0);
  });
});

describe("laufende Prüfung im Browserspeicher", () => {
  const speicher = new Map<string, string>();

  beforeEach(() => {
    speicher.clear();
    vi.stubGlobal("window", {
      sessionStorage: {
        getItem: (k: string) => speicher.get(k) ?? null,
        setItem: (k: string, v: string) => void speicher.set(k, v),
        removeItem: (k: string) => void speicher.delete(k),
      },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  const stand: PruefungsStand = {
    exam: EXAM,
    antworten: { f1: { ...leereAntwort("f1"), selectedOptionIds: ["f1-a"] } },
    index: 1,
    gestartetAm: 1_000,
  };

  it("übersteht ein Neuladen: schreiben, lesen, löschen", () => {
    schreibeStand("SWHV-BHVT-ERW", stand);

    expect(leseStand("SWHV-BHVT-ERW")).toEqual(stand);
    // Katalogcode egal in welcher Schreibweise (die Adresse ist klein geschrieben).
    expect(leseStand("swhv-bhvt-erw")).toEqual(stand);

    loescheStand("SWHV-BHVT-ERW");
    expect(leseStand("SWHV-BHVT-ERW")).toBeNull();
  });

  it("trennt die Kataloge", () => {
    schreibeStand("SWHV-BHVT-ERW", stand);

    expect(leseStand("SWHV-BHVT-JGD")).toBeNull();
  });

  it("kaputter Inhalt ergibt keine Prüfung statt eines Absturzes", () => {
    speicher.set("dogity.sachkunde.pruefung.swhv-bhvt-erw", "{kaputt");
    expect(leseStand("SWHV-BHVT-ERW")).toBeNull();

    speicher.set("dogity.sachkunde.pruefung.swhv-bhvt-erw", JSON.stringify({ exam: { questions: [] } }));
    expect(leseStand("SWHV-BHVT-ERW")).toBeNull();
  });

  it("gesperrter Speicher wirft nicht", () => {
    vi.stubGlobal("window", {
      get sessionStorage(): never {
        throw new Error("gesperrt");
      },
    });

    expect(() => schreibeStand("SWHV-BHVT-ERW", stand)).not.toThrow();
    expect(() => loescheStand("SWHV-BHVT-ERW")).not.toThrow();
    expect(leseStand("SWHV-BHVT-ERW")).toBeNull();
  });

});

describe("istGueltigerStand", () => {
  const gut: PruefungsStand = { exam: EXAM, antworten: {}, index: 0, gestartetAm: 1 };

  it("nimmt einen vollständigen Stand an", () => {
    expect(istGueltigerStand(gut, "SWHV-BHVT-ERW")).toBe(true);
  });

  it("weist falsche Kataloge, falsche Indizes und Unsinn ab", () => {
    expect(istGueltigerStand(gut, "SWHV-BHVT-JGD")).toBe(false);
    expect(istGueltigerStand({ ...gut, index: 3 }, "SWHV-BHVT-ERW")).toBe(false);
    expect(istGueltigerStand({ ...gut, index: -1 }, "SWHV-BHVT-ERW")).toBe(false);
    expect(istGueltigerStand({ ...gut, index: 0.5 }, "SWHV-BHVT-ERW")).toBe(false);
    expect(istGueltigerStand({ ...gut, gestartetAm: "gestern" }, "SWHV-BHVT-ERW")).toBe(false);
    expect(istGueltigerStand(null, "SWHV-BHVT-ERW")).toBe(false);
    expect(istGueltigerStand("text", "SWHV-BHVT-ERW")).toBe(false);
    expect(istGueltigerStand({ ...gut, exam: { ...EXAM, questions: [{ id: 5 }] } }, "SWHV-BHVT-ERW")).toBe(false);
  });
});

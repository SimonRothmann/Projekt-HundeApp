import { describe, expect, it } from "vitest";
import {
  adresseMit,
  aktiveHunde,
  ankerWunsch,
  geltenderReiter,
  hundeAdresse,
  laufendesZiel,
  reiterAusParameter,
  ungeseheneRueckmeldungen,
  waehleReiter,
  zeigeHundeChips,
  zielStatus,
} from "./hundeseite";
import { feedbackKennung } from "./feedback-gesehen";
import type { Dog, Goal, TrainingPlanItem, TrainingSession } from "./types";

const JETZT = Date.parse("2026-10-08T10:00:00");

function posten(woche: number, erledigt: boolean): TrainingPlanItem {
  return {
    id: `${woche}-${erledigt}-${Math.random()}`, weekNumber: woche, exerciseId: null, exerciseName: "Sitz", freeTextLabel: null,
    repetitionsTarget: 1, isRestWeek: false, completedCount: erledigt ? 1 : 0, isComplete: erledigt, logs: [], reason: null, dayIndex: 0,
  };
}

function ziel(optionen: Partial<Goal> = {}): Goal {
  return {
    id: "g", dogId: "d", sportId: "s", sportName: "Begleithund", regulationId: null, regulationName: null,
    targetDate: "2026-12-01", status: 0, notes: null, isCustom: false, weeklyExerciseCount: 2, trainingDaysPerWeek: 2,
    weekConfigs: [], trainingPlan: null, planManagedByTrainer: false, ...optionen,
  };
}

function hund(id: string, archiviert = false): Dog {
  return { id, name: id, breed: null, birthday: null, gender: 0, imageUrl: null, notes: null, archivedAt: archiviert ? "2026-01-01" : null, hasImage: false };
}

describe("reiterAusParameter", () => {
  it("kennt genau die zwei Reiter", () => {
    expect(reiterAusParameter("plan")).toBe("plan");
    expect(reiterAusParameter("tagebuch")).toBe("tagebuch");
  });

  it("macht aus allem anderen 'keine Angabe'", () => {
    for (const wert of [null, undefined, "", "Plan", "tagebuch ", "diary", "0"]) {
      expect(reiterAusParameter(wert)).toBeNull();
    }
  });
});

describe("laufendesZiel", () => {
  it("nimmt das aktive Ziel mit dem nächsten Datum", () => {
    const naechstes = ziel({ id: "bald", targetDate: "2026-11-01" });
    expect(laufendesZiel([ziel({ id: "spaeter" }), naechstes], JETZT)?.id).toBe("bald");
  });

  it("überspringt abgelaufene, erreichte und abgebrochene Ziele", () => {
    const liste = [
      ziel({ targetDate: "2026-10-01" }),
      ziel({ status: 1 }),
      ziel({ status: 2 }),
    ];
    expect(laufendesZiel(liste, JETZT)).toBeNull();
  });

  it("zählt den Prüfungstag selbst noch mit", () => {
    expect(laufendesZiel([ziel({ targetDate: "2026-10-08" })], JETZT)).not.toBeNull();
  });

  it("kommt mit fehlenden Zielen zurecht", () => {
    expect(laufendesZiel(null, JETZT)).toBeNull();
    expect(laufendesZiel(undefined, JETZT)).toBeNull();
    expect(laufendesZiel([], JETZT)).toBeNull();
  });
});

describe("geltenderReiter", () => {
  it("lässt einen gültigen Parameter gewinnen", () => {
    expect(geltenderReiter("tagebuch", "plan", [ziel()], { jetzt: JETZT })).toBe("tagebuch");
  });

  it("hält den eingefrorenen Standard, auch wenn das laufende Ziel verschwindet", () => {
    // Eingefroren als "plan"; das einzige Ziel ist inzwischen erreicht.
    expect(geltenderReiter(null, "plan", [ziel({ status: 1 })], { jetzt: JETZT })).toBe("plan");
    // Und umgekehrt: kein Sprung auf den Plan, wenn ein Ziel dazukommt.
    expect(geltenderReiter("ungültig", "tagebuch", [ziel()], { jetzt: JETZT })).toBe("tagebuch");
  });

  it("rechnet bis zum Einfrieren aus den Zielen", () => {
    expect(geltenderReiter(null, null, [ziel()], { jetzt: JETZT })).toBe("plan");
    expect(geltenderReiter(null, null, [], { jetzt: JETZT })).toBe("tagebuch");
    expect(geltenderReiter(null, null, [ziel()], { eintrag: true, jetzt: JETZT })).toBe("tagebuch");
  });
});

describe("waehleReiter", () => {
  const laufend = [ziel()];
  const abgelaufen = [ziel({ targetDate: "2026-10-01" })];

  it("folgt einem gültigen Parameter, was immer sonst gilt", () => {
    expect(waehleReiter("tagebuch", laufend, { jetzt: JETZT })).toBe("tagebuch");
    expect(waehleReiter("plan", [], { jetzt: JETZT })).toBe("plan");
    expect(waehleReiter("plan", abgelaufen, { eintrag: true, jetzt: JETZT })).toBe("plan");
  });

  it("beginnt ohne Parameter beim Plan, wenn der Hund ein laufendes Ziel hat", () => {
    expect(waehleReiter(null, laufend, { jetzt: JETZT })).toBe("plan");
  });

  it("beginnt sonst beim Tagebuch - ohne Ziel, mit abgelaufenem Ziel, ohne geladene Ziele", () => {
    expect(waehleReiter(null, [], { jetzt: JETZT })).toBe("tagebuch");
    expect(waehleReiter(undefined, abgelaufen, { jetzt: JETZT })).toBe("tagebuch");
    expect(waehleReiter(null, null, { jetzt: JETZT })).toBe("tagebuch");
  });

  it("öffnet bei einem Link auf einen Eintrag das Tagebuch, auch mit laufendem Ziel", () => {
    expect(waehleReiter(null, laufend, { eintrag: true, jetzt: JETZT })).toBe("tagebuch");
  });

  it("behandelt ungültige Werte wie keinen Parameter", () => {
    expect(waehleReiter("quatsch", laufend, { jetzt: JETZT })).toBe("plan");
    expect(waehleReiter("", [], { jetzt: JETZT })).toBe("tagebuch");
    expect(waehleReiter("PLAN", [], { jetzt: JETZT })).toBe("tagebuch");
  });
});

describe("ankerWunsch", () => {
  it("ordnet die drei alten Anker zu", () => {
    expect(ankerWunsch("#trainingsplan")).toEqual({ reiter: "plan", formular: false, faehrte: false });
    expect(ankerWunsch("#training-erfassen")).toEqual({ reiter: "tagebuch", formular: true, faehrte: false });
    expect(ankerWunsch("#faehrte-aufnehmen")).toEqual({ reiter: null, formular: false, faehrte: true });
  });

  it("nimmt das Fragment mit und ohne Raute", () => {
    expect(ankerWunsch("trainingsplan")?.reiter).toBe("plan");
  });

  it("kennt sonst nichts", () => {
    for (const hash of [null, undefined, "", "#", "#leistungen", "#ziel-formular", "#Trainingsplan"]) {
      expect(ankerWunsch(hash)).toBeNull();
    }
  });
});

describe("adresseMit", () => {
  it("setzt einen Parameter und behält die übrigen", () => {
    expect(adresseMit("/dogs/1", "from=%2Ftrainer", { tab: "plan" })).toBe("/dogs/1?from=%2Ftrainer&tab=plan");
  });

  it("überschreibt einen vorhandenen Wert", () => {
    expect(adresseMit("/dogs/1", "tab=plan", { tab: "tagebuch" })).toBe("/dogs/1?tab=tagebuch");
  });

  it("entfernt mit null und lässt bei leerem Rest das Fragezeichen weg", () => {
    expect(adresseMit("/dogs/1", "eintrag=abc", { eintrag: null })).toBe("/dogs/1");
  });

  it("lässt ohne Änderung alles, wie es war", () => {
    expect(adresseMit("/dogs/1", "tab=plan", {})).toBe("/dogs/1?tab=plan");
  });
});

describe("Hunde-Chips", () => {
  it("zählt nur aktive Hunde", () => {
    expect(aktiveHunde([hund("a"), hund("b", true), hund("c")]).map((d) => d.id)).toEqual(["a", "c"]);
    expect(aktiveHunde(null)).toEqual([]);
  });

  it("erscheinen erst ab zwei aktiven Hunden", () => {
    expect(zeigeHundeChips([hund("a")], true)).toBe(false);
    expect(zeigeHundeChips([hund("a"), hund("b", true)], true)).toBe(false);
    expect(zeigeHundeChips([hund("a"), hund("b")], true)).toBe(true);
    expect(zeigeHundeChips(undefined, true)).toBe(false);
  });

  it("erscheinen nicht auf der Seite eines fremden Hundes", () => {
    expect(zeigeHundeChips([hund("a"), hund("b")], false)).toBe(false);
  });

  it("führen auf die Seite des anderen Hundes und behalten Reiter und Zurück-Ziel, nicht den Eintrag", () => {
    expect(hundeAdresse("neu", "tab=tagebuch&eintrag=x&from=%2Ftrainer")).toBe("/dogs/neu?tab=tagebuch&from=%2Ftrainer");
    expect(hundeAdresse("neu", "eintrag=x")).toBe("/dogs/neu");
    expect(hundeAdresse("neu", "")).toBe("/dogs/neu");
  });
});

describe("zielStatus", () => {
  it("nennt Prüfung, Woche und Stand der laufenden Woche", () => {
    const plan = {
      id: "p", generatedAt: "2026-10-01T00:00:00",
      items: [posten(1, true), posten(2, true), posten(2, false), posten(2, false)],
    };
    const status = zielStatus([ziel({ regulationName: "BH", trainingPlan: plan })], JETZT);
    expect(status).toEqual({ name: "BH", woche: { woche: 2, erledigt: 1, geplant: 3 }, tage: 54 });
  });

  it("nennt ohne Plan nur Prüfung und Tage", () => {
    expect(zielStatus([ziel()], JETZT)).toEqual({ name: "Begleithund", woche: null, tage: 54 });
  });

  it("schweigt ohne laufendes Ziel", () => {
    expect(zielStatus([], JETZT)).toBeNull();
    expect(zielStatus([ziel({ targetDate: "2026-10-01" })], JETZT)).toBeNull();
  });
});

describe("ungeseheneRueckmeldungen", () => {
  function eintrag(id: string, feedback: string | null): TrainingSession {
    return { id, trainerFeedback: feedback, feedbackAt: feedback ? "2026-10-07T10:00:00Z" : null } as TrainingSession;
  }

  it("findet Feedback, das nicht in der gesehen-Liste steht", () => {
    const liste = [eintrag("a", "Gut"), eintrag("b", "Prima"), eintrag("c", null)];
    const gesehen = new Set([feedbackKennung("b", "2026-10-07T10:00:00Z")]);
    expect(ungeseheneRueckmeldungen(liste, gesehen, true).map((s) => s.id)).toEqual(["a"]);
  });

  it("zeigt der Trainer:in nichts - sie hat es selbst geschrieben", () => {
    expect(ungeseheneRueckmeldungen([eintrag("a", "Gut")], new Set(), false)).toEqual([]);
  });

  it("kommt mit fehlender Liste zurecht", () => {
    expect(ungeseheneRueckmeldungen(null, new Set(), true)).toEqual([]);
  });
});

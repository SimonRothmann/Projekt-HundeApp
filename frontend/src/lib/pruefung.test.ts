import { describe, expect, it } from "vitest";
import {
  datumKurz,
  datumMitWochentag,
  heuteIso,
  leistungen,
  morgenIso,
  naechstesZiel,
  pruefungsName,
  punkteText,
  tageBisPruefung,
  vorgabePruefungstag,
} from "./pruefung";
import type { Goal } from "./types";

// Mittags Ortszeit: liegt auch bei Zeitzonen-Sprüngen sicher am selben Kalendertag.
const JETZT = new Date(2026, 8, 30, 12, 0, 0).getTime(); // 30.09.2026

function ziel(teil: Partial<Goal>): Goal {
  return {
    id: "g",
    dogId: "d",
    sportId: "s",
    sportName: "IGP",
    regulationId: null,
    regulationName: null,
    targetDate: "2026-12-01",
    status: 0,
    notes: null,
    isCustom: false,
    weeklyExerciseCount: 3,
    trainingDaysPerWeek: 2,
    weekConfigs: [],
    trainingPlan: null,
    planManagedByTrainer: false,
    ...teil,
  };
}

// Schlichter Übersetzer wie im Produktivcode: Platzhalter einsetzen.
const t = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (treffer, name: string) => (werte && name in werte ? String(werte[name]) : treffer));

describe("tageBisPruefung", () => {
  it("zählt Kalendertage, nicht Stunden", () => {
    expect(tageBisPruefung("2026-10-03", JETZT)).toBe(3);
    // Spätabends ändert sich nichts: noch derselbe Kalendertag.
    expect(tageBisPruefung("2026-10-03", new Date(2026, 8, 30, 23, 59).getTime())).toBe(3);
    expect(tageBisPruefung("2026-10-03", new Date(2026, 8, 30, 0, 1).getTime())).toBe(3);
  });

  it("liefert 0 für heute und negative Werte für vergangene Termine", () => {
    expect(tageBisPruefung("2026-09-30", JETZT)).toBe(0);
    expect(tageBisPruefung("2026-09-28", JETZT)).toBe(-2);
  });

  it("rechnet über Monats- und Jahresgrenzen und die Sommerzeit", () => {
    expect(tageBisPruefung("2026-11-01", JETZT)).toBe(32); // Zeitumstellung am 25.10. dazwischen
    expect(tageBisPruefung("2027-01-01", JETZT)).toBe(93);
  });

  it("nimmt auch ein ISO-Datum mit Uhrzeit-Anhang", () => {
    expect(tageBisPruefung("2026-10-03T00:00:00", JETZT)).toBe(3);
  });
});

describe("vorgabePruefungstag", () => {
  it("nimmt das Zieldatum, wenn es in der Vergangenheit liegt", () => {
    expect(vorgabePruefungstag("2026-09-25", JETZT)).toBe("2026-09-25");
  });

  it("nimmt heute, wenn der Termin noch bevorsteht", () => {
    expect(vorgabePruefungstag("2026-10-15", JETZT)).toBe("2026-09-30");
  });

  it("nimmt heute, wenn der Termin heute ist", () => {
    expect(vorgabePruefungstag("2026-09-30", JETZT)).toBe("2026-09-30");
  });
});

describe("heuteIso und morgenIso", () => {
  it("schreibt Monat und Tag zweistellig", () => {
    expect(heuteIso(new Date(2026, 0, 5, 12).getTime())).toBe("2026-01-05");
  });

  it("rollt am Monatsende um", () => {
    expect(morgenIso(JETZT)).toBe("2026-10-01");
  });
});

describe("naechstesZiel", () => {
  it("wählt unter den aktiven das mit dem nächsten Zieldatum", () => {
    const ziele = [
      ziel({ id: "spaet", targetDate: "2027-03-01" }),
      ziel({ id: "bald", targetDate: "2026-11-15" }),
      ziel({ id: "erreicht", targetDate: "2026-01-01", status: 1 }),
      ziel({ id: "abgebrochen", targetDate: "2026-02-01", status: 2 }),
    ];
    expect(naechstesZiel(ziele)?.id).toBe("bald");
  });

  it("zählt ein überschrittenes Datum mit - es wartet auf sein Ergebnis", () => {
    const ziele = [ziel({ id: "neu", targetDate: "2026-12-01" }), ziel({ id: "vorbei", targetDate: "2026-09-20" })];
    expect(naechstesZiel(ziele)?.id).toBe("vorbei");
  });

  it("liefert null ohne aktives Ziel", () => {
    expect(naechstesZiel([ziel({ status: 1 }), ziel({ status: 2 })])).toBeNull();
    expect(naechstesZiel([])).toBeNull();
  });
});

describe("leistungen", () => {
  it("zeigt nur erreichte Ziele, neueste Prüfung zuerst, ohne Datum ans Ende", () => {
    const ziele = [
      ziel({ id: "alt", status: 1, examDate: "2025-05-01" }),
      ziel({ id: "offen", status: 0, examDate: null }),
      ziel({ id: "ohneA", status: 1, examDate: null, targetDate: "2024-01-01" }),
      ziel({ id: "neu", status: 1, examDate: "2026-09-19" }),
      ziel({ id: "ohneB", status: 1, targetDate: "2025-01-01" }),
      ziel({ id: "abgebrochen", status: 2 }),
    ];
    expect(leistungen(ziele).map((z) => z.id)).toEqual(["neu", "alt", "ohneB", "ohneA"]);
  });

  it("verändert die übergebene Liste nicht", () => {
    const ziele = [ziel({ id: "a", status: 1, examDate: "2025-01-01" }), ziel({ id: "b", status: 1, examDate: "2026-01-01" })];
    leistungen(ziele);
    expect(ziele.map((z) => z.id)).toEqual(["a", "b"]);
  });
});

describe("Anzeigetexte", () => {
  it("nennt die Prüfungsordnung, sonst die Sportart", () => {
    expect(pruefungsName({ regulationName: "FCI-IGP 1", sportName: "IGP" })).toBe("FCI-IGP 1");
    expect(pruefungsName({ regulationName: null, sportName: "Agility" })).toBe("Agility");
  });

  it("schreibt Punkte mit Höchstpunktzahl, wenn sie bekannt ist", () => {
    expect(punkteText(t, 272, 300)).toBe("272 von 300 Punkten");
    expect(punkteText(t, 272, null)).toBe("272 Punkte");
    expect(punkteText(t, 0, undefined)).toBe("0 Punkte");
  });

  it("formatiert Kalendertage ohne Zeitzonenverschiebung", () => {
    expect(datumKurz("2026-10-03")).toBe("03.10.2026");
    expect(datumMitWochentag("2026-10-03")).toBe("Samstag, 3. Oktober");
    expect(datumMitWochentag("2026-10-03", "en")).toContain("Saturday");
  });
});

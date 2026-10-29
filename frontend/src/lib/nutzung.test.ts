import { describe, expect, it } from "vitest";
import { uebersetze } from "./i18n";
import { funktionsName, groessterWert, nutzungStatus, sortiereFunktionen, wochentagKurz } from "./nutzung";
import type { AdminFeatureUsage } from "./types";

function funktion(key: string, konten30: number, eintraege30: number | null, konten: number): AdminFeatureUsage {
  return { key, accountsLast30Days: konten30, entriesLast30Days: eintraege30, accountsEver: konten, lastUsed: null };
}

describe("nutzungStatus", () => {
  it("unterscheidet aktiv, ruhend (nur früher) und nie genutzt", () => {
    expect(nutzungStatus(funktion("a", 2, 5, 3))).toBe("aktiv");
    expect(nutzungStatus(funktion("b", 0, 0, 3))).toBe("ruhend");
    expect(nutzungStatus(funktion("c", 0, 0, 0))).toBe("nie");
  });
});

describe("sortiereFunktionen", () => {
  it("ordnet nach Konten der letzten 30 Tage, dann Einträgen, dann Konten insgesamt", () => {
    const liste = [
      funktion("nie", 0, 0, 0),
      funktion("ruhend", 0, 0, 4),
      funktion("viele", 9, 20, 9),
      funktion("gleich-wenig", 3, 3, 3),
      funktion("gleich-viel", 3, 10, 3),
    ];

    expect(sortiereFunktionen(liste).map((f) => f.key)).toEqual(["viele", "gleich-viel", "gleich-wenig", "ruhend", "nie"]);
  });

  it("verändert die übergebene Liste nicht und kommt mit fehlenden Einträgen (Push) zurecht", () => {
    const liste = [funktion("push", 1, null, 1), funktion("training", 1, 4, 1)];

    expect(sortiereFunktionen(liste).map((f) => f.key)).toEqual(["training", "push"]);
    expect(liste.map((f) => f.key)).toEqual(["push", "training"]);
  });
});

describe("funktionsName", () => {
  it("kennt die Schlüssel des Servers und lässt unbekannte unverändert", () => {
    expect(uebersetze("en", funktionsName("track-laid"))).toBe("Lay the track");
    expect(funktionsName("etwas-neues")).toBe("etwas-neues");
  });
});

describe("groessterWert", () => {
  it("liefert das Maximum, bei leerer Reihe 0", () => {
    expect(groessterWert([1, 7, 3])).toBe(7);
    expect(groessterWert([])).toBe(0);
  });
});

describe("wochentagKurz", () => {
  it("beginnt mit Montag", () => {
    expect(wochentagKurz(0, "de")).toMatch(/^Mo/);
    expect(wochentagKurz(6, "de")).toMatch(/^So/);
    expect(wochentagKurz(0, "en")).toMatch(/^Mon/);
  });
});

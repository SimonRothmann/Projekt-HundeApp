import { describe, expect, it } from "vitest";

import { internerPfad } from "./interner-pfad";

describe("internerPfad", () => {
  it("lässt seiteninterne Pfade samt Abfrage und Anker durch", () => {
    expect(internerPfad("/trainer/groups/abc")).toBe("/trainer/groups/abc");
    expect(internerPfad("/dogs/1?tab=plan#ziel")).toBe("/dogs/1?tab=plan#ziel");
  });

  it("weist leere und relative Werte ab", () => {
    expect(internerPfad(null)).toBeNull();
    expect(internerPfad(undefined)).toBeNull();
    expect(internerPfad("")).toBeNull();
    expect(internerPfad("dogs")).toBeNull();
    expect(internerPfad("https://fremd.example/")).toBeNull();
    expect(internerPfad("javascript:alert(1)")).toBeNull();
  });

  it("weist alles ab, was der Browser zu einer fremden Seite auflöst", () => {
    for (const wert of [
      "//fremd.example",
      "/\\fremd.example",
      "/\t/fremd.example",
      "/\n/fremd.example",
      "/\r/fremd.example",
      "\t//fremd.example",
      "/\\/fremd.example",
    ]) {
      expect(internerPfad(wert), JSON.stringify(wert)).toBeNull();
    }
  });
});

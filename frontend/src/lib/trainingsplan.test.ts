import { describe, expect, it } from "vitest";
import { sichtbareWochen } from "./trainingsplan";

const wochen: [number, string][] = [
  [1, "a"],
  [2, "b"],
  [3, "c"],
  [4, "d"],
];

describe("sichtbareWochen", () => {
  it("zeigt nur die laufende Woche", () => {
    expect(sichtbareWochen(wochen, 3, false)).toEqual([[3, "c"]]);
  });

  it("zeigt alle Wochen, sobald man es verlangt", () => {
    expect(sichtbareWochen(wochen, 3, true)).toEqual(wochen);
  });

  it("verbirgt nichts, wenn sich die laufende Woche nicht bestimmen lässt", () => {
    // Abgeschlossener Plan: dann ist keine Woche die richtige, und eine
    // willkürlich gewählte wäre schlechter als die vollständige Liste.
    expect(sichtbareWochen(wochen, null, false)).toEqual(wochen);
    // computeCurrentWeek liefert undefined statt null, wenn es keine Wochen
    // gibt - beides muss dasselbe bedeuten.
    expect(sichtbareWochen(wochen, undefined, false)).toEqual(wochen);
  });

  it("verbirgt nichts bei zwei Wochen - das spart keinen Platz, kostet aber einen Knopf", () => {
    const kurz: [number, string][] = [
      [1, "a"],
      [2, "b"],
    ];

    expect(sichtbareWochen(kurz, 1, false)).toEqual(kurz);
  });
});

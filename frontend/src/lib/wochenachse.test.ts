import { describe, expect, it } from "vitest";
import { wochenBeschriftungen } from "./wochenachse";

const wochen = (nummern: number[]) => nummern.map((n) => ({ week: `2026-KW${String(n).padStart(2, "0")}` }));

describe("wochenBeschriftungen", () => {
  it("beschriftet jede dritte Woche, die jüngste immer", () => {
    const l = wochenBeschriftungen(wochen([35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46]));
    expect(l).toEqual([null, null, 37, null, null, 40, null, null, 43, null, null, 46]);
  });

  it("lässt unlesbare Wochen unbeschriftet", () => {
    expect(wochenBeschriftungen([{ week: "kaputt" }])).toEqual([null]);
  });

  it("liefert für eine leere Liste nichts", () => {
    expect(wochenBeschriftungen([])).toEqual([]);
  });
});

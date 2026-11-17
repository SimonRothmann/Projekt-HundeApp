import { describe, expect, it } from "vitest";
import { ENTSPERR_SCHWELLE, entsperrFortschritt, entsperrt } from "@/lib/aufnahme-sperre";

describe("entsperrFortschritt", () => {
  it("rechnet den Weg ohne die Griffbreite", () => {
    expect(entsperrFortschritt(100, 300, 50)).toBeCloseTo(0.4);
    expect(entsperrFortschritt(250, 300, 50)).toBe(1);
  });

  it("begrenzt auf 0..1", () => {
    expect(entsperrFortschritt(-40, 300, 50)).toBe(0);
    expect(entsperrFortschritt(900, 300, 50)).toBe(1);
  });

  it("bleibt bei 0, wenn es keinen Weg gibt oder dx ungültig ist", () => {
    expect(entsperrFortschritt(10, 50, 50)).toBe(0);
    expect(entsperrFortschritt(10, 0, 50)).toBe(0);
    expect(entsperrFortschritt(Number.NaN, 300, 50)).toBe(0);
  });
});

describe("entsperrt", () => {
  it("entsperrt erst ab der Schwelle", () => {
    expect(entsperrt(0)).toBe(false);
    expect(entsperrt(0.5)).toBe(false);
    expect(entsperrt(ENTSPERR_SCHWELLE - 0.01)).toBe(false);
    expect(entsperrt(ENTSPERR_SCHWELLE)).toBe(true);
    expect(entsperrt(1)).toBe(true);
  });
});

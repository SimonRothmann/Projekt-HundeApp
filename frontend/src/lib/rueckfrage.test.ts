import { describe, expect, it } from "vitest";
import { eingabePasst } from "./rueckfrage";

describe("eingabePasst", () => {
  it("verlangt genau den Text", () => {
    expect(eingabePasst("Bello", "Bello")).toBe(true);
    expect(eingabePasst("bello", "Bello")).toBe(false);
    expect(eingabePasst("Bell", "Bello")).toBe(false);
    expect(eingabePasst("", "Bello")).toBe(false);
  });

  it("ignoriert Leerzeichen am Rand, nicht in der Mitte", () => {
    expect(eingabePasst("  Bello ", "Bello")).toBe(true);
    expect(eingabePasst("Von Bello", "Von  Bello")).toBe(false);
  });
});

import { describe, expect, it } from "vitest";
import { anteilProzent, balkenProzent } from "./kennzahlen";

describe("balkenProzent", () => {
  it("rechnet den Anteil am Bezugswert in ganzen Prozent", () => {
    expect(balkenProzent(5, 10)).toBe(50);
    expect(balkenProzent(10, 10)).toBe(100);
  });

  it("lässt null bei null und ohne Bezugswert", () => {
    expect(balkenProzent(0, 10)).toBe(0);
    expect(balkenProzent(3, 0)).toBe(0);
  });

  it("zeigt einen kleinen, echten Wert mit Mindestbreite", () => {
    expect(balkenProzent(1, 1000)).toBe(2);
  });

  it("wird nie breiter als der Balken", () => {
    // Bei den "aktiven Konten" kann der Wert über dem Bezug liegen, wenn
    // zwischen zwei Abfragen ein Konto gelöscht wurde.
    expect(balkenProzent(12, 10)).toBe(100);
  });
});

describe("anteilProzent", () => {
  it("rundet auf ganze Prozent", () => {
    expect(anteilProzent(1, 3)).toBe(33);
  });

  it("liefert ohne Bezugswert nichts statt NaN", () => {
    expect(anteilProzent(0, 0)).toBeNull();
  });
});

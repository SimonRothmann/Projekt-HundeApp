import { describe, expect, it } from "vitest";
import {
  gueltigeSchrittlaenge,
  schritte,
  schritteText,
  STANDARD_SCHRITT_CM,
  wirksameSchrittlaenge,
} from "@/lib/schritte";

const t = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (_, name: string) => String(werte?.[name]));

describe("schritte", () => {
  it("rechnet Meter mit der Schrittlänge um und rundet auf 10", () => {
    // 300 m bei 75 cm = 400 Schritte.
    expect(schritte(300, 75)).toBe(400);
    // 312 m / 0,75 = 416 -> 420
    expect(schritte(312, 75)).toBe(420);
    // 410 m / 0,75 = 546,7 -> 550
    expect(schritte(410, 75)).toBe(550);
  });

  it("nimmt die eingestellte Schrittlänge", () => {
    // Kürzerer Schritt, mehr Schritte.
    expect(schritte(300, 60)).toBe(500);
    expect(schritte(300, 100)).toBe(300);
  });

  it("rundet auf 10 mit der Grenze bei 5", () => {
    // 3,7 m / 0,75 = 4,9 -> 0, 4,0 m -> 5,33 -> 10
    expect(schritte(3.7, 75)).toBeNull();
    expect(schritte(4, 75)).toBe(10);
  });

  it("liefert nichts ohne Strecke", () => {
    expect(schritte(0, 75)).toBeNull();
    expect(schritte(-3, 75)).toBeNull();
    expect(schritte(Number.NaN, 75)).toBeNull();
  });
});

describe("schritteText", () => {
  it("schreibt die Zahl mit dem Näherungszeichen", () => {
    expect(schritteText(300, 75, t)).toBe("≈ 400 Schritte");
  });

  it("bleibt leer ohne Länge", () => {
    expect(schritteText(null, 75, t)).toBeNull();
    expect(schritteText(undefined, 75, t)).toBeNull();
    expect(schritteText(0, 75, t)).toBeNull();
  });
});

describe("wirksameSchrittlaenge", () => {
  it("nimmt den Standard ohne Angabe", () => {
    expect(wirksameSchrittlaenge(null)).toBe(STANDARD_SCHRITT_CM);
    expect(wirksameSchrittlaenge(undefined)).toBe(75);
  });

  it("nimmt die gespeicherte Angabe im Bereich 40 bis 120", () => {
    expect(wirksameSchrittlaenge(40)).toBe(40);
    expect(wirksameSchrittlaenge(120)).toBe(120);
    expect(wirksameSchrittlaenge(82)).toBe(82);
  });

  it("fällt bei einem Wert außerhalb auf den Standard zurück", () => {
    expect(wirksameSchrittlaenge(39)).toBe(75);
    expect(wirksameSchrittlaenge(121)).toBe(75);
    expect(wirksameSchrittlaenge(70.5)).toBe(75);
  });
});

describe("gueltigeSchrittlaenge", () => {
  it("lässt nur ganze Zahlen von 40 bis 120 zu", () => {
    expect(gueltigeSchrittlaenge(40)).toBe(true);
    expect(gueltigeSchrittlaenge(120)).toBe(true);
    expect(gueltigeSchrittlaenge(39)).toBe(false);
    expect(gueltigeSchrittlaenge(121)).toBe(false);
    expect(gueltigeSchrittlaenge(75.5)).toBe(false);
    expect(gueltigeSchrittlaenge(Number.NaN)).toBe(false);
  });
});

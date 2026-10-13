import { describe, expect, it } from "vitest";
import { bearingDegrees } from "@/lib/geo";
import {
  BEARING_MIN_DISTANCE_M,
  HEADING_SMOOTH_ALPHA,
  glaettePeilung,
  naechsteAusrichtung,
  rohePeilung,
  zielPeilung,
} from "./peilung";

// Meter östlich/nördlich eines Bezugspunkts bei 50° N - genau genug, um
// Abstände von wenigen Metern zu bauen.
const BEZUG = { lat: 50, lon: 8 };
function ort(ostMeter: number, nordMeter: number) {
  return {
    latitude: BEZUG.lat + nordMeter / 111_320,
    longitude: BEZUG.lon + ostMeter / (111_320 * Math.cos((BEZUG.lat * Math.PI) / 180)),
  };
}

describe("rohePeilung", () => {
  it("braucht mindestens zwei Punkte", () => {
    expect(rohePeilung([])).toBeNull();
    expect(rohePeilung([ort(0, 0)])).toBeNull();
  });

  it("zeigt die Himmelsrichtung der Bewegung", () => {
    expect(rohePeilung([ort(0, 0), ort(0, 10)])).toBeCloseTo(0, 0);
    expect(rohePeilung([ort(0, 0), ort(10, 0)])).toBeCloseTo(90, 0);
    expect(rohePeilung([ort(0, 0), ort(0, -10)])).toBeCloseTo(180, 0);
    expect(rohePeilung([ort(0, 0), ort(-10, 0)])).toBeCloseTo(270, 0);
  });

  it("nimmt den jüngsten früheren Punkt mit Mindestabstand, nicht den vorletzten", () => {
    // Die letzten Punkte liegen unter 1 m auseinander (Rauschen im Schritttempo);
    // erst der Punkt davor ist weit genug weg. Gegenprobe: vorletzter und
    // ältester Punkt gäben eine ganz andere Richtung als Nord.
    const punkte = [ort(-20, 0), ort(0, 0), ort(0.7, 4.5), ort(-0.7, 4.8), ort(0, 5)];
    expect(rohePeilung(punkte)).toBeCloseTo(bearingDegrees(punkte[1], punkte[4]), 6);
    expect(bearingDegrees(punkte[1], punkte[4])).toBeCloseTo(0, 0);
    expect(bearingDegrees(punkte[3], punkte[4])).toBeGreaterThan(45);
    expect(bearingDegrees(punkte[0], punkte[4])).toBeGreaterThan(45);
  });

  it("geht beliebig weit zurück, wenn der Abstand sonst zu klein ist", () => {
    // Sieben Punkte stehen praktisch still (unter dem Mindestabstand), der
    // älteste liegt 30 m westlich. Nur der erreicht den Mindestabstand.
    const stehend = Array.from({ length: 7 }, (_, i) => ort(30 + (i % 2) * 0.5, 0));
    const punkte = [ort(0, 0), ...stehend];
    expect(rohePeilung(punkte)).toBeCloseTo(90, 0);
  });

  it("nimmt den ältesten Punkt, wenn keiner den Mindestabstand erreicht", () => {
    const punkte = [ort(0, 0), ort(0, 1), ort(0, 2)];
    expect(rohePeilung(punkte)).toBeCloseTo(bearingDegrees(punkte[0], punkte[2]), 6);
  });

  it("nimmt genau den Mindestabstand noch an", () => {
    const punkte = [ort(0, 0), ort(0, BEARING_MIN_DISTANCE_M + 0.1), ort(0, BEARING_MIN_DISTANCE_M + 0.2)];
    expect(rohePeilung(punkte)).toBeCloseTo(0, 0);
  });
});

describe("glaettePeilung", () => {
  it("übernimmt den ersten Wert unverändert", () => {
    expect(glaettePeilung(null, 123)).toBe(123);
  });

  it("nähert sich dem neuen Wert um den Glättungsfaktor", () => {
    expect(glaettePeilung(100, 150)).toBeCloseTo(100 + HEADING_SMOOTH_ALPHA * 50, 6);
  });

  it("dreht über Nord auf dem kurzen Weg: 350° -> 10° sind +20°", () => {
    expect(glaettePeilung(350, 10)).toBeCloseTo((350 + HEADING_SMOOTH_ALPHA * 20) % 360, 6);
  });

  it("dreht ebenso zurück: 10° -> 350° sind -20°", () => {
    expect(glaettePeilung(10, 350)).toBeCloseTo(10 - HEADING_SMOOTH_ALPHA * 20, 6);
  });

  it("bleibt im Bereich [0, 360)", () => {
    for (const [bisher, roh] of [[0, 181], [359, 1], [1, 359], [180, 0], [0, 0]] as const) {
      const ergebnis = glaettePeilung(bisher, roh);
      expect(ergebnis).toBeGreaterThanOrEqual(0);
      expect(ergebnis).toBeLessThan(360);
    }
    // Knapp über der Gegenrichtung geht es nach der anderen Seite herum.
    expect(glaettePeilung(0, 181)).toBeCloseTo(360 - HEADING_SMOOTH_ALPHA * 179, 6);
  });
});

describe("Ausrichtung", () => {
  it("schaltet zwischen Laufrichtung und Norden hin und her", () => {
    expect(naechsteAusrichtung("heading")).toBe("north");
    expect(naechsteAusrichtung("north")).toBe("heading");
  });

  it("dreht nur in Laufrichtung mit der Peilung, sonst steht Nord oben", () => {
    expect(zielPeilung("heading", 42)).toBe(42);
    expect(zielPeilung("heading", null)).toBeNull();
    expect(zielPeilung("north", 42)).toBe(0);
    expect(zielPeilung("north", null)).toBe(0);
  });
});

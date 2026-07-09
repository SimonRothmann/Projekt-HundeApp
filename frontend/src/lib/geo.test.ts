import { describe, expect, it } from "vitest";
import { bearingDegrees, estimateLengthMeters, haversineMeters } from "@/lib/geo";

// Referenzpunkt für alle Tests: Karlsbad (Nordschwarzwald), realistische
// Trainingsplatz-Koordinaten. 1° Breite ≈ 111.111 m, 1° Länge ≈ 76.500 m
// bei 48.9° N.
const BASE = { latitude: 48.9, longitude: 8.5 };

// Verschiebt BASE um Meter nach Nord/Ost (Kleinwinkelnäherung - für
// Distanzen im Testbereich (< 1 km) genauer als jede Toleranz unten).
function offsetMeters(north: number, east: number) {
  return {
    latitude: BASE.latitude + north / 111111,
    longitude: BASE.longitude + east / (111111 * Math.cos((BASE.latitude * Math.PI) / 180)),
  };
}

describe("haversineMeters", () => {
  it("liefert 0 für identische Punkte", () => {
    expect(haversineMeters(BASE, BASE)).toBe(0);
  });

  // ±0.5 m Toleranz: die Testpunkte werden über die 111111-m/Grad-Näherung
  // erzeugt, haversine rechnet mit Erdradius 6371 km (111195 m/Grad) -
  // die ~0.08 % Abweichung sind Erwartungsfehler des Tests, kein Codefehler.
  it("misst 100 m Nord-Verschiebung auf ±0.5 m genau", () => {
    expect(haversineMeters(BASE, offsetMeters(100, 0))).toBeCloseTo(100, 0);
  });

  it("misst 100 m Ost-Verschiebung auf ±0.5 m genau", () => {
    expect(haversineMeters(BASE, offsetMeters(0, 100))).toBeCloseTo(100, 0);
  });

  it("ist symmetrisch", () => {
    const p = offsetMeters(250, -80);
    expect(haversineMeters(BASE, p)).toBeCloseTo(haversineMeters(p, BASE), 6);
  });
});

describe("bearingDegrees", () => {
  it("Norden = 0°", () => {
    expect(bearingDegrees(BASE, offsetMeters(100, 0))).toBeCloseTo(0, 1);
  });

  it("Osten = 90°", () => {
    expect(bearingDegrees(BASE, offsetMeters(0, 100))).toBeCloseTo(90, 1);
  });

  it("Süden = 180°", () => {
    expect(bearingDegrees(BASE, offsetMeters(-100, 0))).toBeCloseTo(180, 1);
  });

  it("Westen = 270°", () => {
    expect(bearingDegrees(BASE, offsetMeters(0, -100))).toBeCloseTo(270, 1);
  });

  it("liefert immer Werte in [0, 360)", () => {
    for (const [n, e] of [[1, 1], [-1, 1], [-1, -1], [1, -1]] as const) {
      const b = bearingDegrees(BASE, offsetMeters(n * 50, e * 50));
      expect(b).toBeGreaterThanOrEqual(0);
      expect(b).toBeLessThan(360);
    }
  });
});

describe("estimateLengthMeters", () => {
  it("leerer Track und Einzelpunkt ergeben 0", () => {
    expect(estimateLengthMeters([])).toBe(0);
    expect(estimateLengthMeters([BASE])).toBe(0);
  });

  it("summiert Segmentlängen (L-förmiger Track: 100 m + 50 m)", () => {
    const track = [BASE, offsetMeters(100, 0), offsetMeters(100, 50)];
    expect(estimateLengthMeters(track)).toBe(150);
  });

  it("ignoriert manuell gesetzte Marker (pointType 1)", () => {
    // Der Marker liegt 500 m abseits - dürfte die Länge massiv verfälschen,
    // wenn er mitgerechnet würde.
    const track = [
      { ...BASE, pointType: 0 },
      { ...offsetMeters(0, 500), pointType: 1 },
      { ...offsetMeters(100, 0), pointType: 0 },
    ];
    expect(estimateLengthMeters(track)).toBe(100);
  });
});

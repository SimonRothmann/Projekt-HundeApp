import { describe, expect, it } from "vitest";
import { findTurnPoints } from "@/lib/use-walk-run-haptics";
import type { GpsPoint } from "@/lib/types";

const BASE_LAT = 48.9;
const BASE_LON = 8.5;
const M_PER_DEG_LAT = 111111;
const M_PER_DEG_LON = 111111 * Math.cos((BASE_LAT * Math.PI) / 180);

function pt(north: number, east: number, pointType: 0 | 1 = 0): GpsPoint {
  return {
    latitude: BASE_LAT + north / M_PER_DEG_LAT,
    longitude: BASE_LON + east / M_PER_DEG_LON,
    timestamp: new Date().toISOString(),
    accuracy: 5,
    pointType,
    label: null,
  };
}

describe("findTurnPoints", () => {
  it("gerade Strecke enthält keine Abbiegungen", () => {
    const track = [pt(0, 0), pt(20, 0), pt(40, 0), pt(60, 0)];
    expect(findTurnPoints(track)).toHaveLength(0);
  });

  it("erkennt einen 90°-Winkel", () => {
    // 40 m nach Norden, dann 40 m nach Osten - klassischer Fährten-Winkel.
    const track = [pt(0, 0), pt(20, 0), pt(40, 0), pt(40, 20), pt(40, 40)];
    const turns = findTurnPoints(track);
    expect(turns).toHaveLength(1);
    // Abbiegung liegt am Knickpunkt (40 m Nord).
    expect(turns[0].latitude).toBeCloseTo(BASE_LAT + 40 / M_PER_DEG_LAT, 6);
    expect(turns[0].kind).toBe("turn");
  });

  it("ignoriert GPS-Zittern bei zu kurzen Segmenten", () => {
    // Winkel vorhanden, aber Nachbarsegmente nur 2 m lang (< 5 m Minimum) -
    // das ist Rauschen, keine echte Abbiegung.
    const track = [pt(0, 0), pt(2, 0), pt(2, 2), pt(4, 2)];
    expect(findTurnPoints(track)).toHaveLength(0);
  });

  it("ignoriert manuell gesetzte Marker (pointType 1) für die Winkelberechnung", () => {
    // Der Marker liegt quer zur Laufrichtung - würde er mitgerechnet,
    // entstünde ein falscher Winkel.
    const track = [pt(0, 0), pt(20, 0), pt(10, 30, 1), pt(40, 0), pt(60, 0)];
    expect(findTurnPoints(track)).toHaveLength(0);
  });

  it("erkennt mehrere Winkel auf einer Fährte mit 3 Schenkeln", () => {
    // Nord → Ost → Nord (2 Winkel wie bei Fährte A).
    const track = [
      pt(0, 0), pt(25, 0), pt(50, 0),
      pt(50, 25), pt(50, 50),
      pt(75, 50), pt(100, 50),
    ];
    expect(findTurnPoints(track)).toHaveLength(2);
  });
});

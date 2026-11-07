import { describe, expect, it } from "vitest";
import type { GpsPoint } from "@/lib/types";
import { ANSATZ_RADIUS_M, ansatzHinweis, ansatzPunkt, pfeilWinkel } from "@/lib/ansatz";

const BASIS = { latitude: 48.9, longitude: 8.5 };
const M_BREITE = 1 / 111111;
const M_LAENGE = 1 / (111111 * Math.cos((BASIS.latitude * Math.PI) / 180));

/** Eine Position `nord` Meter nördlich und `ost` Meter östlich der Basis. */
function ort(nord: number, ost: number) {
  return { latitude: BASIS.latitude + nord * M_BREITE, longitude: BASIS.longitude + ost * M_LAENGE };
}

function legungspunkt(nord: number, ost: number, minute: number, pointType = 0): GpsPoint {
  return {
    ...ort(nord, ost),
    timestamp: new Date(Date.UTC(2026, 10, 7, 10, minute)).toISOString(),
    accuracy: 5,
    pointType: pointType as GpsPoint["pointType"],
    label: null,
    markerType: 0,
  };
}

describe("ansatzPunkt", () => {
  it("ist der erste automatische Punkt nach Zeit", () => {
    const erster = legungspunkt(0, 0, 0);
    expect(ansatzPunkt([legungspunkt(50, 0, 2), erster, legungspunkt(20, 0, 1)])).toBe(erster);
  });

  it("überspringt Marker, auch wenn sie als Erstes gesetzt wurden", () => {
    const marker = legungspunkt(5, 0, 0, 1);
    const erster = legungspunkt(0, 0, 1);
    expect(ansatzPunkt([marker, erster, legungspunkt(20, 0, 2)])).toBe(erster);
  });

  it("gibt es nicht ohne Punkte", () => {
    expect(ansatzPunkt([])).toBeNull();
    expect(ansatzPunkt([legungspunkt(0, 0, 0, 1)])).toBeNull();
  });

  it("verändert die Eingabe nicht", () => {
    const punkte = [legungspunkt(50, 0, 2), legungspunkt(0, 0, 0)];
    ansatzPunkt(punkte);
    expect(punkte[0].timestamp > punkte[1].timestamp).toBe(true);
  });
});

describe("ansatzHinweis", () => {
  const ansatz = ort(0, 0);

  it("zeigt Entfernung und Peilung zum Ansatz", () => {
    // 35 m südlich: der Ansatz liegt im Norden.
    const hinweis = ansatzHinweis([ort(-35, 0)], ansatz)!;
    expect(hinweis.meter).toBe(35);
    expect(hinweis.peilung).toBeCloseTo(0, 0);

    // 40 m westlich: der Ansatz liegt im Osten.
    const osten = ansatzHinweis([ort(0, -40)], ansatz)!;
    expect(osten.meter).toBe(40);
    expect(osten.peilung).toBeCloseTo(90, 0);
  });

  it("peilt aus der aktuellen, also der letzten Position", () => {
    const hinweis = ansatzHinweis([ort(-100, 0), ort(0, 30)], ansatz)!;
    expect(hinweis.meter).toBe(30);
    expect(hinweis.peilung).toBeCloseTo(270, 0);
  });

  it("blendet sich innerhalb von 10 m aus", () => {
    expect(ANSATZ_RADIUS_M).toBe(10);
    expect(ansatzHinweis([ort(-10.5, 0)], ansatz)).not.toBeNull();
    expect(ansatzHinweis([ort(-9.5, 0)], ansatz)).toBeNull();
  });

  it("kommt nicht wieder, wenn man sich nach dem Finden entfernt", () => {
    expect(ansatzHinweis([ort(-40, 0), ort(-5, 0), ort(60, 0)], ansatz)).toBeNull();
  });

  it("zeigt nichts ohne Position oder ohne Ansatz", () => {
    expect(ansatzHinweis([], ansatz)).toBeNull();
    expect(ansatzHinweis([ort(-40, 0)], null)).toBeNull();
  });
});

describe("pfeilWinkel", () => {
  it("dreht den Pfeil relativ zur Laufrichtung", () => {
    // Ich laufe nach Osten, der Ansatz liegt im Norden: links von mir.
    expect(pfeilWinkel(0, 90)).toBe(270);
    // Ich laufe nach Norden, der Ansatz liegt im Osten: rechts.
    expect(pfeilWinkel(90, 0)).toBe(90);
    // Genau voraus und genau hinter mir.
    expect(pfeilWinkel(45, 45)).toBe(0);
    expect(pfeilWinkel(0, 180)).toBe(180);
  });

  it("rechnet über 360 Grad hinweg", () => {
    expect(pfeilWinkel(10, 350)).toBe(20);
    expect(pfeilWinkel(350, 10)).toBe(340);
  });

  it("zeigt ohne bekannte Laufrichtung nach Norden oben", () => {
    expect(pfeilWinkel(135, null)).toBe(135);
  });
});

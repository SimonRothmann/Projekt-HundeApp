import { describe, expect, it } from "vitest";
import { kalmanInit, kalmanStep, type KalmanState } from "@/lib/kalman";

const LAT = 48.9;
const LON = 8.5;
const M_PER_DEG_LAT = 111111;

describe("kalmanInit", () => {
  it("übernimmt die erste Messung unverändert als Zustand", () => {
    const s = kalmanInit(LAT, LON, 10);
    expect(s.lat).toBe(LAT);
    expect(s.lon).toBe(LON);
    // Unsicherheit entspricht der Messgenauigkeit (10 m als Grad²-Varianz).
    expect(Math.sqrt(s.P_lat) * M_PER_DEG_LAT).toBeCloseTo(10, 5);
  });
});

describe("kalmanStep", () => {
  it("glättet Rauschen: Schätzung bleibt näher am alten Zustand als an einem Ausreißer", () => {
    // Eingeschwungener Zustand (kleine Unsicherheit ~1 m), dann ein
    // Ausreißer 20 m nördlich mit schlechter Genauigkeit (15 m).
    let s: KalmanState = { lat: LAT, lon: LON, P_lat: (1 / M_PER_DEG_LAT) ** 2, P_lon: (1 / M_PER_DEG_LAT) ** 2 };
    const outlierLat = LAT + 20 / M_PER_DEG_LAT;
    s = kalmanStep(s, outlierLat, LON, 15);

    const movedMeters = (s.lat - LAT) * M_PER_DEG_LAT;
    // Der Filter darf dem Ausreißer nur einen Bruchteil folgen.
    expect(movedMeters).toBeGreaterThan(0);
    expect(movedMeters).toBeLessThan(2);
  });

  it("gewichtet gute Messungen stärker als schlechte", () => {
    const start: KalmanState = {
      lat: LAT,
      lon: LON,
      P_lat: (5 / M_PER_DEG_LAT) ** 2,
      P_lon: (5 / M_PER_DEG_LAT) ** 2,
    };
    const targetLat = LAT + 10 / M_PER_DEG_LAT;

    const goodAccuracy = kalmanStep(start, targetLat, LON, 3);
    const badAccuracy = kalmanStep(start, targetLat, LON, 25);

    // Bei 3 m Genauigkeit springt die Schätzung deutlich weiter Richtung
    // Messung als bei 25 m - das ist der Kern des adaptiven Verhaltens
    // (Unterschied zu festem EMA-α).
    expect(goodAccuracy.lat - LAT).toBeGreaterThan((badAccuracy.lat - LAT) * 3);
  });

  it("konvergiert bei konstanter Position gegen diese Position", () => {
    let s = kalmanInit(LAT, LON, 20);
    const trueLat = LAT + 5 / M_PER_DEG_LAT;
    for (let i = 0; i < 50; i++) {
      s = kalmanStep(s, trueLat, LON, 8);
    }
    expect((s.lat - trueLat) * M_PER_DEG_LAT).toBeCloseTo(0, 0);
  });

  it("reduziert die Unsicherheit mit jeder Messung unter die Messgenauigkeit", () => {
    let s = kalmanInit(LAT, LON, 20);
    for (let i = 0; i < 10; i++) {
      s = kalmanStep(s, LAT, LON, 8);
    }
    const sigmaMeters = Math.sqrt(s.P_lat) * M_PER_DEG_LAT;
    expect(sigmaMeters).toBeLessThan(8);
  });
});

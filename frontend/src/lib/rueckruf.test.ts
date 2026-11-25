import { describe, expect, it } from "vitest";
import { anschussFlaeche, anschussVersatz } from "./anschussflaeche";
import { haversineMeters } from "./geo";
import { mitRueckrufenDurchgefallen, rueckrufe, rueckrufFortsetzen, NEUER_RUECKRUF_ZUSTAND } from "./rueckruf";

const M = 6371000 * (Math.PI / 180);
const lat = (y: number) => 50 + y / M;
const lon = (x: number) => 8 + x / (M * Math.cos((50 * Math.PI) / 180));

// Gerade Legung nach Norden, 400 m, ein Punkt je Meter.
const linie = Array.from({ length: 401 }, (_, y) => ({ latitude: lat(y), longitude: lon(0), timestamp: new Date(Date.UTC(2026, 0, 1, 10, 0, y)).toISOString() }));

/** Ein Ablauf aus Wegpunkten, ein Punkt je Meter und 1,5 s. */
function lauf(...wege: [number, number][]) {
  const punkte: { latitude: number; longitude: number; timestamp: string }[] = [];
  let s = 0;
  const neu = (x: number, y: number) => punkte.push({ latitude: lat(y), longitude: lon(x), timestamp: new Date(Date.UTC(2026, 0, 1, 11, 0, 0) + s * 1500).toISOString() });
  neu(...wege[0]);
  for (let i = 1; i < wege.length; i++) {
    const [x0, y0] = wege[i - 1];
    const [x1, y1] = wege[i];
    const n = Math.max(1, Math.round(Math.hypot(x1 - x0, y1 - y0)));
    for (let k = 1; k <= n; k++) {
      s++;
      neu(x0 + ((x1 - x0) * k) / n, y0 + ((y1 - y0) * k) / n);
    }
  }
  return punkte;
}

describe("Rückrufe (Spiegel von GpsTrackEvaluator.Rueckrufe)", () => {
  it("gerader Weg auf der Linie: keiner", () => expect(rueckrufe(linie, lauf([0, 0], [0, 300]))).toHaveLength(0));
  it("90 m parallel in 40 m Abstand: einer", () =>
    expect(rueckrufe(linie, lauf([0, 0], [0, 20], [40, 20], [40, 110]))).toHaveLength(1));
  it("70 m abgekommen, dann zurück: keiner", () =>
    expect(rueckrufe(linie, lauf([0, 0], [0, 20], [40, 20], [40, 40], [0, 40], [0, 200]))).toHaveLength(0));
  it("zweimal abgekommen mit Rückkehr dazwischen: zwei", () =>
    expect(rueckrufe(linie, lauf([0, 0], [0, 20], [40, 20], [40, 110], [0, 110], [0, 130], [40, 130], [40, 220]))).toHaveLength(2));
  it("drei Rückrufe: drei, und das ist durchgefallen", () => {
    const stellen = rueckrufe(linie, lauf([0, 0], [0, 10], [40, 10], [40, 100], [0, 100], [0, 110], [40, 110], [40, 200], [0, 200], [0, 210], [40, 210], [40, 300]));
    expect(stellen).toHaveLength(3);
    expect(mitRueckrufenDurchgefallen(3)).toBe(true);
    expect(mitRueckrufenDurchgefallen(2)).toBe(false);
  });
  it("eine lange Abweichung ohne Rückkehr zählt nur einmal", () =>
    expect(rueckrufe(linie, lauf([0, 0], [0, 10], [40, 10], [40, 300]))).toHaveLength(1));
  it("vor dem Anschuss zählt nichts", () => expect(rueckrufe(linie, lauf([60, 100], [60, 250], [0, 250], [0, 0], [0, 100]))).toHaveLength(0));
  it("ein Lückenstück zählt nicht als Weg", () => {
    const punkte = lauf([0, 0], [0, 10], [40, 10], [40, 40]);
    punkte.push({ latitude: lat(140), longitude: lon(40), timestamp: new Date(Date.parse(punkte[punkte.length - 1].timestamp) + 90_000).toISOString() });
    expect(rueckrufe(linie, punkte)).toHaveLength(0);
  });
  it("punktweise fortgesetzt kommt dasselbe heraus wie auf einmal", () => {
    const punkte = lauf([0, 0], [0, 20], [40, 20], [40, 110], [0, 110]);
    let z = NEUER_RUECKRUF_ZUSTAND;
    for (let i = 1; i <= punkte.length; i++) z = rueckrufFortsetzen(z, linie, punkte.slice(0, i));
    expect(z.stellen).toEqual(rueckrufe(linie, punkte));
  });
});

describe("Anschussfläche", () => {
  const anschuss = { latitude: 50, longitude: 8 };
  it("ist stabil je Kennung, höchstens 10 m versetzt und enthält den Anschuss", () => {
    for (const id of ["a", "b", "5f3c-1", "zzz"]) {
      const { ost, nord } = anschussVersatz(id);
      expect(Math.abs(ost)).toBeLessThanOrEqual(10);
      expect(Math.abs(nord)).toBeLessThanOrEqual(10);
      expect(anschussFlaeche(anschuss, id)).toEqual(anschussFlaeche(anschuss, id));
      const [sw, , no] = anschussFlaeche(anschuss, id);
      expect(anschuss.longitude).toBeGreaterThan(sw[0]);
      expect(anschuss.longitude).toBeLessThan(no[0]);
      expect(anschuss.latitude).toBeGreaterThan(sw[1]);
      expect(anschuss.latitude).toBeLessThan(no[1]);
    }
  });
  it("hat 30 m Kantenlänge und ist nicht für alle Kennungen gleich verschoben", () => {
    const [sw, so, no] = anschussFlaeche(anschuss, "x");
    expect(haversineMeters({ latitude: sw[1], longitude: sw[0] }, { latitude: so[1], longitude: so[0] })).toBeCloseTo(30, 0);
    expect(haversineMeters({ latitude: so[1], longitude: so[0] }, { latitude: no[1], longitude: no[0] })).toBeCloseTo(30, 0);
    expect(anschussVersatz("x")).not.toEqual(anschussVersatz("y"));
  });
});

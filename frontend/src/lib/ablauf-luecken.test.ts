import { describe, expect, it } from "vitest";
import { uebersetze } from "@/lib/i18n";
import type { GpsWalkPoint, GpsWalkRun } from "@/lib/types";
import { istLuecke, lueckeDauerText, stueckeAnLuecken } from "./ablauf-luecken";
import { ablaufDaten, LUECKE_FARBE, liveAblaufDaten } from "./karte/spur-daten";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);

const GRAD_PRO_METER = 1 / 111_195;
const T0 = Date.parse("2026-11-22T09:00:00Z");

/** Punkt `meter` nördlich des Bezugs, `sekunden` nach T0. */
function p(meter: number, sekunden: number, rest: Partial<GpsWalkPoint> = {}): GpsWalkPoint {
  return {
    latitude: 50 + meter * GRAD_PRO_METER,
    longitude: 8,
    timestamp: new Date(T0 + sekunden * 1000).toISOString(),
    accuracy: 4,
    deviationMeters: null,
    ...rest,
  };
}

function lauf(points: GpsWalkPoint[]): GpsWalkRun {
  return {
    id: "r", trackId: "t", createdAt: "2026-11-22T09:00:00Z", lengthMeters: null, comment: null, points,
    avgDeviationMeters: null, maxDeviationMeters: null, onTrackPercent: null, articlesFound: null,
    articlesTotal: null, evaluatedAt: null, stops: [],
  };
}

describe("istLuecke", () => {
  it("erkennt 217 s bei 8,7 m als Lücke", () => expect(istLuecke(p(0, 0), p(8.7, 217))).toBe(true));
  it("ist beim Stehen am Gegenstand (79 s, 2,1 m) keine Lücke", () => expect(istLuecke(p(0, 0), p(2.1, 79))).toBe(false));
  it("ist bei schnellem Weg (20 s, 15 m) keine Lücke", () => expect(istLuecke(p(0, 0), p(15, 20))).toBe(false));
  it("verlangt mehr als 30 s und mehr als 5 m", () => {
    expect(istLuecke(p(0, 0), p(10, 30))).toBe(false);
    expect(istLuecke(p(0, 0), p(10, 31))).toBe(true);
    expect(istLuecke(p(0, 0), p(4.5, 100))).toBe(false);
  });
});

describe("lueckeDauerText", () => {
  const min = (n: number) => t("{n} Min.", { n });
  it("nennt Sekunden unter einer Minute, sonst Minuten", () => {
    expect(lueckeDauerText(45, min)).toBe("45 s");
    expect(lueckeDauerText(217, min)).toBe("4 Min.");
  });
});

describe("stueckeAnLuecken", () => {
  it("zerlegt in Strecke, Lückenstück, Strecke", () => {
    const s = stueckeAnLuecken([1, 2, 3, 4], (i) => i === 2);
    expect(s).toStrictEqual([
      { punkte: [1, 2], luecke: false },
      { punkte: [2, 3], luecke: true },
      { punkte: [3, 4], luecke: false },
    ]);
  });
});

describe("Lücken auf der Karte", () => {
  it("zeichnet gespeicherte Abläufe nach afterGap, nicht nach den vereinfachten Punkten", () => {
    // Langsames gerades Gehen (zwei Minuten, 20 m zwischen zwei Punkten) ohne afterGap ist keine Lücke.
    const ohne = ablaufDaten([lauf([p(0, 0, { deviationMeters: 1 }), p(20, 120, { deviationMeters: 1 })])], t);
    expect(ohne.luecken.features).toHaveLength(0);
    expect(ohne.bewertet.features).toHaveLength(1);

    const mit = ablaufDaten([lauf([p(0, 0, { deviationMeters: 1 }), p(20, 120, { deviationMeters: 1, afterGap: true })])], t);
    expect(mit.luecken.features).toHaveLength(1);
    expect(mit.bewertet.features).toHaveLength(0);
    expect(mit.luecken.features[0].properties).toStrictEqual({ farbe: LUECKE_FARBE, text: "Keine Aufzeichnung (2 Min.)" });
  });

  it("trennt unbewertete Abläufe in Strecke und Lückenstück", () => {
    const d = ablaufDaten([lauf([p(0, 0), p(2, 2), p(30, 200, { afterGap: true }), p(32, 202)])], t);
    expect(d.unbewertet.features).toHaveLength(2);
    expect(d.luecken.features).toHaveLength(1);
    expect(d.luecken.features[0].geometry.coordinates).toStrictEqual([
      [8, p(2, 0).latitude],
      [8, p(30, 0).latitude],
    ]);
  });

  it("wendet für die laufende Linie die Regel auf den rohen Punkten an", () => {
    const d = liveAblaufDaten([p(0, 0), p(2, 2), p(11, 219), p(13, 221)], 0, t);
    expect(d.luecken.features).toHaveLength(1);
    expect(d.luecken.features[0].properties.text).toBe("Keine Aufzeichnung (4 Min.)");
    expect(d.linie.features).toHaveLength(2);
    // Stehen (79 s, 2 m) ergibt keine Lücke.
    expect(liveAblaufDaten([p(0, 0), p(2, 79)], 0, t).luecken.features).toHaveLength(0);
  });
});

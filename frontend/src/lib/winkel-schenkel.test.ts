import { describe, expect, it } from "vitest";
import type { GpsWalkRunCorner, GpsWalkRunLeg } from "@/lib/types";
import { uebersetze } from "@/lib/i18n";
import {
  abschnittsZeilen,
  dauerText,
  hatWinkelUndSchenkel,
  winkelBeschriftung,
  winkelZustand,
  zustandBeschriftung,
} from "./winkel-schenkel";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);

function schenkel(number: number): GpsWalkRunLeg {
  return { number, lengthMeters: 100, durationSeconds: 130, speedMetersPerMinute: 46, avgDeviationMeters: 2 };
}

function winkel(number: number, rest: Partial<GpsWalkRunCorner> = {}): GpsWalkRunCorner {
  return {
    number,
    direction: 1,
    sharp: false,
    turnDegrees: 90,
    latitude: 50,
    longitude: 8,
    reached: true,
    overshootMeters: 1,
    overshot: false,
    holdSeconds: 0,
    timeAtCornerSeconds: 20,
    ...rest,
  };
}

describe("winkelZustand", () => {
  it("unterscheidet sauber, überschossen und nicht erreicht", () => {
    expect(winkelZustand(winkel(1))).toBe("sauber");
    expect(winkelZustand(winkel(1, { overshot: true, overshootMeters: 9 }))).toBe("ueberschossen");
    expect(winkelZustand(winkel(1, { reached: false, overshootMeters: null }))).toBe("nicht-erreicht");
  });
});

describe("abschnittsZeilen", () => {
  it("ordnet Schenkel und Winkel abwechselnd in Laufreihenfolge", () => {
    const zeilen = abschnittsZeilen({ legs: [schenkel(3), schenkel(1), schenkel(2)], corners: [winkel(2), winkel(1)] });

    expect(zeilen.map((z) => (z.art === "schenkel" ? `S${z.leg.number}` : `W${z.corner.number}`))).toEqual([
      "S1",
      "W1",
      "S2",
      "W2",
      "S3",
    ]);
  });

  it("verträgt fehlende Felder älterer Stände", () => {
    expect(abschnittsZeilen({})).toEqual([]);
    expect(hatWinkelUndSchenkel({})).toBe(false);
  });
});

describe("hatWinkelUndSchenkel", () => {
  it("zeigt den Bereich erst mit einem Winkel oder mehr als einem Schenkel", () => {
    expect(hatWinkelUndSchenkel({ legs: [schenkel(1)], corners: [] })).toBe(false);
    expect(hatWinkelUndSchenkel({ legs: [schenkel(1), schenkel(2)], corners: [] })).toBe(true);
    expect(hatWinkelUndSchenkel({ legs: [], corners: [winkel(1)] })).toBe(true);
  });
});

describe("dauerText", () => {
  it("schreibt Minuten:Sekunden mit zweistelligen Sekunden", () => {
    expect(dauerText(130)).toBe("2:10");
    expect(dauerText(59.6)).toBe("1:00");
    expect(dauerText(5)).toBe("0:05");
    expect(dauerText(0)).toBe("0:00");
  });
});

describe("Beschriftungen", () => {
  it("nennt Nummer, Richtung und spitz", () => {
    expect(winkelBeschriftung(winkel(1), t)).toBe("Winkel 1 · rechts");
    expect(winkelBeschriftung(winkel(2, { direction: 0, sharp: true }), t)).toBe("Winkel 2 · links · spitz");
  });

  it("nennt den Zustand, beim Überschießen mit ganzen Metern", () => {
    expect(zustandBeschriftung(winkel(1), t)).toBe("sauber");
    expect(zustandBeschriftung(winkel(1, { overshot: true, overshootMeters: 8.6 }), t)).toBe("überschossen 9 m");
    expect(zustandBeschriftung(winkel(1, { reached: false, overshootMeters: null }), t)).toBe("nicht erreicht");
  });
});

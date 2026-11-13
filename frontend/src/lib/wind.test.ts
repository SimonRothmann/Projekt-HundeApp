import { describe, expect, it } from "vitest";
import type { GpsPoint, GpsWalkRunCorner, GpsWalkRunLeg } from "@/lib/types";
import {
  himmelsrichtung,
  massgeblicherWind,
  normalisiere,
  schenkelPeilungen,
  windJeSchenkel,
  windZuSchenkel,
  zeigtWindrichtung,
} from "@/lib/wind";

describe("himmelsrichtung", () => {
  it("ordnet Kompassgrade der Windrose zu", () => {
    expect(himmelsrichtung(0)).toBe("N");
    expect(himmelsrichtung(45)).toBe("NO");
    expect(himmelsrichtung(90)).toBe("O");
    expect(himmelsrichtung(135)).toBe("SO");
    expect(himmelsrichtung(180)).toBe("S");
    expect(himmelsrichtung(225)).toBe("SW");
    expect(himmelsrichtung(270)).toBe("W");
    expect(himmelsrichtung(315)).toBe("NW");
  });

  it("schneidet die Sektoren bei 22,5 Grad", () => {
    expect(himmelsrichtung(22)).toBe("N");
    expect(himmelsrichtung(23)).toBe("NO");
    expect(himmelsrichtung(337)).toBe("NW");
    expect(himmelsrichtung(338)).toBe("N");
  });

  it("läuft über 360 Grad hinweg", () => {
    expect(himmelsrichtung(359)).toBe("N");
    expect(himmelsrichtung(360)).toBe("N");
    expect(himmelsrichtung(-90)).toBe("W");
  });
});

describe("normalisiere", () => {
  it("bringt Winkel nach [0, 360)", () => {
    expect(normalisiere(360)).toBe(0);
    expect(normalisiere(-10)).toBe(350);
    expect(normalisiere(725)).toBe(5);
  });
});

describe("zeigtWindrichtung", () => {
  it("zeigt bei Windstille keine Richtung", () => {
    expect(zeigtWindrichtung(270, 0)).toBe(false);
    // 0,4 km/h wird als 0 km/h angezeigt - dann auch keine Richtung.
    expect(zeigtWindrichtung(270, 0.4)).toBe(false);
  });

  it("zeigt sie mit Wind und Richtung, sonst nicht", () => {
    expect(zeigtWindrichtung(270, 7)).toBe(true);
    expect(zeigtWindrichtung(0, 3)).toBe(true);
    expect(zeigtWindrichtung(null, 7)).toBe(false);
    expect(zeigtWindrichtung(270, null)).toBe(false);
  });
});

describe("windZuSchenkel", () => {
  it("Wind aus der Laufrichtung ist Gegenwind, aus der Gegenrichtung Rückenwind", () => {
    // Läuft nach Norden, Wind kommt aus Norden: er bläst einem ins Gesicht.
    expect(windZuSchenkel(0, 0)).toBe("gegenwind");
    expect(windZuSchenkel(0, 180)).toBe("rueckenwind");
  });

  it("Seitenwind von rechts und links nach der Laufrichtung", () => {
    // Läuft nach Norden: Wind aus Osten kommt von rechts, aus Westen von links.
    expect(windZuSchenkel(0, 90)).toBe("seitenwind-rechts");
    expect(windZuSchenkel(0, 270)).toBe("seitenwind-links");
    // Läuft nach Osten: aus Süden kommt von rechts.
    expect(windZuSchenkel(90, 180)).toBe("seitenwind-rechts");
    expect(windZuSchenkel(90, 0)).toBe("seitenwind-links");
  });

  it("die Sektoren sind je 90 Grad breit, die Grenzen zählen zu Gegen- und Rückenwind", () => {
    expect(windZuSchenkel(0, 45)).toBe("gegenwind");
    expect(windZuSchenkel(0, 46)).toBe("seitenwind-rechts");
    expect(windZuSchenkel(0, 134)).toBe("seitenwind-rechts");
    expect(windZuSchenkel(0, 135)).toBe("rueckenwind");
    expect(windZuSchenkel(0, 315)).toBe("gegenwind");
    expect(windZuSchenkel(0, 314)).toBe("seitenwind-links");
    expect(windZuSchenkel(0, 226)).toBe("seitenwind-links");
    expect(windZuSchenkel(0, 225)).toBe("rueckenwind");
  });

  it("rechnet über 360 Grad hinweg", () => {
    // Läuft nach 350, Wind aus 10: nur 20 Grad daneben, also Gegenwind.
    expect(windZuSchenkel(350, 10)).toBe("gegenwind");
    // Läuft nach 10, Wind aus 350: ebenso.
    expect(windZuSchenkel(10, 350)).toBe("gegenwind");
    // Läuft nach 350, Wind aus 100: 110 Grad im Uhrzeigersinn, also von rechts.
    expect(windZuSchenkel(350, 100)).toBe("seitenwind-rechts");
    // Läuft nach 10, Wind aus 260: 110 Grad gegen den Uhrzeigersinn, also von links.
    expect(windZuSchenkel(10, 260)).toBe("seitenwind-links");
    // Läuft nach 350, Wind aus 170: genau von hinten.
    expect(windZuSchenkel(350, 170)).toBe("rueckenwind");
  });
});

describe("massgeblicherWind", () => {
  it("nimmt den Wind beim Suchen, wenn dafür eine Richtung vorliegt", () => {
    expect(
      massgeblicherWind({ laidWindDirectionDeg: 90, laidWindSpeedKmh: 10, searchWindDirectionDeg: 200, searchWindSpeedKmh: 8 }),
    ).toEqual({ aus: 200, kmh: 8 });
  });

  it("fällt auf den Wind beim Legen zurück, wenn beim Suchen keine Richtung vorliegt", () => {
    expect(
      massgeblicherWind({ laidWindDirectionDeg: 90, laidWindSpeedKmh: 10, searchWindDirectionDeg: null, searchWindSpeedKmh: 8 }),
    ).toEqual({ aus: 90, kmh: 10 });
  });

  it("braucht mindestens 5 km/h und greift bei schwachem Wind beim Suchen nicht auf das Legen zurück", () => {
    expect(massgeblicherWind({ laidWindDirectionDeg: 90, laidWindSpeedKmh: 4.9 })).toBeNull();
    expect(massgeblicherWind({ laidWindDirectionDeg: 90, laidWindSpeedKmh: 5 })).toEqual({ aus: 90, kmh: 5 });
    expect(
      massgeblicherWind({ laidWindDirectionDeg: 90, laidWindSpeedKmh: 12, searchWindDirectionDeg: 200, searchWindSpeedKmh: 3 }),
    ).toBeNull();
  });

  it("liefert nichts für Fährten ohne Richtung", () => {
    expect(massgeblicherWind({ laidWindSpeedKmh: 20 })).toBeNull();
    expect(massgeblicherWind({})).toBeNull();
  });
});

// Eine Fährte von 100 m nach Norden, dann 100 m nach Osten: zwei Schenkel, ein Winkel (nach 100 m).
const BASIS = { latitude: 48.9, longitude: 8.5 };
const M_BREITE = 1 / 111111;
const M_LAENGE = 1 / (111111 * Math.cos((BASIS.latitude * Math.PI) / 180));

function punkt(nord: number, ost: number, minute: number): GpsPoint {
  return {
    latitude: BASIS.latitude + nord * M_BREITE,
    longitude: BASIS.longitude + ost * M_LAENGE,
    timestamp: new Date(Date.UTC(2026, 10, 7, 10, minute)).toISOString(),
    accuracy: 5,
    pointType: 0,
    label: null,
    markerType: 0,
  };
}

const LEGUNG: GpsPoint[] = [punkt(0, 0, 0), punkt(100, 0, 1), punkt(100, 100, 2)];
const WINKEL = [{ number: 1, latitude: LEGUNG[1].latitude, longitude: LEGUNG[1].longitude }] as Pick<
  GpsWalkRunCorner,
  "number" | "latitude" | "longitude"
>[];
const SCHENKEL = [{ number: 1 }, { number: 2 }] as Pick<GpsWalkRunLeg, "number">[];

describe("schenkelPeilungen", () => {
  it("misst je Schenkel Anfang nach Ende", () => {
    const [erster, zweiter] = schenkelPeilungen(SCHENKEL, WINKEL, LEGUNG);
    expect(erster).toBeCloseTo(0, 0);
    expect(zweiter).toBeCloseTo(90, 0);
  });

  it("ignoriert gesetzte Marker und die Reihenfolge der Eingabe", () => {
    const marker: GpsPoint = { ...punkt(50, 30, 0), pointType: 1 };
    const [erster, zweiter] = schenkelPeilungen(SCHENKEL, WINKEL, [LEGUNG[2], marker, LEGUNG[0], LEGUNG[1]]);
    expect(erster).toBeCloseTo(0, 0);
    expect(zweiter).toBeCloseTo(90, 0);
  });

  it("ohne Winkel ist die Fährte ein einziger Schenkel", () => {
    const [einziger] = schenkelPeilungen([{ number: 1 }], [], [LEGUNG[0], LEGUNG[2]]);
    expect(einziger).toBeCloseTo(45, 0);
  });

  it("gibt keine Richtung, wenn Schenkel und Winkel nicht zusammenpassen", () => {
    expect(schenkelPeilungen(SCHENKEL, [], LEGUNG)).toEqual([null, null]);
  });

  it("gibt keine Richtung ohne Legung", () => {
    expect(schenkelPeilungen(SCHENKEL, WINKEL, [])).toEqual([null, null]);
  });
});

describe("windJeSchenkel", () => {
  it("ordnet jedem Schenkel seine Windlage zu", () => {
    // Wind aus Norden (0): der erste Schenkel läuft nach Norden = Gegenwind, der zweite nach Osten, Wind kommt
    // von links.
    const lage = windJeSchenkel({ points: LEGUNG, laidWindDirectionDeg: 0, laidWindSpeedKmh: 12 }, SCHENKEL, WINKEL);
    expect(lage.get(1)).toBe("gegenwind");
    expect(lage.get(2)).toBe("seitenwind-links");
  });

  it("zeigt nichts bei schwachem Wind oder ohne Richtung", () => {
    expect(windJeSchenkel({ points: LEGUNG, laidWindDirectionDeg: 0, laidWindSpeedKmh: 2 }, SCHENKEL, WINKEL).size).toBe(0);
    expect(windJeSchenkel({ points: LEGUNG, laidWindSpeedKmh: 12 }, SCHENKEL, WINKEL).size).toBe(0);
    expect(windJeSchenkel({ laidWindDirectionDeg: 0, laidWindSpeedKmh: 12 }, SCHENKEL, WINKEL).size).toBe(0);
  });

  it("nimmt den Wind beim Suchen vor dem beim Legen", () => {
    const lage = windJeSchenkel(
      { points: LEGUNG, laidWindDirectionDeg: 0, laidWindSpeedKmh: 12, searchWindDirectionDeg: 180, searchWindSpeedKmh: 9 },
      SCHENKEL,
      WINKEL,
    );
    expect(lage.get(1)).toBe("rueckenwind");
    expect(lage.get(2)).toBe("seitenwind-rechts");
  });
});

import { describe, expect, it } from "vitest";
import { achsenbereich, gewichte, kgText, kurvenPunkte, parseKg, pruefeGewicht, veraenderung, veraenderungText } from "./gewicht";
import { uebersetze } from "./i18n";
import { GESUNDHEIT_ART, type DogHealthRecord } from "./types";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const HEUTE = "2026-11-20";

function wiegung(date: string, kg: number | null, teil: Partial<DogHealthRecord> = {}): DogHealthRecord {
  return {
    id: `${date}-${kg}`,
    kind: GESUNDHEIT_ART.Gewicht,
    title: "Gewicht",
    date,
    time: null,
    dueDate: null,
    note: null,
    weightKg: kg,
    isCurrent: false,
    ...teil,
  };
}

describe("parseKg", () => {
  it("liest Komma und Punkt und rundet auf eine Nachkommastelle", () => {
    expect(parseKg("24,3")).toBe(24.3);
    expect(parseKg(" 24.34 ")).toBe(24.3);
    expect(parseKg("24")).toBe(24);
  });
  it("lehnt Unbrauchbares ab", () => {
    for (const text of ["", "abc", "-3", "1,2,3", "24 kg", ","]) expect(parseKg(text)).toBeNull();
  });
});

describe("kgText und veraenderungText", () => {
  it("setzt immer eine Nachkommastelle und je Sprache das Dezimalzeichen", () => {
    expect(kgText(24.3, "de")).toBe("24,3 kg");
    expect(kgText(24, "en")).toBe("24.0 kg");
  });
  it("nennt das Vorzeichen immer", () => {
    expect(veraenderungText(0.4)).toBe("+0,4 kg");
    expect(veraenderungText(-0.2)).toBe("−0,2 kg");
    expect(veraenderungText(0)).toBe("±0,0 kg");
  });
});

describe("pruefeGewicht", () => {
  it("prüft Pflicht, Bereich und Datum wie der Server", () => {
    expect(pruefeGewicht(t, { datum: HEUTE, kg: "24,3" }, HEUTE)).toBeNull();
    expect(pruefeGewicht(t, { datum: HEUTE, kg: "" }, HEUTE)).toBe("Bitte ein Gewicht angeben.");
    expect(pruefeGewicht(t, { datum: HEUTE, kg: "0,4" }, HEUTE)).toContain("zwischen 0,5 und 150");
    expect(pruefeGewicht(t, { datum: HEUTE, kg: "150,1" }, HEUTE)).toContain("zwischen 0,5 und 150");
    expect(pruefeGewicht(t, { datum: HEUTE, kg: "0,5" }, HEUTE)).toBeNull();
    expect(pruefeGewicht(t, { datum: HEUTE, kg: "150" }, HEUTE)).toBeNull();
    expect(pruefeGewicht(t, { datum: "1990-01-01", kg: "20" }, HEUTE)).toBe("Das Datum liegt zu weit entfernt.");
    expect(pruefeGewicht(t, { datum: "", kg: "20" }, HEUTE)).toBe("Bitte ein Datum angeben.");
  });
});

describe("gewichte und veraenderung", () => {
  const liste = [
    wiegung("2026-11-12", 24.3),
    wiegung("2026-10-01", 23.9),
    wiegung("2026-01-01", null), // ohne Wert: gehört nicht dazu
    { ...wiegung("2026-11-01", 1), kind: GESUNDHEIT_ART.Impfung },
  ];
  it("sortiert nach Datum, ältestes zuerst, und lässt andere Arten weg", () => {
    expect(gewichte(liste).map((e) => e.date)).toEqual(["2026-10-01", "2026-11-12"]);
  });
  it("rechnet die Veränderung zum vorigen Eintrag", () => {
    expect(veraenderung(gewichte(liste))).toBe(0.4);
    expect(veraenderung(gewichte(liste).slice(0, 1))).toBeNull();
  });
});

describe("achsenbereich", () => {
  it("beginnt nicht bei 0 und lässt Luft um die Werte", () => {
    const { min, max } = achsenbereich([23.9, 24.3, 25.1]);
    expect(min).toBeGreaterThan(20);
    expect(min).toBeLessThanOrEqual(23.9);
    expect(max).toBeGreaterThanOrEqual(25.1);
  });
  it("ist bei gleichen Werten mindestens 2 kg hoch", () => {
    const { min, max } = achsenbereich([24, 24]);
    expect(max - min).toBeGreaterThanOrEqual(2);
    expect(min).toBeLessThan(24);
    expect(max).toBeGreaterThan(24);
  });
  it("wird nie negativ", () => {
    expect(achsenbereich([0.5, 0.6]).min).toBe(0);
  });
});

describe("kurvenPunkte", () => {
  it("verteilt x nach Datum (echte Abstände) und dreht y um", () => {
    const punkte = kurvenPunkte([wiegung("2026-10-01", 20), wiegung("2026-10-11", 22), wiegung("2026-10-31", 24)], 300, 100);
    expect(punkte.map((p) => Math.round(p.x))).toEqual([0, 100, 300]);
    expect(punkte[0].y).toBeGreaterThan(punkte[2].y);
    for (const p of punkte) {
      expect(p.y).toBeGreaterThanOrEqual(0);
      expect(p.y).toBeLessThanOrEqual(100);
    }
  });
  it("setzt Einträge vom selben Tag in die Mitte", () => {
    expect(kurvenPunkte([wiegung("2026-10-01", 20), wiegung("2026-10-01", 21)], 300, 100).map((p) => p.x)).toEqual([150, 150]);
  });
});

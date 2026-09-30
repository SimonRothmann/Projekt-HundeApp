import { describe, expect, it } from "vitest";
import { AMBER_MAX_M, GREEN_MAX_M } from "@/components/tracking/walk-run-evaluation";
import {
  ampelStufe,
  baueBildTexte,
  faehrtenGeometrie,
  faehrtenKennzahlen,
  faerbeSegmente,
  formatiereMassstab,
  inMeter,
  legedatumFuerDatei,
  liegezeitMinuten,
  massstabsbalken,
  mittelpunkt,
  neuesterAusgewerteterAblauf,
  passeEin,
} from "./faehrten-bild";
import { uebersetze } from "./i18n";
import type { GpsPoint, GpsTrack, GpsWalkPoint, GpsWalkRun } from "./types";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const tEn = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

// 0,001 Grad Breite sind rund 111,19 m.
const METER_JE_GRAD = (6371000 * Math.PI) / 180;

function punkt(latitude: number, longitude: number, zeit: string, pointType: 0 | 1 = 0, markerType: 0 | 1 | 2 | 3 = 0): GpsPoint {
  return { latitude, longitude, timestamp: zeit, accuracy: 5, pointType, label: null, markerType };
}

function laufpunkt(latitude: number, longitude: number, zeit: string, deviationMeters: number | null): GpsWalkPoint {
  return { latitude, longitude, timestamp: zeit, accuracy: 5, deviationMeters };
}

function ablauf(teil: Partial<GpsWalkRun> = {}): GpsWalkRun {
  return {
    id: "r1",
    trackId: "t",
    createdAt: "2026-09-10T10:00:00Z",
    lengthMeters: 100,
    comment: null,
    points: [laufpunkt(48.9, 8.5, "2026-09-10T09:40:00Z", 1), laufpunkt(48.9005, 8.5, "2026-09-10T09:41:00Z", 4)],
    avgDeviationMeters: 2.6,
    maxDeviationMeters: 5,
    onTrackPercent: 86.6,
    articlesFound: 3,
    articlesTotal: 4,
    evaluatedAt: "2026-09-10T10:05:00Z",
    stops: [],
    ...teil,
  };
}

function track(teil: Partial<GpsTrack> = {}): GpsTrack {
  return {
    id: "t",
    trainingSessionId: "s",
    lengthMeters: 312.4,
    ageMinutes: null,
    surface: null,
    weather: null,
    wind: null,
    comment: null,
    points: [
      punkt(48.9, 8.5, "2026-09-10T09:00:00Z"),
      punkt(48.9005, 8.5, "2026-09-10T09:10:00Z"),
      punkt(48.9005, 8.501, "2026-09-10T09:20:00Z"),
      punkt(48.9003, 8.5005, "2026-09-10T09:25:00Z", 1, 0),
    ],
    walkRuns: [],
    laidTemperatureC: null,
    laidRelativeHumidity: null,
    laidWindSpeedKmh: null,
    laidWeatherCode: null,
    searchTemperatureC: null,
    searchRelativeHumidity: null,
    searchWindSpeedKmh: null,
    searchWeatherCode: null,
    temperatureDeltaC: null,
    weatherFetchedAt: null,
    ...teil,
  };
}

describe("mittelpunkt", () => {
  it("nimmt die Mitte des umschließenden Rechtecks", () => {
    expect(mittelpunkt([{ latitude: 48, longitude: 8 }, { latitude: 50, longitude: 12 }, { latitude: 49, longitude: 9 }])).toEqual({
      latitude: 49,
      longitude: 10,
    });
  });

  it("gibt ohne Punkte null zurück", () => {
    expect(mittelpunkt([])).toBeNull();
  });
});

describe("inMeter", () => {
  it("setzt den Mittelpunkt auf den Ursprung", () => {
    const [p] = inMeter([{ latitude: 48.9, longitude: 8.5 }], { latitude: 48.9, longitude: 8.5 });
    expect(p.x).toBeCloseTo(0);
    expect(p.y).toBeCloseTo(0);
  });

  it("zählt Norden als positives y und rechnet Breitengrade in Meter", () => {
    const [p] = inMeter([{ latitude: 48.901, longitude: 8.5 }], { latitude: 48.9, longitude: 8.5 });
    expect(p.y).toBeCloseTo(0.001 * METER_JE_GRAD, 3);
    expect(p.x).toBeCloseTo(0);
  });

  it("verkürzt Längengrade um den Kosinus der Breite", () => {
    const mitte = { latitude: 60, longitude: 8.5 };
    const [p] = inMeter([{ latitude: 60, longitude: 8.501 }], mitte);
    // Bei 60 Grad Nord ist ein Längengrad halb so lang wie am Äquator.
    expect(p.x).toBeCloseTo(0.001 * METER_JE_GRAD * 0.5, 3);
    expect(p.y).toBeCloseTo(0);
  });
});

describe("passeEin", () => {
  const flaeche = { x: 100, y: 200, breite: 1000, hoehe: 500 };

  it("erhält das Seitenverhältnis: gleicher Maßstab in beide Richtungen", () => {
    const e = passeEin([{ x: 0, y: 0 }, { x: 100, y: 50 }], flaeche, 50);
    const oben = e.abbilden({ x: 0, y: 50 });
    const unten = e.abbilden({ x: 0, y: 0 });
    const rechts = e.abbilden({ x: 100, y: 0 });
    expect(unten.y - oben.y).toBeCloseTo(50 * e.masstab);
    expect(rechts.x - unten.x).toBeCloseTo(100 * e.masstab);
  });

  it("hält den Rand ein und nutzt die engere Richtung voll aus", () => {
    // 100 m breit, 50 m hoch; innen 900 x 400 px -> Höhe bestimmt nicht, Breite 9 px/m, Höhe 8 px/m.
    const e = passeEin([{ x: 0, y: 0 }, { x: 100, y: 50 }], flaeche, 50);
    expect(e.masstab).toBeCloseTo(8);
    const links = e.abbilden({ x: 0, y: 25 });
    const rechts = e.abbilden({ x: 100, y: 25 });
    expect(links.x).toBeGreaterThanOrEqual(flaeche.x + 50);
    expect(rechts.x).toBeLessThanOrEqual(flaeche.x + flaeche.breite - 50);
    const oben = e.abbilden({ x: 50, y: 50 });
    const unten = e.abbilden({ x: 50, y: 0 });
    expect(oben.y).toBeCloseTo(flaeche.y + 50);
    expect(unten.y).toBeCloseTo(flaeche.y + flaeche.hoehe - 50);
  });

  it("zeichnet Norden oben und Osten rechts", () => {
    const e = passeEin([{ x: -10, y: -10 }, { x: 10, y: 10 }], flaeche, 0);
    expect(e.abbilden({ x: 0, y: 10 }).y).toBeLessThan(e.abbilden({ x: 0, y: -10 }).y);
    expect(e.abbilden({ x: 10, y: 0 }).x).toBeGreaterThan(e.abbilden({ x: -10, y: 0 }).x);
  });

  it("zentriert in der Fläche", () => {
    const e = passeEin([{ x: 0, y: 0 }, { x: 100, y: 50 }], flaeche, 50);
    const mitte = e.abbilden({ x: 50, y: 25 });
    expect(mitte.x).toBeCloseTo(flaeche.x + flaeche.breite / 2);
    expect(mitte.y).toBeCloseTo(flaeche.y + flaeche.hoehe / 2);
  });

  it("kommt mit einem einzelnen Punkt und einer geraden Linie zurecht", () => {
    const punktE = passeEin([{ x: 5, y: 5 }], flaeche, 50);
    expect(Number.isFinite(punktE.masstab)).toBe(true);
    expect(punktE.abbilden({ x: 5, y: 5 }).x).toBeCloseTo(flaeche.x + flaeche.breite / 2);

    const gerade = passeEin([{ x: 0, y: 0 }, { x: 200, y: 0 }], flaeche, 50);
    expect(Number.isFinite(gerade.masstab)).toBe(true);
    expect(gerade.masstab).toBeCloseTo(4.5);
  });
});

describe("massstabsbalken", () => {
  it("wählt die größte runde Länge, die in die Breite passt", () => {
    // 2 px/m, 300 px Platz: 150 m passen, rund ist 100 m.
    expect(massstabsbalken(2, 300)).toEqual({ meter: 100, breitePx: 200 });
    // 3 px/m: 100 m wären 300 px - passt gerade noch.
    expect(massstabsbalken(3, 300)).toEqual({ meter: 100, breitePx: 300 });
    expect(massstabsbalken(1.5, 300)?.meter).toBe(200);
    expect(massstabsbalken(10, 300)?.meter).toBe(20);
    expect(massstabsbalken(6, 300)?.meter).toBe(50);
    expect(massstabsbalken(40, 300)?.meter).toBe(5);
  });

  it("geht über 1000 m hinaus", () => {
    expect(massstabsbalken(0.1, 300)?.meter).toBe(2000);
  });

  it("gibt bei unbrauchbarem Maßstab null zurück", () => {
    expect(massstabsbalken(0, 300)).toBeNull();
    expect(massstabsbalken(Number.NaN, 300)).toBeNull();
    expect(massstabsbalken(1, 0)).toBeNull();
  });
});

describe("formatiereMassstab", () => {
  it("schreibt Meter und Kilometer", () => {
    expect(formatiereMassstab(50)).toBe("50 m");
    expect(formatiereMassstab(1000)).toBe("1 km");
    expect(formatiereMassstab(2000)).toBe("2 km");
  });
});

describe("ampelStufe", () => {
  it("nutzt die Schwellen der Auswertung im Tagebuch", () => {
    expect(ampelStufe(GREEN_MAX_M)).toBe("gruen");
    expect(ampelStufe(GREEN_MAX_M + 0.1)).toBe("gelb");
    expect(ampelStufe(AMBER_MAX_M)).toBe("gelb");
    expect(ampelStufe(AMBER_MAX_M + 0.1)).toBe("rot");
    expect(ampelStufe(0)).toBe("gruen");
  });
});

describe("faerbeSegmente", () => {
  it("fasst aufeinanderfolgende Stücke gleicher Farbe zusammen", () => {
    const stuecke = faerbeSegmente([1, 2, 2.5, 4, 5, 8, 9]);
    expect(stuecke).toEqual([
      { stufe: "gruen", von: 0, bis: 2 },
      { stufe: "gelb", von: 2, bis: 4 },
      { stufe: "rot", von: 4, bis: 6 },
    ]);
  });

  it("färbt ein Stück nach der schlechteren seiner beiden Enden", () => {
    expect(faerbeSegmente([1, 7])).toEqual([{ stufe: "rot", von: 0, bis: 1 }]);
  });

  it("lässt Stücke ohne Abweichung neutral", () => {
    expect(faerbeSegmente([null, null, 1])).toEqual([
      { stufe: null, von: 0, bis: 1 },
      { stufe: "gruen", von: 1, bis: 2 },
    ]);
  });

  it("liefert ohne zwei Punkte keine Stücke", () => {
    expect(faerbeSegmente([])).toEqual([]);
    expect(faerbeSegmente([2])).toEqual([]);
  });
});

describe("neuesterAusgewerteterAblauf", () => {
  it("nimmt den jüngsten ausgewerteten und übergeht nicht ausgewertete", () => {
    const alt = ablauf({ id: "alt", createdAt: "2026-09-10T10:00:00Z" });
    const neu = ablauf({ id: "neu", createdAt: "2026-09-11T10:00:00Z" });
    const offen = ablauf({ id: "offen", createdAt: "2026-09-12T10:00:00Z", evaluatedAt: null, avgDeviationMeters: null });
    expect(neuesterAusgewerteterAblauf([neu, offen, alt])?.id).toBe("neu");
  });

  it("gibt null zurück, wenn nichts ausgewertet ist", () => {
    expect(neuesterAusgewerteterAblauf([])).toBeNull();
    expect(neuesterAusgewerteterAblauf([ablauf({ evaluatedAt: null, avgDeviationMeters: null })])).toBeNull();
  });

  it("verträgt fehlende Felder älterer Stände", () => {
    const aelter = { id: "x", createdAt: "2026-09-10T10:00:00Z", points: [] } as unknown as GpsWalkRun;
    expect(neuesterAusgewerteterAblauf([aelter])).toBeNull();
  });
});

describe("liegezeitMinuten", () => {
  it("nimmt eine am Track hinterlegte Liegezeit", () => {
    expect(liegezeitMinuten(track({ ageMinutes: 30 }), ablauf())).toBe(30);
  });

  it("errechnet sie sonst vom Ende des Legens bis zum Start des Ablaufs", () => {
    // Ende des Legens 09:20 (der Marker um 09:25 zählt nicht), Ablauf ab 09:40.
    expect(liegezeitMinuten(track(), ablauf())).toBe(20);
  });

  it("gibt ohne Ablauf null zurück", () => {
    expect(liegezeitMinuten(track(), null)).toBeNull();
  });

  it("lehnt einen Ablauf vor dem Legen ab", () => {
    const vorher = ablauf({ points: [laufpunkt(48.9, 8.5, "2026-09-10T08:00:00Z", 1)] });
    expect(liegezeitMinuten(track(), vorher)).toBeNull();
  });
});

describe("faehrtenKennzahlen", () => {
  it("rundet auf ganze Zahlen", () => {
    const kz = faehrtenKennzahlen(track(), ablauf());
    expect(kz.aufFaehrtePercent).toBe(87);
    expect(kz.mittlereAbweichungM).toBe(3);
    expect(kz.ampel).toBe("gruen");
    expect(kz.laengeM).toBe(312);
    expect(kz.gefunden).toBe(3);
    expect(kz.gesamt).toBe(4);
    expect(kz.liegezeitMin).toBe(20);
  });

  it("färbt die Ampel nach der ungerundeten Abweichung", () => {
    // 3,4 m runden auf 3, sind aber über der grünen Schwelle.
    expect(faehrtenKennzahlen(track(), ablauf({ avgDeviationMeters: 3.4 })).ampel).toBe("gelb");
  });

  it("lässt ohne Ablauf nur Länge und Liegezeit übrig", () => {
    const kz = faehrtenKennzahlen(track(), null);
    expect(kz.aufFaehrtePercent).toBeNull();
    expect(kz.ampel).toBeNull();
    expect(kz.gesamt).toBe(0);
    expect(kz.laengeM).toBe(312);
  });

  it("schätzt die Länge aus den Punkten, wenn keine gespeichert ist", () => {
    const kz = faehrtenKennzahlen(track({ lengthMeters: null }), null);
    expect(kz.laengeM).toBeGreaterThan(100);
  });

  it("übernimmt die Temperatur nur, wenn sie am Track steht", () => {
    expect(faehrtenKennzahlen(track(), ablauf()).legenC).toBeNull();
    const mit = faehrtenKennzahlen(track({ laidTemperatureC: 12.4, searchTemperatureC: 15.1, temperatureDeltaC: 2.7 }), ablauf());
    expect([mit.legenC, mit.suchenC, mit.deltaC]).toEqual([12.4, 15.1, 2.7]);
  });
});

describe("faehrtenGeometrie", () => {
  it("braucht mindestens zwei automatische Punkte", () => {
    expect(faehrtenGeometrie(track({ points: [punkt(48.9, 8.5, "2026-09-10T09:00:00Z")] }), null)).toBeNull();
    // Marker zählen nicht zur Linie.
    expect(
      faehrtenGeometrie(
        track({ points: [punkt(48.9, 8.5, "2026-09-10T09:00:00Z"), punkt(48.9, 8.5, "2026-09-10T09:01:00Z", 1)] }),
        null,
      ),
    ).toBeNull();
  });

  it("trennt Linie, Marker und Ablauf", () => {
    const g = faehrtenGeometrie(track(), ablauf());
    expect(g?.gelegt).toHaveLength(3);
    expect(g?.marker).toHaveLength(1);
    expect(g?.marker[0].gegenstand).toBe(true);
    expect(g?.ablauf?.punkte).toHaveLength(2);
    expect(g?.ablauf?.abweichungen).toEqual([1, 4]);
  });

  it("unterscheidet Gegenstände von anderen Markern", () => {
    const g = faehrtenGeometrie(
      track({
        points: [
          punkt(48.9, 8.5, "2026-09-10T09:00:00Z"),
          punkt(48.9005, 8.5, "2026-09-10T09:10:00Z"),
          punkt(48.9002, 8.5, "2026-09-10T09:05:00Z", 1, 1),
        ],
      }),
      null,
    );
    expect(g?.marker[0].gegenstand).toBe(false);
    expect(g?.ablauf).toBeNull();
  });

  it("gibt Koordinaten nicht weiter: nur Meter um den Mittelpunkt", () => {
    const g = faehrtenGeometrie(track(), ablauf());
    const alle = [...(g?.gelegt ?? []), ...(g?.ablauf?.punkte ?? [])];
    for (const p of alle) {
      expect(Math.abs(p.x)).toBeLessThan(1000);
      expect(Math.abs(p.y)).toBeLessThan(1000);
    }
  });
});

describe("baueBildTexte", () => {
  const eingabe = { track: track(), ablauf: ablauf(), hundeName: "Rex", sprache: "de" };

  it("nennt Hund und Fährte im Titel, ohne Namen nur die Fährte", () => {
    expect(baueBildTexte(t, eingabe).titel).toBe("Rex · Fährte");
    expect(baueBildTexte(t, { ...eingabe, hundeName: null }).titel).toBe("Fährte");
    expect(baueBildTexte(tEn, eingabe).titel).toBe("Rex · Track");
  });

  it("schreibt den Tag aus, ohne Uhrzeit", () => {
    const { datum } = baueBildTexte(t, eingabe);
    expect(datum).toContain("2026");
    expect(datum).not.toMatch(/\d:\d/);
  });

  it("zeigt Prozent, Ampelwort und Zeilen in ganzen Zahlen", () => {
    const texte = baueBildTexte(t, eingabe);
    expect(texte.kopfzahl).toBe("87 % auf der Fährte");
    expect(texte.ampelWort).toBe("eng an der Fährte");
    expect(texte.zeilen).toEqual(["3/4 Gegenstände", "Liegezeit 20 min", "Länge 312 m"]);
  });

  it("übersetzt auch das Ampelwort", () => {
    expect(baueBildTexte(tEn, eingabe).kopfzahl).toBe("87 % on the track");
  });

  it("lässt Gegenstände weg, wenn es keine gab", () => {
    const texte = baueBildTexte(t, { ...eingabe, ablauf: ablauf({ articlesFound: 0, articlesTotal: 0 }) });
    expect(texte.zeilen.some((z) => z.includes("Gegenstände"))).toBe(false);
  });

  it("zeigt die Temperatur nur, wenn sie am Track steht", () => {
    expect(baueBildTexte(t, eingabe).zeilen.some((z) => z.includes("°C"))).toBe(false);
    const mit = baueBildTexte(t, {
      ...eingabe,
      track: track({ laidTemperatureC: 12.4, searchTemperatureC: 15.1, temperatureDeltaC: 2.7 }),
    });
    expect(mit.zeilen[mit.zeilen.length - 1]).toBe("Legen 12,4 °C · Suchen 15,1 °C (+2,7 K)");
  });

  it("zeigt bei nur einer Temperatur keine Änderung", () => {
    const nurLegen = baueBildTexte(t, { ...eingabe, track: track({ laidTemperatureC: 12.4 }) });
    expect(nurLegen.zeilen[nurLegen.zeilen.length - 1]).toBe("Legen 12,4 °C");
  });

  it("zeigt ohne ausgewerteten Ablauf nur die gelegte Fährte mit Länge und Liegezeit", () => {
    const mitOffenemAblauf = track({ walkRuns: [ablauf({ evaluatedAt: null, avgDeviationMeters: null })] });
    const texte = baueBildTexte(t, { ...eingabe, track: mitOffenemAblauf, ablauf: null });
    expect(texte.kopfzahl).toBe("Länge 312 m");
    expect(texte.ampelWort).toBeNull();
    expect(texte.zeilen).toEqual(["Liegezeit 20 min"]);
  });

  it("kommt ganz ohne Ablauf mit der Länge aus", () => {
    const texte = baueBildTexte(t, { ...eingabe, track: track(), ablauf: null });
    expect(texte.kopfzahl).toBe("Länge 312 m");
    expect(texte.zeilen).toEqual([]);
  });

  it("enthält nichts, was wie Koordinaten oder eine Uhrzeit aussieht", () => {
    const texte = baueBildTexte(t, {
      ...eingabe,
      track: track({ laidTemperatureC: 12.4, searchTemperatureC: 15.1, temperatureDeltaC: 2.7 }),
    });
    const alles = [texte.titel, texte.datum ?? "", texte.kopfzahl, texte.ampelWort ?? "", ...texte.zeilen].join(" | ");
    expect(alles).not.toMatch(/48\.9|8\.5|48,9|\d{1,2}:\d{2}/);
  });
});

describe("legedatumFuerDatei", () => {
  it("nennt den Tag des Legens ohne Hundenamen", () => {
    expect(legedatumFuerDatei(track({ points: [punkt(48.9, 8.5, "2026-09-10T12:00:00Z"), punkt(48.9, 8.5, "2026-09-10T12:10:00Z")] }))).toBe(
      "2026-09-10",
    );
  });
});

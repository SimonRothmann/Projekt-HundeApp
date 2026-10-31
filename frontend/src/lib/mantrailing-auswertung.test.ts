import { describe, expect, it } from "vitest";
import { ampelLabel } from "@/components/tracking/walk-run-evaluation";
import { ampelStufe, baueBildTexte, faehrtenKennzahlen, faerbeSegmente } from "./faehrten-bild";
import { ABLAUF_FRIST_STUNDEN, ABLAUF_FRIST_STUNDEN_TRAIL, kannNochAblaufen, spurArten } from "./faehrte";
import { uebersetze } from "./i18n";
import { ablaufDaten, DEVIATION_COLORS, deviationColor } from "./karte/spur-daten";
import { ART_FAEHRTE, ART_MANTRAILING, FAEHRTE_GRENZEN, MANTRAILING_GRENZEN, spurGrenzen } from "./mantrailing";
import { anfrageAusSicherung, type FaehrtenSicherung } from "./aufzeichnung-sicherung";
import type { GpsPoint, GpsTrack, GpsWalkRun, Sport } from "./types";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);

function punkt(zeit: string): GpsPoint {
  return { latitude: 48.9, longitude: 8.5, timestamp: zeit, accuracy: 5, pointType: 0, label: null, markerType: 0 };
}

function spur(kind: number, teil: Partial<GpsTrack> = {}): GpsTrack {
  return {
    id: "t",
    trainingSessionId: "s",
    lengthMeters: 300,
    ageMinutes: null,
    surface: null,
    weather: null,
    wind: null,
    comment: null,
    points: [punkt("2026-10-30T10:00:00Z"), { ...punkt("2026-10-30T10:05:00Z"), latitude: 48.9027 }],
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
    kind,
    ...teil,
  };
}

function lauf(abweichungen: number[], avg: number): GpsWalkRun {
  return {
    id: "r",
    trackId: "t",
    createdAt: "2026-10-30T12:00:00Z",
    lengthMeters: 300,
    comment: null,
    points: abweichungen.map((d, i) => ({
      latitude: 48.9 + i * 0.0001,
      longitude: 8.5,
      timestamp: `2026-10-30T12:0${i}:00Z`,
      accuracy: 5,
      deviationMeters: d,
    })),
    avgDeviationMeters: avg,
    maxDeviationMeters: Math.max(...abweichungen),
    onTrackPercent: 40,
    articlesFound: 0,
    articlesTotal: 0,
    evaluatedAt: "2026-10-30T12:10:00Z",
    stops: [],
  };
}

describe("Ampel je Art", () => {
  it("dieselbe Abweichung: bei der Fährte rot, beim Mantrailing grün bzw. gelb", () => {
    expect(ampelStufe(8)).toBe("rot");
    expect(ampelStufe(8, MANTRAILING_GRENZEN)).toBe("gruen");
    expect(ampelStufe(18, MANTRAILING_GRENZEN)).toBe("gelb");
    expect(ampelStufe(26, MANTRAILING_GRENZEN)).toBe("rot");
  });

  it("die Grenzen gelten einschließlich: 10 m und 25 m sind noch grün bzw. gelb", () => {
    expect(ampelStufe(10, MANTRAILING_GRENZEN)).toBe("gruen");
    expect(ampelStufe(10.1, MANTRAILING_GRENZEN)).toBe("gelb");
    expect(ampelStufe(25, MANTRAILING_GRENZEN)).toBe("gelb");
    expect(ampelStufe(25.1, MANTRAILING_GRENZEN)).toBe("rot");
  });

  it("die Fährte bleibt bei 3/6 m, auch ohne Angabe", () => {
    expect(ampelStufe(3)).toBe("gruen");
    expect(ampelStufe(6)).toBe("gelb");
    expect(ampelStufe(6.1)).toBe("rot");
    expect(ampelStufe(6.1, FAEHRTE_GRENZEN)).toBe("rot");
  });

  it("färbt die Linie nach den Grenzen der Art", () => {
    expect(faerbeSegmente([8, 9])).toEqual([{ stufe: "rot", von: 0, bis: 1 }]);
    expect(faerbeSegmente([8, 9], MANTRAILING_GRENZEN)).toEqual([{ stufe: "gruen", von: 0, bis: 1 }]);
  });

  it("färbt die Kartenlinie nach den Grenzen der Art", () => {
    expect(deviationColor(8)).toBe(DEVIATION_COLORS.red);
    expect(deviationColor(8, MANTRAILING_GRENZEN)).toBe(DEVIATION_COLORS.green);
    const daten = ablaufDaten([lauf([8, 18], 13)], t, MANTRAILING_GRENZEN);
    expect(daten.bewertet.features[0].properties.farbe).toBe(DEVIATION_COLORS.amber);
    expect(ablaufDaten([lauf([8, 18], 13)], t).bewertet.features[0].properties.farbe).toBe(DEVIATION_COLORS.red);
  });

  it("nennt die Ampel beim Mantrailing „nah an der Spur“", () => {
    expect(ampelLabel(8)).toBe("deutlich abgekommen");
    expect(ampelLabel(8, { kind: ART_MANTRAILING })).toBe("nah an der Spur");
    expect(ampelLabel(2)).toBe("eng an der Fährte");
    expect(ampelLabel(18, { kind: ART_MANTRAILING })).toBe("mittlere Abweichung");
  });

  it("nimmt die Grenzen aus der Spur", () => {
    expect(spurGrenzen(spur(ART_MANTRAILING))).toBe(MANTRAILING_GRENZEN);
    expect(spurGrenzen(spur(ART_FAEHRTE))).toBe(FAEHRTE_GRENZEN);
  });
});

describe("Fährtenbild beim Trail", () => {
  const eingabe = (kind: number) => ({
    track: spur(kind),
    ablauf: lauf([6, 9], 7.5),
    hundeName: "Rex",
    sprache: "de",
  });

  it("zeigt die Ampel nach den weiteren Grenzen und nennt sie in der Legende", () => {
    expect(faehrtenKennzahlen(spur(ART_MANTRAILING), lauf([6, 9], 7.5)).ampel).toBe("gruen");
    expect(faehrtenKennzahlen(spur(ART_FAEHRTE), lauf([6, 9], 7.5)).ampel).toBe("rot");

    const texte = baueBildTexte(t, eingabe(ART_MANTRAILING));
    expect(texte.titel).toBe("Rex · Trail");
    expect(texte.kopfzahl).toBe("40 % auf der Spur");
    expect(texte.ampelWort).toBe("nah an der Spur");
    expect(texte.legende.abgelaufen).toBe("Abgelaufen, grün bis 10 m");
  });

  it("lässt die Fährte unverändert", () => {
    const texte = baueBildTexte(t, eingabe(ART_FAEHRTE));
    expect(texte.titel).toBe("Rex · Fährte");
    expect(texte.kopfzahl).toBe("40 % auf der Fährte");
    expect(texte.legende.abgelaufen).toBe("Abgelaufen");
  });
});

describe("Spuren eines Hundes", () => {
  const sport = (id: string, code: string): Sport => ({ id, code, name: code, description: null, clubId: null });
  const SPORTS = [sport("bh", "BH"), sport("faehrte", "FAERTE"), sport("mt", "MANTRAILING")];

  it("ohne Auswahl nur die Fährte - ein Trail nur auf ausdrückliche Wahl", () => {
    expect(spurArten(null, SPORTS)).toEqual({ faehrte: true, mantrailing: false });
    expect(spurArten([], SPORTS)).toEqual({ faehrte: true, mantrailing: false });
  });

  it("trennt Fährte und Trail nach den gewählten Sportarten", () => {
    expect(spurArten(["mt"], SPORTS)).toEqual({ faehrte: false, mantrailing: true });
    expect(spurArten(["faehrte"], SPORTS)).toEqual({ faehrte: true, mantrailing: false });
    expect(spurArten(["faehrte", "mt"], SPORTS)).toEqual({ faehrte: true, mantrailing: true });
    expect(spurArten(["bh"], SPORTS)).toEqual({ faehrte: false, mantrailing: false });
  });
});

describe("Frist zum Suchen", () => {
  const gelegt = (kind: number) => spur(kind, { points: [punkt("2026-10-30T10:00:00Z")] });
  const nach = (stunden: number) => Date.parse("2026-10-30T10:00:00Z") + stunden * 3_600_000;

  it("Fährte 24 Stunden, Trail 48", () => {
    expect(kannNochAblaufen(gelegt(ART_FAEHRTE), nach(ABLAUF_FRIST_STUNDEN))).toBe(true);
    expect(kannNochAblaufen(gelegt(ART_FAEHRTE), nach(ABLAUF_FRIST_STUNDEN + 1))).toBe(false);
    expect(kannNochAblaufen(gelegt(ART_MANTRAILING), nach(ABLAUF_FRIST_STUNDEN + 1))).toBe(true);
    expect(kannNochAblaufen(gelegt(ART_MANTRAILING), nach(ABLAUF_FRIST_STUNDEN_TRAIL + 1))).toBe(false);
  });
});

describe("Anfrage beim Speichern", () => {
  const sicherung = (trail?: FaehrtenSicherung["trail"]): FaehrtenSicherung => ({
    art: "faehrte",
    userId: "u",
    seite: "/dogs/d",
    begonnen: Date.parse("2026-10-30T10:00:00Z"),
    trackId: "t",
    dogId: "d",
    untergrund: [],
    points: [punkt("2026-10-30T10:00:00Z")],
    trail,
  });

  it("schickt bei der Fährte nichts von den Trail-Angaben", () => {
    const { body } = anfrageAusSicherung(sicherung());
    expect(body).not.toHaveProperty("kind");
    expect(body).not.toHaveProperty("doubleBlind");
    expect(body).not.toHaveProperty("scentArticle");
  });

  it("schickt beim Trail Art und Angaben, der Geruchsartikel getrimmt", () => {
    const { body } = anfrageAusSicherung(sicherung({ doubleBlind: true, negativeTrail: false, scentArticle: "  Mütze " }));
    expect(body).toMatchObject({ kind: ART_MANTRAILING, doubleBlind: true, negativeTrail: false, scentArticle: "Mütze" });
    const leer = anfrageAusSicherung(sicherung({ doubleBlind: false, negativeTrail: true, scentArticle: "  " })).body;
    expect(leer).toMatchObject({ negativeTrail: true, scentArticle: null });
  });
});

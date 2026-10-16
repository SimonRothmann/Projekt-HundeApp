import { describe, expect, it } from "vitest";
import { kannNochAblaufen, laeuftFaehrte, nichtAbgelaufeneFaehrten } from "./faehrte";
import type { GpsPoint, GpsTrack, Sport } from "./types";

const sport = (id: string, code: string): Sport => ({ id, code, name: code, description: null, clubId: null });
const SPORTS = [sport("bh", "BH"), sport("fpr", "FPR1"), sport("faehrte", "FAERTE")];

describe("laeuftFaehrte", () => {
  it("bietet die Fährte an, solange keine Einschränkung gilt", () => {
    expect(laeuftFaehrte(null, SPORTS)).toBe(true);
    expect(laeuftFaehrte([], SPORTS)).toBe(true);
  });

  it("bietet sie an, wenn eine Fährten-Sportart gewählt ist", () => {
    expect(laeuftFaehrte(["bh", "faehrte"], SPORTS)).toBe(true);
  });

  it("bietet sie nicht an, wenn nur andere Sportarten gewählt sind", () => {
    expect(laeuftFaehrte(["bh"], SPORTS)).toBe(false);
  });
});

function punkt(timestamp: string, pointType = 0): GpsPoint {
  return { latitude: 48.9, longitude: 8.5, timestamp, accuracy: 5, pointType, label: null, markerType: 0 } as GpsPoint;
}

function track(id: string, punkte: GpsPoint[], mitAblauf = false): GpsTrack {
  return {
    id,
    trainingSessionId: "s",
    lengthMeters: 300,
    ageMinutes: null,
    surface: null,
    weather: null,
    wind: null,
    comment: null,
    points: punkte,
    walkRuns: mitAblauf ? [{ id: "r" } as GpsTrack["walkRuns"][number]] : [],
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
  };
}

describe("nichtAbgelaufeneFaehrten", () => {
  const JETZT = Date.parse("2026-09-10T10:00:00Z");

  it("lässt Fährten weg, die schon abgelaufen wurden", () => {
    const tracks = [track("a", [punkt("2026-09-10T09:00:00Z")], true), track("b", [punkt("2026-09-10T09:10:00Z")])];

    expect(nichtAbgelaufeneFaehrten(tracks, JETZT).map((f) => f.track.id)).toEqual(["b"]);
  });

  it("zählt das Alter ab dem Ende des Legens und übergeht spätere Marker", () => {
    // Ein Marker kann nach dem letzten Linienpunkt gesetzt worden sein - er
    // darf das Fährtenalter nicht verkürzen.
    const tracks = [
      track("a", [punkt("2026-09-10T09:00:00Z"), punkt("2026-09-10T09:20:00Z"), punkt("2026-09-10T09:40:00Z", 1)]),
    ];

    expect(nichtAbgelaufeneFaehrten(tracks, JETZT)[0].alterMinuten).toBe(40);
  });

  it("sortiert in Legereihenfolge", () => {
    const tracks = [track("spaet", [punkt("2026-09-10T09:30:00Z")]), track("frueh", [punkt("2026-09-10T08:00:00Z")])];

    expect(nichtAbgelaufeneFaehrten(tracks, JETZT).map((f) => f.track.id)).toEqual(["frueh", "spaet"]);
  });
});

describe("kannNochAblaufen", () => {
  const stunden = (n: number) => n * 60 * 60 * 1000;
  const ENDE = Date.parse("2026-09-10T21:00:00Z");
  const gelegt = track("a", [punkt("2026-09-10T20:40:00Z"), punkt("2026-09-10T21:00:00Z")]);

  it("erlaubt den Ablauf kurz nach dem Legen", () => {
    expect(kannNochAblaufen(gelegt, ENDE + stunden(1))).toBe(true);
  });

  it("erlaubt den Ablauf über Mitternacht hinweg (23 Stunden nach dem Legen)", () => {
    // Abends gelegt, am nächsten Tag gesucht: Der Kalendertag wechselt, die Frist nicht.
    expect(kannNochAblaufen(gelegt, ENDE + stunden(23))).toBe(true);
  });

  it("sperrt den Ablauf nach 25 Stunden", () => {
    expect(kannNochAblaufen(gelegt, ENDE + stunden(25))).toBe(false);
  });

  it("zählt ab dem Ende des Legens, nicht ab dem Beginn", () => {
    const lang = track("b", [punkt("2026-09-10T08:00:00Z"), punkt("2026-09-10T09:00:00Z")]);
    // 24 h nach dem Beginn, aber nur 23 h nach dem Ende
    expect(kannNochAblaufen(lang, Date.parse("2026-09-11T08:00:00Z"))).toBe(true);
    expect(kannNochAblaufen(lang, Date.parse("2026-09-11T09:00:01Z"))).toBe(false);
  });

  it("übergeht spätere Marker", () => {
    const mitMarker = track("c", [punkt("2026-09-10T09:00:00Z"), punkt("2026-09-11T08:00:00Z", 1)]);
    expect(kannNochAblaufen(mitMarker, Date.parse("2026-09-11T10:00:00Z"))).toBe(false);
  });

  it("lehnt eine Fährte ohne automatische Punkte ab", () => {
    expect(kannNochAblaufen(track("d", []), ENDE)).toBe(false);
    expect(kannNochAblaufen(track("e", [punkt("2026-09-10T09:00:00Z", 1)]), ENDE)).toBe(false);
  });
});

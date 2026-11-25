import { describe, expect, it } from "vitest";
import { nichtAbgelaufeneFaehrten, tageSeitLegen } from "./faehrte";
import { ART_MANTRAILING } from "./mantrailing";
import type { GpsPoint, GpsTrack } from "./types";

// Der Kalendertag hängt an der Zeitzone des Geräts - der Test legt sie selbst fest.
process.env.TZ = "Europe/Berlin";

const stunden = (n: number) => n * 60 * 60 * 1000;

function punkt(timestamp: string): GpsPoint {
  return { latitude: 48.9, longitude: 8.5, timestamp, accuracy: 5, pointType: 0, label: null, markerType: 0 } as GpsPoint;
}

function spur(id: string, ende: string, kind = 0): GpsTrack {
  return { id, kind, points: [punkt(ende)], walkRuns: [] } as unknown as GpsTrack;
}

describe("nichtAbgelaufeneFaehrten: Frist", () => {
  const ENDE = "2026-10-30T21:00:00Z";
  const ende = Date.parse(ENDE);

  it("zeigt die Fährte über Mitternacht hinweg, bis 24 Stunden nach dem Legen", () => {
    const f = nichtAbgelaufeneFaehrten([spur("a", ENDE)], ende + stunden(23));
    expect(f.map((x) => x.track.id)).toEqual(["a"]);
    expect(nichtAbgelaufeneFaehrten([spur("a", ENDE)], ende + stunden(25))).toEqual([]);
  });

  it("zeigt den Trail bis 48 Stunden nach dem Legen", () => {
    const trail = spur("t", ENDE, ART_MANTRAILING);
    expect(nichtAbgelaufeneFaehrten([trail], ende + stunden(47)).map((x) => x.track.id)).toEqual(["t"]);
    expect(nichtAbgelaufeneFaehrten([trail], ende + stunden(49))).toEqual([]);
  });

  it("blendet auch aus, was ein Zwischenspeicher noch hält, die Frist aber überschritten hat", () => {
    expect(nichtAbgelaufeneFaehrten([spur("a", ENDE), spur("neu", "2026-11-01T20:00:00Z")], Date.parse("2026-11-01T21:00:00Z")).map((x) => x.track.id)).toEqual(["neu"]);
  });
});

describe("tageSeitLegen", () => {
  it("zählt Kalendertage auf dem Gerät: 23:30 Uhr gelegt, 07:00 Uhr am Morgen danach = gestern", () => {
    const gelegt = new Date("2026-10-30T22:30:00Z"); // 23:30 MEZ
    expect(tageSeitLegen(gelegt, Date.parse("2026-10-30T22:45:00Z"))).toBe(0);
    expect(tageSeitLegen(gelegt, Date.parse("2026-10-31T06:00:00Z"))).toBe(1);
    expect(tageSeitLegen(gelegt, Date.parse("2026-11-01T06:00:00Z"))).toBe(2);
  });
});

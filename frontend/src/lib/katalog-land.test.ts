import { describe, expect, it } from "vitest";
import type { Regulation } from "@/lib/types";
import { ordnungenImLand, ordnungenPfad, sportartenPfad } from "./katalog-land";

function ordnung(id: string, countryCode?: string | null): Regulation {
  return { id, name: id, countryCode, sourceUrl: null, lastSyncedAt: null, latestKnownVersionLabel: null, description: null };
}

describe("ordnungenImLand", () => {
  const liste = [ordnung("fci", null), ordnung("de", "DE"), ordnung("at", "AT"), ordnung("ohne")];

  it("behält internationale und die des Landes", () => {
    expect(ordnungenImLand(liste, "AT").map((o) => o.id)).toEqual(["fci", "at", "ohne"]);
    expect(ordnungenImLand(liste, "DE").map((o) => o.id)).toEqual(["fci", "de", "ohne"]);
  });

  it("zeigt in einem Land ohne eigene Ordnungen nur die internationalen", () => {
    expect(ordnungenImLand(liste, "CH").map((o) => o.id)).toEqual(["fci", "ohne"]);
  });

  it("ignoriert die Schreibweise des Kürzels", () => {
    expect(ordnungenImLand(liste, "at").map((o) => o.id)).toContain("at");
  });

  it("lässt die ausdrücklich angesteuerte Ordnung stehen", () => {
    expect(ordnungenImLand(liste, "DE", "at").map((o) => o.id)).toEqual(["fci", "de", "at", "ohne"]);
  });
});

describe("Adressen", () => {
  it("hebt die Eingrenzung der Sportarten mit alle auf", () => {
    expect(sportartenPfad(false)).toBe("/api/sports");
    expect(sportartenPfad(true)).toBe("/api/sports?alle=true");
  });

  it("hängt das Land nur an, wenn es eines gibt", () => {
    expect(ordnungenPfad("s1", "AT")).toBe("/api/sports/s1/regulations?country=AT");
    expect(ordnungenPfad("s1", null)).toBe("/api/sports/s1/regulations");
  });
});

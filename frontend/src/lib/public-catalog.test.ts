import { describe, expect, it } from "vitest";
import { groupIntoFamilies, type CatalogEntry } from "./public-catalog";

function entry(name: string): CatalogEntry {
  return {
    slug: name.toLowerCase(),
    sport: { id: "s", name: "Sport", description: null },
    regulation: { id: name, name, description: null, latestKnownVersionLabel: null, sourceUrl: null },
  };
}

const titles = (names: string[]) => groupIntoFamilies(names.map(entry)).map((f) => f.title);

describe("groupIntoFamilies", () => {
  it("bündelt die Stufen einer Prüfungsfamilie unter einer Überschrift", () => {
    const families = groupIntoFamilies(["IBGH1", "IBGH3", "IBGH2"].map(entry));

    expect(families).toHaveLength(1);
    expect(families[0].title).toBe("IBGH – Internationale Begleithundeprüfung");
    // Innerhalb der Familie aufsteigend, damit Stufe 1 vor 2 vor 3 steht.
    expect(families[0].entries.map((e) => e.regulation.name)).toEqual(["IBGH1", "IBGH2", "IBGH3"]);
  });

  it("ordnet Turnierhundsport und Agility ihren eigenen Familien zu", () => {
    expect(titles(["VDH-VK1", "VDH-CSC"])).toEqual(["Turnierhundsport"]);
    expect(titles(["Agility 1 (A1)", "Jumping (JP0-JP3)"])).toEqual(["Agility"]);
  });

  it("trennt die Fährten von den Einzelprüfungen", () => {
    // "FCI-FPr 1" enthält kein "Fährte" im Namen und gehört zu den
    // Einzelprüfungen - sonst stünde dieselbe Prüfung in zwei Familien.
    expect(titles(["IGP 1 - Fährte", "FCI-IFH 1"])).toEqual(["Fährtenarbeit"]);
    expect(titles(["FCI-FPr 1", "FCI-UPr 1"])).toEqual(["Einzelprüfungen"]);
  });

  it("sammelt Unbekanntes am Ende unter „Weitere“ statt es zu verlieren", () => {
    const families = groupIntoFamilies([entry("BH"), entry("Irgendwas Neues")]);

    expect(families.map((f) => f.title)).toEqual(["BH – Begleithundeprüfung", "Weitere"]);
    expect(families[1].entries).toHaveLength(1);
  });

  it("liefert nur Familien, zu denen es auch Einträge gibt", () => {
    expect(titles(["BH"])).toEqual(["BH – Begleithundeprüfung"]);
  });
});

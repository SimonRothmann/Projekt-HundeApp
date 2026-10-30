import { describe, expect, it } from "vitest";
import type { ClubOverviewMember } from "@/lib/types";
import { filtereMitglieder, mitgliedName, monatsLabel } from "@/lib/vereinsuebersicht";

const mitglied = (firstName: string, lastName: string, groups: string[] = []): ClubOverviewMember => ({
  membershipId: `${firstName}-${lastName}`,
  firstName,
  lastName,
  memberSince: "2026-01-01T00:00:00Z",
  groups,
  roles: 0,
});

const liste = [
  mitglied("Anna", "Müller", ["Welpen"]),
  mitglied("Ben", "Birke", ["Alpha", "Welpen"]),
  mitglied("", ""),
];

describe("filtereMitglieder", () => {
  it("zeigt bei leerer Suche alle, in der Reihenfolge der Liste", () => {
    expect(filtereMitglieder(liste, "")).toEqual(liste);
    expect(filtereMitglieder(liste, "   ")).toEqual(liste);
  });

  it("findet nach Vor- oder Nachname, ohne auf Groß-/Kleinschreibung und Akzente zu achten", () => {
    expect(filtereMitglieder(liste, "anna").map((m) => m.firstName)).toEqual(["Anna"]);
    expect(filtereMitglieder(liste, "MULLER").map((m) => m.lastName)).toEqual(["Müller"]);
    expect(filtereMitglieder(liste, "birk").map((m) => m.firstName)).toEqual(["Ben"]);
  });

  it("verlangt, dass jedes Wort passt", () => {
    expect(filtereMitglieder(liste, "ben birke")).toHaveLength(1);
    expect(filtereMitglieder(liste, "anna birke")).toHaveLength(0);
  });

  it("findet auch über den Gruppennamen", () => {
    expect(filtereMitglieder(liste, "alpha").map((m) => m.firstName)).toEqual(["Ben"]);
    expect(filtereMitglieder(liste, "welpen")).toHaveLength(2);
  });

  it("verändert die Liste nicht und liefert bei keinem Treffer eine leere", () => {
    const kopie = [...liste];
    expect(filtereMitglieder(liste, "xyz")).toEqual([]);
    expect(liste).toEqual(kopie);
  });
});

describe("mitgliedName", () => {
  it("setzt Vor- und Nachname zusammen und bleibt leer, wenn das Konto fehlt", () => {
    expect(mitgliedName({ firstName: "Anna", lastName: "Müller" })).toBe("Anna Müller");
    expect(mitgliedName({ firstName: "", lastName: "" })).toBe("");
  });
});

describe("monatsLabel", () => {
  it("nennt Monat und Jahr in der Sprache", () => {
    expect(monatsLabel(2026, 10, "de-DE")).toMatch(/Okt\.? 2026/);
    expect(monatsLabel(2026, 10, "en-GB")).toMatch(/Oct 2026/);
  });
});

import { describe, expect, it } from "vitest";
import { einladbareGruppen, gewaehlteGruppe, vorauswahlGruppe } from "./beitrittsfreigabe";
import type { Group, GroupRelation } from "./types";

function gruppe(id: string, myRelation: GroupRelation): Group {
  return { id, name: id, description: null, trainerId: "t", clubId: "c", memberCount: 0, trainerName: null, myRelation };
}

describe("einladbareGruppen", () => {
  it("behält nur Gruppen, die die Person als Trainer:in verwaltet", () => {
    const liste = [gruppe("a", 3), gruppe("b", 0), gruppe("c", 2), gruppe("d", 3)];
    expect(einladbareGruppen(liste).map((g) => g.id)).toEqual(["a", "d"]);
  });
});

describe("vorauswahlGruppe", () => {
  it("wählt die einzige Gruppe vor", () => {
    expect(vorauswahlGruppe([{ id: "a" }])).toBe("a");
  });

  it("wählt bei mehreren Gruppen keine vor", () => {
    expect(vorauswahlGruppe([{ id: "a" }, { id: "b" }])).toBeNull();
  });

  it("wählt ohne Gruppen keine vor", () => {
    expect(vorauswahlGruppe([])).toBeNull();
  });
});

describe("gewaehlteGruppe", () => {
  const eine = [{ id: "a" }];
  const zwei = [{ id: "a" }, { id: "b" }];

  it("nimmt ohne Wahl die Vorauswahl", () => {
    expect(gewaehlteGruppe(undefined, eine)).toBe("a");
    expect(gewaehlteGruppe(undefined, zwei)).toBeNull();
  });

  it("lässt ein bewusstes Keine auch bei nur einer Gruppe stehen", () => {
    expect(gewaehlteGruppe(null, eine)).toBeNull();
  });

  it("nimmt die getroffene Wahl", () => {
    expect(gewaehlteGruppe("b", zwei)).toBe("b");
  });

  it("fällt auf Keine zurück, wenn die gewählte Gruppe nicht mehr da ist", () => {
    expect(gewaehlteGruppe("weg", zwei)).toBeNull();
  });
});

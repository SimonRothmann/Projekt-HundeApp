import { describe, expect, it } from "vitest";
import type { TrainerOpenCounts } from "@/lib/types";
import { anfragenJeGruppe, offeneAnfragen, zuErledigen } from "./trainer-offen";

const zahlen = (teil: Partial<TrainerOpenCounts> = {}): TrainerOpenCounts => ({
  groupJoinRequests: 0,
  clubJoinRequests: 0,
  sessionsToRate: 0,
  groups: [],
  ...teil,
});

describe("zuErledigen", () => {
  it("ist leer, solange nichts geladen ist", () => {
    expect(zuErledigen(null)).toStrictEqual([]);
  });

  it("ist leer, wenn alles 0 ist - der Block entfällt", () => {
    expect(zuErledigen(zahlen())).toStrictEqual([]);
  });

  it("fasst Gruppen- und Vereinsanfragen zu einem Zähler zusammen", () => {
    const liste = zuErledigen(zahlen({ groupJoinRequests: 1, clubJoinRequests: 3 }));
    expect(liste).toStrictEqual([{ art: "anfragen", href: "/trainer/anfragen", anzahl: 4 }]);
  });

  it("zeigt nur, was offen ist", () => {
    expect(zuErledigen(zahlen({ sessionsToRate: 10 }))).toStrictEqual([
      { art: "bewerten", href: "/trainer/bewerten", anzahl: 10 },
    ]);
  });

  it("nennt Anfragen vor dem Bewerten", () => {
    const liste = zuErledigen(zahlen({ clubJoinRequests: 2, sessionsToRate: 5 }));
    expect(liste.map((z) => z.art)).toStrictEqual(["anfragen", "bewerten"]);
  });

  it("behandelt unsinnige Zahlen als 0", () => {
    expect(zuErledigen(zahlen({ groupJoinRequests: -2, sessionsToRate: Number.NaN }))).toStrictEqual([]);
  });
});

describe("offeneAnfragen", () => {
  it("addiert Gruppen und Vereine", () => {
    expect(offeneAnfragen(zahlen({ groupJoinRequests: 2, clubJoinRequests: 5 }))).toBe(7);
  });
});

describe("anfragenJeGruppe", () => {
  it("schlägt Anfragen je Gruppe nach und lässt leere weg", () => {
    const nachGruppe = anfragenJeGruppe(
      zahlen({
        groups: [
          { groupId: "a", joinRequests: 2 },
          { groupId: "b", joinRequests: 0 },
        ],
      }),
    );
    expect(nachGruppe).toStrictEqual({ a: 2 });
  });

  it("liefert ohne Zahlen nichts", () => {
    expect(anfragenJeGruppe(null)).toStrictEqual({});
  });
});

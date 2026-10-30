import { describe, expect, it } from "vitest";
import { aenderbareRollen, darfAbberufen, hatRolle, mitRolle, ROLLE, ROLLEN, rollenVon } from "./vereinsrollen";

describe("Rollenwerte", () => {
  it("stimmen mit dem Server überein (ClubRole) - sie sind gespeicherte Daten", () => {
    expect(ROLLE).toEqual({ training: 1, verwaltung: 2, ausbildungswart: 4, mitgliederverwaltung: 8, vorstand: 16 });
  });

  it("jede Rolle hat Name und Erklärung, und kein Wert kommt doppelt vor", () => {
    expect(new Set(ROLLEN.map((r) => r.bit)).size).toBe(5);
    for (const r of ROLLEN) {
      expect(r.name.length).toBeGreaterThan(0);
      expect(r.erklaerung.endsWith(".")).toBe(true);
    }
  });
});

describe("rollenVon / hatRolle / mitRolle", () => {
  it("liest eine Kombination", () => {
    const menge = ROLLE.training | ROLLE.vorstand;
    expect(rollenVon(menge).map((r) => r.bit)).toEqual([ROLLE.training, ROLLE.vorstand]);
    expect(hatRolle(menge, ROLLE.vorstand)).toBe(true);
    expect(hatRolle(menge, ROLLE.verwaltung)).toBe(false);
  });

  it("keine Rolle: leere Liste", () => {
    expect(rollenVon(0)).toEqual([]);
  });

  it("setzt und entfernt ein Bit, ohne die anderen zu berühren", () => {
    expect(mitRolle(ROLLE.vorstand, ROLLE.training, true)).toBe(ROLLE.vorstand | ROLLE.training);
    expect(mitRolle(ROLLE.vorstand | ROLLE.training, ROLLE.training, false)).toBe(ROLLE.vorstand);
    expect(mitRolle(ROLLE.vorstand, ROLLE.training, false)).toBe(ROLLE.vorstand);
    expect(mitRolle(ROLLE.training, ROLLE.training, true)).toBe(ROLLE.training);
  });

  it("zeigt die Verwaltung zuletzt (die Leitung steht am Ende der Reihe)", () => {
    expect(rollenVon(ROLLE.verwaltung | ROLLE.training).map((r) => r.bit)).toEqual([ROLLE.training, ROLLE.verwaltung]);
  });
});

describe("aenderbareRollen", () => {
  it("Verwaltung und Admin: alle Rollen", () => {
    expect(aenderbareRollen({ canManage: true })).toHaveLength(5);
    expect(aenderbareRollen({}, true)).toHaveLength(5);
  });

  it("Ausbildungswart:in: nur Training", () => {
    expect(aenderbareRollen({ canAppointTrainers: true })).toEqual([ROLLE.training]);
  });

  it("alle anderen: nichts", () => {
    expect(aenderbareRollen({})).toEqual([]);
    expect(aenderbareRollen({ canManage: false, canAppointTrainers: false })).toEqual([]);
  });
});

describe("darfAbberufen", () => {
  const nurTraining = [ROLLE.training];

  it("Ausbildungswart:in nur, wenn die Person nichts außer Training hat", () => {
    expect(darfAbberufen(ROLLE.training, nurTraining)).toBe(true);
    expect(darfAbberufen(ROLLE.training | ROLLE.vorstand, nurTraining)).toBe(false);
    expect(darfAbberufen(ROLLE.verwaltung, nurTraining)).toBe(false);
  });

  it("wer alles ändern darf, darf jede Person abberufen", () => {
    expect(darfAbberufen(ROLLE.verwaltung | ROLLE.vorstand, ROLLEN.map((r) => r.bit))).toBe(true);
  });

  it("wer nichts ändern darf, darf niemanden abberufen; ohne Rolle gibt es nichts abzuberufen", () => {
    expect(darfAbberufen(ROLLE.training, [])).toBe(false);
    expect(darfAbberufen(0, ROLLEN.map((r) => r.bit))).toBe(false);
  });
});

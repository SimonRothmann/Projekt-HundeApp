import { describe, expect, it } from "vitest";

import { istHundeStartseite, zurueckZiel } from "./zurueck";

describe("zurueckZiel", () => {
  it("führt zur übergeordneten Route", () => {
    expect(zurueckZiel("/dogs/123/print", null, null)).toBe("/dogs/123");
    expect(zurueckZiel("/trainer/group-training", null, true)).toBe("/trainer");
    expect(zurueckZiel("/neu", null, null)).toBe("/dashboard");
  });

  it("lässt ein seiteninternes ?from= gewinnen - auch für Nicht-Trainer:innen", () => {
    expect(zurueckZiel("/trainer/anfragen", "/trainer/verein?verein=abc", false)).toBe("/trainer/verein?verein=abc");
    expect(zurueckZiel("/dogs/123", "/trainer/g1", true)).toBe("/trainer/g1");
  });

  it("ignoriert ein fremdes ?from= und nimmt die Route", () => {
    expect(zurueckZiel("/dogs/123", "https://fremd.example/", true)).toBe("/dogs?liste=1");
  });

  it("schickt Nicht-Trainer:innen von /trainer-Seiten zu den Vereinen statt auf die leere Trainer-Übersicht", () => {
    expect(zurueckZiel("/trainer/anfragen", null, false)).toBe("/clubs");
    expect(zurueckZiel("/trainer/verein", null, false)).toBe("/clubs");
  });

  it("lässt Trainer:innen und noch Unbekannte bei /trainer", () => {
    expect(zurueckZiel("/trainer/anfragen", null, true)).toBe("/trainer");
    expect(zurueckZiel("/trainer/anfragen", null, null)).toBe("/trainer");
  });

  it("ändert nur Seiten direkt unter /trainer - der Aushang geht über seine Weiterleitung zum Verein", () => {
    expect(zurueckZiel("/trainer/aushang/abc", null, false)).toBe("/trainer/aushang");
  });
});

describe("Hundeseite und Reiter Hunde", () => {
  it("führt eine Hundeseite ohne ?from= auf die Liste (nicht auf /dogs, das gleich wieder weiterleitet)", () => {
    expect(zurueckZiel("/dogs/123", null, null)).toBe("/dogs?liste=1");
  });

  it("führt die Liste zurück auf /dogs", () => {
    expect(zurueckZiel("/dogs", null, null)).toBe("/dogs");
  });

  it("erkennt die Hundeseite als Startpunkt des Reiters nur mit ?from=/dogs", () => {
    expect(istHundeStartseite("/dogs/123", "/dogs")).toBe(true);
    expect(istHundeStartseite("/dogs/123", null)).toBe(false);
    expect(istHundeStartseite("/dogs/123", "/stats")).toBe(false);
    expect(istHundeStartseite("/dogs/123", "/trainer/g1")).toBe(false);
    expect(istHundeStartseite("/dogs/123/print", "/dogs")).toBe(false);
    expect(istHundeStartseite("/dashboard", "/dogs")).toBe(false);
  });
});

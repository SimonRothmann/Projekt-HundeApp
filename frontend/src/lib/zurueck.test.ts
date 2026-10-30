import { describe, expect, it } from "vitest";

import { zurueckZiel } from "./zurueck";

describe("zurueckZiel", () => {
  it("führt zur übergeordneten Route", () => {
    expect(zurueckZiel("/dogs/123", null, null)).toBe("/dogs");
    expect(zurueckZiel("/trainer/group-training", null, true)).toBe("/trainer");
    expect(zurueckZiel("/neu", null, null)).toBe("/dashboard");
  });

  it("lässt ein seiteninternes ?from= gewinnen - auch für Nicht-Trainer:innen", () => {
    expect(zurueckZiel("/trainer/anfragen", "/trainer/verein?verein=abc", false)).toBe("/trainer/verein?verein=abc");
    expect(zurueckZiel("/dogs/123", "/trainer/g1", true)).toBe("/trainer/g1");
  });

  it("ignoriert ein fremdes ?from= und nimmt die Route", () => {
    expect(zurueckZiel("/dogs/123", "https://fremd.example/", true)).toBe("/dogs");
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

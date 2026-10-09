import { describe, expect, it } from "vitest";
import { direktZumHund } from "./hundeliste";
import type { Dog } from "./types";

const hund = (id: string, archiviert = false) => ({ id, name: id, archivedAt: archiviert ? "2026-01-01T00:00:00Z" : null }) as unknown as Dog;
const ohne = { einladungen: 0, listeGewuenscht: false };

describe("direktZumHund", () => {
  it("springt bei genau einem aktiven Hund auf dessen Seite", () => {
    expect(direktZumHund([hund("a")], ohne)).toBe("a");
  });

  it("zählt archivierte Hunde nicht mit", () => {
    expect(direktZumHund([hund("a"), hund("b", true)], ohne)).toBe("a");
  });

  it("bleibt bei keinem oder mehreren aktiven Hunden in der Liste", () => {
    expect(direktZumHund([], ohne)).toBeNull();
    expect(direktZumHund([hund("a", true)], ohne)).toBeNull();
    expect(direktZumHund([hund("a"), hund("b")], ohne)).toBeNull();
  });

  it("bleibt in der Liste, wenn eine Einladung wartet oder die Liste verlangt ist", () => {
    expect(direktZumHund([hund("a")], { einladungen: 1, listeGewuenscht: false })).toBeNull();
    expect(direktZumHund([hund("a")], { einladungen: 0, listeGewuenscht: true })).toBeNull();
  });
});

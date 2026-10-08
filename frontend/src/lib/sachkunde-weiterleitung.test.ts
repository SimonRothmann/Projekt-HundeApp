import { describe, expect, it } from "vitest";
import { appPfadFuerSachkunde, sachkundeWeiterleitung } from "./sachkunde-weiterleitung";

describe("appPfadFuerSachkunde", () => {
  it("bildet Übersicht und Katalog auf die App-Route ab", () => {
    expect(appPfadFuerSachkunde("/sachkunde")).toBe("/lernen");
    expect(appPfadFuerSachkunde("/sachkunde/swhv-bhvt-erw")).toBe("/lernen/swhv-bhvt-erw");
  });

  it("verträgt einen abschließenden Schrägstrich", () => {
    expect(appPfadFuerSachkunde("/sachkunde/")).toBe("/lernen");
    expect(appPfadFuerSachkunde("/sachkunde/bh/")).toBe("/lernen/bh");
  });

  it("lässt alles andere unberührt", () => {
    expect(appPfadFuerSachkunde("/")).toBeNull();
    expect(appPfadFuerSachkunde("/lernen")).toBeNull();
    expect(appPfadFuerSachkunde("/sachkundeX")).toBeNull();
    expect(appPfadFuerSachkunde("/sachkunde/a/b")).toBeNull();
    expect(appPfadFuerSachkunde("/pruefungsordnungen")).toBeNull();
  });
});

describe("sachkundeWeiterleitung", () => {
  it("leitet Angemeldete um", () => {
    expect(sachkundeWeiterleitung("/sachkunde", true, false)).toBe("/lernen");
    expect(sachkundeWeiterleitung("/sachkunde/bh", true, false)).toBe("/lernen/bh");
  });

  it("lässt Gäste auf der öffentlichen Seite", () => {
    expect(sachkundeWeiterleitung("/sachkunde", false, false)).toBeNull();
    expect(sachkundeWeiterleitung("/sachkunde/bh", false, false)).toBeNull();
  });

  it("wartet, bis die Anmeldung geklärt ist", () => {
    expect(sachkundeWeiterleitung("/sachkunde", true, true)).toBeNull();
  });
});

import { describe, expect, it } from "vitest";
import { UNTERGRUENDE, untergrundAlsText, untergrundUmschalten } from "./untergrund";

describe("untergrundUmschalten", () => {
  it("wählt beim ersten Antippen an und beim zweiten wieder ab", () => {
    const einmal = untergrundUmschalten([], "Wiese");

    expect(einmal).toEqual(["Wiese"]);
    expect(untergrundUmschalten(einmal, "Wiese")).toEqual([]);
  });

  it("hält die Reihenfolge der Liste, egal in welcher Reihenfolge angetippt wird", () => {
    // Derselbe Untergrundwechsel soll in den Daten immer gleich lauten.
    const auswahl = untergrundUmschalten(untergrundUmschalten([], "Acker"), "Wiese");

    expect(auswahl).toEqual(["Wiese", "Acker"]);
  });
});

describe("untergrundAlsText", () => {
  it("liefert null ohne Auswahl, damit kein leerer Untergrund gespeichert wird", () => {
    expect(untergrundAlsText([])).toBeNull();
  });

  it("verbindet mehrere Untergründe mit Komma", () => {
    expect(untergrundAlsText(["Wiese", "Acker"])).toBe("Wiese, Acker");
  });

  it("passt auch mit allen Untergründen in die Datenbankspalte (100 Zeichen)", () => {
    expect(untergrundAlsText(UNTERGRUENDE)!.length).toBeLessThanOrEqual(100);
  });
});

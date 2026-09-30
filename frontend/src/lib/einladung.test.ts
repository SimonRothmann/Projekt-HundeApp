import { describe, expect, it } from "vitest";
import { einladungscodeAus, einladungsUrl, istGueltigerEinladungscode } from "./einladung";

describe("Einladungscode", () => {
  it("nimmt einen erzeugten Code an (URL-sicher, 22 Zeichen)", () => {
    expect(istGueltigerEinladungscode("Ab3_-Zy9Ab3_-Zy9Ab3_-Z")).toBe(true);
  });

  it.each([
    ["nichts", null],
    ["leer", ""],
    ["undefined", undefined],
    ["Schrägstrich (Pfad)", "abc/../admin"],
    ["Doppelpunkt (Adresse)", "https://boese.example"],
    ["Leerzeichen", "abc def"],
    ["Umlaut", "abcäöü"],
    ["Fragezeichen", "abc?x=1"],
    ["zu lang", "a".repeat(33)],
  ])("weist %s ab", (_name, wert) => {
    expect(istGueltigerEinladungscode(wert)).toBe(false);
    expect(einladungscodeAus(wert)).toBeNull();
  });

  it("reicht einen gültigen Code unverändert durch", () => {
    expect(einladungscodeAus("Abc123_-")).toBe("Abc123_-");
  });

  it("setzt die Adresse für den QR-Code aus Herkunft und Code zusammen", () => {
    expect(einladungsUrl("https://dogity.net", "Abc123")).toBe("https://dogity.net/v/Abc123");
  });
});

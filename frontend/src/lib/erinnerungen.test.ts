import { describe, expect, it } from "vitest";
import {
  ERSTE_STUNDE,
  LETZTE_STUNDE,
  STUNDEN,
  VORGABE_STUNDE,
  VORGABE_TAGE,
  WOCHENTAGE,
  anzahlTage,
  gueltigeStunde,
  stundeText,
  tagGewaehlt,
  tagUmschalten,
} from "./erinnerungen";

describe("Wochentage", () => {
  it("sind sieben Bits von Montag (1) bis Sonntag (64)", () => {
    expect(WOCHENTAGE.map((t) => t.bit)).toStrictEqual([1, 2, 4, 8, 16, 32, 64]);
    expect(WOCHENTAGE[0].kurz).toBe("Mo");
    expect(WOCHENTAGE[6].lang).toBe("Sonntag");
  });

  it("Vorgabe ist nur der Sonntag um 18 Uhr", () => {
    expect(VORGABE_TAGE).toBe(64);
    expect(tagGewaehlt(VORGABE_TAGE, 64)).toBe(true);
    expect(anzahlTage(VORGABE_TAGE)).toBe(1);
    expect(VORGABE_STUNDE).toBe(18);
  });
});

describe("tagUmschalten", () => {
  it("wählt einen weiteren Tag dazu", () => {
    expect(tagUmschalten(64, 1)).toBe(65);
  });

  it("wählt einen Tag ab, solange ein anderer bleibt", () => {
    expect(tagUmschalten(65, 64)).toBe(1);
  });

  it("lässt den letzten Tag stehen", () => {
    expect(tagUmschalten(64, 64)).toBe(64);
    expect(tagUmschalten(1, 1)).toBe(1);
  });

  it("kommt mit allen sieben Tagen zurecht", () => {
    expect(tagUmschalten(127, 8)).toBe(119);
    expect(tagUmschalten(119, 8)).toBe(127);
  });
});

describe("Uhrzeit", () => {
  it("bietet die vollen Stunden von 8 bis 21 Uhr an", () => {
    expect(ERSTE_STUNDE).toBe(8);
    expect(LETZTE_STUNDE).toBe(21);
    expect(STUNDEN).toHaveLength(14);
    expect(STUNDEN[0]).toBe(8);
    expect(STUNDEN[13]).toBe(21);
  });

  it("schreibt die Stunde zweistellig", () => {
    expect(stundeText(8)).toBe("08:00");
    expect(stundeText(18)).toBe("18:00");
  });

  it("ersetzt eine unmögliche Stunde durch die Vorgabe", () => {
    expect(gueltigeStunde(7)).toBe(18);
    expect(gueltigeStunde(22)).toBe(18);
    expect(gueltigeStunde(8.5)).toBe(18);
    expect(gueltigeStunde(21)).toBe(21);
  });
});

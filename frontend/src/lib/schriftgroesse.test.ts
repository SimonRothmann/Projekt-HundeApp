import { describe, expect, it } from "vitest";
import {
  bestimmeSchriftgroesse,
  istSchriftgroesse,
  SCHRIFTGROESSEN,
  VORGABE_SCHRIFT,
} from "./schriftgroesse";

describe("istSchriftgroesse", () => {
  it("erkennt jede angebotene Stufe", () => {
    for (const stufe of SCHRIFTGROESSEN) expect(istSchriftgroesse(stufe)).toBe(true);
  });

  it("weist alles andere ab", () => {
    expect(istSchriftgroesse("riesig")).toBe(false);
    expect(istSchriftgroesse("")).toBe(false);
    expect(istSchriftgroesse(null)).toBe(false);
    expect(istSchriftgroesse(125)).toBe(false);
  });
});

describe("bestimmeSchriftgroesse", () => {
  it("übernimmt eine gültige Stufe", () => {
    expect(bestimmeSchriftgroesse("sehr-gross")).toBe("sehr-gross");
  });

  it("fällt bei Unbekanntem auf die Vorgabe zurück statt auf nichts", () => {
    // Eine Stufe aus einer neueren Fassung, ein von Hand verbogener Eintrag,
    // ein leerer Speicher: Die Oberfläche steht dann normal da.
    expect(bestimmeSchriftgroesse("riesig")).toBe(VORGABE_SCHRIFT);
    expect(bestimmeSchriftgroesse(null)).toBe(VORGABE_SCHRIFT);
    expect(bestimmeSchriftgroesse(undefined)).toBe(VORGABE_SCHRIFT);
    expect(bestimmeSchriftgroesse("")).toBe(VORGABE_SCHRIFT);
  });

  it("führt die Vorgabe selbst als gültige Stufe", () => {
    expect(SCHRIFTGROESSEN).toContain(VORGABE_SCHRIFT);
  });
});

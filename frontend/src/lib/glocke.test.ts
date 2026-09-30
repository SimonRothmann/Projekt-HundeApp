import { describe, expect, it } from "vitest";
import { abrufFaellig, RUECKKEHR_MINDESTABSTAND_MS } from "./glocke";

describe("abrufFaellig", () => {
  it("ruft nicht ab, solange der letzte Abruf keine 10 Sekunden her ist", () => {
    expect(abrufFaellig(1_000, 1_000 + RUECKKEHR_MINDESTABSTAND_MS - 1)).toBe(false);
  });

  it("ruft ab, sobald die 10 Sekunden um sind", () => {
    expect(abrufFaellig(1_000, 1_000 + RUECKKEHR_MINDESTABSTAND_MS)).toBe(true);
  });

  it("ruft beim allerersten Mal ab", () => {
    expect(abrufFaellig(0, 1_700_000_000_000)).toBe(true);
  });

  it("kennt einen eigenen Mindestabstand", () => {
    expect(abrufFaellig(0, 500, 1_000)).toBe(false);
    expect(abrufFaellig(0, 1_000, 1_000)).toBe(true);
  });
});

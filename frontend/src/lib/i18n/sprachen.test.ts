import { describe, expect, it } from "vitest";
import { bestimmeSprache, istSprache } from "./sprachen";

describe("bestimmeSprache", () => {
  it("nimmt die ausdrückliche Wahl", () => {
    expect(bestimmeSprache("en", ["de-DE"])).toBe("en");
    expect(bestimmeSprache("de", ["en-US"])).toBe("de");
  });

  it("fällt ohne Wahl auf das Gerät zurück", () => {
    expect(bestimmeSprache(null, ["en-US", "de"])).toBe("en");
  });

  it("kürzt die Region weg - die App unterscheidet sie nicht", () => {
    expect(bestimmeSprache(null, ["en-GB"])).toBe("en");
    expect(bestimmeSprache(null, ["de-AT"])).toBe("de");
  });

  it("überspringt Sprachen, die es nicht gibt, statt aufzugeben", () => {
    // Ein französisches Gerät mit Englisch an zweiter Stelle soll Englisch
    // bekommen und nicht die Vorgabe.
    expect(bestimmeSprache(null, ["fr-FR", "en"])).toBe("en");
  });

  it("fällt am Ende auf Deutsch zurück", () => {
    expect(bestimmeSprache(null, [])).toBe("de");
    expect(bestimmeSprache("klingonisch", ["fr"])).toBe("de");
  });
});

describe("istSprache", () => {
  it("erkennt nur die gepflegten Sprachen", () => {
    expect(istSprache("de")).toBe(true);
    expect(istSprache("en")).toBe(true);
    expect(istSprache("fr")).toBe(false);
    expect(istSprache(null)).toBe(false);
  });
});

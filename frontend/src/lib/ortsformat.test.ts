import { describe, expect, it } from "vitest";
import { ortsformat, zahlText } from "./ortsformat";

describe("ortsformat", () => {
  it("wählt das Gebietsschema nach der Sprache", () => {
    expect(ortsformat("de")).toBe("de-DE");
    expect(ortsformat("en")).toBe("en-GB");
  });
});

describe("zahlText", () => {
  it("setzt auf Deutsch ein Komma, auf Englisch einen Punkt", () => {
    expect(zahlText(1.8, "de")).toBe("1,8");
    expect(zahlText(1.8, "en")).toBe("1.8");
  });

  it("zeigt die verlangten Nachkommastellen, auch bei ganzen Zahlen", () => {
    expect(zahlText(8, "de")).toBe("8,0");
    expect(zahlText(8.4, "de", 0)).toBe("8");
  });
});

import { describe, expect, it } from "vitest";
import { alterMinutenText } from "./alter-minuten";

describe("alterMinutenText", () => {
  it("bleibt unter einer Stunde bei Minuten", () => {
    expect(alterMinutenText(0)).toBe("0 Min.");
    expect(alterMinutenText(59)).toBe("59 Min.");
  });

  it("zeigt ab 60 Minuten Stunden, bei glatten Stunden ohne Minuten", () => {
    expect(alterMinutenText(60)).toBe("1 Std.");
    expect(alterMinutenText(61)).toBe("1 Std. 1 Min.");
    expect(alterMinutenText(180)).toBe("3 Std.");
  });

  it("rechnet lange Alter in Stunden und Minuten um", () => {
    expect(alterMinutenText(1307)).toBe("21 Std. 47 Min.");
  });
});

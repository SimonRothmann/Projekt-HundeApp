import { describe, expect, it } from "vitest";
import { gastUhrzeit } from "./gast";

describe("gastUhrzeit", () => {
  it("zeigt Stunde und Minute zweistellig, in beiden Sprachen", () => {
    const iso = new Date(2026, 10, 19, 9, 5).toISOString();
    expect(gastUhrzeit(iso, "de")).toBe("09:05");
    expect(gastUhrzeit(iso, "en")).toBe("09:05");
  });
});

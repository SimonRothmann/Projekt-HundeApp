import { describe, expect, it } from "vitest";
import { geraeteZeitzone, mussMelden } from "./zeitzone";

describe("geraeteZeitzone", () => {
  it("nennt die Zone des Geräts als IANA-Kennung", () => {
    const vorher = process.env.TZ;
    process.env.TZ = "America/New_York";
    try {
      expect(geraeteZeitzone()).toBe("America/New_York");
    } finally {
      process.env.TZ = vorher;
    }
  });
});

describe("mussMelden", () => {
  it("meldet, wenn nichts gespeichert ist oder die Zone abweicht", () => {
    expect(mussMelden(null, "Europe/London")).toBe(true);
    expect(mussMelden(undefined, "Europe/London")).toBe(true);
    expect(mussMelden("Europe/Berlin", "Europe/London")).toBe(true);
  });

  it("meldet nicht, wenn die Zone stimmt oder das Gerät keine nennt", () => {
    expect(mussMelden("Europe/London", "Europe/London")).toBe(false);
    expect(mussMelden(null, null)).toBe(false);
    expect(mussMelden("Europe/London", null)).toBe(false);
  });
});

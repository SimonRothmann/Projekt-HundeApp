import { describe, expect, it } from "vitest";
import { ApiError } from "./api";
import { ladefehlerArt } from "./use-group-detail";

describe("ladefehlerArt", () => {
  it("404 und 403: die Gruppe gibt es nicht oder gehört nicht zu dir", () => {
    expect(ladefehlerArt(new ApiError(404, ["Gruppe nicht gefunden."]))).toBe("nichtVerfuegbar");
    expect(ladefehlerArt(new ApiError(403, []))).toBe("nichtVerfuegbar");
  });

  it("Serverfehler und Drosselung sind Ladefehler, die sich wiederholen lassen", () => {
    expect(ladefehlerArt(new ApiError(500, []))).toBe("ladefehler");
    expect(ladefehlerArt(new ApiError(503, []))).toBe("ladefehler");
    expect(ladefehlerArt(new ApiError(429, []))).toBe("ladefehler");
  });

  it("ein Netzfehler (kein ApiError) ist ein Ladefehler", () => {
    expect(ladefehlerArt(new TypeError("Failed to fetch"))).toBe("ladefehler");
  });
});

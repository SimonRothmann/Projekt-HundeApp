import { afterEach, describe, expect, it, vi } from "vitest";
import { kurzeHaptik } from "@/lib/haptik";

describe("kurzeHaptik", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("vibriert kurz, wo der Browser es kennt", () => {
    const vibrate = vi.fn();
    vi.stubGlobal("navigator", { vibrate });
    vi.stubGlobal("window", { matchMedia: () => ({ matches: false }) });
    kurzeHaptik();
    expect(vibrate).toHaveBeenCalledWith(10);
  });

  it("tut ohne vibrate (iOS Safari) nichts", () => {
    vi.stubGlobal("navigator", {});
    expect(() => kurzeHaptik()).not.toThrow();
  });

  it("schweigt bei reduzierter Bewegung", () => {
    const vibrate = vi.fn();
    vi.stubGlobal("navigator", { vibrate });
    vi.stubGlobal("window", { matchMedia: () => ({ matches: true }) });
    kurzeHaptik();
    expect(vibrate).not.toHaveBeenCalled();
  });
});

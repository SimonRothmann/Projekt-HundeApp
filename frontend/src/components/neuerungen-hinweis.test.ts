import { describe, expect, it } from "vitest";
import { sollHinweisZeigen } from "./neuerungen-hinweis";

describe("sollHinweisZeigen", () => {
  it("zeigt nichts, wenn die laufende Fassung schon gesehen wurde", () => {
    expect(sollHinweisZeigen("0.9", "0.9", false)).toBe(false);
  });

  it("zeigt den Hinweis nach einer neuen Fassung wieder", () => {
    expect(sollHinweisZeigen("0.8", "0.9", false)).toBe(true);
  });

  it("zeigt ihn auch, wenn noch nie etwas vermerkt wurde", () => {
    expect(sollHinweisZeigen(null, "0.9", false)).toBe(true);
  });

  it("schweigt während des Erststarts - für Neulinge ist alles neu", () => {
    expect(sollHinweisZeigen(null, "0.9", true)).toBe(false);
    expect(sollHinweisZeigen("0.8", "0.9", true)).toBe(false);
  });
});

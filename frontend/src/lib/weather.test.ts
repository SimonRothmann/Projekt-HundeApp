import { describe, expect, it } from "vitest";
import { stehzeitWetterText } from "./weather";

// t() setzt nur die Werte ein; die Übersetzung selbst prüft i18n.test.ts.
const t = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (_, name) => String(werte?.[name]));

describe("stehzeitWetterText", () => {
  it("zeigt Regen und Temperaturspanne mit deutschem Dezimalkomma", () => {
    expect(stehzeitWetterText(t, "de", false, 4.2, 3.1, 8.6)).toBe("Während der Stehzeit: 4,2 mm Regen · 3 bis 9 °C");
  });

  it("nimmt die englische Zahlschreibweise und für den Trail die Liegezeit", () => {
    expect(stehzeitWetterText(t, "en", true, 4.2, 3, 9)).toBe("Während der Liegezeit: 4.2 mm Regen · 3 bis 9 °C");
  });

  it("sagt bei 0 mm: kein Regen", () => {
    expect(stehzeitWetterText(t, "de", false, 0, 3, 9)).toBe("Während der Stehzeit: kein Regen · 3 bis 9 °C");
  });

  it("fasst gleiche gerundete Temperaturen zusammen und zeigt kein -0", () => {
    expect(stehzeitWetterText(t, "de", false, 0, -0.3, 0.2)).toBe("Während der Stehzeit: kein Regen · 0 °C");
  });

  it("ist leer, solange ein Wert fehlt", () => {
    expect(stehzeitWetterText(t, "de", false, null, 3, 9)).toBeNull();
    expect(stehzeitWetterText(t, "de", false, 1, undefined, 9)).toBeNull();
  });
});

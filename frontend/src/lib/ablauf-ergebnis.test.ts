import { describe, expect, it } from "vitest";
import { istWischNachUnten, kommentarAenderung, WISCH_MINDESTWEG } from "./ablauf-ergebnis";

describe("kommentarAenderung", () => {
  it("meldet nichts, wenn die Eingabe dem gespeicherten Kommentar entspricht", () => {
    expect(kommentarAenderung("bei Regen", "bei Regen")).toBeNull();
    expect(kommentarAenderung(null, "")).toBeNull();
    expect(kommentarAenderung(undefined, "")).toBeNull();
  });

  it("ignoriert Leerraum an den Rändern", () => {
    expect(kommentarAenderung("bei Regen", "  bei Regen ")).toBeNull();
    expect(kommentarAenderung(null, "   ")).toBeNull();
  });

  it("liefert den neuen, getrimmten Kommentar", () => {
    expect(kommentarAenderung(null, " Hund hat bei Winkel 2 verloren ")).toEqual({ kommentar: "Hund hat bei Winkel 2 verloren" });
    expect(kommentarAenderung("alt", "neu")).toEqual({ kommentar: "neu" });
  });

  it("speichert einen geleerten Kommentar als null", () => {
    expect(kommentarAenderung("bei Regen", "")).toEqual({ kommentar: null });
  });
});

describe("istWischNachUnten", () => {
  it("erkennt eine deutliche Bewegung nach unten", () => {
    expect(istWischNachUnten(5, 120)).toBe(true);
    expect(istWischNachUnten(0, WISCH_MINDESTWEG)).toBe(true);
  });

  it("ignoriert kurze Bewegungen und Antippen", () => {
    expect(istWischNachUnten(0, WISCH_MINDESTWEG - 1)).toBe(false);
    expect(istWischNachUnten(0, 0)).toBe(false);
  });

  it("ignoriert Bewegungen nach oben und schräge Bewegungen", () => {
    expect(istWischNachUnten(0, -120)).toBe(false);
    expect(istWischNachUnten(100, 120)).toBe(false);
    expect(istWischNachUnten(-100, 120)).toBe(false);
  });
});

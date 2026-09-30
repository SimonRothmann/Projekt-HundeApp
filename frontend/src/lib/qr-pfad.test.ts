import { describe, expect, it } from "vitest";
import { encode } from "uqr";
import { qrPfad } from "./qr-pfad";

describe("qrPfad", () => {
  it("fasst waagerechte Reihen dunkler Module zu einem Rechteck zusammen", () => {
    expect(qrPfad([[true, true, false, true]])).toBe("M0 0h2v1h-2zM3 0h1v1h-1z");
  });

  it("setzt die Zeilen untereinander", () => {
    expect(qrPfad([[true], [false], [true]])).toBe("M0 0h1v1h-1zM0 2h1v1h-1z");
  });

  it("verschiebt um den Rand (Ruhezone) in beide Richtungen", () => {
    expect(qrPfad([[false, true]], 4)).toBe("M5 4h1v1h-1z");
  });

  it("liefert für eine leere Matrix einen leeren Pfad", () => {
    expect(qrPfad([])).toBe("");
    expect(qrPfad([[false, false]])).toBe("");
  });
});

describe("QR-Code einer Einladungsadresse", () => {
  it("ergibt eine quadratische Matrix, die sich zu einem Pfad zeichnen lässt", () => {
    const { data, size } = encode("https://dogity.net/v/Ab3_-Zy9Ab3_-Zy9Ab3_-Z", { ecc: "Q", border: 0 });

    expect(data).toHaveLength(size);
    expect(data.every((zeile) => zeile.length === size)).toBe(true);
    // Eine Adresse dieser Länge passt in eine kleine Version (21 + 4 je Stufe).
    expect(size).toBeLessThanOrEqual(45);
    expect(qrPfad(data, 4)).toMatch(/^M\d+ \d+h\d+v1h-\d+z/);
  });
});

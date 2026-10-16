import { describe, expect, it } from "vitest";
import { katalogName, uebungName } from "./katalog-name";

describe("katalogName", () => {
  it("zeigt auf Englisch den englischen Namen", () => {
    expect(katalogName({ name: "Freifolge", nameEn: "Free heeling" }, "en")).toBe("Free heeling");
  });

  it("zeigt auf Deutsch immer den deutschen Namen", () => {
    expect(katalogName({ name: "Freifolge", nameEn: "Free heeling" }, "de")).toBe("Freifolge");
  });

  it("fällt ohne englischen Namen auf den deutschen zurück", () => {
    expect(katalogName({ name: "Agility 1 (A1)" }, "en")).toBe("Agility 1 (A1)");
    expect(katalogName({ name: "Agility 1 (A1)", nameEn: null }, "en")).toBe("Agility 1 (A1)");
  });

  it("behandelt einen leeren oder blanken englischen Namen wie keinen", () => {
    expect(katalogName({ name: "Hoop", nameEn: "" }, "en")).toBe("Hoop");
    expect(katalogName({ name: "Hoop", nameEn: "   " }, "en")).toBe("Hoop");
  });
});

describe("uebungName", () => {
  it("nutzt exerciseNameEn auf Englisch", () => {
    expect(uebungName({ exerciseName: "Sitz aus der Bewegung", exerciseNameEn: "Sit in motion" }, "en")).toBe("Sit in motion");
  });

  it("lässt Freitext, wie er eingegeben wurde", () => {
    expect(uebungName({ exerciseName: "Kopfarbeit", exerciseNameEn: null }, "en")).toBe("Kopfarbeit");
    expect(uebungName({ exerciseName: "Kopfarbeit" }, "en")).toBe("Kopfarbeit");
  });

  it("zeigt auf Deutsch den deutschen Namen", () => {
    expect(uebungName({ exerciseName: "Sitz aus der Bewegung", exerciseNameEn: "Sit in motion" }, "de")).toBe("Sitz aus der Bewegung");
  });
});

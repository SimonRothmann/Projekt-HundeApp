import { describe, expect, it } from "vitest";
import { katalogKategorie, katalogName, katalogText, uebungName } from "./katalog-name";

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

describe("katalogText", () => {
  it("zeigt auf Englisch den englischen Text", () => {
    expect(katalogText("Hund läuft eng.", "The dog heels closely.", "en")).toBe("The dog heels closely.");
  });

  it("zeigt auf Deutsch immer den deutschen Text", () => {
    expect(katalogText("Hund läuft eng.", "The dog heels closely.", "de")).toBe("Hund läuft eng.");
  });

  it("fällt ohne englischen Text auf den deutschen zurück", () => {
    expect(katalogText("Nur deutsch.", undefined, "en")).toBe("Nur deutsch.");
    expect(katalogText("Nur deutsch.", null, "en")).toBe("Nur deutsch.");
  });

  it("behandelt einen leeren oder blanken englischen Text wie keinen", () => {
    expect(katalogText("Nur deutsch.", "", "en")).toBe("Nur deutsch.");
    expect(katalogText("Nur deutsch.", "  \n ", "en")).toBe("Nur deutsch.");
  });

  it("liefert null, wenn es gar keinen Text gibt", () => {
    expect(katalogText(null, null, "en")).toBeNull();
    expect(katalogText("  ", undefined, "de")).toBeNull();
  });

  it("zeigt einen englischen Text auch dann, wenn der deutsche fehlt", () => {
    expect(katalogText(null, "Only English.", "en")).toBe("Only English.");
  });

  it("lässt Zeilenumbrüche des Textes unberührt", () => {
    expect(katalogText("a\nb", "c\nd", "en")).toBe("c\nd");
  });
});

describe("katalogKategorie", () => {
  it("übersetzt die festen Kategorien der FCI-Übungen auf Englisch", () => {
    expect(katalogKategorie({ category: "Abteilung B", nameEn: "Free heeling" }, "en")).toBe("Phase B");
    expect(katalogKategorie({ category: "Unterordnung", nameEn: "Heeling on leash" }, "en")).toBe("Obedience");
    expect(katalogKategorie({ category: "Stöbern", nameEn: "Tenacity" }, "en")).toBe("Article search");
  });

  it("lässt die Kategorie auf Deutsch, wie sie ist", () => {
    expect(katalogKategorie({ category: "Abteilung B", nameEn: "Free heeling" }, "de")).toBe("Abteilung B");
  });

  it("lässt nationale Übungen ohne englischen Namen deutsch", () => {
    expect(katalogKategorie({ category: "Verhalten", nameEn: null }, "en")).toBe("Verhalten");
    expect(katalogKategorie({ category: "Unterordnung" }, "en")).toBe("Unterordnung");
  });

  it("lässt frei vergebene Kategorien auch bei FCI-Übungen stehen", () => {
    expect(katalogKategorie({ category: "Mein Block", nameEn: "Free heeling" }, "en")).toBe("Mein Block");
  });

  it("liefert null ohne Kategorie", () => {
    expect(katalogKategorie({ category: null, nameEn: "x" }, "en")).toBeNull();
    expect(katalogKategorie({ category: "  " }, "de")).toBeNull();
  });
});

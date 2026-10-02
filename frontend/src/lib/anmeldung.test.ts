import { describe, expect, it } from "vitest";
import {
  anmeldungsUrl,
  istDoppelteAnmeldung,
  istGueltigeTelefonnummer,
  istGueltigerAnmeldecode,
  jahreZurueck,
  wurftagVorschlag,
  normalisiereTelefon,
  pruefeAnmeldung,
  telefonLink,
  wurftagFehler,
} from "./anmeldung";
import { formatDogAge } from "./dog-age";

describe("istGueltigeTelefonnummer", () => {
  it.each(["0721 123456", "+49 (721) 12 34-56", "0721/123456", "  0160 1234567  ", "123456"])(
    "nimmt %j an",
    (nummer) => expect(istGueltigeTelefonnummer(nummer)).toBe(true),
  );

  it.each([
    ["", "leer"],
    ["12345", "zu kurz"],
    ["abc 123456", "Buchstaben"],
    ["0721 123456 ext. 5", "Buchstaben und Punkt"],
    ["( ) - / +", "keine Ziffern"],
    ["1".repeat(31), "zu lang"],
    ["+49 1", "zu wenig Ziffern"],
  ])("lehnt %j ab (%s)", (nummer) => expect(istGueltigeTelefonnummer(nummer)).toBe(false));

  it("lässt genau 30 Zeichen zu", () => {
    expect(istGueltigeTelefonnummer("1".repeat(30))).toBe(true);
  });
});

describe("normalisiereTelefon", () => {
  it.each([
    ["0721 123456", "0721123456"],
    ["+49 721 123456", "0721123456"],
    ["0049 721 123456", "0721123456"],
    ["0721/12 34-56", "0721123456"],
    ["+41 44 123 45 67", "41441234567"],
    ["+49 (0) 721 123456", "0721123456"],
    ["+49 0721 123456", "0721123456"],
  ])("macht aus %j %j", (eingabe, erwartet) => expect(normalisiereTelefon(eingabe)).toBe(erwartet));
});

describe("telefonLink", () => {
  it.each([
    ["0721 / 12 34-56", "tel:0721123456"],
    ["+49 721 123456", "tel:+49721123456"],
    ["+49 (0) 721 123456", "tel:+49721123456"],
    ["+49 0721 123456", "tel:+49721123456"],
    ["+41 (0)44 123 45 67", "tel:+41441234567"],
  ])("macht aus %j %j", (eingabe, erwartet) => expect(telefonLink(eingabe)).toBe(erwartet));
});

describe("istDoppelteAnmeldung", () => {
  it("erkennt dieselbe Nummer in anderer Schreibweise und den Rufnamen ohne Rücksicht auf Groß-/Kleinschreibung", () => {
    expect(
      istDoppelteAnmeldung({ phone: "0721 123456", dogName: "Bella" }, { phone: "+49 721 123456", dogName: " BELLA " }),
    ).toBe(true);
  });

  it("hält Geschwister (anderer Rufname) und andere Nummern auseinander", () => {
    expect(istDoppelteAnmeldung({ phone: "0721 123456", dogName: "Bella" }, { phone: "0721 123456", dogName: "Luna" })).toBe(false);
    expect(istDoppelteAnmeldung({ phone: "0721 123456", dogName: "Bella" }, { phone: "0721 654321", dogName: "Bella" })).toBe(false);
  });
});

describe("jahreZurueck", () => {
  it("rechnet in ganzen Jahren", () => {
    expect(jahreZurueck("2026-10-01", 2)).toBe("2024-10-01");
  });

  it("macht aus dem 29. Februar den 28., wenn das Zieljahr kein Schaltjahr ist", () => {
    expect(jahreZurueck("2028-02-29", 2)).toBe("2026-02-28");
    expect(jahreZurueck("2028-02-29", 4)).toBe("2024-02-29");
  });
});

describe("wurftagFehler", () => {
  const heute = "2026-10-01";

  it("nimmt einen Wurftag innerhalb der zwei Jahre an, auch genau heute und genau vor zwei Jahren", () => {
    expect(wurftagFehler("2026-07-15", heute)).toBeNull();
    expect(wurftagFehler("2026-10-01", heute)).toBeNull();
    expect(wurftagFehler("2024-10-01", heute)).toBeNull();
  });

  it("lehnt Zukunft und mehr als zwei Jahre ab", () => {
    expect(wurftagFehler("2026-10-02", heute)).toBe("zukunft");
    expect(wurftagFehler("2024-09-30", heute)).toBe("zuAlt");
  });

  it("meldet einen fehlenden oder unlesbaren Wurftag als fehlend", () => {
    expect(wurftagFehler("", heute)).toBe("fehlt");
    expect(wurftagFehler("01.10.2026", heute)).toBe("fehlt");
  });

  it("misst die zwei Jahre beim Import gegen den Tag der Anmeldung", () => {
    // Angemeldet im Oktober 2025, Hund damals 18 Monate: heute 30 Monate alt, aber gültig.
    expect(wurftagFehler("2024-04-01", heute, "2025-10-01")).toBeNull();
    expect(wurftagFehler("2024-04-01", heute)).toBe("zuAlt");
  });
});

describe("Anmeldelink", () => {
  it("setzt Adresse und Code zusammen", () => {
    expect(anmeldungsUrl("https://dogity.net", "abc_DEF-123")).toBe("https://dogity.net/anmeldung/abc_DEF-123");
  });

  it("prüft die Form des Codes wie beim Vereinslink", () => {
    expect(istGueltigerAnmeldecode("AAAAAAAAAAAAAAAAAAAAAA")).toBe(true);
    expect(istGueltigerAnmeldecode("../etc/passwd")).toBe(false);
    expect(istGueltigerAnmeldecode(null)).toBe(false);
  });
});

describe("Alter aus dem Wurftag (formatDogAge)", () => {
  const heute = new Date(2026, 9, 1); // 01.10.2026

  it("zeigt Welpen in Monaten", () => {
    expect(formatDogAge("2026-07-01", heute)).toBe("3 Monate");
    expect(formatDogAge("2026-08-15", heute)).toBe("1 Monat");
  });

  it("zeigt einen Hund vom heutigen Wurftag als 0 Monate, nicht als fehlend", () => {
    expect(formatDogAge("2026-10-01", heute)).toBe("0 Monate");
  });
});

describe("pruefeAnmeldung", () => {
  const heute = "2026-10-01";
  const gut = { vorname: "Anna", nachname: "Muster", rufname: "Bella", rasse: "Labrador", wurftag: "2026-07-15", telefon: "0721 123456" };

  it("nimmt eine vollständige Anmeldung an", () => {
    expect(pruefeAnmeldung(gut, heute)).toBeNull();
  });

  it.each([
    ["vorname", { vorname: "  " }],
    ["nachname", { nachname: "" }],
    ["rufname", { rufname: "" }],
    ["rasse", { rasse: " " }],
    ["wurftagFehlt", { wurftag: "" }],
    ["wurftagZukunft", { wurftag: "2026-10-02" }],
    ["wurftagAlt", { wurftag: "2024-09-30" }],
    ["telefon", { telefon: "abc" }],
    ["zuLang", { vorname: "x".repeat(101) }],
    ["zuLang", { rasse: "x".repeat(101) }],
    ["zuLang", { telefon: "1".repeat(31) }],
  ] as const)("meldet %s", (erwartet, abweichung) => {
    expect(pruefeAnmeldung({ ...gut, ...abweichung }, heute)).toBe(erwartet);
  });

  it("unterscheidet einen leeren von einem unlesbaren Wurftag (Import)", () => {
    expect(pruefeAnmeldung({ ...gut, wurftag: "" }, heute, heute, true)).toBe("wurftagFormat");
    expect(pruefeAnmeldung({ ...gut, wurftag: "" }, heute, heute, false)).toBe("wurftagFehlt");
  });

  it("meldet den ersten Fehler in der Reihenfolge des Formulars", () => {
    expect(pruefeAnmeldung({ ...gut, vorname: "", wurftag: "" }, heute)).toBe("vorname");
  });
});

describe("wurftagVorschlag", () => {
  it.each([
    ["2026-10-02", "2026-08-07"],
    ["2026-03-05", "2026-01-08"],
    ["2024-04-25", "2024-02-29"],
  ])("macht aus %j %j (8 Wochen zurück)", (heute, erwartet) => expect(wurftagVorschlag(heute)).toBe(erwartet));
});

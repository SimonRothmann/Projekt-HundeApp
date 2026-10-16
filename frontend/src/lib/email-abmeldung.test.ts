import { describe, expect, it } from "vitest";
import { abmeldeFehlerArt, abmeldeTokenAus } from "./email-abmeldung";

describe("abmeldeTokenAus", () => {
  it("übernimmt ein Token aus base64url-Zeichen", () => {
    expect(abmeldeTokenAus("CfDJ8abc_DEF-123")).toBe("CfDJ8abc_DEF-123");
  });

  it("schneidet Leerraum außen ab", () => {
    expect(abmeldeTokenAus("  abc  ")).toBe("abc");
  });

  it("lehnt fehlende und leere Werte ab", () => {
    expect(abmeldeTokenAus(null)).toBeNull();
    expect(abmeldeTokenAus(undefined)).toBeNull();
    expect(abmeldeTokenAus("")).toBeNull();
    expect(abmeldeTokenAus("   ")).toBeNull();
  });

  it("lehnt Zeichen ab, die in einem Token nicht vorkommen", () => {
    expect(abmeldeTokenAus("abc def")).toBeNull();
    expect(abmeldeTokenAus("abc=")).toBeNull();
    expect(abmeldeTokenAus("abc/def")).toBeNull();
    expect(abmeldeTokenAus("<script>")).toBeNull();
  });

  it("lehnt ein zu langes Token ab", () => {
    expect(abmeldeTokenAus("a".repeat(512))).not.toBeNull();
    expect(abmeldeTokenAus("a".repeat(513))).toBeNull();
  });
});

describe("abmeldeFehlerArt", () => {
  it("hält den Link nur bei einer Absage des Servers für ungültig", () => {
    expect(abmeldeFehlerArt(400)).toBe("ungueltig");
    expect(abmeldeFehlerArt(404)).toBe("ungueltig");
  });

  it("hält Drosselung, Serverfehler und fehlendes Netz für vorübergehend", () => {
    expect(abmeldeFehlerArt(429)).toBe("nichtErreichbar");
    expect(abmeldeFehlerArt(500)).toBe("nichtErreichbar");
    expect(abmeldeFehlerArt(null)).toBe("nichtErreichbar");
  });
});

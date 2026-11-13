import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, api, istVoruebergehendUnerreichbar } from "@/lib/api";
import { TEXTLAENGE } from "./textlaengen";
import {
  ausEingabe,
  bannerInhalt,
  entwurfPruefen,
  istWartungsantwort,
  wartungLaeuftJetzt,
  wartungNachAntwort,
  wartungZuruecksetzen,
  wegklickSchluessel,
  zuEingabe,
} from "./wartung";

/** Ortszeit als ISO-Zeitpunkt: unabhängig von der Zeitzone, in der der Test läuft. */
const um = (monat: number, tag: number, stunde: number, minute = 0) => new Date(2026, monat - 1, tag, stunde, minute).toISOString();

const hinweis = (teil: Partial<{ text: string | null; beginn: string; ende: string | null; laeuft: boolean }> = {}) => ({
  text: null,
  beginn: um(11, 14, 20),
  ende: um(11, 14, 22),
  laeuft: false,
  ...teil,
});

describe("bannerInhalt", () => {
  it("zeigt nichts ohne Hinweis und ohne erkannte Wartung", () => {
    expect(bannerInhalt(null, false, "de")).toBeNull();
  });

  it("kündigt vorher mit Tag, Beginn und Ende an", () => {
    const inhalt = bannerInhalt(hinweis(), false, "de")!;

    expect(inhalt.laeuft).toBe(false);
    expect(inhalt.text).toMatch(/^Wartungsarbeiten am Samstag, 14\. November von 20:00 bis 22:00 Uhr – /);
    expect(inhalt.text).toContain("Dogity ist in dieser Zeit kurz nicht erreichbar.");
    expect(inhalt.text).toContain("Einträge auf dem Handy werden gespeichert und danach nachgeschickt.");
  });

  it("sagt ohne Ende nur \"ab\"", () => {
    const inhalt = bannerInhalt(hinweis({ ende: null }), false, "de")!;

    expect(inhalt.text).toMatch(/^Wartungsarbeiten am Samstag, 14\. November ab 20:00 Uhr – /);
  });

  it("nennt beide Tage, wenn die Wartung über Mitternacht geht", () => {
    const inhalt = bannerInhalt(hinweis({ ende: um(11, 15, 2) }), false, "de")!;

    expect(inhalt.text).toMatch(/^Wartungsarbeiten von Sa\., 14\. Nov\.,? 20:00 bis So\., 15\. Nov\.,? 02:00 Uhr – /);
  });

  it("sagt während der Wartung, dass Dogity gleich wieder da ist", () => {
    const inhalt = bannerInhalt(hinweis({ laeuft: true }), false, "de")!;

    expect(inhalt.laeuft).toBe(true);
    expect(inhalt.text).toBe("Wartungsarbeiten laufen – Dogity ist gleich wieder da. Deine Einträge werden auf dem Gerät gespeichert und nachgeschickt.");
  });

  it("zeigt die Wartung auch ohne Hinweis aus der Datenbank, wenn der Proxy sie meldet", () => {
    const inhalt = bannerInhalt(null, true, "de")!;

    expect(inhalt.laeuft).toBe(true);
    expect(inhalt.text).toContain("Wartungsarbeiten laufen");
  });

  it("macht aus einer angekündigten Wartung den Zustand während, sobald der Proxy sie meldet", () => {
    expect(bannerInhalt(hinweis(), true, "de")!.laeuft).toBe(true);
  });

  it("setzt vor einen eigenen Text die Zeiten und lässt den Standardtext weg", () => {
    const inhalt = bannerInhalt(hinweis({ text: "Wir ziehen auf einen neuen Server um." }), false, "de")!;

    expect(inhalt.text).toMatch(/^Sa\., 14\. Nov\.,? 20:00–22:00 Uhr: Wir ziehen auf einen neuen Server um\.$/);
    expect(inhalt.text).not.toContain("kurz nicht erreichbar");
  });

  it("setzt vor einen eigenen Text ohne Ende \"ab\"", () => {
    const inhalt = bannerInhalt(hinweis({ text: "Update", ende: null }), false, "de")!;

    expect(inhalt.text).toMatch(/^Sa\., 14\. Nov\.,? ab 20:00 Uhr: Update$/);
  });

  it("zeigt den eigenen Text auch während der Wartung", () => {
    const inhalt = bannerInhalt(hinweis({ text: "Update", laeuft: true }), false, "de")!;

    expect(inhalt.laeuft).toBe(true);
    expect(inhalt.text).toMatch(/Uhr: Update$/);
  });

  it("schreibt auf Englisch mit englischer Datumsform und ohne \"Uhr\"", () => {
    const inhalt = bannerInhalt(hinweis(), false, "en")!;

    expect(inhalt.text).toMatch(/^Maintenance on Saturday,? 14 November from 20:00 to 22:00 – /);
    expect(inhalt.text).not.toContain("Uhr");
    expect(bannerInhalt(null, true, "en")!.text).toBe("Maintenance is under way – Dogity will be back shortly. Your entries are saved on your device and sent afterwards.");
  });
});

describe("wegklickSchluessel", () => {
  it("hängt am Änderungsstand, damit ein geänderter Hinweis wieder erscheint", () => {
    const a = wegklickSchluessel("2026-11-08T10:00:00+00:00");
    const b = wegklickSchluessel("2026-11-08T10:05:00+00:00");

    expect(a).not.toBe(b);
    expect(a).toContain("2026-11-08T10:00:00");
    expect(wegklickSchluessel("2026-11-08T10:00:00+00:00")).toBe(a);
  });
});

describe("Wartung erkennen", () => {
  it("erkennt nur die 503 mit Kopfzeile als Wartung", () => {
    expect(istWartungsantwort(503, "1")).toBe(true);
    expect(istWartungsantwort(503, null)).toBe(false);
    expect(istWartungsantwort(502, "1")).toBe(false);
    expect(istWartungsantwort(200, "1")).toBe(false);
  });

  it("schaltet bei der Wartungsantwort ein und bei der nächsten erfolgreichen Antwort aus", () => {
    expect(wartungNachAntwort(false, 503, "1")).toBe(true);
    expect(wartungNachAntwort(true, 200, null)).toBe(false);
    expect(wartungNachAntwort(true, 204, null)).toBe(false);
    expect(wartungNachAntwort(true, 304, null)).toBe(false);
  });

  it("lässt den Zustand bei anderen Fehlern, wie er ist", () => {
    expect(wartungNachAntwort(true, 503, null)).toBe(true);
    expect(wartungNachAntwort(true, 500, null)).toBe(true);
    expect(wartungNachAntwort(true, 401, null)).toBe(true);
    expect(wartungNachAntwort(false, 502, null)).toBe(false);
    expect(wartungNachAntwort(false, 400, null)).toBe(false);
  });
});

describe("Wartung im API-Helfer", () => {
  beforeEach(() => {
    wartungZuruecksetzen();
    vi.stubGlobal("window", {
      localStorage: { getItem: () => null, setItem: () => {}, removeItem: () => {} },
      location: { protocol: "http:", hostname: "localhost", pathname: "/", href: "http://localhost/" },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    wartungZuruecksetzen();
  });

  function antwortet(...antworten: Response[]) {
    const fetchMock = vi.fn();
    antworten.forEach((a) => fetchMock.mockResolvedValueOnce(a));
    vi.stubGlobal("fetch", fetchMock);
  }

  it("setzt \"Wartung läuft\" bei der Antwort des Proxys und nimmt sie bei der nächsten erfolgreichen zurück", async () => {
    antwortet(
      new Response(JSON.stringify({ wartung: true }), { status: 503, headers: { "X-Dogity-Wartung": "1" } }),
      new Response(JSON.stringify({ ok: true }), { status: 200 }),
    );

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);
    expect(wartungLaeuftJetzt()).toBe(true);

    await api.get("/api/dogs");
    expect(wartungLaeuftJetzt()).toBe(false);
  });

  it("behandelt die Wartungsantwort für die Offline-Warteschlange weiter wie jede 503", async () => {
    antwortet(new Response("{}", { status: 503, headers: { "X-Dogity-Wartung": "1" } }));

    const fehler = await api.post("/api/trainings", {}).catch((e: unknown) => e);

    expect(fehler).toBeInstanceOf(ApiError);
    expect(istVoruebergehendUnerreichbar(fehler)).toBe(true);
  });

  it("hält eine 503 ohne Kopfzeile für einen gewöhnlichen Ausfall", async () => {
    antwortet(new Response("{}", { status: 503 }));

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);
    expect(wartungLaeuftJetzt()).toBe(false);
  });
});

describe("Eingabe des Betreibers", () => {
  it("rechnet Datum und Uhrzeit in Ortszeit hin und zurück", () => {
    const iso = um(11, 14, 20, 30);

    expect(zuEingabe(iso)).toEqual({ datum: "2026-11-14", zeit: "20:30" });
    expect(ausEingabe(zuEingabe(iso))).toBe(iso);
  });

  it("liefert für nichts leere Felder und für unvollständige Felder nichts", () => {
    expect(zuEingabe(null)).toEqual({ datum: "", zeit: "" });
    expect(ausEingabe({ datum: "2026-11-14", zeit: "" })).toBeNull();
    expect(ausEingabe({ datum: "", zeit: "20:00" })).toBeNull();
    expect(ausEingabe({ datum: "2026-13-45", zeit: "20:00" })).toBeNull();
  });

  const gut = { beginn: { datum: "2026-11-14", zeit: "20:00" }, ende: { datum: "2026-11-14", zeit: "22:00" }, text: "  ", aktiv: true };

  it("baut die Anfrage; ein leerer Text wird zu keinem Text", () => {
    expect(entwurfPruefen(gut)).toEqual({
      art: "ok",
      anfrage: { text: null, beginn: um(11, 14, 20), ende: um(11, 14, 22), aktiv: true },
    });
  });

  it("erlaubt ein Ende ganz ohne Angabe", () => {
    const ergebnis = entwurfPruefen({ ...gut, ende: { datum: "", zeit: "" }, text: " Update " });

    expect(ergebnis).toEqual({ art: "ok", anfrage: { text: "Update", beginn: um(11, 14, 20), ende: null, aktiv: true } });
  });

  it("verlangt einen vollständigen Beginn", () => {
    expect(entwurfPruefen({ ...gut, beginn: { datum: "2026-11-14", zeit: "" } })).toEqual({ art: "beginn" });
  });

  it("verlangt ein Ende ganz oder gar nicht", () => {
    expect(entwurfPruefen({ ...gut, ende: { datum: "2026-11-14", zeit: "" } })).toEqual({ art: "ende" });
  });

  it("lehnt ein Ende vor oder zum Beginn ab", () => {
    expect(entwurfPruefen({ ...gut, ende: { datum: "2026-11-14", zeit: "19:00" } })).toEqual({ art: "reihenfolge" });
    expect(entwurfPruefen({ ...gut, ende: { datum: "2026-11-14", zeit: "20:00" } })).toEqual({ art: "reihenfolge" });
  });
});

describe("Frontend und Backend meinen dieselbe Länge", () => {
  it("Text des Wartungshinweises (Textlaengen.cs)", () => {
    const quelle = readFileSync(join(process.cwd(), "..", "backend", "src", "Dogity.Application", "Common", "Textlaengen.cs"), "utf8");
    const treffer = /\bWartungshinweisText\s*=\s*(\d+);/.exec(quelle);

    expect(Number(treffer?.[1])).toBe(TEXTLAENGE.wartungshinweisText);
  });
});

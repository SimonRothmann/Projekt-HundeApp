import { describe, expect, it } from "vitest";
import { anfrageAusSicherung, type FaehrtenSicherung } from "./aufzeichnung-sicherung";
import { hauptSpurArt } from "./faehrte";
import { uebersetze } from "./i18n";
import { markerListe } from "./gegenstaende";
import { MARKER_LABELS } from "./karte/spur-daten";
import { spurGrenzen } from "./mantrailing";
import {
  LEERE_SCHWEISS_ANGABEN,
  SCHWEISS_LEITFAEDEN,
  schweissFortschritt,
  schweissLeitfadenNachId,
  schweissLeitfadenZeile,
  schweissMlAusText,
  stehzeitErreichtAb,
  stueckAenderung,
  vorgabeSchweissLeitfaden,
} from "./schweiss";

describe("Ergebnis am Stück (SF2)", () => {
  it("meldet nur Änderungen und streicht das Verhalten ohne Stück", () => {
    expect(stueckAenderung({ personFound: null }, { gefunden: null, verhalten: null })).toBeNull();
    expect(stueckAenderung({ personFound: null }, { gefunden: true, verhalten: 0 })).toEqual({ personFound: true, indication: 0 });
    expect(stueckAenderung({ personFound: true, indication: 0 }, { gefunden: true, verhalten: 0 })).toBeNull();
    expect(stueckAenderung({ personFound: true, indication: 4 }, { gefunden: false, verhalten: 4 })).toEqual({ personFound: false, indication: null });
  });
});
import { ART_SCHWEISS, istSchweiss, spurArt } from "./spurart";
import type { GpsPoint } from "./types";

const t = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);

describe("Art der Spur", () => {
  it("kennt die drei Arten, ohne Angabe die Fährte", () => {
    expect(spurArt({ kind: 0 })).toBe("faehrte");
    expect(spurArt({ kind: 1 })).toBe("trail");
    expect(spurArt({ kind: ART_SCHWEISS })).toBe("schweiss");
    expect(spurArt({})).toBe("faehrte");
    expect(spurArt(undefined)).toBe("faehrte");
    expect(istSchweiss({ kind: 2 })).toBe(true);
    expect(istSchweiss({ kind: 0 })).toBe(false);
    expect(istSchweiss("JAGD")).toBe(true);
  });

  it("Grenzen der Schweißfährte: 10/30 m", () => {
    expect(spurGrenzen({ kind: ART_SCHWEISS })).toEqual({ gruenBis: 10, gelbBis: 30 });
  });

  it("nennt als Hauptart die Fährte, sonst die eine mögliche", () => {
    expect(hauptSpurArt({ faehrte: true, mantrailing: true, schweiss: true })).toBe("faehrte");
    expect(hauptSpurArt({ faehrte: false, mantrailing: false, schweiss: true })).toBe("schweiss");
    expect(hauptSpurArt({ faehrte: false, mantrailing: true, schweiss: false })).toBe("trail");
  });
});

describe("Leitfaden der Schweißfährte", () => {
  it("führt die sechs Stufen mit den Stehzeiten der Prüfungsordnungen", () => {
    expect(SCHWEISS_LEITFAEDEN.map((l) => [l.id, l.stehzeitStunden])).toEqual([
      ["vgp-tag", 2],
      ["vgp-nacht", 14],
      ["vswp-20", 20],
      ["vswp-40", 40],
      ["vfsp-20", 20],
      ["vfsp-40", 40],
    ]);
  });

  it("belegt aus dem aktiven Ziel vor, sonst aus der gemerkten Stufe", () => {
    const ziel = (name: string, datum: string, status = 0) => ({ status, regulationName: name, targetDate: datum }) as never;
    expect(vorgabeSchweissLeitfaden([ziel("JGHV VSwP 40 h", "2027-01-01")], null)?.id).toBe("vswp-40");
    expect(vorgabeSchweissLeitfaden([ziel("JGHV VFsP 20 h", "2027-05-01"), ziel("JGHV VSwP 20 h", "2027-02-01")], null)?.id).toBe("vswp-20");
    expect(vorgabeSchweissLeitfaden([ziel("JGHV VSwP 40 h", "2027-01-01", 1)], "vfsp-40")?.id).toBe("vfsp-40");
    expect(vorgabeSchweissLeitfaden([], "gibt-es-nicht")).toBeNull();
    expect(schweissLeitfadenNachId(null)).toBeNull();
  });

  it("rechnet den Fortschritt in Metern, Wundbetten und Verweiserpunkten", () => {
    const vswp = schweissLeitfadenNachId("vswp-20")!;
    expect(schweissFortschritt(vswp, 612.4, 1, 3)).toEqual({
      meterIst: 612,
      meterErreicht: false,
      wundbettenIst: 1,
      wundbettenErreicht: false,
      verweiserIst: 3,
      verweiserErreicht: false,
    });
    expect(schweissFortschritt(vswp, 1000, 2, 4)).toMatchObject({ meterErreicht: true, wundbettenErreicht: true, verweiserErreicht: true });
  });

  it("beschreibt die Stufe in einer Zeile, beim Fährtenschuh mit Tropfbetten", () => {
    expect(schweissLeitfadenZeile(schweissLeitfadenNachId("vswp-20")!, t)).toBe(
      "VSwP 20 h: mind. 1000 m · 3 Haken · 2 Wundbetten · 4 Verweiserpunkte · bis 250 ml Schweiß · mind. 20 Std. Stehzeit",
    );
    expect(schweissLeitfadenZeile(schweissLeitfadenNachId("vfsp-40")!, t)).toContain("4 Tropfbetten · bis 100 ml");
    expect(schweissLeitfadenZeile(schweissLeitfadenNachId("vgp-tag")!, t)).toContain("1 Wundbett · bis 250 ml");
  });

  it("liest die Schweißmenge: leer ist erlaubt, über 1000 nicht", () => {
    expect(schweissMlAusText("")).toBeNull();
    expect(schweissMlAusText(" 250 ")).toBe(250);
    expect(schweissMlAusText("1000")).toBe(1000);
    expect(schweissMlAusText("1001")).toBeUndefined();
    expect(schweissMlAusText("12a")).toBeUndefined();
  });

  it("nennt, wann die Stehzeit erreicht ist", () => {
    const ende = new Date("2026-11-23T10:00:00Z");
    expect(stehzeitErreichtAb(ende, 20)?.toISOString()).toBe("2026-11-24T06:00:00.000Z");
    expect(stehzeitErreichtAb(ende, null)).toBeNull();
  });
});

describe("Marker der Schweißfährte", () => {
  it("benennt Wundbett und Verweiserpunkt und zählt gleiche Arten durch", () => {
    expect(MARKER_LABELS[4]).toBe("Wundbett");
    expect(MARKER_LABELS[5]).toBe("Verweiserpunkt");
    const marker = (id: string, zeit: string, markerType: number) =>
      ({ id, latitude: 48.9, longitude: 8.5, timestamp: zeit, accuracy: 5, pointType: 1, label: null, markerType }) as GpsPoint;
    const liste = markerListe([
      marker("w1", "2026-11-23T10:01:00Z", 4),
      marker("v1", "2026-11-23T10:02:00Z", 5),
      marker("v2", "2026-11-23T10:03:00Z", 5),
    ]);
    expect(liste.map((m) => [m.id, m.art, m.nummer])).toEqual([
      ["w1", 4, null],
      ["v1", 5, 1],
      ["v2", 5, 2],
    ]);
  });
});

describe("Anfrage beim Speichern einer Schweißfährte", () => {
  const sicherung = (schweiss?: FaehrtenSicherung["schweiss"]): FaehrtenSicherung => ({
    art: "faehrte",
    userId: "u",
    seite: "/dogs/d",
    begonnen: Date.parse("2026-11-23T10:00:00Z"),
    trackId: "t",
    dogId: "d",
    untergrund: ["Laubwald"],
    bodenzustand: ["feucht"],
    points: [],
    ...(schweiss ? { schweiss } : {}),
  });

  it("schickt Art und nur die gewählten Angaben mit - Bodenzustand wie bei der Fährte", () => {
    const body = anfrageAusSicherung(
      sicherung({ verfahren: "Tupf", wildart: null, schweissMl: 100, stehzeitStunden: 20, leitfadenId: "vswp-20" }),
    ).body as Record<string, unknown>;
    expect(body).toMatchObject({ kind: ART_SCHWEISS, verfahren: "Tupf", schweissMl: 100, stehzeitStunden: 20, groundCondition: "feucht" });
    expect(body).not.toHaveProperty("wildart");
    expect(body).not.toHaveProperty("doubleBlind");
  });

  it("lässt bei leeren Angaben nur die Art mitgehen, bei einer Fährte gar nichts davon", () => {
    expect(anfrageAusSicherung(sicherung(LEERE_SCHWEISS_ANGABEN)).body).toMatchObject({ kind: ART_SCHWEISS });
    const fahrte = anfrageAusSicherung(sicherung()).body as Record<string, unknown>;
    expect(fahrte).not.toHaveProperty("kind");
    expect(fahrte).not.toHaveProperty("verfahren");
  });
});

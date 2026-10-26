import { describe, expect, it } from "vitest";
import {
  ampelSatz,
  chipText,
  grundText,
  teileUebungen,
  theorieLink,
  theorieText,
  zeitText,
  zuletztText,
} from "./pruefungsreife";
import { uebersetze } from "./i18n";
import {
  REIFE_AMPEL,
  REIFE_GRUND,
  REIFE_ZUSTAND,
  type PruefungsreifeTheorie,
  type PruefungsreifeUebung,
} from "./types";

const tDe = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const tEn = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

function uebung(teil: Partial<PruefungsreifeUebung> = {}): PruefungsreifeUebung {
  return {
    exerciseId: "u",
    name: "Fußarbeit",
    isMandatory: true,
    maxPoints: 30,
    zustand: REIFE_ZUSTAND.Wackelig,
    grund: REIFE_GRUND.SeltenTrainiert,
    tageSeit: 4,
    pausenTage: 0,
    bewertung: 4.2,
    ...teil,
  };
}

function theorie(teil: Partial<PruefungsreifeTheorie> = {}): PruefungsreifeTheorie {
  return { zustand: REIFE_ZUSTAND.Wackelig, catalogCode: "SWHV-BHVT-ERW", gewertet: 2, bestanden: 1, inFehlerspeicher: 0, ...teil };
}

describe("chipText", () => {
  it("nennt Ampel, offene und wackelige Übungen", () => {
    expect(chipText(tDe, { ampel: REIFE_AMPEL.Gelb, offen: 2, wackelig: 3 })).toBe("Prüfungsreife: gelb · 2 offen, 3 wackelig");
  });

  it("lässt Nullen weg", () => {
    expect(chipText(tDe, { ampel: REIFE_AMPEL.Rot, offen: 4, wackelig: 0 })).toBe("Prüfungsreife: rot · 4 offen");
    expect(chipText(tDe, { ampel: REIFE_AMPEL.Gelb, offen: 0, wackelig: 1 })).toBe("Prüfungsreife: gelb · 1 wackelig");
  });

  it("sagt bei grün und nichts Offenem 'alles sicher'", () => {
    expect(chipText(tDe, { ampel: REIFE_AMPEL.Gruen, offen: 0, wackelig: 0 })).toBe("Prüfungsreife: grün · alles sicher");
  });

  it("gibt es auf Englisch", () => {
    expect(chipText(tEn, { ampel: REIFE_AMPEL.Gelb, offen: 2, wackelig: 3 })).toBe("Exam readiness: yellow · 2 open, 3 shaky");
    expect(chipText(tEn, { ampel: REIFE_AMPEL.Gruen, offen: 0, wackelig: 0 })).toBe("Exam readiness: green · all secure");
  });
});

describe("ampelSatz", () => {
  it("gelb ohne Offenes: 'Fast so weit: 2 Übungen wackeln noch.'", () => {
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Gelb, offen: 0, wackelig: 2, theorie: null })).toBe("Fast so weit: 2 Übungen wackeln noch.");
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Gelb, offen: 0, wackelig: 1, theorie: null })).toBe("Fast so weit: 1 Übung wackelt noch.");
  });

  it("gelb, weil nur die Theorie fehlt", () => {
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Gelb, offen: 0, wackelig: 0, theorie: theorie() })).toContain("jetzt fehlt noch die Theorie");
  });

  it("gelb mit Offenem (Wahlübungen)", () => {
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Gelb, offen: 1, wackelig: 2, theorie: null })).toBe("Auf dem Weg – offen: 1, wackelig: 2.");
  });

  it("rot nennt die nie trainierten Übungen oder den zu kleinen Anteil", () => {
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Rot, offen: 1, wackelig: 3, theorie: null })).toBe("Noch nicht so weit: 1 Übung wurde noch nie trainiert.");
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Rot, offen: 3, wackelig: 0, theorie: null })).toBe("Noch nicht so weit: 3 Übungen wurden noch nie trainiert.");
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Rot, offen: 0, wackelig: 5, theorie: null })).toBe("Noch nicht so weit: Die meisten Übungen sitzen noch nicht sicher.");
  });

  it("grün: alle oder fast alle", () => {
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Gruen, offen: 0, wackelig: 0, theorie: null })).toBe("Bereit: Alle Übungen sitzen sicher.");
    expect(ampelSatz(tDe, { ampel: REIFE_AMPEL.Gruen, offen: 0, wackelig: 1, theorie: null })).toBe("Bereit: Fast alle Übungen sitzen sicher.");
  });
});

describe("zeitText", () => {
  it("zählt Wochen, mit Pausenanteil", () => {
    expect(zeitText(tDe, { tage: 35, wochen: 5, pausenTage: 0 })).toBe("Noch 5 Wochen bis zur Prüfung");
    expect(zeitText(tDe, { tage: 6, wochen: 1, pausenTage: 0 })).toBe("Noch 1 Woche bis zur Prüfung");
    expect(zeitText(tDe, { tage: 35, wochen: 3, pausenTage: 14 })).toBe("Noch 3 Wochen bis zur Prüfung (ohne 14 Tage Pause)");
  });

  it("sagt am Prüfungstag und danach keine Wochen", () => {
    expect(zeitText(tDe, { tage: 0, wochen: 0, pausenTage: 0 })).toBe("Der Prüfungstag ist heute oder schon vorbei.");
  });
});

describe("grundText", () => {
  it("offen: noch nie trainiert", () => {
    expect(grundText(tDe, uebung({ zustand: REIFE_ZUSTAND.Offen, grund: null, tageSeit: null, bewertung: null }), "de")).toBe("noch nie trainiert");
  });

  it("schwache Bewertung mit Dezimalkomma bzw. -punkt", () => {
    const u = uebung({ grund: REIFE_GRUND.SchwacheBewertung, bewertung: 2.8 });
    expect(grundText(tDe, u, "de")).toBe("Bewertung im Schnitt 2,8");
    expect(grundText(tEn, u, "en")).toBe("Average rating 2.8");
  });

  it("lange her: zuletzt vor 35 Tagen, mit Pausenanteil", () => {
    const u = uebung({ grund: REIFE_GRUND.LangeHer, tageSeit: 35 });
    expect(grundText(tDe, u, "de")).toBe("zuletzt vor 35 Tagen");
    expect(grundText(tDe, { ...u, pausenTage: 10 }, "de")).toBe("zuletzt vor 35 Tagen (davon 10 Tage Pause)");
  });

  it("selten trainiert", () => {
    expect(grundText(tDe, uebung({ grund: REIFE_GRUND.SeltenTrainiert }), "de")).toBe("noch nicht oft genug gut gelaufen");
  });

  it("sicher: wann zuletzt", () => {
    expect(grundText(tDe, uebung({ zustand: REIFE_ZUSTAND.Sicher, grund: null, tageSeit: 1 }), "de")).toBe("zuletzt gestern");
    expect(grundText(tDe, uebung({ zustand: REIFE_ZUSTAND.Sicher, grund: null, tageSeit: 0 }), "de")).toBe("zuletzt heute");
  });
});

describe("zuletztText", () => {
  it("heute, gestern, vor n Tagen", () => {
    expect(zuletztText(tDe, 0, 0)).toBe("zuletzt heute");
    expect(zuletztText(tDe, 1, 0)).toBe("zuletzt gestern");
    expect(zuletztText(tDe, 12, 0)).toBe("zuletzt vor 12 Tagen");
  });
});

describe("theorie", () => {
  it("theorieText je Zustand", () => {
    expect(theorieText(tDe, theorie({ zustand: REIFE_ZUSTAND.Offen, gewertet: 0, bestanden: 0 }))).toBe("Noch keine Übungsprüfung abgelegt");
    expect(theorieText(tDe, theorie())).toBe("1 von den letzten 2 als Übung bestanden");
    expect(theorieText(tDe, theorie({ zustand: REIFE_ZUSTAND.Sicher, bestanden: 2 }))).toBe("Die letzten 2 Übungsprüfungen bestanden");
  });

  it("der Link führt zum Katalog: erst Übungsprüfung, danach falsche Fragen", () => {
    expect(theorieLink(tDe, theorie({ zustand: REIFE_ZUSTAND.Offen }))).toEqual({ href: "/lernen/swhv-bhvt-erw", text: "Übungsprüfung machen" });
    expect(theorieLink(tDe, theorie({ inFehlerspeicher: 7 }))).toEqual({ href: "/lernen/swhv-bhvt-erw", text: "Falsche Fragen üben (7)" });
    expect(theorieLink(tDe, theorie({ zustand: REIFE_ZUSTAND.Sicher }))).toEqual({ href: "/lernen/swhv-bhvt-erw", text: "Neue Übungsprüfung" });
  });

  it("der Link geht in den Katalog der letzten Übungsprüfung (Jugend)", () => {
    expect(theorieLink(tDe, theorie({ catalogCode: "SWHV-BHVT-JGD" })).href).toBe("/lernen/swhv-bhvt-jgd");
  });
});

describe("teileUebungen", () => {
  it("trennt Fehlendes von Sicherem und behält die Reihenfolge", () => {
    const liste = [
      uebung({ exerciseId: "a", zustand: REIFE_ZUSTAND.Offen }),
      uebung({ exerciseId: "b", zustand: REIFE_ZUSTAND.Wackelig }),
      uebung({ exerciseId: "c", zustand: REIFE_ZUSTAND.Sicher }),
    ];

    const { fehlend, sicher } = teileUebungen(liste);

    expect(fehlend.map((u) => u.exerciseId)).toEqual(["a", "b"]);
    expect(sicher.map((u) => u.exerciseId)).toEqual(["c"]);
  });
});

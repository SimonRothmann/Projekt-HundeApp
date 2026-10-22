import { describe, expect, it } from "vitest";
import {
  aktuellePause,
  datumTagMonat,
  laeufigkeitEnde,
  pausenStand,
  pausenZeile,
  pausenZeitraumText,
  pausentageZwischen,
  pausiertAm,
  plusTage,
  pruefePause,
  vorgeschlagenesEnde,
  vorschlagZieldatum,
  waehlbareGruende,
  type PausenEingabe,
} from "./pausen";
import { uebersetze } from "./i18n";
import { PAUSE_GRUND, type DogPause, type Goal, type TrainingPlanItem } from "./types";

const tDe = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const tEn = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

function pause(teil: Partial<DogPause> = {}): DogPause {
  return { id: "p", startDate: "2026-11-01", endDate: "2026-11-03", reason: PAUSE_GRUND.Verletzt, note: null, ...teil };
}

describe("plusTage", () => {
  it("rechnet über Monats- und Jahresgrenzen", () => {
    expect(plusTage("2026-10-30", 3)).toBe("2026-11-02");
    expect(plusTage("2026-12-30", 3)).toBe("2027-01-02");
    expect(plusTage("2027-03-01", -1)).toBe("2027-02-28");
  });
});

describe("laeufigkeitEnde", () => {
  it("schlägt als Ende Beginn + 21 Tage vor", () => {
    expect(laeufigkeitEnde("2026-10-01")).toBe("2026-10-22");
  });
});

describe("pausiertAm", () => {
  it("zählt beide Grenzen mit", () => {
    const p = pause();
    expect(pausiertAm(p, "2026-10-31")).toBe(false);
    expect(pausiertAm(p, "2026-11-01")).toBe(true);
    expect(pausiertAm(p, "2026-11-03")).toBe(true);
    expect(pausiertAm(p, "2026-11-04")).toBe(false);
  });

  it("gilt ohne Ende unbegrenzt, aber nicht vor dem Beginn", () => {
    const offen = pause({ endDate: null });
    expect(pausiertAm(offen, "2030-01-01")).toBe(true);
    expect(pausiertAm(offen, "2026-10-31")).toBe(false);
  });

  it("kennt keine Pause ohne Pause", () => {
    expect(pausiertAm(null, "2026-11-01")).toBe(false);
    expect(pausiertAm(undefined, "2026-11-01")).toBe(false);
  });
});

describe("aktuellePause", () => {
  it("liefert die Pause des Hundes, solange sie heute gilt", () => {
    const p = pause();
    expect(aktuellePause({ pause: p }, "2026-11-02")).toBe(p);
  });

  it("verwirft eine Pause, die der Lesecache noch trägt, obwohl sie vorbei ist", () => {
    expect(aktuellePause({ pause: pause() }, "2026-11-05")).toBeNull();
  });

  it("verträgt Hunde ohne das Feld (älterer Zwischenstand)", () => {
    expect(aktuellePause({}, "2026-11-02")).toBeNull();
    expect(aktuellePause(null, "2026-11-02")).toBeNull();
  });
});

describe("pausenStand", () => {
  it("unterscheidet vorbei, laufend und geplant", () => {
    expect(pausenStand(pause(), "2026-11-04")).toBe("vorbei");
    expect(pausenStand(pause(), "2026-11-02")).toBe("laufend");
    expect(pausenStand(pause(), "2026-10-20")).toBe("geplant");
    expect(pausenStand(pause({ endDate: null }), "2027-01-01")).toBe("laufend");
  });
});

describe("pausenZeile", () => {
  it("nennt Grund und Ende", () => {
    expect(pausenZeile(tDe, pause(), "de")).toBe("Pause: verletzt · bis 03.11.");
    expect(pausenZeile(tDe, pause({ reason: PAUSE_GRUND.Krank }), "de")).toBe("Pause: krank · bis 03.11.");
    expect(pausenZeile(tDe, pause({ reason: PAUSE_GRUND.Laeufig }), "de")).toBe("Pause: läufig · bis 03.11.");
  });

  it("sagt bei offenem Ende: bis auf Weiteres", () => {
    expect(pausenZeile(tDe, pause({ endDate: null }), "de")).toBe("Pause: verletzt · bis auf Weiteres");
  });

  it("kennt ohne Grund (Trainer:in) nur das Ende - nie den Grund", () => {
    const fuerTrainerin = pause({ reason: null, note: null });
    expect(pausenZeile(tDe, fuerTrainerin, "de")).toBe("Pause bis 03.11.");
    expect(pausenZeile(tDe, { ...fuerTrainerin, endDate: null }, "de")).toBe("Pause bis auf Weiteres");
  });

  it("gibt es auf Englisch", () => {
    expect(pausenZeile(tEn, pause({ reason: PAUSE_GRUND.Laeufig }), "en")).toBe("Break: in heat · until 03/11");
    expect(pausenZeile(tEn, pause({ reason: null }), "en")).toBe("Break until 03/11");
  });
});

describe("datumTagMonat", () => {
  it("zeigt Tag und Monat", () => {
    expect(datumTagMonat("2026-11-03", "de")).toBe("03.11.");
  });
});

describe("pausenZeitraumText", () => {
  const datum = (iso: string) => iso;
  it("beschreibt mehrtägige, eintägige und offene Pausen", () => {
    expect(pausenZeitraumText(tDe, pause(), datum)).toBe("2026-11-01 – 2026-11-03");
    expect(pausenZeitraumText(tDe, pause({ endDate: "2026-11-01" }), datum)).toBe("2026-11-01");
    expect(pausenZeitraumText(tDe, pause({ endDate: null }), datum)).toBe("ab 2026-11-01");
  });
});

describe("waehlbareGruende", () => {
  it("bietet Läufigkeit nur bei Hündinnen an", () => {
    expect(waehlbareGruende(1)).toEqual([PAUSE_GRUND.Verletzt, PAUSE_GRUND.Krank, PAUSE_GRUND.Laeufig]);
    expect(waehlbareGruende(0)).toEqual([PAUSE_GRUND.Verletzt, PAUSE_GRUND.Krank]);
  });
});

describe("vorgeschlagenesEnde", () => {
  it("schlägt bei Läufigkeit Beginn + 21 Tage vor", () => {
    expect(vorgeschlagenesEnde(PAUSE_GRUND.Laeufig, "2026-10-01", "", false)).toBe("2026-10-22");
  });

  it("schlägt bei anderen Gründen nichts vor", () => {
    expect(vorgeschlagenesEnde(PAUSE_GRUND.Verletzt, "2026-10-01", "", false)).toBe("");
    expect(vorgeschlagenesEnde(null, "2026-10-01", "", false)).toBe("");
  });

  it("lässt ein von Hand gesetztes Ende stehen", () => {
    expect(vorgeschlagenesEnde(PAUSE_GRUND.Laeufig, "2026-10-01", "2026-10-10", true)).toBe("2026-10-10");
    expect(vorgeschlagenesEnde(PAUSE_GRUND.Laeufig, "2026-10-01", "", true)).toBe("");
  });

  it("wartet auf einen vollständigen Beginn", () => {
    expect(vorgeschlagenesEnde(PAUSE_GRUND.Laeufig, "", "", false)).toBe("");
  });
});

describe("pruefePause", () => {
  const heute = "2026-11-04";
  const gut: PausenEingabe = { grund: PAUSE_GRUND.Verletzt, von: "2026-11-04", bis: "", notiz: "" };

  it("nimmt eine vollständige Eingabe an - auch mit offenem Ende und für heute", () => {
    expect(pruefePause(tDe, gut, 0, heute)).toBeNull();
    expect(pruefePause(tDe, { ...gut, bis: "2026-11-04" }, 0, heute)).toBeNull();
  });

  it("verlangt einen Grund", () => {
    expect(pruefePause(tDe, { ...gut, grund: null }, 0, heute)).toBe("Bitte einen Grund für die Pause wählen.");
  });

  it("lässt Läufigkeit nur bei Hündinnen zu", () => {
    const laeufig = { ...gut, grund: PAUSE_GRUND.Laeufig };
    expect(pruefePause(tDe, laeufig, 0, heute)).toBe("Läufigkeit gibt es nur bei Hündinnen.");
    expect(pruefePause(tDe, laeufig, 1, heute)).toBeNull();
  });

  it("verlangt einen Beginn", () => {
    expect(pruefePause(tDe, { ...gut, von: "" }, 0, heute)).toBe("Bitte den Beginn der Pause angeben.");
  });

  it("lehnt ein Ende vor dem Beginn ab", () => {
    expect(pruefePause(tDe, { ...gut, bis: "2026-11-03" }, 0, heute)).toBe("Das Ende der Pause darf nicht vor ihrem Beginn liegen.");
  });

  it("lehnt unsinnig weit entfernte Daten ab, wie der Server", () => {
    expect(pruefePause(tDe, { ...gut, von: "2206-01-01" }, 0, heute)).toBe("Der Beginn der Pause liegt zu weit entfernt.");
    expect(pruefePause(tDe, { ...gut, von: "1999-01-01" }, 0, heute)).toBe("Der Beginn der Pause liegt zu weit entfernt.");
    expect(pruefePause(tDe, { ...gut, bis: "2029-01-01" }, 0, heute)).toBe(
      "Die Pause ist zu lang - für eine unbestimmte Dauer das Ende offen lassen.",
    );
  });
});

describe("pausentageZwischen", () => {
  const heute = "2026-11-04";
  it("zählt Tage von (einschließlich) bis (ausschließlich)", () => {
    const pausen = [{ start: "2026-10-05", end: "2026-10-08" }];
    expect(pausentageZwischen(pausen, "2026-10-01", "2026-10-30", heute)).toBe(4);
    expect(pausentageZwischen(pausen, "2026-10-01", "2026-10-07", heute)).toBe(2);
    expect(pausentageZwischen(pausen, "2026-10-08", "2026-10-30", heute)).toBe(1);
  });

  it("zählt Überschneidungen einmal", () => {
    const pausen = [{ start: "2026-10-02", end: "2026-10-08" }, { start: "2026-10-06", end: "2026-10-12" }];
    expect(pausentageZwischen(pausen, "2026-10-01", "2026-11-01", heute)).toBe(11);
  });

  it("lässt eine offene Pause bis heute gelten", () => {
    expect(pausentageZwischen([{ start: "2026-10-31", end: null }], "2026-10-01", "2026-12-31", heute)).toBe(5);
  });
});

describe("vorschlagZieldatum", () => {
  const item = (woche: number): TrainingPlanItem => ({
    id: `i${woche}`, weekNumber: woche, exerciseId: "e", exerciseName: "Sitz", freeTextLabel: null, repetitionsTarget: 1,
    isRestWeek: false, completedCount: 0, isComplete: false, logs: [], reason: null, dayIndex: 1,
  });
  // Plan von 8 Wochen ab 1.10. (Tag 0), Zieldatum Tag 55 = 25.11.
  const ziel = (planPausen: Goal["planPausen"]): Pick<Goal, "targetDate" | "planPausen" | "trainingPlan"> => ({
    targetDate: "2026-11-25",
    planPausen,
    trainingPlan: { id: "p", generatedAt: new Date(2026, 9, 1, 12).toISOString(), items: [1, 2, 3, 4, 5, 6, 7, 8].map(item) },
  });

  it("verschiebt um die Tage, die dem Plan bis zur letzten Woche fehlen", () => {
    // 18 Pausentage: 55 - 18 = 37 aktive Tage, gebraucht 49 -> 12 Tage später.
    expect(vorschlagZieldatum(ziel([{ start: "2026-10-10", end: "2026-10-27" }]), "2026-11-04")).toBe("2026-12-07");
  });

  it("fällt ohne fehlende Tage auf zwei Wochen später zurück", () => {
    expect(vorschlagZieldatum(ziel([]), "2026-11-04")).toBe("2026-12-09");
  });

  it("fällt ohne Plan auf zwei Wochen später zurück", () => {
    expect(vorschlagZieldatum({ targetDate: "2026-11-25", planPausen: [], trainingPlan: null }, "2026-11-04")).toBe("2026-12-09");
  });
});

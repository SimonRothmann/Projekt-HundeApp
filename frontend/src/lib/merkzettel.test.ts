import { describe, expect, it } from "vitest";
import { MERKZETTEL_ZIEL, TRAININGS_QUELLE } from "@/lib/types";
import type { Goal, MerkzettelEintrag, NaechstesTraining, TrainingSession, TrainingsQuelle } from "@/lib/types";
import { uebersetze } from "@/lib/i18n";
import { katalogZeile, zeilenSchluessel } from "@/lib/eintragen";
import {
  baueMerkenNutzlast,
  fuerHeutigesTraining,
  hakeAn,
  heutigeEintraege,
  istGueltigerText,
  istGueltigesZielDatum,
  naechstesTrainingText,
  rhythmusNutzlast,
  schalteTag,
  uebungsChips,
  uhrzeitKurz,
  vorgabeZielDatum,
  zeitpunktText,
  zielText,
} from "./merkzettel";

const tDe = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const tEn = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

// Mittwoch, 4.11.2026.
const HEUTE = "2026-11-04";

const naechstes = (date: string | null, time: string | null, source: TrainingsQuelle = TRAININGS_QUELLE.Tagebuch): NaechstesTraining => ({ date, time, source });

function eintrag(teil: Partial<MerkzettelEintrag> = {}): MerkzettelEintrag {
  return {
    id: "e1",
    dogId: "d1",
    text: "Fuß rückwärts",
    exerciseId: null,
    exerciseName: null,
    exerciseNameEn: null,
    goal: MERKZETTEL_ZIEL.NaechstesTraining,
    targetDate: null,
    targetTime: null,
    doneAt: null,
    doneInTrainingId: null,
    createdAt: "2026-11-01T10:00:00Z",
    ...teil,
  };
}

describe("uhrzeitKurz", () => {
  it("kürzt die Sekunden ab", () => {
    expect(uhrzeitKurz("18:00:00")).toBe("18:00");
    expect(uhrzeitKurz("09:30")).toBe("09:30");
    expect(uhrzeitKurz(null)).toBeNull();
  });
});

describe("zeitpunktText", () => {
  it("heute und morgen als Wort, danach der Wochentag", () => {
    expect(zeitpunktText(tDe, "de", "2026-11-04", "18:00:00", HEUTE)).toBe("heute 18:00");
    expect(zeitpunktText(tDe, "de", "2026-11-05", "18:00:00", HEUTE)).toBe("morgen 18:00");
    expect(zeitpunktText(tDe, "de", "2026-11-10", "18:00:00", HEUTE)).toBe("Di 18:00");
    expect(zeitpunktText(tDe, "de", "2026-11-10", null, HEUTE)).toBe("Di");
  });

  it("ab sieben Tagen voraus mit Tag und Monat", () => {
    expect(zeitpunktText(tDe, "de", "2026-11-11", "17:30:00", HEUTE)).toBe("Mi 11.11. 17:30");
    expect(zeitpunktText(tDe, "de", "2026-11-16", null, HEUTE)).toBe("Mo 16.11.");
  });

  it("englisch: Wochentag und Datum der Sprache", () => {
    expect(zeitpunktText(tEn, "en", "2026-11-10", "18:00:00", HEUTE)).toBe("Tue 18:00");
    expect(zeitpunktText(tEn, "en", "2026-11-04", null, HEUTE)).toBe("today");
    expect(zeitpunktText(tEn, "en", "2026-11-16", "09:00:00", HEUTE)).toBe("Mon 16/11 09:00");
  });

  it("jeder Wochentag trifft sein Kürzel (Sonntag ist der siebte, nicht der nullte)", () => {
    const tage = ["2026-11-09", "2026-11-10", "2026-11-11", "2026-11-12", "2026-11-13", "2026-11-14", "2026-11-15"];
    expect(tage.map((tag) => zeitpunktText(tDe, "de", tag, null, "2026-11-08").replace(/ .*/, ""))).toEqual(["morgen", "Di", "Mi", "Do", "Fr", "Sa", "So"]);
  });
});

describe("naechstesTrainingText", () => {
  it("mit Termin: Nächstes Training · Di 18:00", () => {
    expect(naechstesTrainingText(tDe, "de", naechstes("2026-11-10", "18:00:00"), HEUTE)).toBe("Nächstes Training · Di 18:00");
    expect(naechstesTrainingText(tEn, "en", naechstes("2026-11-10", "18:00:00"), HEUTE)).toBe("Next training · Tue 18:00");
  });

  it("ohne Uhrzeit nur der Tag", () => {
    expect(naechstesTrainingText(tDe, "de", naechstes("2026-11-10", null), HEUTE)).toBe("Nächstes Training · Di");
  });

  it("unbekannt oder noch nicht geladen: ohne Zeit", () => {
    expect(naechstesTrainingText(tDe, "de", naechstes(null, null, TRAININGS_QUELLE.Unbekannt), HEUTE)).toBe("Nächstes Training");
    expect(naechstesTrainingText(tDe, "de", null, HEUTE)).toBe("Nächstes Training");
  });
});

describe("zielText", () => {
  it("mit bestimmtem Tag: der Zeitpunkt", () => {
    expect(zielText(tDe, "de", eintrag({ targetDate: "2026-11-10", targetTime: "18:00:00" }), HEUTE)).toBe("Di 18:00");
  });

  it("ohne Tag: nach Ziel", () => {
    expect(zielText(tDe, "de", eintrag(), HEUTE)).toBe("Nächstes Training");
    expect(zielText(tDe, "de", eintrag({ goal: MERKZETTEL_ZIEL.NaechsteWoche }), HEUTE)).toBe("Nächste Woche");
  });
});

describe("Eingabe", () => {
  it("Text: nicht leer, höchstens 500 Zeichen (getrimmt gezählt)", () => {
    expect(istGueltigerText("  ")).toBe(false);
    expect(istGueltigerText(" Fuß ")).toBe(true);
    expect(istGueltigerText("x".repeat(500))).toBe(true);
    expect(istGueltigerText("x".repeat(501))).toBe(false);
  });

  it("Datum: heute bis ein Jahr voraus, nichts Vergangenes", () => {
    expect(istGueltigesZielDatum("2026-11-04", HEUTE)).toBe(true);
    expect(istGueltigesZielDatum("2026-11-03", HEUTE)).toBe(false);
    expect(istGueltigesZielDatum("2027-11-04", HEUTE)).toBe(true);
    expect(istGueltigesZielDatum("2027-11-05", HEUTE)).toBe(false);
    expect(istGueltigesZielDatum("", HEUTE)).toBe(false);
    expect(istGueltigesZielDatum("morgen", HEUTE)).toBe(false);
    expect(vorgabeZielDatum(HEUTE)).toBe("2026-11-05");
  });

  it("Nutzlast: Id vom Gerät, Datum nur beim Ziel Datum", () => {
    expect(
      baueMerkenNutzlast({ id: "abc", text: " Sitz ", exerciseId: "u1", ziel: MERKZETTEL_ZIEL.NaechstesTraining, datum: "2026-11-09" }),
    ).toEqual({ id: "abc", text: "Sitz", exerciseId: "u1", goal: 0, targetDate: null });
    expect(
      baueMerkenNutzlast({ id: "abc", text: "Sitz", exerciseId: null, ziel: MERKZETTEL_ZIEL.NaechsteWoche, datum: "2026-11-09" }).targetDate,
    ).toBeNull();
    expect(
      baueMerkenNutzlast({ id: "abc", text: "Sitz", exerciseId: null, ziel: MERKZETTEL_ZIEL.Datum, datum: "2026-11-09" }),
    ).toEqual({ id: "abc", text: "Sitz", exerciseId: null, goal: 2, targetDate: "2026-11-09" });
  });
});

describe("Rhythmus", () => {
  it("Tage lassen sich ein- und ausschalten, auch bis keiner mehr gewählt ist", () => {
    let maske = 0;
    maske = schalteTag(maske, 4);
    maske = schalteTag(maske, 32);
    expect(maske).toBe(36);
    expect(schalteTag(maske, 4)).toBe(32);
    expect(schalteTag(32, 32)).toBe(0);
  });

  it("Nutzlast nur mit mindestens einem Tag", () => {
    expect(rhythmusNutzlast(0, 18)).toBeNull();
    expect(rhythmusNutzlast(36, 18)).toEqual({ days: 36, hour: 18 });
    expect(rhythmusNutzlast(4, null)).toEqual({ days: 4, hour: null });
  });
});

// ---- Übungs-Chips ----

function einheit(datum: string, uebungen: { id: string | null; name: string }[]): TrainingSession {
  return {
    id: `s-${datum}`,
    dogId: "d1",
    date: datum,
    durationMinutes: 30,
    exercises: uebungen.map((u, i) => ({ id: `${datum}-${i}`, exerciseId: u.id, exerciseName: u.name })),
  } as unknown as TrainingSession;
}

describe("uebungsChips", () => {
  it("die zuletzt geübten Katalog-Übungen, neueste zuerst, jede einmal, Freitext nicht", () => {
    const sessions = [
      einheit("2026-10-20", [{ id: "u3", name: "Voran" }, { id: "u1", name: "Sitz" }]),
      einheit("2026-10-27", [{ id: "u1", name: "Sitz" }, { id: null, name: "Eigene" }, { id: "u2", name: "Platz" }]),
    ];

    expect(uebungsChips(null, sessions).map((c) => c.exerciseId)).toEqual(["u1", "u2", "u3"]);
  });

  it("höchstens sechs", () => {
    const sessions = [einheit("2026-10-27", Array.from({ length: 10 }, (_, i) => ({ id: `u${i}`, name: `Übung ${i}` })))];

    expect(uebungsChips(null, sessions)).toHaveLength(6);
  });

  it("ohne Daten: keine", () => {
    expect(uebungsChips(null, null)).toEqual([]);
    expect(uebungsChips([] as Goal[], [])).toEqual([]);
  });
});

// ---- Training erfassen ----

describe("fuerHeutigesTraining", () => {
  it("Nächstes Training immer, Datum und Woche ab ihrem Tag, Zukünftiges wartet, Erledigtes nie", () => {
    const liste = [
      eintrag({ id: "nt" }),
      eintrag({ id: "heute", goal: MERKZETTEL_ZIEL.Datum, targetDate: "2026-11-04" }),
      eintrag({ id: "ueberfaellig", goal: MERKZETTEL_ZIEL.Datum, targetDate: "2026-11-02" }),
      eintrag({ id: "morgen", goal: MERKZETTEL_ZIEL.Datum, targetDate: "2026-11-05" }),
      eintrag({ id: "woche", goal: MERKZETTEL_ZIEL.NaechsteWoche, targetDate: "2026-11-11" }),
      eintrag({ id: "woche-da", goal: MERKZETTEL_ZIEL.NaechsteWoche, targetDate: "2026-11-04" }),
      eintrag({ id: "fertig", doneAt: "2026-11-03T10:00:00Z" }),
    ];

    expect(fuerHeutigesTraining(liste, HEUTE).map((e) => e.id)).toEqual(["nt", "heute", "ueberfaellig", "woche-da"]);
  });

  it("ohne Liste: leer", () => {
    expect(fuerHeutigesTraining(null, HEUTE)).toEqual([]);
  });
});

describe("heutigeEintraege", () => {
  it("nur offene Einträge mit Ziel heute, die früheste Uhrzeit gilt", () => {
    const liste = [
      eintrag({ id: "a", targetDate: "2026-11-04", targetTime: "18:00:00" }),
      eintrag({ id: "b", targetDate: "2026-11-04", targetTime: "17:00:00" }),
      eintrag({ id: "c", targetDate: "2026-11-04", targetTime: null }),
      eintrag({ id: "morgen", targetDate: "2026-11-05", targetTime: "09:00:00" }),
      eintrag({ id: "unbekannt", targetDate: null }),
      eintrag({ id: "fertig", targetDate: "2026-11-04", targetTime: "08:00:00", doneAt: "2026-11-04T07:00:00Z" }),
    ];

    const heute = heutigeEintraege(liste, HEUTE);

    expect(heute.eintraege.map((e) => e.id)).toEqual(["a", "b", "c"]);
    expect(heute.uhrzeit).toBe("17:00:00");
  });

  it("ohne Treffer: leer und ohne Uhrzeit", () => {
    expect(heutigeEintraege(null, HEUTE)).toEqual({ eintraege: [], uhrzeit: null });
    expect(heutigeEintraege([eintrag({ targetDate: "2026-11-05" })], HEUTE)).toEqual({ eintraege: [], uhrzeit: null });
  });
});

describe("hakeAn", () => {
  const sitz = eintrag({ id: "n1", exerciseId: "u1", exerciseName: "Sitz", exerciseNameEn: "Sit" });
  const ohneUebung = eintrag({ id: "n2" });

  it("Anhaken ergänzt die verknüpfte Übung als Zeile und merkt sich, woher sie kommt", () => {
    const ergebnis = hakeAn([], {}, sitz, true, []);

    expect(ergebnis.zeilen).toHaveLength(1);
    expect(ergebnis.zeilen[0]).toMatchObject({ exerciseId: "u1", name: "Sitz", nameEn: "Sit" });
    expect(ergebnis.ergaenzt).toEqual({ n1: ergebnis.zeilen[0].schluessel });
  });

  it("keine Dublette: Steht die Übung schon im Training, wird nichts ergänzt", () => {
    const vorhanden = katalogZeile({ id: "u1", name: "Sitz" });

    const ergebnis = hakeAn([vorhanden], {}, sitz, true, []);

    expect(ergebnis.zeilen).toEqual([vorhanden]);
    expect(ergebnis.ergaenzt).toEqual({});
  });

  it("ein Eintrag ohne Übung ergänzt nichts", () => {
    expect(hakeAn([], {}, ohneUebung, true, []).zeilen).toEqual([]);
  });

  it("Abhaken nimmt die ergänzte Zeile wieder heraus", () => {
    const an = hakeAn([], {}, sitz, true, []);

    const aus = hakeAn(an.zeilen, an.ergaenzt, sitz, false, [sitz]);

    expect(aus.zeilen).toEqual([]);
    expect(aus.ergaenzt).toEqual({});
  });

  it("Abhaken lässt eine Zeile stehen, die nicht vom Anhaken stammt", () => {
    const vorhanden = katalogZeile({ id: "u1", name: "Sitz" });
    const an = hakeAn([vorhanden], {}, sitz, true, []);

    const aus = hakeAn(an.zeilen, an.ergaenzt, sitz, false, [sitz]);

    expect(aus.zeilen).toEqual([vorhanden]);
  });

  it("Abhaken lässt die Zeile stehen, solange ein anderer angehakter Eintrag dieselbe Übung meint", () => {
    const zweiter = eintrag({ id: "n3", exerciseId: "u1", exerciseName: "Sitz" });
    const an = hakeAn([], {}, sitz, true, []);
    const beide = hakeAn(an.zeilen, an.ergaenzt, zweiter, true, [sitz]);
    expect(beide.zeilen).toHaveLength(1); // keine zweite Zeile

    const erster_aus = hakeAn(beide.zeilen, beide.ergaenzt, sitz, false, [sitz, zweiter]);
    expect(erster_aus.zeilen).toHaveLength(1);
    expect(zeilenSchluessel(erster_aus.zeilen[0])).toBe("katalog:u1");
  });

  it("Abhaken eines Eintrags, der nie ergänzt hat, ändert nichts", () => {
    const zeilen = [katalogZeile({ id: "u9", name: "Andere" })];

    expect(hakeAn(zeilen, {}, ohneUebung, false, []).zeilen).toEqual(zeilen);
  });
});

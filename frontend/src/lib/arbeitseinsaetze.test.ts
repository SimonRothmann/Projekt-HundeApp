import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import {
  abhakbar,
  auswertungAlsCsv,
  csvDateiname,
  csvFeld,
  dauerText,
  filtereKandidaten,
  gleicheGastPerson,
  helferName,
  minutenAusEingabe,
  pruefeEinsatz,
  pruefeMinuten,
  spaetesterTag,
  stundenText,
  stundenZahl,
} from "./arbeitseinsaetze";
import { TEXTLAENGE } from "./textlaengen";
import type { ArbeitseinsatzAuswertung } from "./types";

const t = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (treffer, name: string) => (werte && name in werte ? String(werte[name]) : treffer));

describe("Dauer und Stunden", () => {
  it("zeigt Minuten unter einer Stunde, volle Stunden und Stunden mit Rest", () => {
    expect(dauerText(15)).toBe("15 min");
    expect(dauerText(60)).toBe("1 h");
    expect(dauerText(120)).toBe("2 h");
    expect(dauerText(150)).toBe("2 h 30 min");
    expect(dauerText(720)).toBe("12 h");
  });

  it("rechnet Stunden auf zwei Nachkommastellen", () => {
    expect(stundenZahl(90)).toBe(1.5);
    expect(stundenZahl(20)).toBe(0.33);
    expect(stundenZahl(0)).toBe(0);
  });

  it("schreibt Stunden mit dem Dezimalzeichen der Sprache", () => {
    expect(stundenText(90, "de")).toBe("1,5 h");
    expect(stundenText(90, "en")).toBe("1.5 h");
    expect(stundenText(120, "de")).toBe("2 h");
  });
});

describe("Eingaben prüfen", () => {
  it("liest nur ganze Minuten", () => {
    expect(minutenAusEingabe(" 120 ")).toBe(120);
    expect(minutenAusEingabe("")).toBeNull();
    expect(minutenAusEingabe("1,5")).toBeNull();
    expect(minutenAusEingabe("abc")).toBeNull();
    expect(minutenAusEingabe("-5")).toBeNull();
  });

  it("verlangt 15 bis 720 Minuten", () => {
    const meldung = "Die Dauer muss zwischen 15 und 720 Minuten liegen.";
    expect(pruefeMinuten(t, 14)).toBe(meldung);
    expect(pruefeMinuten(t, 721)).toBe(meldung);
    expect(pruefeMinuten(t, null)).toBe(meldung);
    expect(pruefeMinuten(t, 15)).toBeNull();
    expect(pruefeMinuten(t, 720)).toBeNull();
  });

  it("prüft Datum, Titel und Dauer eines Einsatzes - Datum höchstens 30 Tage voraus", () => {
    const heute = "2026-10-20";
    const gut = { datum: "2026-10-20", titel: "Platzpflege", minuten: "120" };
    expect(pruefeEinsatz(t, gut, heute)).toBeNull();
    expect(pruefeEinsatz(t, { ...gut, datum: "2026-11-19" }, heute)).toBeNull();
    expect(pruefeEinsatz(t, { ...gut, datum: "2026-11-20" }, heute)).toBe("Ein Arbeitseinsatz lässt sich höchstens 30 Tage im Voraus anlegen.");
    expect(pruefeEinsatz(t, { ...gut, datum: "" }, heute)).toBe("Bitte das Datum des Einsatzes angeben.");
    expect(pruefeEinsatz(t, { ...gut, titel: "   " }, heute)).toBe("Bitte einen Titel angeben.");
    expect(pruefeEinsatz(t, { ...gut, minuten: "10" }, heute)).toContain("zwischen 15 und 720");
  });

  it("nennt den spätesten Tag und wann abgehakt werden darf", () => {
    expect(spaetesterTag("2026-10-20")).toBe("2026-11-19");
    expect(abhakbar("2026-10-20", "2026-10-20")).toBe(true);
    expect(abhakbar("2026-10-19", "2026-10-20")).toBe(true);
    expect(abhakbar("2026-10-21", "2026-10-20")).toBe(false);
  });
});

describe("Auswahl der Personen", () => {
  const liste = [
    { userId: "1", firstName: "Anna", lastName: "Müller" },
    { userId: "2", firstName: "Max", lastName: "Mustermann" },
  ];

  it("filtert nach Wörtern im Namen, ohne auf Akzente zu achten", () => {
    expect(filtereKandidaten(liste, "")).toHaveLength(2);
    expect(filtereKandidaten(liste, "muller").map((k) => k.userId)).toEqual(["1"]);
    expect(filtereKandidaten(liste, "max must").map((k) => k.userId)).toEqual(["2"]);
    expect(filtereKandidaten(liste, "xyz")).toEqual([]);
  });

  it("schreibt für Namenlose 'Ehemaliges Mitglied'", () => {
    expect(helferName(t, { firstName: "Anna", lastName: "Abel" })).toBe("Anna Abel");
    expect(helferName(t, { firstName: "", lastName: "" })).toBe("Ehemaliges Mitglied");
  });
});

describe("Personen ohne Konto", () => {
  const liste = [
    { id: "1", firstName: "Karl", lastName: "Opa" },
    { id: "2", firstName: "Erna", lastName: "" },
  ];

  it("erkennt dieselbe Person ohne Groß-/Kleinschreibung, Akzente und Leerzeichen", () => {
    expect(gleicheGastPerson(liste, "karl", "OPA")?.id).toBe("1");
    expect(gleicheGastPerson(liste, " Karl ", "O pa")?.id).toBe("1");
    expect(gleicheGastPerson(liste, "Erna", "")?.id).toBe("2");
    expect(gleicheGastPerson([{ id: "3", firstName: "José", lastName: "" }], "Jose", "")?.id).toBe("3");
  });

  it("kennt keine Treffer bei anderem oder leerem Namen", () => {
    expect(gleicheGastPerson(liste, "Karl", "")).toBeUndefined();
    expect(gleicheGastPerson(liste, "Karla", "Opa")).toBeUndefined();
    expect(gleicheGastPerson(liste, "", "")).toBeUndefined();
  });

  it("sucht auch in Personen ohne Konto nach jedem Wort", () => {
    expect(filtereKandidaten(liste, "opa karl").map((g) => g.id)).toEqual(["1"]);
  });

  it("setzt in der CSV '(ohne Konto)' hinter den Namen", () => {
    const a: ArbeitseinsatzAuswertung = {
      year: 2026, years: [2026], eventCount: 1, participationCount: 2, totalMinutes: 180,
      rows: [
        { firstName: "Karl", lastName: "Opa", eventCount: 1, minutes: 120, guest: true },
        { firstName: "Erna", lastName: "", eventCount: 1, minutes: 60, guest: true },
      ],
      former: null,
    };
    const csv = auswertungAlsCsv(t, a, "de");
    expect(csv).toContain("\r\nKarl Opa (ohne Konto);1;2\r\n");
    expect(csv).toContain("\r\nErna (ohne Konto);1;1\r\n");
  });
});

describe("CSV der Jahresauswertung", () => {
  const auswertung: ArbeitseinsatzAuswertung = {
    year: 2026,
    years: [2026],
    eventCount: 3,
    participationCount: 4,
    totalMinutes: 540,
    rows: [
      { firstName: "Anna", lastName: "Abel", eventCount: 2, minutes: 270 },
      { firstName: "Max", lastName: "Mustermann", eventCount: 1, minutes: 90 },
    ],
    former: { firstName: "", lastName: "", eventCount: 1, minutes: 180 },
  };

  it("schreibt Name; Einsätze; Stunden mit Komma für Excel, Sammelzeile und Gesamt", () => {
    expect(auswertungAlsCsv(t, auswertung, "de")).toBe(
      "﻿Name;Einsätze;Stunden\r\nAnna Abel;2;4,5\r\nMax Mustermann;1;1,5\r\nEhemalige Mitglieder;1;3\r\nGesamt;3;9\r\n",
    );
  });

  it("nimmt im Englischen den Punkt als Dezimalzeichen und lässt die Sammelzeile ohne Ehemalige weg", () => {
    const csv = auswertungAlsCsv(t, { ...auswertung, former: null }, "en");
    expect(csv).toContain("Anna Abel;2;4.5");
    expect(csv).not.toContain("Ehemalige");
  });

  it("setzt Felder mit Trenner oder Anführungszeichen in Anführungszeichen", () => {
    expect(csvFeld("Meier; Hans")).toBe('"Meier; Hans"');
    expect(csvFeld('Der "Chef"')).toBe('"Der ""Chef"""');
    expect(csvFeld("Anna")).toBe("Anna");
  });

  it("entschärft Namen, die eine Tabellenkalkulation als Formel läse", () => {
    expect(csvFeld("=HYPERLINK(1)")).toBe("'=HYPERLINK(1)");
    expect(csvFeld("+49")).toBe("'+49");
    expect(csvFeld("@sum")).toBe("'@sum");
    expect(csvFeld("-1")).toBe("'-1");
  });

  it("benennt die Datei nach dem Jahr", () => {
    expect(csvDateiname(2026)).toBe("arbeitseinsaetze-2026.csv");
  });
});

describe("Frontend und Backend meinen dieselben Längen", () => {
  const quelle = readFileSync(join(process.cwd(), "..", "backend", "src", "Dogity.Application", "Common", "Textlaengen.cs"), "utf8");
  const wert = (name: string) => {
    const treffer = new RegExp(`\\b${name}\\s*=\\s*(\\d+);`).exec(quelle);
    if (!treffer) throw new Error(`${name} steht nicht in Textlaengen.cs - umbenannt? Dann diesen Test nachziehen.`);
    return Number(treffer[1]);
  };

  it("Titel und Notiz eines Arbeitseinsatzes (Textlaengen.cs)", () => {
    expect(TEXTLAENGE.arbeitseinsatzTitel).toBe(wert("ArbeitseinsatzTitel"));
    expect(TEXTLAENGE.arbeitseinsatzNotiz).toBe(wert("ArbeitseinsatzNotiz"));
  });

  it("Vor- und Nachname einer Person ohne Konto (Textlaengen.cs)", () => {
    expect(TEXTLAENGE.arbeitseinsatzGastName).toBe(wert("ArbeitseinsatzGastName"));
  });
});

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import {
  ergebnisText,
  filtereKandidaten,
  gruppiereTeams,
  hatErgebnisse,
  hundName,
  kommendeUndVergangene,
  kurzDatum,
  naechstePruefungText,
  ordnungenText,
  pruefeGast,
  pruefeTag,
  pruefeTeamErgebnis,
  teamName,
  uhrzeitText,
  zaehleErgebnisse,
} from "./pruefungen";
import { TEXTLAENGE } from "./textlaengen";
import { PRUEFUNG_ERGEBNIS, PRUEFUNG_STATUS, TEAM_ART } from "./types";
import type { PruefungOrdnung, PruefungTeam, VereinsPruefungListe } from "./types";

const t = (text: string, werte?: Record<string, string | number>) =>
  text.replace(/\{(\w+)\}/g, (treffer, name: string) => (werte && name in werte ? String(werte[name]) : treffer));

const bh: PruefungOrdnung = { id: "bh", name: "BH", nameEn: "Companion dog", maxPoints: null };
const ibgh: PruefungOrdnung = { id: "ibgh", name: "IBGH 1", maxPoints: 300 };

function tag(datum: string, startTime: string | null = null): VereinsPruefungListe {
  return { id: datum + (startTime ?? ""), date: datum, startTime, location: "Platz", status: PRUEFUNG_STATUS.Geplant, teamCount: 0, passedCount: 0, regulations: [] };
}

function team(id: string, regulationId: string, startNumber: number, result: PruefungTeam["result"] = PRUEFUNG_ERGEBNIS.Offen): PruefungTeam {
  return {
    id, regulationId, startNumber, kind: TEAM_ART.Mitglied, handlerName: "Max Mustermann", dogName: "Bello", breed: null, birthday: null,
    info: null, reife: null, result, score: null, resultNote: null,
  };
}

describe("kommendeUndVergangene", () => {
  it("trennt am heutigen Tag: heute zählt noch als kommend", () => {
    const { kommend, vergangen } = kommendeUndVergangene([tag("2026-10-01"), tag("2026-11-01"), tag("2026-11-02")], "2026-11-01");
    expect(kommend.map((p) => p.date)).toEqual(["2026-11-01", "2026-11-02"]);
    expect(vergangen.map((p) => p.date)).toEqual(["2026-10-01"]);
  });

  it("sortiert Kommende aufsteigend nach Datum und Uhrzeit, Vergangene absteigend", () => {
    const liste = [tag("2026-12-05", "14:00:00"), tag("2026-12-05", "09:00:00"), tag("2026-11-20"), tag("2026-05-01"), tag("2026-09-01")];
    const { kommend, vergangen } = kommendeUndVergangene(liste, "2026-11-01");
    expect(kommend.map((p) => p.startTime ?? p.date)).toEqual(["2026-11-20", "09:00:00", "14:00:00"]);
    expect(vergangen.map((p) => p.date)).toEqual(["2026-09-01", "2026-05-01"]);
  });

  it("verändert die übergebene Liste nicht", () => {
    const liste = [tag("2026-12-05"), tag("2026-11-20")];
    kommendeUndVergangene(liste, "2026-11-01");
    expect(liste.map((p) => p.date)).toEqual(["2026-12-05", "2026-11-20"]);
  });
});

describe("Anzeige", () => {
  it("kürzt die Uhrzeit auf Stunde und Minute", () => {
    expect(uhrzeitText("10:00:00")).toBe("10:00");
    expect(uhrzeitText(null)).toBeNull();
  });

  it("schreibt die Ordnungen in der Sprache der Oberfläche", () => {
    expect(ordnungenText([bh, ibgh], "de")).toBe("BH, IBGH 1");
    expect(ordnungenText([bh, ibgh], "en")).toBe("Companion dog, IBGH 1");
  });

  it("schreibt Tag und Monat ohne Jahr", () => {
    expect(kurzDatum("2027-03-14", "de")).toBe("14.3.");
  });

  it("bildet die Zeile für Training und Vorstand", () => {
    expect(naechstePruefungText(t, { date: "2027-03-14", location: "Platz", regulations: [bh, ibgh] }, "de")).toBe("Nächste Prüfung: BH, IBGH 1 am 14.3.");
    expect(naechstePruefungText(t, { date: "2027-03-14", location: "Platz", regulations: [] }, "de")).toBe("Nächste Prüfung am 14.3.");
  });

  it("setzt für ein ehemaliges Mitglied einen Platzhalter statt eines Namens", () => {
    expect(teamName(t, { kind: TEAM_ART.Mitglied, handlerName: "Max Mustermann" })).toBe("Max Mustermann");
    expect(teamName(t, { kind: TEAM_ART.Gast, handlerName: "Gerd Gast" })).toBe("Gerd Gast");
    expect(teamName(t, { kind: TEAM_ART.EhemaligesMitglied, handlerName: null })).toBe("Ehemaliges Mitglied");
    expect(hundName({ dogName: null })).toBe("–");
  });

  it("schreibt das Ergebnis mit Punkten", () => {
    expect(ergebnisText(t, { result: PRUEFUNG_ERGEBNIS.Bestanden, score: 272 }, 300)).toBe("bestanden · 272 von 300 Punkten");
    expect(ergebnisText(t, { result: PRUEFUNG_ERGEBNIS.NichtBestanden, score: null }, 300)).toBe("nicht bestanden");
    expect(ergebnisText(t, { result: PRUEFUNG_ERGEBNIS.Offen, score: null }, null)).toBe("Ergebnis offen");
  });
});

describe("gruppiereTeams", () => {
  it("folgt der Reihenfolge der Ordnungen und der Starterliste, lässt leere Ordnungen aus", () => {
    const gruppen = gruppiereTeams({
      regulations: [bh, ibgh, { id: "leer", name: "X" }],
      entries: [team("a", "ibgh", 1), team("b", "bh", 2), team("c", "bh", 1)],
    });
    expect(gruppen.map((g) => g.ordnung.id)).toEqual(["bh", "ibgh"]);
    expect(gruppen[0].teams.map((e) => e.id)).toEqual(["c", "b"]);
  });
});

describe("filtereKandidaten", () => {
  const liste = [
    { userId: "1", firstName: "Max", lastName: "Müller", dogs: [{ id: "a", name: "Bello", breed: null, birthday: null }] },
    { userId: "2", firstName: "Anna", lastName: "Schmidt", dogs: [{ id: "b", name: "Emma", breed: null, birthday: null }] },
  ];

  it("findet nach Name und Hundename, ohne auf Groß- und Kleinschreibung oder Akzente zu achten", () => {
    expect(filtereKandidaten(liste, "muller").map((k) => k.userId)).toEqual(["1"]);
    expect(filtereKandidaten(liste, "EMMA").map((k) => k.userId)).toEqual(["2"]);
    expect(filtereKandidaten(liste, "max bello").map((k) => k.userId)).toEqual(["1"]);
    expect(filtereKandidaten(liste, "max emma")).toEqual([]);
  });

  it("zeigt bei leerer Suche alle", () => {
    expect(filtereKandidaten(liste, "  ")).toHaveLength(2);
  });
});

describe("Ergebnisse", () => {
  it("erkennt eingetragene Ergebnisse und zählt sie", () => {
    const teams = [
      team("a", "bh", 1, PRUEFUNG_ERGEBNIS.Bestanden),
      team("b", "bh", 2, PRUEFUNG_ERGEBNIS.NichtBestanden),
      team("c", "bh", 3, PRUEFUNG_ERGEBNIS.NichtAngetreten),
      team("d", "bh", 4),
    ];
    expect(hatErgebnisse(teams)).toBe(true);
    expect(hatErgebnisse([team("d", "bh", 4)])).toBe(false);
    expect(zaehleErgebnisse(teams)).toEqual({ bestanden: 1, nichtBestanden: 1, nichtAngetreten: 1, offen: 1 });
  });
});

describe("Eingaben prüfen", () => {
  it("verlangt Datum und Ort eines Prüfungstags", () => {
    expect(pruefeTag(t, { datum: "", ort: "Platz" })).toBe("Bitte das Datum der Prüfung angeben.");
    expect(pruefeTag(t, { datum: "2026-11-01", ort: "   " })).toBe("Bitte den Ort der Prüfung angeben.");
    expect(pruefeTag(t, { datum: "2026-11-01", ort: "Platz" })).toBeNull();
  });

  it("verlangt bei einem Gast Name und Hund", () => {
    expect(pruefeGast(t, { fuehrer: "Gerd", hund: " " })).toBe("Bitte Name und Hund des Gasts angeben.");
    expect(pruefeGast(t, { fuehrer: "", hund: "Rex" })).toBe("Bitte Name und Hund des Gasts angeben.");
    expect(pruefeGast(t, { fuehrer: "Gerd", hund: "Rex" })).toBeNull();
  });

  const jetzt = new Date(2026, 9, 20, 12).getTime();
  const eingabe = (punkte: string) => ({ punkte, notiz: "" });

  it("prüft Punkte wie beim Ergebnis eines Ziels: ganz, nicht negativ, nicht über dem Höchstwert", () => {
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.Bestanden, eingabe("272"), "2026-10-10", 300, jetzt)).toBeNull();
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.Bestanden, eingabe(""), "2026-10-10", 300, jetzt)).toBeNull();
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.Bestanden, eingabe("301"), "2026-10-10", 300, jetzt)).toBe("Die Punktzahl darf höchstens 300 betragen.");
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.NichtBestanden, eingabe("-1"), "2026-10-10", 300, jetzt)).toBe("Die Punktzahl muss eine ganze Zahl ab 0 sein.");
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.Bestanden, eingabe("12,5"), "2026-10-10", null, jetzt)).toBe("Die Punktzahl muss eine ganze Zahl ab 0 sein.");
  });

  it("lässt vor dem Prüfungstag kein Ergebnis zu - außer dem Zurücksetzen und 'nicht angetreten'", () => {
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.Bestanden, eingabe(""), "2026-12-01", null, jetzt)).toBe("Der Prüfungstag darf nicht in der Zukunft liegen.");
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.Offen, eingabe("999"), "2026-12-01", 300, jetzt)).toBeNull();
    expect(pruefeTeamErgebnis(t, PRUEFUNG_ERGEBNIS.NichtAngetreten, eingabe(""), "2026-10-10", 300, jetzt)).toBeNull();
  });
});

describe("Frontend und Backend meinen dieselben Längen", () => {
  const quelle = readFileSync(join(process.cwd(), "..", "backend", "src", "Dogity.Application", "Common", "Textlaengen.cs"), "utf8");
  const wert = (name: string) => {
    const treffer = new RegExp(`\\b${name}\\s*=\\s*(\\d+);`).exec(quelle);
    if (!treffer) throw new Error(`${name} steht nicht in Textlaengen.cs - umbenannt? Dann diesen Test nachziehen.`);
    return Number(treffer[1]);
  };

  it("Prüfungstag und Gast-Team (Textlaengen.cs)", () => {
    expect(TEXTLAENGE.pruefungsOrt).toBe(wert("PruefungsOrt"));
    expect(TEXTLAENGE.pruefungsRichter).toBe(wert("PruefungsRichter"));
    expect(TEXTLAENGE.pruefungsBemerkung).toBe(wert("PruefungsBemerkung"));
    expect(TEXTLAENGE.gastName).toBe(wert("GastName"));
    expect(TEXTLAENGE.gastInfo).toBe(wert("GastInfo"));
    expect(TEXTLAENGE.pruefungsNotiz).toBe(wert("PruefungsNotiz"));
  });
});

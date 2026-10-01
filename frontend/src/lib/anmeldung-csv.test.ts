import { describe, expect, it } from "vitest";
import {
  bildeVorschau,
  fehlendeSpalten,
  inStuecke,
  leseAnmeldungen,
  ordneSpalten,
  parseCsv,
  parseDatum,
  parseZeitstempel,
} from "./anmeldung-csv";

const HEUTE = "2026-10-01";

describe("parseCsv", () => {
  it("zerlegt einfache Zeilen", () => {
    expect(parseCsv("a,b,c\n1,2,3\n")).toStrictEqual([
      ["a", "b", "c"],
      ["1", "2", "3"],
    ]);
  });

  it("kennt Anführungszeichen mit eingebettetem Komma", () => {
    expect(parseCsv('Name,Ort\n"Müller, Anna",Karlsruhe')).toStrictEqual([
      ["Name", "Ort"],
      ["Müller, Anna", "Karlsruhe"],
    ]);
  });

  it("kennt doppelte Anführungszeichen im Feld", () => {
    expect(parseCsv('a,b\n"sie sagte ""Platz""",x')).toStrictEqual([
      ["a", "b"],
      ['sie sagte "Platz"', "x"],
    ]);
  });

  it("kennt Zeilenumbrüche im Feld, auch als Windows-Umbruch", () => {
    expect(parseCsv('a,b\r\n"erste\r\nzweite Zeile",x\r\n')).toStrictEqual([
      ["a", "b"],
      ["erste\r\nzweite Zeile", "x"],
    ]);
  });

  it("erkennt das Semikolon als Trennzeichen", () => {
    expect(parseCsv("a;b;c\n1;2,5;3")).toStrictEqual([
      ["a", "b", "c"],
      ["1", "2,5", "3"],
    ]);
  });

  it("zählt Kommas in Anführungszeichen der Kopfzeile nicht als Trennzeichen", () => {
    expect(parseCsv('"Rasse, Mischung";Name\nx;y')).toStrictEqual([
      ["Rasse, Mischung", "Name"],
      ["x", "y"],
    ]);
  });

  it("entfernt eine BOM am Anfang", () => {
    expect(parseCsv("﻿Vorname,Nachname\nAnna,Muster")[0][0]).toBe("Vorname");
  });

  it("behält leere Felder, auch am Zeilenende", () => {
    expect(parseCsv("a,b,c\n1,,\n,,3")).toStrictEqual([
      ["a", "b", "c"],
      ["1", "", ""],
      ["", "", "3"],
    ]);
  });

  it("nimmt den letzten Datensatz auch ohne Zeilenumbruch am Ende", () => {
    expect(parseCsv("a,b\n1,2")).toHaveLength(2);
  });

  it("liefert bei leerem Text nichts", () => {
    expect(parseCsv("")).toStrictEqual([]);
    expect(parseCsv("\n\n")).toStrictEqual([]);
  });

  it("kommt mit einem unvollständigen letzten Feld in Anführungszeichen zurecht", () => {
    expect(parseCsv('a,b\n1,"offen')).toStrictEqual([
      ["a", "b"],
      ["1", "offen"],
    ]);
  });
});

describe("ordneSpalten", () => {
  it("ordnet die Überschriften des Google-Formulars zu", () => {
    const z = ordneSpalten([
      "Zeitstempel",
      "Vorname*",
      "Nachname*",
      "Rufname des Hundes*",
      "Hunderasse*",
      "Wurftag*",
      "Telefonnummer*",
    ]);
    expect(z).toStrictEqual({ zeitstempel: 0, vorname: 1, nachname: 2, rufname: 3, rasse: 4, wurftag: 5, telefon: 6 });
    expect(fehlendeSpalten(z)).toStrictEqual([]);
  });

  it("ist tolerant gegen Groß-/Kleinschreibung, Leerzeichen und Reihenfolge", () => {
    const z = ordneSpalten(["  TELEFONNUMMER ", "wurftag", "hunderasse", "RUFNAME DES HUNDES", "nachname", "VORNAME"]);
    expect(fehlendeSpalten(z)).toStrictEqual([]);
    expect(z.telefon).toBe(0);
    expect(z.vorname).toBe(5);
    expect(z.zeitstempel).toBeUndefined();
  });

  it("hält eine Adressspalte nicht für die Rasse", () => {
    expect(ordneSpalten(["Strasse", "Straße", "Hunderasse"]).rasse).toBe(2);
  });

  it("meldet fehlende Pflichtspalten - der Zeitstempel ist freiwillig", () => {
    expect(fehlendeSpalten(ordneSpalten(["Vorname", "Nachname", "Rufname", "Rasse"]))).toStrictEqual(["wurftag", "telefon"]);
  });

  it("nimmt bei doppelten Überschriften die erste", () => {
    expect(ordneSpalten(["Vorname", "Vorname"]).vorname).toBe(0);
  });

  it("verwechselt 'Hunderasse' nicht mit dem Rufnamen", () => {
    const z = ordneSpalten(["Rufname des Hundes", "Hunderasse"]);
    expect(z.rufname).toBe(0);
    expect(z.rasse).toBe(1);
  });
});

describe("parseDatum", () => {
  it.each([
    ["30.09.2026", "2026-09-30"],
    ["3.9.2026", "2026-09-03"],
    ["2026-09-30", "2026-09-30"],
    ["9/30/2026", "2026-09-30"],
    ["1/5/2026", "2026-01-05"],
    ["30.09.2026 14:23:11", "2026-09-30"],
    ["2026-09-30 14:23:11", "2026-09-30"],
    ["9/30/2026 14:23:11", "2026-09-30"],
    ["  30.09.2026  ", "2026-09-30"],
  ])("liest %j als %j", (text, iso) => expect(parseDatum(text)).toBe(iso));

  it.each(["", "gestern", "31.02.2026", "13/13/2026", "30.09.", "2026/09/30", "30-09-2026"])(
    "lehnt %j ab",
    (text) => expect(parseDatum(text)).toBeNull(),
  );

  it("kennt Schaltjahre", () => {
    expect(parseDatum("29.02.2028")).toBe("2028-02-29");
    expect(parseDatum("29.02.2027")).toBeNull();
  });
});

describe("parseZeitstempel", () => {
  it("liest Datum und Uhrzeit als Ortszeit", () => {
    const iso = parseZeitstempel("30.09.2026 14:23:11");
    expect(iso).not.toBeNull();
    const d = new Date(iso!);
    expect([d.getFullYear(), d.getMonth() + 1, d.getDate(), d.getHours(), d.getMinutes(), d.getSeconds()]).toStrictEqual([
      2026, 9, 30, 14, 23, 11,
    ]);
  });

  it("liest auch das amerikanische Format und Zeiten ohne Sekunden", () => {
    const d = new Date(parseZeitstempel("9/30/2026 8:05")!);
    expect([d.getDate(), d.getMonth() + 1, d.getHours(), d.getMinutes()]).toStrictEqual([30, 9, 8, 5]);
  });

  it("nimmt ohne Uhrzeit 12 Uhr, damit der Tag nicht kippt", () => {
    expect(new Date(parseZeitstempel("30.09.2026")!).getHours()).toBe(12);
  });

  it("liefert null bei Unlesbarem oder unmöglicher Uhrzeit", () => {
    expect(parseZeitstempel("")).toBeNull();
    expect(parseZeitstempel("irgendwann")).toBeNull();
    expect(parseZeitstempel("30.09.2026 25:00:00")).toBeNull();
  });
});

const KOPF = "Zeitstempel,Vorname*,Nachname*,Rufname des Hundes*,Hunderasse*,Wurftag*,Telefonnummer*";

describe("leseAnmeldungen", () => {
  it("liest gültige Zeilen samt Zeitstempel und Zeilennummer wie in der Tabelle", () => {
    const ergebnis = leseAnmeldungen(
      `${KOPF}\n30.09.2026 14:23:11,Anna,Muster,Bella,Labrador,15.07.2026,0721 123456\n01.10.2026 09:00:00,Max,Mann,Rex,Mix,2026-06-01,+49 160 1234567\n`,
      HEUTE,
    );
    expect(ergebnis.art).toBe("ok");
    if (ergebnis.art !== "ok") return;
    expect(ergebnis.zeilen).toHaveLength(2);
    expect(ergebnis.zeilen[0]).toMatchObject({
      zeile: 2,
      vorname: "Anna",
      nachname: "Muster",
      rufname: "Bella",
      rasse: "Labrador",
      wurftag: "2026-07-15",
      telefon: "0721 123456",
      fehler: null,
    });
    expect(ergebnis.zeilen[0].zeitstempel).not.toBeNull();
    expect(ergebnis.zeilen[1]).toMatchObject({ zeile: 3, wurftag: "2026-06-01", fehler: null });
  });

  it("liest eine Semikolon-Datei mit BOM, Anführungszeichen und Umbruch im Feld", () => {
    const csv = `﻿${KOPF.replace(/,/g, ";")}\r\n30.09.2026 14:23;"Anna";"Muster";"Bella ""Bee""";"Labrador, gelb";15.07.2026;"0721 123456"\r\n`;
    const ergebnis = leseAnmeldungen(csv, HEUTE);
    expect(ergebnis.art).toBe("ok");
    if (ergebnis.art !== "ok") return;
    expect(ergebnis.zeilen[0]).toMatchObject({ rufname: 'Bella "Bee"', rasse: "Labrador, gelb", fehler: null });
  });

  it("meldet jede Zeile mit dem ersten Fehler", () => {
    const ergebnis = leseAnmeldungen(
      [
        KOPF,
        ",Anna,Muster,Bella,Labrador,,0721 123456", // Wurftag fehlt
        ",Anna,Muster,Bella,Labrador,irgendwann,0721 123456", // Wurftag unlesbar
        ",Anna,Muster,Bella,Labrador,02.10.2026,0721 123456", // Zukunft
        ",Anna,Muster,Bella,Labrador,01.01.2020,0721 123456", // zu alt
        ",Anna,Muster,Bella,Labrador,15.07.2026,abc", // Telefon
        ",,Muster,Bella,Labrador,15.07.2026,0721 123456", // Vorname
        ",Anna,,Bella,Labrador,15.07.2026,0721 123456", // Nachname
        ",Anna,Muster,,Labrador,15.07.2026,0721 123456", // Rufname
        ",Anna,Muster,Bella,,15.07.2026,0721 123456", // Rasse
        `,${"x".repeat(101)},Muster,Bella,Labrador,15.07.2026,0721 123456`, // zu lang
      ].join("\n"),
      HEUTE,
    );
    expect(ergebnis.art).toBe("ok");
    if (ergebnis.art !== "ok") return;
    expect(ergebnis.zeilen.map((z) => [z.zeile, z.fehler])).toStrictEqual([
      [2, "wurftagFehlt"],
      [3, "wurftagFormat"],
      [4, "wurftagZukunft"],
      [5, "wurftagAlt"],
      [6, "telefon"],
      [7, "vorname"],
      [8, "nachname"],
      [9, "rufname"],
      [10, "rasse"],
      [11, "zuLang"],
    ]);
  });

  it("misst das Alter des Hundes gegen den Tag des Zeitstempels", () => {
    // Angemeldet am 01.10.2025, Hund vom 01.04.2024: damals 18 Monate, heute 30 - gültig.
    const ergebnis = leseAnmeldungen(`${KOPF}\n01.10.2025 12:00:00,Anna,Muster,Opa,Mix,01.04.2024,0721 123456`, HEUTE);
    expect(ergebnis.art === "ok" && ergebnis.zeilen[0].fehler).toBeNull();
  });

  it("überspringt vollständig leere Zeilen, behält aber die Zeilennummern", () => {
    const ergebnis = leseAnmeldungen(
      `${KOPF}\n,,,,,,\n,Anna,Muster,Bella,Labrador,15.07.2026,0721 123456\n\n,,,,,,\n`,
      HEUTE,
    );
    expect(ergebnis.art === "ok" && ergebnis.zeilen.map((z) => z.zeile)).toStrictEqual([3]);
  });

  it("meldet fehlende Spalten", () => {
    expect(leseAnmeldungen("Vorname,Nachname\nAnna,Muster", HEUTE)).toStrictEqual({
      art: "spaltenFehlen",
      fehlend: ["rufname", "rasse", "wurftag", "telefon"],
    });
  });

  it("meldet leere Dateien und Dateien nur mit Kopfzeile", () => {
    expect(leseAnmeldungen("", HEUTE)).toStrictEqual({ art: "leer" });
    expect(leseAnmeldungen(KOPF, HEUTE)).toStrictEqual({ art: "leer" });
  });

  it("kommt ohne Zeitstempel-Spalte aus", () => {
    const ergebnis = leseAnmeldungen(
      "Vorname,Nachname,Rufname,Rasse,Wurftag,Telefon\nAnna,Muster,Bella,Labrador,15.07.2026,0721 123456",
      HEUTE,
    );
    expect(ergebnis.art === "ok" && ergebnis.zeilen[0]).toMatchObject({ zeitstempel: null, fehler: null });
  });
});

describe("bildeVorschau", () => {
  const zeile = (nr: number, rufname: string, telefon: string, fehler: "telefon" | null = null) => ({
    zeile: nr,
    vorname: "A",
    nachname: "B",
    rufname,
    rasse: "Mix",
    wurftag: "2026-07-01",
    telefon,
    zeitstempel: null,
    fehler,
  });

  it("trennt neue, vorhandene und fehlerhafte Zeilen", () => {
    const v = bildeVorschau(
      [
        zeile(2, "Rex", "0160 111111"),
        zeile(3, "Bella", "+49 721 123456"), // schon in der Gruppe
        zeile(4, "Luna", "abc", "telefon"),
        zeile(5, "Rex", "0160/111111"), // doppelt in der Datei
      ],
      [{ phone: "0721 123456", dogName: "bella" }],
    );
    expect(v.neu.map((z) => z.zeile)).toStrictEqual([2]);
    expect(v.vorhanden.map((z) => z.zeile)).toStrictEqual([3, 5]);
    expect(v.fehlerhaft.map((z) => z.zeile)).toStrictEqual([4]);
  });

  it("zählt Geschwister mit gleicher Nummer als verschiedene Anmeldungen", () => {
    const v = bildeVorschau([zeile(2, "Rex", "0160 111111"), zeile(3, "Luna", "0160 111111")], []);
    expect(v.neu).toHaveLength(2);
  });
});

describe("inStuecke", () => {
  it("teilt in Stücke zu höchstens 500", () => {
    const stuecke = inStuecke(Array.from({ length: 1201 }, (_, i) => i));
    expect(stuecke.map((s) => s.length)).toStrictEqual([500, 500, 201]);
  });

  it("liefert bei nichts nichts", () => {
    expect(inStuecke([])).toStrictEqual([]);
  });
});

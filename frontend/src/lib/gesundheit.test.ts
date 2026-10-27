import { describe, expect, it } from "vitest";
import {
  ARTEN,
  ART_TEXT,
  TITEL_VORSCHLAEGE,
  faelligNach,
  fristText,
  gliedere,
  hinweiseFuerHund,
  naechstes,
  plusMonate,
  pruefeEintrag,
  schnellwahlen,
  terminText,
  uhrzeitFuerServer,
  uhrzeitKurz,
  zustand,
  type GesundheitsEingabe,
} from "./gesundheit";
import { uebersetze } from "./i18n";
import {
  GESUNDHEIT_ART,
  GESUNDHEIT_ZUSTAND,
  type DogGesundheitStand,
  type DogHealthRecord,
} from "./types";

const tDe = (text: string, werte?: Record<string, string | number>) => uebersetze("de", text, werte);
const tEn = (text: string, werte?: Record<string, string | number>) => uebersetze("en", text, werte);

const HEUTE = "2026-11-04";

function eintrag(teil: Partial<DogHealthRecord> = {}): DogHealthRecord {
  return {
    id: "e",
    kind: GESUNDHEIT_ART.Impfung,
    title: "Tollwut",
    date: "2026-01-01",
    time: null,
    dueDate: "2029-01-01",
    note: null,
    isCurrent: true,
    ...teil,
  };
}

describe("zustand", () => {
  it.each([
    ["2026-11-03", GESUNDHEIT_ZUSTAND.Ueberfaellig], // gestern abgelaufen
    ["2026-11-04", GESUNDHEIT_ZUSTAND.Bald], // läuft heute ab: noch gültig, aber dringend
    ["2026-12-04", GESUNDHEIT_ZUSTAND.Bald], // 30 Tage: die Grenze gehört zu "bald"
    ["2026-12-05", GESUNDHEIT_ZUSTAND.Ok],
    ["2029-01-01", GESUNDHEIT_ZUSTAND.Ok],
  ])("%s -> %s", (faellig, erwartet) => {
    expect(zustand(faellig, HEUTE)).toBe(erwartet);
  });

  it("ohne Frist ist nichts zu beanstanden", () => {
    expect(zustand(null, HEUTE)).toBe(GESUNDHEIT_ZUSTAND.Ok);
  });

  it("rechnet über den Jahreswechsel", () => {
    expect(zustand("2027-01-20", "2026-12-31")).toBe(GESUNDHEIT_ZUSTAND.Bald);
    expect(zustand("2027-02-01", "2026-12-31")).toBe(GESUNDHEIT_ZUSTAND.Ok);
  });
});

describe("Schnellwahl", () => {
  it("Impfung: +1 Jahr und +3 Jahre; Entwurmung und Parasitenschutz: +3 Monate; sonst keine", () => {
    expect(schnellwahlen(GESUNDHEIT_ART.Impfung).map((w) => [w.jahre, w.monate])).toEqual([[1, undefined], [3, undefined]]);
    expect(schnellwahlen(GESUNDHEIT_ART.Entwurmung).map((w) => w.monate)).toEqual([3]);
    expect(schnellwahlen(GESUNDHEIT_ART.Parasitenschutz).map((w) => w.monate)).toEqual([3]);
    expect(schnellwahlen(GESUNDHEIT_ART.Tierarzttermin)).toEqual([]);
    expect(schnellwahlen(GESUNDHEIT_ART.Sonstiges)).toEqual([]);
  });

  it("rechnet ab dem Datum des Eintrags", () => {
    const [einJahr, dreiJahre] = schnellwahlen(GESUNDHEIT_ART.Impfung);
    expect(faelligNach("2026-11-04", einJahr)).toBe("2027-11-04");
    expect(faelligNach("2026-11-04", dreiJahre)).toBe("2029-11-04");
    expect(faelligNach("2026-11-04", schnellwahlen(GESUNDHEIT_ART.Entwurmung)[0])).toBe("2027-02-04");
  });

  it("der 29.2. im Schaltjahr fällt im Folgejahr auf den 28.2.", () => {
    expect(faelligNach("2028-02-29", { label: "", jahre: 1 })).toBe("2029-02-28");
  });

  it("plusMonate hält sich an das Monatsende", () => {
    expect(plusMonate("2026-11-30", 3)).toBe("2027-02-28");
    expect(plusMonate("2026-10-31", 1)).toBe("2026-11-30");
    expect(plusMonate("2026-11-15", 3)).toBe("2027-02-15");
    expect(plusMonate("2026-12-15", 1)).toBe("2027-01-15");
  });
});

describe("Uhrzeit", () => {
  it("kürzt die Sekunden und gibt sie dem Server zurück", () => {
    expect(uhrzeitKurz("10:30:00")).toBe("10:30");
    expect(uhrzeitKurz(null)).toBeNull();
    expect(uhrzeitFuerServer("10:30")).toBe("10:30:00");
    expect(uhrzeitFuerServer("")).toBeNull();
  });
});

describe("pruefeEintrag", () => {
  const gut: GesundheitsEingabe = {
    art: GESUNDHEIT_ART.Impfung,
    titel: "Tollwut",
    datum: HEUTE,
    uhrzeit: "",
    faellig: "2029-11-04",
    notiz: "",
  };

  it("lässt eine gute Eingabe durch", () => {
    expect(pruefeEintrag(tDe, gut, HEUTE)).toBeNull();
    expect(pruefeEintrag(tDe, { ...gut, faellig: "" }, HEUTE)).toBeNull();
  });

  it("verlangt einen Titel", () => {
    expect(pruefeEintrag(tDe, { ...gut, titel: "   " }, HEUTE)).toBe("Bitte einen Titel angeben.");
    expect(pruefeEintrag(tEn, { ...gut, titel: "" }, HEUTE)).toBe("Please enter a title.");
  });

  it("begrenzt Titel und Notiz auf die Längen des Servers", () => {
    expect(pruefeEintrag(tDe, { ...gut, titel: "a".repeat(101) }, HEUTE)).toContain("zu lang");
    expect(pruefeEintrag(tDe, { ...gut, titel: "a".repeat(100) }, HEUTE)).toBeNull();
    expect(pruefeEintrag(tDe, { ...gut, notiz: "n".repeat(501) }, HEUTE)).toContain("zu lang");
    expect(pruefeEintrag(tDe, { ...gut, notiz: "n".repeat(500) }, HEUTE)).toBeNull();
  });

  it("verlangt ein Datum in vernünftigen Grenzen", () => {
    expect(pruefeEintrag(tDe, { ...gut, datum: "" }, HEUTE)).toBe("Bitte ein Datum angeben.");
    expect(pruefeEintrag(tDe, { ...gut, datum: "2000-01-01" }, HEUTE)).toBe("Das Datum liegt zu weit entfernt.");
    expect(pruefeEintrag(tDe, { ...gut, datum: "2030-01-01" }, HEUTE)).toBe("Das Datum liegt zu weit entfernt.");
  });

  it("das Ablaufdatum liegt nicht vor dem Datum und nicht mehr als zehn Jahre danach", () => {
    expect(pruefeEintrag(tDe, { ...gut, faellig: "2026-11-03" }, HEUTE)).toBe("Das Ablaufdatum darf nicht vor dem Datum des Eintrags liegen.");
    expect(pruefeEintrag(tDe, { ...gut, faellig: HEUTE }, HEUTE)).toBeNull();
    expect(pruefeEintrag(tDe, { ...gut, faellig: "2036-11-05" }, HEUTE)).toBe("Das Ablaufdatum liegt zu weit entfernt.");
    expect(pruefeEintrag(tDe, { ...gut, faellig: "2036-11-04" }, HEUTE)).toBeNull();
  });

  it("beim Tierarzttermin zählt das Ablaufdatum nicht", () => {
    expect(pruefeEintrag(tDe, { ...gut, art: GESUNDHEIT_ART.Tierarzttermin, faellig: "1999-01-01", uhrzeit: "10:30" }, HEUTE)).toBeNull();
  });
});

describe("fristText", () => {
  it("Impfung: gültig bis, läuft heute ab, abgelaufen am", () => {
    expect(fristText(tDe, GESUNDHEIT_ART.Impfung, "2026-11-18", HEUTE)).toBe("gültig bis 18.11.2026");
    expect(fristText(tDe, GESUNDHEIT_ART.Impfung, HEUTE, HEUTE)).toBe("läuft heute ab");
    expect(fristText(tDe, GESUNDHEIT_ART.Impfung, "2026-10-28", HEUTE)).toBe("abgelaufen am 28.10.2026");
  });

  it("Entwurmung und Co.: nächste fällig am, heute fällig, fällig seit", () => {
    expect(fristText(tDe, GESUNDHEIT_ART.Entwurmung, "2026-11-18", HEUTE)).toBe("nächste fällig am 18.11.2026");
    expect(fristText(tDe, GESUNDHEIT_ART.Parasitenschutz, HEUTE, HEUTE)).toBe("heute fällig");
    expect(fristText(tDe, GESUNDHEIT_ART.Sonstiges, "2026-11-02", HEUTE)).toBe("fällig seit 02.11.2026");
  });

  it("gibt es auf Englisch", () => {
    expect(fristText(tEn, GESUNDHEIT_ART.Impfung, "2026-11-18", HEUTE, "en")).toBe("valid until 18/11/2026");
    expect(fristText(tEn, GESUNDHEIT_ART.Entwurmung, "2026-11-02", HEUTE, "en")).toBe("due since 02/11/2026");
  });
});

describe("terminText", () => {
  it("Datum, mit Uhrzeit wenn eingetragen", () => {
    expect(terminText(tDe, "2026-11-06", "10:30:00")).toBe("06.11.2026, 10:30 Uhr");
    expect(terminText(tDe, "2026-11-06", null)).toBe("06.11.2026");
  });
});

describe("naechstes", () => {
  it("überfällig zuerst (rot), dann bald fällig (gelb), dann der nächste Termin", () => {
    const zeilen = naechstes(
      tDe,
      [
        eintrag({ id: "tollwut", title: "Tollwut", dueDate: "2026-11-14" }),
        eintrag({ id: "wurm", kind: GESUNDHEIT_ART.Entwurmung, title: "Entwurmung", dueDate: "2026-11-02" }),
        eintrag({ id: "ok", title: "SHPPi", dueDate: "2028-01-01" }),
        eintrag({ id: "termin", kind: GESUNDHEIT_ART.Tierarzttermin, title: "Kontrolle", date: "2026-11-06", time: "10:30:00", dueDate: null }),
        eintrag({ id: "spaeter", kind: GESUNDHEIT_ART.Tierarzttermin, title: "Später", date: "2026-12-01", dueDate: null }),
      ],
      HEUTE,
    );

    expect(zeilen.map((z) => [z.id, z.ton])).toEqual([
      ["wurm", "rot"],
      ["tollwut", "gelb"],
      ["termin", "neutral"],
    ]);
    expect(zeilen[0].text).toBe("fällig seit 02.11.2026");
    expect(zeilen[2].text).toBe("06.11.2026, 10:30 Uhr");
  });

  it("alte (nicht aktuelle) Einträge und vergangene Termine zählen nicht", () => {
    const zeilen = naechstes(
      tDe,
      [
        eintrag({ id: "alt", dueDate: "2026-10-01", isCurrent: false }),
        eintrag({ id: "vorbei", kind: GESUNDHEIT_ART.Tierarzttermin, date: "2026-11-03", dueDate: null, isCurrent: false }),
      ],
      HEUTE,
    );
    expect(zeilen).toEqual([]);
  });

  it("ein Termin heute ist noch der nächste", () => {
    const zeilen = naechstes(tDe, [eintrag({ id: "t", kind: GESUNDHEIT_ART.Tierarzttermin, date: HEUTE, dueDate: null })], HEUTE);
    expect(zeilen.map((z) => z.id)).toEqual(["t"]);
  });
});

describe("gliedere", () => {
  it("Aktuell nach Art in der Reihenfolge der Auswahl, Verlauf jüngstes zuerst", () => {
    const { aktuell, verlauf } = gliedere([
      eintrag({ id: "a", kind: GESUNDHEIT_ART.Entwurmung, title: "Entwurmung" }),
      eintrag({ id: "b", title: "SHPPi", dueDate: "2028-01-01" }),
      eintrag({ id: "c", title: "Tollwut", dueDate: "2027-01-01" }),
      eintrag({ id: "d", title: "Tollwut", date: "2023-01-01", isCurrent: false }),
      eintrag({ id: "e", title: "Tollwut", date: "2024-01-01", isCurrent: false }),
    ]);

    expect(aktuell.map((g) => g.art)).toEqual([GESUNDHEIT_ART.Impfung, GESUNDHEIT_ART.Entwurmung]);
    // Früheste Frist zuerst.
    expect(aktuell[0].eintraege.map((e) => e.id)).toEqual(["c", "b"]);
    expect(verlauf.map((e) => e.id)).toEqual(["e", "d"]);
  });
});

describe("hinweiseFuerHund", () => {
  const stand = (teil: Partial<DogGesundheitStand> = {}): DogGesundheitStand => ({
    overdue: 0,
    dueSoon: 0,
    nextDue: null,
    nextAppointment: null,
    ...teil,
  });
  const bello = (gesundheit: DogGesundheitStand | null | undefined) => ({ name: "Bello", gesundheit });

  it("zeigt nichts ohne Stand oder wenn alles weit weg ist", () => {
    expect(hinweiseFuerHund(tDe, bello(null), HEUTE)).toEqual([]);
    expect(hinweiseFuerHund(tDe, bello(undefined), HEUTE)).toEqual([]);
    expect(
      hinweiseFuerHund(
        tDe,
        bello(stand({
          dueSoon: 1,
          nextDue: { id: "x", kind: GESUNDHEIT_ART.Impfung, title: "Tollwut", dueDate: "2026-11-20", state: GESUNDHEIT_ZUSTAND.Bald },
          nextAppointment: { id: "t", title: "Kontrolle", date: "2026-11-10", time: null },
        })),
        HEUTE,
      ),
    ).toEqual([]);
  });

  it("14 Tage sind die Grenze; ein Tag mehr und es bleibt still", () => {
    const mit = (dueDate: string) =>
      hinweiseFuerHund(tDe, bello(stand({ dueSoon: 1, nextDue: { id: "x", kind: GESUNDHEIT_ART.Impfung, title: "Tollwut", dueDate, state: GESUNDHEIT_ZUSTAND.Bald } })), HEUTE);
    expect(mit("2026-11-18")).toEqual([{ ton: "gelb", text: "Bello: Tollwut gültig bis 18.11." }]);
    expect(mit("2026-11-19")).toEqual([]);
  });

  it("Impfung heute und überfällig", () => {
    const mit = (dueDate: string, state: 0 | 1 | 2) =>
      hinweiseFuerHund(tDe, bello(stand({ overdue: state === 2 ? 1 : 0, dueSoon: state === 1 ? 1 : 0, nextDue: { id: "x", kind: GESUNDHEIT_ART.Impfung, title: "Tollwut", dueDate, state } })), HEUTE);
    expect(mit(HEUTE, 1)).toEqual([{ ton: "gelb", text: "Bello: Tollwut läuft heute ab" }]);
    expect(mit("2026-10-28", 2)).toEqual([{ ton: "rot", text: "Bello: Tollwut seit 28.10. abgelaufen" }]);
  });

  it("Entwurmung: fällig am / heute / seit", () => {
    const mit = (dueDate: string) =>
      hinweiseFuerHund(tDe, bello(stand({ dueSoon: 1, nextDue: { id: "x", kind: GESUNDHEIT_ART.Entwurmung, title: "Entwurmung", dueDate, state: GESUNDHEIT_ZUSTAND.Bald } })), HEUTE)[0].text;
    expect(mit("2026-11-10")).toBe("Bello: Entwurmung am 10.11. fällig");
    expect(mit(HEUTE)).toBe("Bello: Entwurmung heute fällig");
    expect(mit("2026-11-02")).toBe("Bello: Entwurmung seit 02.11. fällig");
  });

  it("nennt weitere Fristen, die Aufmerksamkeit brauchen", () => {
    const text = (n: number) =>
      hinweiseFuerHund(tDe, bello(stand({ overdue: 1, dueSoon: n, nextDue: { id: "x", kind: GESUNDHEIT_ART.Entwurmung, title: "Entwurmung", dueDate: "2026-11-02", state: GESUNDHEIT_ZUSTAND.Ueberfaellig } })), HEUTE)[0].text;
    expect(text(0)).toBe("Bello: Entwurmung seit 02.11. fällig");
    expect(text(1)).toBe("Bello: Entwurmung seit 02.11. fällig und 1 weitere");
    expect(text(2)).toBe("Bello: Entwurmung seit 02.11. fällig und 2 weitere");
  });

  it("Tierarzttermin nur in höchstens zwei Tagen: heute, morgen, übermorgen - mit Uhrzeit wenn bekannt", () => {
    const mit = (date: string, time: string | null) =>
      hinweiseFuerHund(tDe, bello(stand({ nextAppointment: { id: "t", title: "Kontrolle", date, time } })), HEUTE);
    expect(mit(HEUTE, null)).toEqual([{ ton: "neutral", text: "Bello: Tierarzttermin heute" }]);
    expect(mit("2026-11-05", "10:30:00")).toEqual([{ ton: "neutral", text: "Bello: Tierarzttermin morgen, 10:30 Uhr" }]);
    expect(mit("2026-11-06", "09:00:00")[0].text).toBe("Bello: Tierarzttermin übermorgen, 09:00 Uhr");
    expect(mit("2026-11-07", null)).toEqual([]);
    // Ein Termin von gestern im Lesecache (der Server hat inzwischen einen neuen) zeigt nichts.
    expect(mit("2026-11-03", null)).toEqual([]);
  });

  it("Frist und Termin zusammen: zwei Zeilen", () => {
    const hinweise = hinweiseFuerHund(
      tDe,
      bello(stand({
        overdue: 1,
        nextDue: { id: "x", kind: GESUNDHEIT_ART.Entwurmung, title: "Entwurmung", dueDate: "2026-11-02", state: GESUNDHEIT_ZUSTAND.Ueberfaellig },
        nextAppointment: { id: "t", title: "Kontrolle", date: "2026-11-06", time: null },
      })),
      HEUTE,
    );
    expect(hinweise.map((h) => h.ton)).toEqual(["rot", "neutral"]);
  });

  it("gibt es auf Englisch", () => {
    const hinweise = hinweiseFuerHund(
      tEn,
      bello(stand({
        dueSoon: 1,
        nextDue: { id: "x", kind: GESUNDHEIT_ART.Impfung, title: "Rabies", dueDate: "2026-11-10", state: GESUNDHEIT_ZUSTAND.Bald },
        nextAppointment: { id: "t", title: "Check-up", date: "2026-11-05", time: "10:30:00" },
      })),
      HEUTE,
      "en",
    );
    expect(hinweise.map((h) => h.text)).toEqual(["Bello: Rabies valid until 10/11", "Bello: Vet appointment tomorrow, 10:30"]);
  });
});

describe("Texte", () => {
  it("jede Art hat einen Namen und Vorschläge (Sonstiges: keine)", () => {
    expect(ARTEN).toHaveLength(5);
    for (const art of ARTEN) expect(ART_TEXT[art]).toBeTruthy();
    expect(TITEL_VORSCHLAEGE[GESUNDHEIT_ART.Impfung]).toContain("Tollwut");
    expect(TITEL_VORSCHLAEGE[GESUNDHEIT_ART.Sonstiges]).toEqual([]);
  });
});

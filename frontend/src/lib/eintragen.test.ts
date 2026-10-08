import { describe, expect, it } from "vitest";
import {
  angeboteneSportarten,
  baueNutzlast,
  DAUER_VORGABE,
  DAUER_VORSCHLAEGE,
  datumFuerTag,
  eigeneUebungAngebot,
  freitextZeile,
  inhaltsSchluessel,
  istGueltigeDauer,
  istGueltigesDatum,
  katalogZeile,
  KEIN_ORT,
  letzteEinheit,
  letzteUebungsdauer,
  loeseZeilenAuf,
  sammelWerte,
  sucheUebungen,
  tagVorher,
  trifftSuche,
  uhrzeitText,
  vorbelegteDauer,
  vorbelegterOrt,
  wochenChips,
  zeileAusPlanItem,
  zeilenAusEinheit,
  zuletztChips,
} from "./eintragen";
import type {
  Exercise,
  Goal,
  GroupTrainingSession,
  Sport,
  TrainingExercise,
  TrainingPlanItem,
  TrainingSession,
} from "@/lib/types";

function uebung(teil: Partial<TrainingExercise>): TrainingExercise {
  return {
    id: "te-1",
    exerciseId: "ex-1",
    exerciseName: "Fußarbeit",
    rating: 5,
    difficulty: 0,
    success: true,
    notes: "lief gut",
    trainingPlanItemId: "plan-1",
    trainerRating: null,
    trainerNote: null,
    ...teil,
  };
}

function einheit(uebungen: TrainingExercise[], teil: Partial<TrainingSession> = {}): TrainingSession {
  return {
    id: "s-1",
    dogId: "d-1",
    date: "2026-09-01",
    durationMinutes: 45,
    notes: "Platz war nass",
    exercises: uebungen,
    trainerFeedback: null,
    feedbackAt: null,
    startTime: null,
    latitude: null,
    longitude: null,
    locationName: null,
    temperatureC: null,
    relativeHumidity: null,
    windSpeedKmh: null,
    weatherCode: null,
    condition: null,
    hasGpsTrack: false,
    ...teil,
  };
}

// Mittwoch, 7. Oktober 2026, 16:40 Ortszeit - die Tests rechnen mit dem Kalender des Geräts.
const JETZT = new Date(2026, 9, 7, 16, 40).getTime();

function planItem(teil: Partial<TrainingPlanItem> = {}): TrainingPlanItem {
  return {
    id: "item-1",
    weekNumber: 1,
    exerciseId: "ex-1",
    exerciseName: "Fußarbeit",
    freeTextLabel: null,
    repetitionsTarget: 3,
    isRestWeek: false,
    completedCount: 1,
    isComplete: false,
    logs: [],
    reason: null,
    dayIndex: 1,
    ...teil,
  };
}

function ziel(items: TrainingPlanItem[], teil: Partial<Goal> = {}): Goal {
  return {
    id: "g-1",
    dogId: "d-1",
    sportId: "sp-1",
    sportName: "IGP",
    regulationId: null,
    regulationName: null,
    targetDate: "2026-12-01",
    status: 0,
    notes: null,
    isCustom: false,
    weeklyExerciseCount: 3,
    trainingDaysPerWeek: 3,
    weekConfigs: [],
    trainingPlan: { id: "p-1", generatedAt: new Date(2026, 9, 5, 8, 0).toISOString(), items },
    planManagedByTrainer: false,
    ...teil,
  };
}

describe("sammelWerte", () => {
  it("rechnet die drei Stufen in Sterne und Erfolg um", () => {
    expect(sammelWerte("maessig")).toEqual({ rating: 2, success: false });
    expect(sammelWerte("gut")).toEqual({ rating: 4, success: true });
    expect(sammelWerte("top")).toEqual({ rating: 5, success: true });
  });
});

describe("zeilenAusEinheit", () => {
  it("übernimmt den Übungssatz in der Reihenfolge der Einheit", () => {
    const zeilen = zeilenAusEinheit(
      einheit([uebung({}), uebung({ id: "te-2", exerciseId: "ex-2", exerciseName: "Voraus" })]),
    );

    expect(zeilen.map((z) => z.exerciseId)).toEqual(["ex-1", "ex-2"]);
    expect(zeilen.map((z) => z.name)).toEqual(["Fußarbeit", "Voraus"]);
  });

  it("übernimmt die Bewertung NICHT, sondern lässt die Zeile der Sammelbewertung folgen", () => {
    // Der wichtigste Punkt der ganzen Vorlage: die Bewertung ist der eine
    // Wert, der jedes Mal ein anderer ist, und der adaptive Plangenerator
    // erkennt an ihr die Schwächen. Mitgeschleppt wäre sie eine Falschangabe.
    const zeilen = zeilenAusEinheit(einheit([uebung({ rating: 5, success: false })]));
    expect(zeilen[0].eigeneWerte).toBeNull();

    const [aufgeloest] = loeseZeilenAuf(zeilen, "maessig", [], "2026-10-07", JETZT);
    expect(aufgeloest.rating).toBe(2);
    expect(aufgeloest.success).toBe(false);
  });

  it("übernimmt weder Notiz noch Plan-Zuordnung der alten Einheit", () => {
    const zeilen = zeilenAusEinheit(einheit([uebung({ notes: "lief gut", trainingPlanItemId: "plan-1" })]));

    expect(zeilen[0].notes).toBe("");
    expect(zeilen[0].vorgabePlan).toBeNull();
    expect(zeilen[0].planGeloest).toBe(false);
  });

  it("macht aus einer Freitext-Übung wieder eine Freitext-Zeile", () => {
    const zeilen = zeilenAusEinheit(
      einheit([uebung({ exerciseId: null, exerciseName: "Spaziergang mit Bällchenspiel" })]),
    );

    expect(zeilen[0].exerciseId).toBeNull();
    expect(zeilen[0].name).toBe("Spaziergang mit Bällchenspiel");
  });

  it("gibt Zeilen mit eigenen Kennungen zurück, auch bei gleicher Übung", () => {
    const [a, b] = zeilenAusEinheit(einheit([uebung({}), uebung({ id: "te-2" })]));
    expect(a.schluessel).not.toBe(b.schluessel);
  });

  it("liefert keine Zeilen für eine Einheit ohne Übungen", () => {
    expect(zeilenAusEinheit(einheit([]))).toEqual([]);
  });
});

describe("letzteEinheit / letzteUebungsdauer", () => {
  it("nimmt die jüngste Einheit mit Übungen, auch wenn die Liste anders sortiert ist", () => {
    const alt = einheit([uebung({})], { id: "alt", date: "2026-08-01", durationMinutes: 60 });
    const neu = einheit([uebung({})], { id: "neu", date: "2026-09-20", durationMinutes: 25 });

    expect(letzteEinheit([alt, neu])?.id).toBe("neu");
    expect(letzteUebungsdauer([alt, neu])).toBe(25);
  });

  it("überspringt Einheiten ohne Übungen (die Einheit einer gelegten Fährte)", () => {
    const fuerFaehrte = einheit([], { id: "faehrte", date: "2026-09-30", durationMinutes: 90 });
    const training = einheit([uebung({})], { id: "training", date: "2026-09-20", durationMinutes: 25 });

    expect(letzteEinheit([fuerFaehrte, training])?.id).toBe("training");
    expect(letzteUebungsdauer([fuerFaehrte, training])).toBe(25);
  });

  it("liefert null ohne Einheiten oder ohne geladene Liste", () => {
    expect(letzteUebungsdauer([])).toBeNull();
    expect(letzteUebungsdauer(null)).toBeNull();
    expect(letzteUebungsdauer(undefined)).toBeNull();
    expect(letzteUebungsdauer([einheit([])])).toBeNull();
  });
});

describe("vorbelegteDauer", () => {
  it("nimmt die Dauer der letzten Einheit", () => {
    expect(vorbelegteDauer([einheit([uebung({})], { durationMinutes: 40 })])).toBe(40);
  });

  it("fällt auf 30 Minuten zurück, wenn nichts Brauchbares vorliegt", () => {
    expect(vorbelegteDauer(null)).toBe(DAUER_VORGABE);
    expect(vorbelegteDauer(undefined)).toBe(30);
    expect(vorbelegteDauer([])).toBe(30);
    for (const kaputt of [0, -5, 7.5, 100000]) {
      expect(vorbelegteDauer([einheit([uebung({})], { durationMinutes: kaputt })])).toBe(30);
    }
  });

  it("bietet die festen Dauern zum Antippen an", () => {
    expect(DAUER_VORSCHLAEGE).toEqual([15, 30, 45, 60, 90]);
  });

  it("prüft die Dauer auf ganze Minuten", () => {
    expect(istGueltigeDauer(30)).toBe(true);
    expect(istGueltigeDauer(0)).toBe(false);
    expect(istGueltigeDauer(1.5)).toBe(false);
    expect(istGueltigeDauer(NaN)).toBe(false);
    expect(istGueltigeDauer(100000)).toBe(false);
  });
});

describe("Diese Woche", () => {
  it("zeigt die offenen Übungen der laufenden Woche mit Stand", () => {
    const chips = wochenChips(
      [
        ziel([
          planItem({ id: "a", exerciseId: "ex-1", exerciseName: "Fußarbeit", completedCount: 1 }),
          planItem({ id: "b", exerciseId: "ex-2", exerciseName: "Voraus", completedCount: 3, isComplete: true }),
          planItem({ id: "c", exerciseId: null, exerciseName: null, freeTextLabel: "Spaziergang", completedCount: 0 }),
        ]),
      ],
      JETZT,
    );

    expect(chips.map((c) => [c.name, c.erledigt, c.ziel])).toEqual([
      ["Fußarbeit", 1, 3],
      ["Spaziergang", 0, 3],
    ]);
  });

  it("fehlt ganz ohne laufendes Ziel", () => {
    expect(wochenChips([], JETZT)).toEqual([]);
    expect(wochenChips(null, JETZT)).toEqual([]);
    expect(wochenChips([ziel([planItem()], { status: 1 })], JETZT)).toEqual([]);
  });

  it("stellt das Ziel mit dem früheren Prüfungstermin nach vorn", () => {
    const spaet = ziel([planItem({ id: "spaet", exerciseName: "Spät" })], { id: "g-s", targetDate: "2026-12-20" });
    const frueh = ziel([planItem({ id: "frueh", exerciseName: "Früh" })], { id: "g-f", targetDate: "2026-11-01" });

    expect(wochenChips([spaet, frueh], JETZT).map((c) => c.name)).toEqual(["Früh", "Spät"]);
  });

  it("macht aus einer Planübung eine Zeile mit ausdrücklichem Plan-Ziel", () => {
    const katalog = zeileAusPlanItem(planItem());
    expect(katalog.exerciseId).toBe("ex-1");
    expect(katalog.vorgabePlan?.id).toBe("item-1");

    const frei = zeileAusPlanItem(planItem({ exerciseId: null, exerciseName: null, freeTextLabel: "Spaziergang" }));
    expect(frei.exerciseId).toBeNull();
    expect(frei.name).toBe("Spaziergang");
    expect(frei.vorgabePlan?.id).toBe("item-1");
  });
});

describe("Zuletzt geübt", () => {
  const einheiten = [
    einheit([uebung({ exerciseId: "ex-1", exerciseName: "Fußarbeit" }), uebung({ exerciseId: "ex-2", exerciseName: "Voraus" })], {
      id: "neu",
      date: "2026-09-20",
    }),
    einheit([uebung({ exerciseId: "ex-1", exerciseName: "Fußarbeit" }), uebung({ exerciseId: null, exerciseName: "Spaziergang" })], {
      id: "alt",
      date: "2026-09-10",
    }),
  ];

  it("zeigt verschiedene Übungen, neueste zuerst, jede nur einmal", () => {
    expect(zuletztChips(einheiten).map((c) => c.name)).toEqual(["Fußarbeit", "Voraus", "Spaziergang"]);
  });

  it("lässt aus, was schon unter Diese Woche steht", () => {
    const ausser = new Set([inhaltsSchluessel("ex-1", "Fußarbeit")]);
    expect(zuletztChips(einheiten, ausser).map((c) => c.name)).toEqual(["Voraus", "Spaziergang"]);
  });

  it("erkennt dieselbe Freitext-Übung auch bei anderer Schreibweise", () => {
    const ausser = new Set([inhaltsSchluessel(null, " spaziergang ")]);
    expect(zuletztChips(einheiten, ausser).map((c) => c.name)).not.toContain("Spaziergang");
  });

  it("zeigt höchstens sechs", () => {
    const viele = einheit(Array.from({ length: 10 }, (_, i) => uebung({ id: `t${i}`, exerciseId: `ex-${i}`, exerciseName: `Übung ${i}` })));
    expect(zuletztChips([viele])).toHaveLength(6);
  });

  it("ist leer ohne Einheiten", () => {
    expect(zuletztChips(null)).toEqual([]);
    expect(zuletztChips([einheit([])])).toEqual([]);
  });
});

describe("Übungssuche", () => {
  const sport = (id: string, name: string): Sport => ({ id, code: id, name, description: null, clubId: null });
  const ex = (id: string, name: string, category: string | null = null): Exercise => ({
    id,
    sportId: null,
    name,
    description: null,
    difficulty: 0,
    category,
    scoringCriteria: null,
    clubId: null,
  });
  const katalog = [
    { sport: sport("bh", "BH"), uebungen: [ex("1", "Freifolgen"), ex("2", "Leinenführigkeit")] },
    { sport: sport("igp", "IGP"), uebungen: [ex("3", "Fußarbeit"), ex("4", "Überspringen der Hürde", "Unterordnung")] },
  ];

  it("findet ohne Rücksicht auf Groß-/Kleinschreibung", () => {
    expect(trifftSuche("Fußarbeit", "FUßARBEIT")).toBe(true);
    expect(trifftSuche("Fußarbeit", "arbeit")).toBe(true);
  });

  it("findet Umlaute und ß mit und ohne Umschreibung", () => {
    // Auf der Handytastatur tippt man "Fuß" - oder umgeht das ß.
    expect(trifftSuche("Fußarbeit", "fuss")).toBe(true);
    expect(trifftSuche("Fußarbeit", "fuß")).toBe(true);
    expect(trifftSuche("Überspringen", "ueber")).toBe(true);
    expect(trifftSuche("Überspringen", "uber")).toBe(true);
    expect(trifftSuche("Überspringen", "Über")).toBe(true);
    expect(trifftSuche("Leinenführigkeit", "fuehr")).toBe(true);
    expect(trifftSuche("Leinenführigkeit", "fuhr")).toBe(true);
    expect(trifftSuche("Fußarbeit", "bein")).toBe(false);
  });

  it("verlangt alle Wörter der Suche, in beliebiger Reihenfolge", () => {
    expect(trifftSuche("Überspringen der Hürde", "hürde uber")).toBe(true);
    expect(trifftSuche("Überspringen der Hürde", "hürde fuss")).toBe(false);
  });

  it("filtert die Katalogübungen und lässt leere Sportarten weg", () => {
    const treffer = sucheUebungen(katalog, "fuss");
    expect(treffer.map((g) => g.sport.id)).toEqual(["igp"]);
    expect(treffer[0].uebungen.map((u) => u.name)).toEqual(["Fußarbeit"]);
  });

  it("zeigt ohne Suchtext alles, in den Gruppen der Sportarten", () => {
    expect(sucheUebungen(katalog, "  ").map((g) => g.uebungen.length)).toEqual([2, 2]);
  });

  it("sucht auch in der Kategorie", () => {
    expect(sucheUebungen(katalog, "unterordnung")[0].uebungen.map((u) => u.id)).toEqual(["4"]);
  });

  it("bietet die eigene Übung ganz oben an, wenn nichts trifft", () => {
    const text = "Spaziergang";
    expect(eigeneUebungAngebot(text, sucheUebungen(katalog, text))).toEqual({ text, oben: true });
  });

  it("hält die eigene Bezeichnung auch bei Treffern bereit - am Ende", () => {
    expect(eigeneUebungAngebot("fuß", sucheUebungen(katalog, "fuß"))).toEqual({ text: "fuß", oben: false });
  });

  it("bietet nichts an, wenn der Name genau einer Katalog-Übung entspricht oder nichts getippt ist", () => {
    expect(eigeneUebungAngebot("fussarbeit", sucheUebungen(katalog, "fussarbeit"))).toBeNull();
    expect(eigeneUebungAngebot("   ", sucheUebungen(katalog, "   "))).toBeNull();
  });

  it("wendet die Auswahl der Sportarten des Hundes an - leer heißt keine Einschränkung", () => {
    const alle = [sport("bh", "BH"), sport("igp", "IGP")];
    expect(angeboteneSportarten(alle, ["igp"]).map((s) => s.id)).toEqual(["igp"]);
    expect(angeboteneSportarten(alle, []).map((s) => s.id)).toEqual(["bh", "igp"]);
    expect(angeboteneSportarten(alle, null).map((s) => s.id)).toEqual(["bh", "igp"]);
  });
});

describe("loeseZeilenAuf", () => {
  const heute = "2026-10-07";

  it("gibt jeder Zeile die Sammelbewertung - bis sie eigene Werte hat", () => {
    const a = katalogZeile({ id: "ex-9", name: "A" });
    const b = { ...katalogZeile({ id: "ex-8", name: "B" }), eigeneWerte: { rating: 1, success: false } };

    const [ra, rb] = loeseZeilenAuf([a, b], "top", [], heute, JETZT);

    expect([ra.rating, ra.success]).toEqual([5, true]);
    // "genauer" überschreibt die Sammelbewertung nur für diese Zeile.
    expect([rb.rating, rb.success]).toEqual([1, false]);
  });

  it("ordnet eine Katalog-Übung, die diese Woche offen im Plan steht, dem Plan zu", () => {
    const [r] = loeseZeilenAuf([katalogZeile({ id: "ex-1", name: "Fußarbeit" })], "gut", [ziel([planItem()])], heute, JETZT);

    expect(r.plan?.item.id).toBe("item-1");
    expect(r.plan?.gezaehlt).toBe(true);
    expect(r.plan?.erledigt).toBe(1);
  });

  it("zählt nicht, wenn der Nutzer 'nicht zählen' gewählt hat - zeigt es aber weiter an", () => {
    const zeile = { ...katalogZeile({ id: "ex-1", name: "Fußarbeit" }), planGeloest: true };
    const [r] = loeseZeilenAuf([zeile], "gut", [ziel([planItem()])], heute, JETZT);

    expect(r.plan?.gezaehlt).toBe(false);
    const nutzlast = baueNutzlast({ dogId: "d", datum: heute, dauer: 30, notiz: "", uhrzeit: "", ort: KEIN_ORT, verfassung: null, zeilen: [r] });
    expect(nutzlast.exercises[0].trainingPlanItemId).toBeNull();
  });

  it("lässt zwei Zeilen mit derselben Übung ein nur einmal offenes Ziel nicht beide füllen", () => {
    const offen = planItem({ completedCount: 2, repetitionsTarget: 3 });
    const zeilen = [katalogZeile({ id: "ex-1", name: "Fußarbeit" }), katalogZeile({ id: "ex-1", name: "Fußarbeit" })];

    const [erste, zweite] = loeseZeilenAuf(zeilen, "gut", [ziel([offen])], heute, JETZT);

    expect(erste.plan?.item.id).toBe("item-1");
    expect(zweite.plan).toBeNull();
  });

  it("zählt für ein nachgetragenes Training nur in der Woche, in der es stattfand", () => {
    const [r] = loeseZeilenAuf([katalogZeile({ id: "ex-1", name: "Fußarbeit" })], "gut", [ziel([planItem()])], "2026-09-20", JETZT);
    expect(r.plan).toBeNull();
  });

  it("verknüpft eine ausdrücklich gewählte Planübung - auch eine Freitext-Planübung", () => {
    const item = planItem({ id: "frei", exerciseId: null, exerciseName: null, freeTextLabel: "Spaziergang" });
    const [r] = loeseZeilenAuf([zeileAusPlanItem(item)], "gut", [ziel([item])], heute, JETZT);

    expect(r.plan?.item.id).toBe("frei");
    expect(r.plan?.gezaehlt).toBe(true);
  });

  it("verknüpft eine ausdrücklich gewählte Planübung nur, wenn das Datum in ihrer Woche liegt", () => {
    // Woche 2 beginnt am 12.10. um 8 Uhr; "Gestern" (11.10.) gehört noch zu Woche 1.
    const jetzt = new Date(2026, 9, 12, 10, 0).getTime();
    const wocheEins = planItem({ id: "w1", weekNumber: 1 });
    const wocheZwei = planItem({ id: "w2", weekNumber: 2 });
    const ziele = [ziel([wocheEins, wocheZwei])];
    const zeilen = [zeileAusPlanItem(wocheZwei)];

    expect(loeseZeilenAuf(zeilen, "gut", ziele, "2026-10-12", jetzt)[0].plan?.item.id).toBe("w2");
    expect(loeseZeilenAuf(zeilen, "gut", ziele, "2026-10-11", jetzt)[0].plan).toBeNull();
  });

  it("verknüpft eine ausdrücklich gewählte Planübung nicht vor dem Planstart und nicht in der Zukunft", () => {
    const item = planItem();
    const zeilen = [zeileAusPlanItem(item)];

    // Der Plan entstand am 5.10. um 8 Uhr.
    expect(loeseZeilenAuf(zeilen, "gut", [ziel([item])], "2026-10-04", JETZT)[0].plan).toBeNull();
    expect(loeseZeilenAuf(zeilen, "gut", [ziel([item])], "2026-10-08", JETZT)[0].plan).toBeNull();
    expect(loeseZeilenAuf(zeilen, "gut", [ziel([item])], "2026-10-05", JETZT)[0].plan?.item.id).toBe("item-1");
  });

  it("behält die Wahl nur für heute, solange keine Ziele geladen sind", () => {
    const zeilen = [zeileAusPlanItem(planItem())];

    expect(loeseZeilenAuf(zeilen, "gut", null, "2026-10-07", JETZT)[0].plan?.item.id).toBe("item-1");
    expect(loeseZeilenAuf(zeilen, "gut", null, "2026-10-06", JETZT)[0].plan).toBeNull();
  });

  it("nimmt für die ausdrückliche Wahl den Stand aus den zuletzt geladenen Zielen", () => {
    const beimAntippen = planItem({ completedCount: 0 });
    const inzwischen = planItem({ completedCount: 2 });
    const [r] = loeseZeilenAuf([zeileAusPlanItem(beimAntippen)], "gut", [ziel([inzwischen])], heute, JETZT);

    expect(r.plan?.erledigt).toBe(2);
  });

  it("ordnet Freitext-Zeilen ohne ausdrückliche Wahl nie einem Plan zu", () => {
    const [r] = loeseZeilenAuf([freitextZeile("Fußarbeit")], "gut", [ziel([planItem()])], heute, JETZT);
    expect(r.plan).toBeNull();
  });
});

describe("baueNutzlast", () => {
  const heute = "2026-10-07";

  it("baut denselben Eintrag wie das frühere Formular", () => {
    const zeilen = loeseZeilenAuf(
      [
        { ...katalogZeile({ id: "ex-1", name: "Fußarbeit" }), notes: "sauber" },
        freitextZeile("  Spaziergang  "),
      ],
      "gut",
      [ziel([planItem()])],
      heute,
      JETZT,
    );

    const nutzlast = baueNutzlast({
      dogId: "d-1",
      datum: heute,
      dauer: 45,
      notiz: "Platz war nass",
      uhrzeit: "16:40",
      ort: { latitude: 48.1, longitude: 11.5, locationName: " Hundeplatz " },
      verfassung: 0,
      zeilen,
    });

    expect(nutzlast).toEqual({
      dogId: "d-1",
      date: heute,
      durationMinutes: 45,
      notes: "Platz war nass",
      startTime: "16:40:00",
      latitude: 48.1,
      longitude: 11.5,
      locationName: "Hundeplatz",
      condition: 0,
      exercises: [
        { exerciseId: "ex-1", freeTextLabel: null, rating: 4, difficulty: 0, success: true, notes: "sauber", trainingPlanItemId: "item-1" },
        { exerciseId: null, freeTextLabel: "Spaziergang", rating: 4, difficulty: 0, success: true, notes: null, trainingPlanItemId: null },
      ],
    });
  });

  it("lässt Uhrzeit, Ort, Notiz und Verfassung weg, wenn nichts eingetragen ist", () => {
    const zeilen = loeseZeilenAuf([katalogZeile({ id: "ex-9", name: "X" })], "maessig", [], heute, JETZT);
    const nutzlast = baueNutzlast({ dogId: "d", datum: heute, dauer: 30, notiz: "", uhrzeit: "", ort: KEIN_ORT, verfassung: null, zeilen });

    expect(nutzlast.notes).toBeNull();
    expect(nutzlast.startTime).toBeNull();
    expect(nutzlast.latitude).toBeNull();
    expect(nutzlast.locationName).toBeNull();
    expect(nutzlast.condition).toBeNull();
    expect(nutzlast.exercises[0]).toMatchObject({ rating: 2, success: false });
  });

  it("lässt Zeilen ohne Inhalt weg", () => {
    const zeilen = loeseZeilenAuf([freitextZeile("   "), freitextZeile("Echt")], "gut", [], heute, JETZT);
    const nutzlast = baueNutzlast({ dogId: "d", datum: heute, dauer: 30, notiz: "", uhrzeit: "", ort: KEIN_ORT, verfassung: null, zeilen });

    expect(nutzlast.exercises.map((e) => e.freeTextLabel)).toEqual(["Echt"]);
  });
});

describe("Tag und Datum", () => {
  it("rechnet Heute und Gestern nach dem Kalender des Geräts", () => {
    expect(datumFuerTag("heute", "", JETZT)).toBe("2026-10-07");
    expect(datumFuerTag("gestern", "", JETZT)).toBe("2026-10-06");
    expect(datumFuerTag("anderer", "2026-09-30", JETZT)).toBe("2026-09-30");
  });

  it("zählt über Monats- und Jahresgrenzen", () => {
    expect(tagVorher("2026-10-01", 1)).toBe("2026-09-30");
    expect(tagVorher("2026-01-01", 1)).toBe("2025-12-31");
    expect(tagVorher("2026-10-07", 2)).toBe("2026-10-05");
  });

  it("lässt keine Zukunft und kein halbes Datum zu", () => {
    expect(istGueltigesDatum("2026-10-07", JETZT)).toBe(true);
    expect(istGueltigesDatum("2026-10-08", JETZT)).toBe(false);
    expect(istGueltigesDatum("", JETZT)).toBe(false);
    expect(istGueltigesDatum("2026-10", JETZT)).toBe(false);
  });

  it("schreibt die Uhrzeit zweistellig", () => {
    expect(uhrzeitText(JETZT)).toBe("16:40");
    expect(uhrzeitText(new Date(2026, 9, 7, 6, 5).getTime())).toBe("06:05");
  });
});

describe("vorbelegterOrt", () => {
  const heute = "2026-10-07";
  const mit = (datum: string, locationName: string | null, teil: Partial<TrainingSession> = {}) =>
    einheit([uebung({})], { date: datum, locationName, latitude: 48.1, longitude: 11.5, ...teil });
  const termin = (teil: Partial<GroupTrainingSession>): GroupTrainingSession =>
    ({
      startsAt: new Date(2026, 9, 7, 18, 0).toISOString(),
      status: 0,
      location: "Vereinsplatz",
      ...teil,
    }) as GroupTrainingSession;

  it("nimmt den Ort der letzten Einheit, wenn sie höchstens 14 Tage her ist", () => {
    expect(vorbelegterOrt([mit("2026-09-23", "Hundeplatz")], [], heute, JETZT)).toEqual({
      latitude: 48.1,
      longitude: 11.5,
      locationName: "Hundeplatz",
    });
  });

  it("nimmt ihn nicht mehr, wenn sie länger her ist - ein geratener Ort samt Wetter ist schlimmer als keiner", () => {
    expect(vorbelegterOrt([mit("2026-09-22", "Hundeplatz")], [], heute, JETZT)).toBeNull();
  });

  it("nimmt nicht den Ort einer früheren Einheit, wenn die letzte keinen hat", () => {
    const juengste = mit("2026-10-05", null, { latitude: null, longitude: null });
    const aeltere = mit("2026-10-01", "Hundeplatz");

    expect(vorbelegterOrt([aeltere, juengste], [], heute, JETZT)).toBeNull();
  });

  it("nimmt den Ort, wenn der jüngste Tag mehrere Einheiten hat und eine davon einen", () => {
    expect(vorbelegterOrt([mit("2026-10-05", null), mit("2026-10-05", "Hundeplatz")], [], heute, JETZT)?.locationName).toBe("Hundeplatz");
  });

  it("nimmt den Ort eines Gruppentermins von heute - vor dem der letzten Einheit", () => {
    expect(vorbelegterOrt([mit("2026-10-05", "Hundeplatz")], [termin({})], heute, JETZT)).toEqual({
      latitude: null,
      longitude: null,
      locationName: "Vereinsplatz",
    });
  });

  it("übernimmt die Koordinaten der letzten Einheit, wenn der Termin denselben Ort meint", () => {
    expect(vorbelegterOrt([mit("2026-10-05", "vereinsplatz")], [termin({})], heute, JETZT)).toEqual({
      latitude: 48.1,
      longitude: 11.5,
      locationName: "vereinsplatz",
    });
  });

  it("beachtet Termine nur für heute, nur ohne Absage und nur mit Ort", () => {
    const morgen = termin({ startsAt: new Date(2026, 9, 8, 18, 0).toISOString() });
    const abgesagt = termin({ status: 1 });
    const ohneOrt = termin({ location: "  " });
    // Wer selbst abgesagt hat, ist dort nicht.
    const selbstAbgesagt = termin({ myResponse: false });

    expect(vorbelegterOrt(null, [morgen, abgesagt, ohneOrt, selbstAbgesagt], heute, JETZT)).toBeNull();
    // Offene und zugesagte Termine zählen.
    expect(vorbelegterOrt(null, [termin({ myResponse: null })], heute, JETZT)?.locationName).toBe("Vereinsplatz");
    expect(vorbelegterOrt(null, [termin({ myResponse: true })], heute, JETZT)?.locationName).toBe("Vereinsplatz");
    // Und nicht für einen anderen Tag, auch wenn heute ein Termin ist.
    expect(vorbelegterOrt(null, [termin({})], "2026-10-06", JETZT)).toBeNull();
  });

  it("liefert null ohne Einheiten und Termine", () => {
    expect(vorbelegterOrt(null, undefined, heute, JETZT)).toBeNull();
    expect(vorbelegterOrt([], [], heute, JETZT)).toBeNull();
  });
});

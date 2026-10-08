import { describe, expect, it } from "vitest";
import { fortschrittDerWoche, istPlanZielFehler, ohnePlanVerknuepfung, passendesPlanItem, planItemName, sichtbareWochen, trainingstageDerWoche } from "./trainingsplan";
import type { Goal, TrainingPlanItem } from "./types";

const wochen: [number, string][] = [
  [1, "a"],
  [2, "b"],
  [3, "c"],
  [4, "d"],
];

describe("sichtbareWochen", () => {
  it("zeigt nur die laufende Woche", () => {
    expect(sichtbareWochen(wochen, 3, false)).toEqual([[3, "c"]]);
  });

  it("zeigt alle Wochen, sobald man es verlangt", () => {
    expect(sichtbareWochen(wochen, 3, true)).toEqual(wochen);
  });

  it("verbirgt nichts, wenn sich die laufende Woche nicht bestimmen lässt", () => {
    // Abgeschlossener Plan: dann ist keine Woche die richtige, und eine
    // willkürlich gewählte wäre schlechter als die vollständige Liste.
    expect(sichtbareWochen(wochen, null, false)).toEqual(wochen);
    // computeCurrentWeek liefert undefined statt null, wenn es keine Wochen
    // gibt - beides muss dasselbe bedeuten.
    expect(sichtbareWochen(wochen, undefined, false)).toEqual(wochen);
  });

  it("verbirgt nichts bei zwei Wochen - das spart keinen Platz, kostet aber einen Knopf", () => {
    const kurz: [number, string][] = [
      [1, "a"],
      [2, "b"],
    ];

    expect(sichtbareWochen(kurz, 1, false)).toEqual(kurz);
  });
});

function uebung(teil: Partial<TrainingPlanItem>): TrainingPlanItem {
  return {
    id: "i",
    weekNumber: 1,
    exerciseId: "e",
    exerciseName: "Sitz",
    freeTextLabel: null,
    repetitionsTarget: 2,
    isRestWeek: false,
    completedCount: 0,
    isComplete: false,
    logs: [],
    reason: null,
    dayIndex: 1,
    ...teil,
  };
}

describe("fortschrittDerWoche", () => {
  it("zählt erledigte gegen geplante Übungen", () => {
    const items = [uebung({ isComplete: true }), uebung({}), uebung({}), uebung({ isComplete: true })];

    expect(fortschrittDerWoche(items)).toEqual({ geplant: 4, erledigt: 2 });
  });

  it("hat in einer Pausenwoche nichts zu erledigen", () => {
    const pause = [uebung({ isRestWeek: true, exerciseId: null, exerciseName: null, repetitionsTarget: 0 })];

    expect(fortschrittDerWoche(pause)).toEqual({ geplant: 0, erledigt: 0 });
  });
});

describe("planItemName", () => {
  it("nimmt die Katalog-Übung, sonst den Freitext", () => {
    expect(planItemName(uebung({}))).toBe("Sitz");
    expect(planItemName(uebung({ exerciseName: null, freeTextLabel: "Kopfarbeit" }))).toBe("Kopfarbeit");
  });

  it("liefert null, wenn die Übung nicht mehr im Katalog steht", () => {
    expect(planItemName(uebung({ exerciseName: null }))).toBeNull();
  });
});

describe("trainingstageDerWoche", () => {
  const ziel = { trainingDaysPerWeek: 3, weekConfigs: [{ weekNumber: 2, trainingDaysPerWeek: 1 }] };

  it("nimmt die Überschreibung der Woche, sonst den Plan-Standard", () => {
    expect(trainingstageDerWoche(ziel, 2)).toBe(1);
    expect(trainingstageDerWoche(ziel, 5)).toBe(3);
  });

  it("übersteht ältere Daten aus dem Lesecache ohne weekConfigs", () => {
    const alt = { trainingDaysPerWeek: 2 } as unknown as Parameters<typeof trainingstageDerWoche>[0];

    expect(trainingstageDerWoche(alt, 1)).toBe(2);
  });
});

describe("passendesPlanItem", () => {
  // Alles in Ortszeit gebaut: Die Funktion rechnet mit dem Kalendertag des
  // Geräts, der Test soll in jeder Zeitzone dasselbe prüfen.
  // Plan seit 1.10. 09:00 -> Woche 2 läuft vom 8.10. 09:00 bis 15.10. 09:00.
  const planstart = new Date(2026, 9, 1, 9, 0).toISOString();
  const jetzt = new Date(2026, 9, 9, 12, 0).getTime();
  const HEUTE = "2026-10-09";

  function ziel(teil: Partial<Goal> = {}, items: TrainingPlanItem[] = []): Goal {
    return {
      id: "g",
      status: 0,
      targetDate: "2026-12-01",
      trainingPlan: { id: "p", generatedAt: planstart, items },
      ...teil,
    } as Goal;
  }

  // Woche 1 und 3 sind andere Wochen, damit "nur die laufende" prüfbar ist.
  const sitz = uebung({ id: "sitz-2", weekNumber: 2, exerciseId: "sitz", repetitionsTarget: 3, completedCount: 1 });
  const planItems = [
    uebung({ id: "sitz-1", weekNumber: 1, exerciseId: "sitz" }),
    sitz,
    uebung({ id: "platz-2", weekNumber: 2, exerciseId: "platz" }),
    uebung({ id: "sitz-3", weekNumber: 3, exerciseId: "sitz" }),
  ];

  it("liefert das offene Ziel derselben Übung in der laufenden Woche", () => {
    expect(passendesPlanItem([ziel({}, planItems)], "sitz", HEUTE, jetzt)?.id).toBe("sitz-2");
  });

  it("liefert nichts für eine Übung, die diese Woche nicht im Plan steht", () => {
    expect(passendesPlanItem([ziel({}, planItems)], "fuss", HEUTE, jetzt)).toBeNull();
  });

  it("verknüpft keine Freitext-Übungen und keine leere Übung", () => {
    const freitext = uebung({ id: "f", weekNumber: 2, exerciseId: null, exerciseName: null, freeTextLabel: "Kopfarbeit" });

    expect(passendesPlanItem([ziel({}, [freitext])], "", HEUTE, jetzt)).toBeNull();
    expect(passendesPlanItem([ziel({}, [freitext])], null, HEUTE, jetzt)).toBeNull();
  });

  it("überspringt ein bereits erfülltes Ziel, nimmt aber ein zweites offenes derselben Übung", () => {
    const erfuellt = uebung({ id: "a", weekNumber: 2, exerciseId: "sitz", repetitionsTarget: 2, completedCount: 2, isComplete: true });
    const offen = uebung({ id: "b", weekNumber: 2, exerciseId: "sitz", repetitionsTarget: 2, completedCount: 0 });

    expect(passendesPlanItem([ziel({}, [erfuellt])], "sitz", HEUTE, jetzt)).toBeNull();
    expect(passendesPlanItem([ziel({}, [erfuellt, offen])], "sitz", HEUTE, jetzt)?.id).toBe("b");
  });

  it("ignoriert Pausen-Platzhalter", () => {
    const pause = uebung({ id: "pause", weekNumber: 2, exerciseId: "sitz", isRestWeek: true, repetitionsTarget: 0 });

    expect(passendesPlanItem([ziel({}, [pause])], "sitz", HEUTE, jetzt)).toBeNull();
  });

  it("nimmt kein erreichtes oder beendetes Ziel", () => {
    expect(passendesPlanItem([ziel({ status: 1 }, planItems)], "sitz", HEUTE, jetzt)).toBeNull();
    expect(passendesPlanItem([ziel({ status: 2 }, planItems)], "sitz", HEUTE, jetzt)).toBeNull();
  });

  it("nimmt kein abgelaufenes Ziel, wohl aber eines mit Prüfung heute", () => {
    expect(passendesPlanItem([ziel({ targetDate: "2026-10-08" }, planItems)], "sitz", HEUTE, jetzt)).toBeNull();
    expect(passendesPlanItem([ziel({ targetDate: HEUTE }, planItems)], "sitz", HEUTE, jetzt)?.id).toBe("sitz-2");
  });

  it("übersteht Ziele ohne Plan und fehlende Zielliste", () => {
    expect(passendesPlanItem([ziel({ trainingPlan: null })], "sitz", HEUTE, jetzt)).toBeNull();
    expect(passendesPlanItem(null, "sitz", HEUTE, jetzt)).toBeNull();
    expect(passendesPlanItem([], "sitz", HEUTE, jetzt)).toBeNull();
  });

  it("verknüpft ein Training von früher in derselben Woche, aber kein Nachtrag aus einer anderen", () => {
    const g = [ziel({}, planItems)];

    expect(passendesPlanItem(g, "sitz", "2026-10-08", jetzt)?.id).toBe("sitz-2");
    // Der Tag vor Beginn der Woche gehört zur ersten Woche.
    expect(passendesPlanItem(g, "sitz", "2026-10-07", jetzt)).toBeNull();
    expect(passendesPlanItem(g, "sitz", "2026-09-20", jetzt)).toBeNull();
  });

  it("verknüpft kein Datum aus der Zukunft, das in eine andere Woche fällt", () => {
    expect(passendesPlanItem([ziel({}, planItems)], "sitz", "2026-10-16", jetzt)).toBeNull();
  });

  it("verknüpft kein Datum aus der Zukunft, auch nicht in derselben Woche oder nach dem Zieldatum", () => {
    // 12.10. liegt noch in Woche 2 (bis 15.10.), ist aber noch nicht geschehen.
    expect(passendesPlanItem([ziel({}, planItems)], "sitz", "2026-10-12", jetzt)).toBeNull();
  });

  it("verknüpft kein Training von vor dem Planstart", () => {
    // Die Plan-Erstellung liegt am Vormittag des Starttags; ein Training des
    // Vortags darf nicht an der ersten Woche hängen.
    const erste = uebung({ id: "e", weekNumber: 1, exerciseId: "sitz" });
    const nachStart = new Date(2026, 9, 1, 15, 0).getTime();

    expect(passendesPlanItem([ziel({}, [erste])], "sitz", "2026-09-30", nachStart)).toBeNull();
    expect(passendesPlanItem([ziel({}, [erste])], "sitz", "2026-10-01", nachStart)?.id).toBe("e");
  });

  it("zählt ein Training von heute auch dann, wenn der Plan erst heute Nachmittag entstand", () => {
    const spaeterStart = new Date(2026, 9, 9, 15, 0).toISOString();
    const jetztAbends = new Date(2026, 9, 9, 18, 0).getTime();
    const erste = uebung({ id: "e", weekNumber: 1, exerciseId: "sitz" });

    expect(passendesPlanItem([ziel({ trainingPlan: { id: "p", generatedAt: spaeterStart, items: [erste] } })], "sitz", HEUTE, jetztAbends)?.id).toBe("e");
  });

  it("nimmt bei mehreren passenden Zielen das mit dem früheren Zieldatum", () => {
    const frueh = ziel({ id: "frueh", targetDate: "2026-11-01" }, [uebung({ id: "f", weekNumber: 2, exerciseId: "sitz" })]);
    const spaet = ziel({ id: "spaet", targetDate: "2026-12-01" }, [uebung({ id: "s", weekNumber: 2, exerciseId: "sitz" })]);

    expect(passendesPlanItem([spaet, frueh], "sitz", HEUTE, jetzt)?.id).toBe("f");
    expect(passendesPlanItem([frueh, spaet], "sitz", HEUTE, jetzt)?.id).toBe("f");
  });

  it("fällt auf das nächste Ziel zurück, wenn das frühere die Übung nicht offen hat", () => {
    const frueh = ziel({ targetDate: "2026-11-01" }, [uebung({ id: "f", weekNumber: 2, exerciseId: "platz" })]);
    const spaet = ziel({ targetDate: "2026-12-01" }, [uebung({ id: "s", weekNumber: 2, exerciseId: "sitz" })]);

    expect(passendesPlanItem([frueh, spaet], "sitz", HEUTE, jetzt)?.id).toBe("s");
  });

  it("belegt ein Ziel nicht doppelt, wenn derselben Eingabe schon Einträge dafür zugeteilt sind", () => {
    const g = [ziel({}, planItems)];

    // sitz-2 steht auf 1/3: zwei weitere sind offen, die dritte Zeile nicht mehr.
    expect(passendesPlanItem(g, "sitz", HEUTE, jetzt, new Map([["sitz-2", 1]]))?.id).toBe("sitz-2");
    expect(passendesPlanItem(g, "sitz", HEUTE, jetzt, new Map([["sitz-2", 2]]))).toBeNull();
  });

  it("weist ein ungültiges oder leeres Datum ab", () => {
    expect(passendesPlanItem([ziel({}, planItems)], "sitz", "", jetzt)).toBeNull();
    expect(passendesPlanItem([ziel({}, planItems)], "sitz", "gestern", jetzt)).toBeNull();
  });

  it("nimmt den UTC-Tag als heute, mit dem das Formular das Datum vorbelegt", () => {
    const utcTag = new Date(jetzt).toISOString().slice(0, 10);

    expect(passendesPlanItem([ziel({}, planItems)], "sitz", utcTag, jetzt)?.id).toBe("sitz-2");
  });
});

describe("Plan-Ziel nicht gefunden", () => {
  const body = {
    dogId: "d",
    exercises: [
      { exerciseId: "sitz", trainingPlanItemId: "p1" },
      { exerciseId: null, trainingPlanItemId: null },
    ],
  };

  it("erkennt die Serverantwort", () => {
    expect(istPlanZielFehler(new Error("Ein oder mehrere Plan-Ziele wurden nicht gefunden."))).toBe(true);
    expect(istPlanZielFehler(new Error("Etwas anderes"))).toBe(false);
    expect(istPlanZielFehler("Ein oder mehrere Plan-Ziele wurden nicht gefunden.")).toBe(false);
  });

  it("löst nur die Verknüpfungen und lässt den Rest unverändert", () => {
    const ohne = ohnePlanVerknuepfung(body) as typeof body;

    expect(ohne.dogId).toBe("d");
    expect(ohne.exercises.map((e) => e.trainingPlanItemId)).toEqual([null, null]);
    expect(ohne.exercises[0].exerciseId).toBe("sitz");
    // Das Original bleibt unberührt.
    expect(body.exercises[0].trainingPlanItemId).toBe("p1");
  });

  it("liefert null, wenn es nichts zu lösen gibt", () => {
    expect(ohnePlanVerknuepfung({ exercises: [{ trainingPlanItemId: null }] })).toBeNull();
    expect(ohnePlanVerknuepfung({})).toBeNull();
    expect(ohnePlanVerknuepfung(null)).toBeNull();
  });
});

import { describe, expect, it } from "vitest";
import type { OnboardingStatus } from "@/lib/types";
import { erststartZustand, GABELUNG_TAGE, pruefeErstenHund, zeigeVereinsweg } from "@/lib/erststart";

const JETZT = Date.parse("2026-09-30T12:00:00Z");
const TAG = 24 * 60 * 60 * 1000;

function stand(teil: Partial<OnboardingStatus> = {}): OnboardingStatus {
  return {
    hasDog: true,
    firstDogId: "hund-1",
    hasGoal: false,
    hasTraining: false,
    hasClubMembership: false,
    hasPendingClubRequest: false,
    hasGroupMembership: false,
    hasPendingGroupRequest: false,
    isDismissed: false,
    isComplete: false,
    firstTrainingAt: null,
    ...teil,
  };
}

const nachTraining = (vorMs: number, teil: Partial<OnboardingStatus> = {}) =>
  stand({ hasTraining: true, isComplete: true, firstTrainingAt: new Date(JETZT - vorMs).toISOString(), ...teil });

describe("erststartZustand", () => {
  it("zeigt nichts, solange der Status fehlt", () => {
    expect(erststartZustand(null, JETZT)).toBeNull();
  });

  it("fragt ohne Hund nach dem Hund", () => {
    expect(erststartZustand(stand({ hasDog: false, firstDogId: null }), JETZT)).toBe("hund");
  });

  it("lässt sich ohne Hund wegklicken", () => {
    expect(erststartZustand(stand({ hasDog: false, isDismissed: true }), JETZT)).toBeNull();
  });

  it("zeigt mit Hund, aber ohne Training die normale Startseite", () => {
    expect(erststartZustand(stand(), JETZT)).toBeNull();
  });

  it("zeigt direkt nach dem ersten Training die Gabelung", () => {
    expect(erststartZustand(nachTraining(60 * 1000), JETZT)).toBe("gabelung");
  });

  it("zeigt die Gabelung nur bei erledigtem Erststart: Das Feld des Servers entscheidet, nicht eine eigene Auslegung", () => {
    expect(erststartZustand(nachTraining(60 * 1000, { isComplete: false }), JETZT)).toBeNull();
  });

  it("zeigt die Gabelung noch am letzten Tag des Fensters, danach nicht mehr", () => {
    expect(erststartZustand(nachTraining(GABELUNG_TAGE * TAG), JETZT)).toBe("gabelung");
    expect(erststartZustand(nachTraining(GABELUNG_TAGE * TAG + 1), JETZT)).toBeNull();
  });

  it("zeigt die Gabelung nicht denen, deren erstes Training lange her ist", () => {
    expect(erststartZustand(nachTraining(90 * TAG), JETZT)).toBeNull();
  });

  it("verträgt eine Uhr, die dem Server etwas vorgeht", () => {
    expect(erststartZustand(nachTraining(-5 * 60 * 1000), JETZT)).toBe("gabelung");
  });

  it("zeigt ohne bekannten Zeitpunkt (alter Zwischenstand) keine Gabelung", () => {
    expect(erststartZustand(nachTraining(60 * 1000, { firstTrainingAt: undefined }), JETZT)).toBeNull();
    expect(erststartZustand(nachTraining(60 * 1000, { firstTrainingAt: "kein Datum" }), JETZT)).toBeNull();
  });

  it("lässt die Gabelung weg, wenn schon ein Weg gegangen ist", () => {
    expect(erststartZustand(nachTraining(60 * 1000, { hasGoal: true }), JETZT)).toBeNull();
    expect(erststartZustand(nachTraining(60 * 1000, { hasClubMembership: true }), JETZT)).toBeNull();
    expect(erststartZustand(nachTraining(60 * 1000, { hasPendingClubRequest: true }), JETZT)).toBeNull();
    expect(erststartZustand(nachTraining(60 * 1000, { hasGroupMembership: true }), JETZT)).toBeNull();
    expect(erststartZustand(nachTraining(60 * 1000, { hasPendingGroupRequest: true }), JETZT)).toBeNull();
  });

  it("lässt sich auch nach dem Training wegklicken", () => {
    expect(erststartZustand(nachTraining(60 * 1000, { isDismissed: true }), JETZT)).toBeNull();
  });
});

describe("pruefeErstenHund", () => {
  it("verlangt einen Namen, auch aus Leerzeichen", () => {
    expect(pruefeErstenHund("", 0)).toEqual({ ok: false, fehler: "name" });
    expect(pruefeErstenHund("   ", 1)).toEqual({ ok: false, fehler: "name" });
  });

  it("verlangt eine Auswahl des Geschlechts - keine stille Vorbelegung", () => {
    expect(pruefeErstenHund("Bella", null)).toEqual({ ok: false, fehler: "geschlecht" });
  });

  it("nennt zuerst den fehlenden Namen", () => {
    expect(pruefeErstenHund("", null)).toEqual({ ok: false, fehler: "name" });
  });

  it("nimmt den bereinigten Namen und das gewählte Geschlecht (auch Rüde = 0)", () => {
    expect(pruefeErstenHund("  Bella ", 1)).toEqual({ ok: true, name: "Bella", geschlecht: 1 });
    expect(pruefeErstenHund("Rex", 0)).toEqual({ ok: true, name: "Rex", geschlecht: 0 });
  });
});

describe("zeigeVereinsweg", () => {
  it("bietet den Weg über den Verein an, solange nichts mit einem Verein läuft", () => {
    expect(zeigeVereinsweg(stand({ hasDog: false }))).toBe(true);
  });

  it("bietet ihn an, wenn der Status unbekannt ist", () => {
    expect(zeigeVereinsweg(null)).toBe(true);
  });

  it("bietet ihn nicht an, wenn die Person schon im Verein ist (auch ohne Hund)", () => {
    expect(zeigeVereinsweg(stand({ hasDog: false, hasClubMembership: true }))).toBe(false);
  });

  it("bietet ihn nicht an, wenn die Beitrittsanfrage schon gestellt ist", () => {
    expect(zeigeVereinsweg(stand({ hasDog: false, hasPendingClubRequest: true }))).toBe(false);
  });
});

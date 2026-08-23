import { describe, expect, it } from "vitest";
import { dogAgeInMonths, formatDogAge } from "./dog-age";

const heute = new Date(2026, 7, 20); // 20.08.2026

describe("dogAgeInMonths", () => {
  it("zählt nur volle Monate", () => {
    expect(dogAgeInMonths("2026-07-20", heute)).toBe(1);
    // Einen Tag vor dem Monatstag ist der Monat noch nicht voll.
    expect(dogAgeInMonths("2026-07-21", heute)).toBe(0);
    expect(dogAgeInMonths("2025-08-20", heute)).toBe(12);
  });

  it("liefert null ohne Datum und bei Datum in der Zukunft", () => {
    expect(dogAgeInMonths(null, heute)).toBeNull();
    expect(dogAgeInMonths("", heute)).toBeNull();
    expect(dogAgeInMonths("2026-09-01", heute)).toBeNull();
  });

  it("kommt mit dem Zeitstempel-Format des Backends zurecht", () => {
    expect(dogAgeInMonths("2024-08-20T00:00:00Z", heute)).toBe(24);
  });
});

describe("formatDogAge", () => {
  it("zeigt Welpen in Monaten", () => {
    expect(formatDogAge("2026-01-20", heute)).toBe("7 Monate");
    expect(formatDogAge("2026-07-20", heute)).toBe("1 Monat");
  });

  it("zeigt im zweiten Lebensjahr Jahr und Monat", () => {
    // Genau der Bereich, in dem die Zulassungsgrenzen liegen (15-20 Monate).
    expect(formatDogAge("2025-03-20", heute)).toBe("1 Jahr 5 Monate");
    expect(formatDogAge("2025-08-20", heute)).toBe("1 Jahr");
    expect(formatDogAge("2025-07-20", heute)).toBe("1 Jahr 1 Monat");
  });

  it("zeigt ab zwei Jahren nur noch Jahre", () => {
    expect(formatDogAge("2024-08-20", heute)).toBe("2 Jahre");
    expect(formatDogAge("2020-02-29", heute)).toBe("6 Jahre");
  });

  it("liefert null ohne Geburtsdatum", () => {
    expect(formatDogAge(null, heute)).toBeNull();
  });
});

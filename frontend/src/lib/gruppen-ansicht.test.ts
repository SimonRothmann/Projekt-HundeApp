import { describe, expect, it } from "vitest";
import { ansichtAusParameter, waehleAnsicht } from "./gruppen-ansicht";

describe("ansichtAusParameter", () => {
  it("kennt genau die zwei Ansichten", () => {
    expect(ansichtAusParameter("mitglieder")).toBe("mitglieder");
    expect(ansichtAusParameter("anmeldungen")).toBe("anmeldungen");
  });

  it("macht aus allem anderen 'keine Angabe'", () => {
    for (const wert of [null, undefined, "", "Mitglieder", "anmeldung", " anmeldungen", "alle", "0"]) {
      expect(ansichtAusParameter(wert)).toBeNull();
    }
  });
});

describe("waehleAnsicht", () => {
  it("folgt einem gültigen Parameter, wenn die Anmeldungen da sind", () => {
    expect(waehleAnsicht("anmeldungen", { anmeldungen: 3, mitglieder: 5 })).toBe("anmeldungen");
    expect(waehleAnsicht("mitglieder", { anmeldungen: 3, mitglieder: 0 })).toBe("mitglieder");
  });

  it("lässt den Parameter auch bei null Anmeldungen gelten (leerer Zustand mit Aktionen)", () => {
    expect(waehleAnsicht("anmeldungen", { anmeldungen: 0, mitglieder: 5 })).toBe("anmeldungen");
  });

  it("zeigt ohne Zugriff immer die Mitglieder, auch wenn der Link etwas anderes will", () => {
    expect(waehleAnsicht("anmeldungen", { anmeldungen: null, mitglieder: 0 })).toBe("mitglieder");
    expect(waehleAnsicht(null, { anmeldungen: null, mitglieder: 4 })).toBe("mitglieder");
  });

  it("beginnt ohne Parameter bei den Anmeldungen, wenn es nur solche gibt", () => {
    expect(waehleAnsicht(null, { anmeldungen: 7, mitglieder: 0 })).toBe("anmeldungen");
  });

  it("beginnt ohne Parameter bei den Anmeldungen, sobald es welche gibt - auch neben Mitgliedern", () => {
    // Bewusst geändert: Früher gewannen die Mitglieder, sobald es welche gab. Wer eine Gruppe
    // mit Anmeldungen verwaltet, kommt aber zum Abhaken der Anwesenheit (Karte "Nächster
    // Termin" führt hierher); die Mitglieder zeigt der Umschalter mit einem Tipp.
    expect(waehleAnsicht(null, { anmeldungen: 7, mitglieder: 2 })).toBe("anmeldungen");
    expect(waehleAnsicht(undefined, { anmeldungen: 1, mitglieder: 40 })).toBe("anmeldungen");
  });

  it("beginnt ohne Parameter sonst bei den Mitgliedern", () => {
    expect(waehleAnsicht(undefined, { anmeldungen: 0, mitglieder: 0 })).toBe("mitglieder");
    expect(waehleAnsicht(null, { anmeldungen: 0, mitglieder: 3 })).toBe("mitglieder");
  });

  it("behandelt ungültige Werte wie keinen Parameter", () => {
    expect(waehleAnsicht("quatsch", { anmeldungen: 7, mitglieder: 0 })).toBe("anmeldungen");
    expect(waehleAnsicht("", { anmeldungen: 0, mitglieder: 2 })).toBe("mitglieder");
    expect(waehleAnsicht("ANMELDUNGEN", { anmeldungen: 0, mitglieder: 0 })).toBe("mitglieder");
  });
});

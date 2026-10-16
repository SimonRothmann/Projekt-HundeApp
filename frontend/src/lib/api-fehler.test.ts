import { afterEach, describe, expect, it } from "vitest";
import { ApiError, fehlerAusAntwort } from "@/lib/api";
import { setzeAktuelleSprache } from "@/lib/i18n/aktuelle-sprache";
import { istPlanZielFehler } from "@/lib/trainingsplan";

describe("Fehlerantworten lesen", () => {
  it("nimmt die Liste der App, wie sie ist", () => {
    expect(fehlerAusAntwort({ errors: ["Hund nicht gefunden."] }, 404)).toEqual(["Hund nicht gefunden."]);
  });

  it("flacht ProblemDetails von ASP.NET ab - dort ist errors ein Objekt", () => {
    const body = {
      title: "One or more validation errors occurred.",
      errors: { "$.durationMinutes": ["Ungültiger Wert."], notes: ["Zu lang.", "Leer."] },
    };
    expect(fehlerAusAntwort(body, 400)).toEqual(["Ungültiger Wert.", "Zu lang.", "Leer."]);
  });

  it("fällt auf den Titel zurück, wenn es keine Meldungen gibt", () => {
    expect(fehlerAusAntwort({ title: "Not Found", errors: {} }, 404)).toEqual(["Not Found"]);
  });

  it("nennt ohne jeden Hinweis wenigstens den Status", () => {
    expect(fehlerAusAntwort(null, 500)).toEqual(["HTTP 500"]);
    expect(fehlerAusAntwort({ errors: [42, null] }, 400)).toEqual(["HTTP 400"]);
  });

  it("ApiError übersteht einen Rumpf, der keine Liste ist - vorher ein TypeError", () => {
    const fehler = new ApiError(400, { feld: ["x"] } as unknown as string[]);
    expect(fehler).toBeInstanceOf(ApiError);
    expect(fehler.errors).toEqual([]);
    expect(fehler.message).toBe("Ein Fehler ist aufgetreten.");
  });
});

describe("ApiError in der Oberflächensprache", () => {
  afterEach(() => setzeAktuelleSprache("de"));

  it("lässt die Meldung auf Deutsch, wie der Server sie schickt", () => {
    expect(new ApiError(404, ["Hund nicht gefunden."]).message).toBe("Hund nicht gefunden.");
  });

  it("übersetzt message für die Anzeige, errors behält die Originaltexte", () => {
    setzeAktuelleSprache("en");
    const fehler = new ApiError(404, ["Hund nicht gefunden.", "Ziel nicht gefunden."]);
    expect(fehler.message).toBe("Dog not found., Goal not found.");
    expect(fehler.errors).toEqual(["Hund nicht gefunden.", "Ziel nicht gefunden."]);
  });

  it("lässt unbekannte Meldungen deutsch stehen", () => {
    setzeAktuelleSprache("en");
    expect(new ApiError(400, ["Das kennt das Wörterbuch nicht."]).message).toBe("Das kennt das Wörterbuch nicht.");
  });

  it("übersetzt auch den Rückfalltext", () => {
    setzeAktuelleSprache("en");
    expect(new ApiError(500, []).message).toBe("An error occurred.");
  });

  it("erkennt den Plan-Ziel-Fehler auch dann, wenn message übersetzt ist", () => {
    setzeAktuelleSprache("en");
    const fehler = new ApiError(400, ["Ein oder mehrere Plan-Ziele wurden nicht gefunden."]);
    expect(fehler.message).toBe("One or more plan goals were not found.");
    expect(istPlanZielFehler(fehler)).toBe(true);
  });
});

import { describe, expect, it } from "vitest";
import { ApiError, fehlerAusAntwort } from "@/lib/api";

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

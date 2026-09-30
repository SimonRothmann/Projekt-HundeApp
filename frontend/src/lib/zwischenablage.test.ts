import { afterEach, describe, expect, it, vi } from "vitest";
import { kopiereText, teileLink } from "./zwischenablage";

afterEach(() => {
  vi.unstubAllGlobals();
});

function feldAttrappe() {
  return {
    value: "",
    style: {} as Record<string, string>,
    setAttribute: vi.fn(),
    select: vi.fn(),
    setSelectionRange: vi.fn(),
  };
}

describe("kopiereText", () => {
  it("nimmt navigator.clipboard, wenn es da ist", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal("navigator", { clipboard: { writeText } });

    expect(await kopiereText("https://dogity.net/v/x")).toBe(true);
    expect(writeText).toHaveBeenCalledWith("https://dogity.net/v/x");
  });

  it("weicht aufs Kopieren über ein verstecktes Feld aus, wenn clipboard fehlt", async () => {
    const feld = feldAttrappe();
    const execCommand = vi.fn().mockReturnValue(true);
    vi.stubGlobal("navigator", {});
    vi.stubGlobal("document", {
      createElement: () => feld,
      body: { appendChild: vi.fn(), removeChild: vi.fn() },
      execCommand,
    });

    expect(await kopiereText("abc")).toBe(true);
    expect(feld.value).toBe("abc");
    expect(execCommand).toHaveBeenCalledWith("copy");
  });

  it("weicht auch aus, wenn clipboard verweigert", async () => {
    const feld = feldAttrappe();
    vi.stubGlobal("navigator", { clipboard: { writeText: vi.fn().mockRejectedValue(new Error("verweigert")) } });
    vi.stubGlobal("document", {
      createElement: () => feld,
      body: { appendChild: vi.fn(), removeChild: vi.fn() },
      execCommand: vi.fn().mockReturnValue(true),
    });

    expect(await kopiereText("abc")).toBe(true);
  });

  it("meldet den Misserfolg, statt einen Erfolg zu behaupten", async () => {
    vi.stubGlobal("navigator", {});
    vi.stubGlobal("document", {
      createElement: () => feldAttrappe(),
      body: { appendChild: vi.fn(), removeChild: vi.fn() },
      execCommand: vi.fn().mockReturnValue(false),
    });

    expect(await kopiereText("abc")).toBe(false);
  });
});

describe("teileLink", () => {
  const daten = { title: "t", text: "x", url: "https://dogity.net/v/x" };

  it("meldet, dass es nicht verfügbar ist, wenn der Browser nicht teilen kann", async () => {
    vi.stubGlobal("navigator", {});
    expect(await teileLink(daten)).toBe("nichtVerfuegbar");
  });

  it("teilt, wenn das Teilen-Menü da ist", async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal("navigator", { share });
    expect(await teileLink(daten)).toBe("geteilt");
    expect(share).toHaveBeenCalledWith(daten);
  });

  it("behandelt Schließen des Menüs als Abbruch, nicht als Fehler", async () => {
    vi.stubGlobal("navigator", { share: vi.fn().mockRejectedValue(new DOMException("zu", "AbortError")) });
    expect(await teileLink(daten)).toBe("abgebrochen");
  });

  it("fällt bei einem anderen Fehler auf 'nicht verfügbar' zurück, damit der Aufrufer kopieren kann", async () => {
    vi.stubGlobal("navigator", { share: vi.fn().mockRejectedValue(new DOMException("nein", "NotAllowedError")) });
    expect(await teileLink(daten)).toBe("nichtVerfuegbar");
  });
});

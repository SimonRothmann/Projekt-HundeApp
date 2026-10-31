import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, api, REFRESH_KEY, TOKEN_KEY, USER_KEY } from "@/lib/api";

// Die Token-Erneuerung hält die Sitzung am Leben: ein 401 mit Token heißt "Access-Token
// abgelaufen", einmal erneuern und wiederholen. Nur ein ausdrückliches "kenne ich nicht"
// beendet die Sitzung - Netzfehler und Drosselung nicht. Und laufen mehrere Aufrufe zugleich in
// den 401, darf nur EINE Erneuerung passieren (sonst löst die Wiederverwendungserkennung des
// Servers aus und wirft die Person hinaus).

let werte: Map<string, string>;
let ort: { protocol: string; hostname: string; pathname: string; href: string };
let aufrufe: { url: string; token: string | null }[];

type Antwort = (url: string, token: string | null) => Promise<Response> | Response;

const json = (status: number, rumpf: unknown) => new Response(JSON.stringify(rumpf), { status });
const istErneuerung = (url: string) => url.endsWith("/api/auth/refresh");
const erneuerungen = () => aufrufe.filter((a) => istErneuerung(a.url)).length;

function server(antwort: Antwort) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url: string, init?: RequestInit) => {
      const kopf = (init?.headers ?? {}) as Record<string, string>;
      const token = kopf.Authorization?.replace("Bearer ", "") ?? null;
      aufrufe.push({ url, token });
      return antwort(url, token);
    }),
  );
}

beforeEach(() => {
  werte = new Map([
    [TOKEN_KEY, "alt"],
    [REFRESH_KEY, "refresh-alt"],
    [USER_KEY, "{}"],
  ]);
  aufrufe = [];
  ort = { protocol: "http:", hostname: "localhost", pathname: "/dashboard", href: "http://localhost/dashboard" };
  vi.stubGlobal("window", {
    localStorage: {
      getItem: (k: string) => werte.get(k) ?? null,
      setItem: (k: string, v: string) => werte.set(k, v),
      removeItem: (k: string) => werte.delete(k),
    },
    location: ort,
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** Ein Server, der nur den Token "neu" annimmt und bei der Erneuerung zwei frische Token ausgibt. */
const erneuernderServer: Antwort = (url, token) => {
  if (istErneuerung(url)) return json(200, { token: "neu", refreshToken: "refresh-neu" });
  return token === "neu" ? json(200, { wert: 42 }) : json(401, { errors: ["Nicht angemeldet."] });
};

describe("Token-Erneuerung: Erfolg", () => {
  it("erneuert nach einem 401 einmal, speichert die neuen Token und wiederholt den Aufruf", async () => {
    server(erneuernderServer);

    await expect(api.get<{ wert: number }>("/api/dogs")).resolves.toEqual({ wert: 42 });

    expect(werte.get(TOKEN_KEY)).toBe("neu");
    expect(werte.get(REFRESH_KEY)).toBe("refresh-neu");
    expect(aufrufe.map((a) => a.token)).toEqual(["alt", null, "neu"]);
    expect(ort.href).toBe("http://localhost/dashboard");
  });

  it("Single-Flight: parallele 401 lösen nur eine Erneuerung aus, alle Aufrufe gelingen", async () => {
    let freigeben!: () => void;
    const warten = new Promise<void>((weiter) => (freigeben = weiter));
    server(async (url, token) => {
      if (istErneuerung(url)) {
        await warten; // die Erneuerung dauert, während die anderen Aufrufe schon im 401 stehen
        return json(200, { token: "neu", refreshToken: "refresh-neu" });
      }
      return token === "neu" ? json(200, { wert: 1 }) : json(401, {});
    });

    const alle = Promise.all([api.get("/api/a"), api.get("/api/b"), api.get("/api/c")]);
    await vi.waitFor(() => expect(aufrufe.filter((a) => a.token === "alt")).toHaveLength(3));
    freigeben();
    await alle;

    expect(erneuerungen()).toBe(1);
    expect(aufrufe.filter((a) => a.token === "neu")).toHaveLength(3);
  });

  it("erneuert beim nächsten Ablauf des Tokens wieder (die Merkung der laufenden Erneuerung ist frei)", async () => {
    server(erneuernderServer);
    await api.get("/api/dogs");

    werte.set(TOKEN_KEY, "alt");
    await api.get("/api/dogs");

    expect(erneuerungen()).toBe(2);
  });

  it("wiederholt nur einmal: ein zweites 401 nach der Erneuerung ist ein Fehler, keine Schleife", async () => {
    server((url) => (istErneuerung(url) ? json(200, { token: "neu", refreshToken: "refresh-neu" }) : json(401, { errors: ["Nein."] })));

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);

    expect(erneuerungen()).toBe(1);
    expect(aufrufe.filter((a) => !istErneuerung(a.url))).toHaveLength(2);
  });
});

describe("Token-Erneuerung: Ausfall", () => {
  it.each([401, 400])("Erneuerung mit %i beendet die Sitzung: Daten weg, Weiterleitung zur Anmeldung", async (status) => {
    server((url) => (istErneuerung(url) ? json(status, {}) : json(401, { errors: ["Nicht angemeldet."] })));

    await expect(api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.size).toBe(0);
    expect(ort.href).toBe("/login");
  });

  it("ohne Refresh-Token ist die Sitzung vorbei, ohne dass der Server gefragt wird", async () => {
    werte.delete(REFRESH_KEY);
    server(() => json(401, {}));

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);

    expect(erneuerungen()).toBe(0);
    expect(werte.has(TOKEN_KEY)).toBe(false);
    expect(ort.href).toBe("/login");
  });

  it.each([429, 500, 503])("Erneuerung mit %i (Drosselung, Serverfehler) lässt die Sitzung bestehen", async (status) => {
    server((url) => (istErneuerung(url) ? json(status, {}) : json(401, { errors: ["Nicht angemeldet."] })));

    await expect(api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.get(TOKEN_KEY)).toBe("alt");
    expect(werte.get(REFRESH_KEY)).toBe("refresh-alt");
    expect(ort.href).toBe("http://localhost/dashboard");
  });

  it("Netzfehler bei der Erneuerung lässt die Sitzung bestehen", async () => {
    server((url) => {
      if (istErneuerung(url)) throw new TypeError("Failed to fetch");
      return json(401, {});
    });

    await expect(api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.get(TOKEN_KEY)).toBe("alt");
    expect(ort.href).toBe("http://localhost/dashboard");
  });

  it("nach einer unerreichbaren Erneuerung versucht der nächste Aufruf sie erneut", async () => {
    let erreichbar = false;
    server((url, token) => {
      if (istErneuerung(url)) return erreichbar ? json(200, { token: "neu", refreshToken: "refresh-neu" }) : json(503, {});
      return token === "neu" ? json(200, { wert: 7 }) : json(401, {});
    });

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);
    erreichbar = true;

    await expect(api.get("/api/dogs")).resolves.toEqual({ wert: 7 });
    expect(werte.get(TOKEN_KEY)).toBe("neu");
  });

  it("ein 401 ohne Token (falsches Passwort beim Anmelden) löst keine Erneuerung aus", async () => {
    werte.delete(TOKEN_KEY);
    server(() => json(401, { errors: ["E-Mail oder Passwort stimmt nicht."] }));

    await expect(api.post("/api/auth/login", { email: "a@b.de", password: "x" })).rejects.toMatchObject({ status: 401 });

    expect(erneuerungen()).toBe(0);
    expect(ort.href).toBe("http://localhost/dashboard");
  });
});

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

type Antwort = (url: string, token: string | null, rumpf: { refreshToken?: string }) => Promise<Response> | Response;

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
      return antwort(url, token, typeof init?.body === "string" ? JSON.parse(init.body) : {});
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

  it("ein 401, das erst nach einer fertigen Erneuerung ankommt, erneuert nicht noch einmal", async () => {
    // Der Aufruf ging mit dem alten Token hinaus; bis seine Ablehnung zurück ist, hat
    // eine andere Erneuerung (dieser oder ein anderer Tab) schon einen neuen Token abgelegt.
    server((url, token) => {
      if (istErneuerung(url)) return json(200, { token: "neuer", refreshToken: "refresh-neuer" });
      if (token === "alt") {
        werte.set(TOKEN_KEY, "neu");
        return json(401, {});
      }
      return json(200, { wert: 7 });
    });

    await expect(api.get<{ wert: number }>("/api/dogs")).resolves.toEqual({ wert: 7 });

    expect(erneuerungen()).toBe(0);
    expect(aufrufe.map((a) => a.token)).toEqual(["alt", "neu"]);
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

// Mehrere Tabs teilen sich localStorage, aber jeder hat seine eigene Single-Flight. Damit
// trotzdem nur EIN Tab erneuert, läuft die Erneuerung unter einer browserweiten Sperre (Web
// Locks); wer sie nach einem anderen Tab bekommt, findet den Refresh-Token schon ersetzt und
// geht nicht zum Server. Ein Tab ist hier eine eigene Modulinstanz von api.ts.
describe("Token-Erneuerung: mehrere Tabs", () => {
  type ApiModul = typeof import("@/lib/api");

  async function neuerTab(): Promise<ApiModul> {
    vi.resetModules();
    return import("@/lib/api");
  }

  /** Eine Sperren-Attrappe wie navigator.locks: ein Halter nach dem anderen, in Reihenfolge der Anfragen. */
  function sperren() {
    let kette: Promise<unknown> = Promise.resolve();
    const gesperrt: string[] = [];
    return {
      gesperrt,
      locks: {
        request: (name: string, rueckruf: () => Promise<unknown>) => {
          gesperrt.push(name);
          const lauf = kette.then(rueckruf);
          kette = lauf.catch(() => undefined);
          return lauf;
        },
      },
    };
  }

  /** Server, der einen Refresh-Token nur einmal annimmt (Rotation) und die Erneuerung festhält, bis beide Tabs im 401 stehen. */
  function rotierenderServer(freigabe: Promise<void>) {
    let gueltig = "refresh-alt";
    server(async (url, token, rumpf) => {
      if (istErneuerung(url)) {
        await freigabe;
        // Ein zweiter Aufruf mit dem alten Token wäre die Wiederverwendung.
        if (rumpf.refreshToken !== gueltig) return json(401, {});
        gueltig = "refresh-neu";
        return json(200, { token: "neu", refreshToken: "refresh-neu" });
      }
      return token === "neu" ? json(200, { wert: 1 }) : json(401, {});
    });
  }

  it("mit Sperre erneuert nur ein Tab, der andere übernimmt den neuen Token aus dem Speicher", async () => {
    const { locks, gesperrt } = sperren();
    vi.stubGlobal("navigator", { locks });
    let freigeben!: () => void;
    rotierenderServer(new Promise<void>((weiter) => (freigeben = weiter)));
    const tab1 = await neuerTab();
    const tab2 = await neuerTab();

    const beide = Promise.all([tab1.api.get("/api/a"), tab2.api.get("/api/b")]);
    await vi.waitFor(() => expect(aufrufe.filter((a) => a.token === "alt")).toHaveLength(2));
    freigeben();

    await expect(beide).resolves.toEqual([{ wert: 1 }, { wert: 1 }]);
    expect(erneuerungen()).toBe(1);
    expect(gesperrt).toEqual(["dogity-token-erneuern", "dogity-token-erneuern"]);
    // Beide Seiten haben mit dem neuen Access-Token wiederholt.
    expect(aufrufe.filter((a) => a.token === "neu")).toHaveLength(2);
    expect(werte.get(REFRESH_KEY)).toBe("refresh-neu");
  });

  it("ohne Web Locks erneuert der Tab wie bisher direkt, ohne Sperre", async () => {
    vi.stubGlobal("navigator", {});
    server(erneuernderServer);
    const tab = await neuerTab();

    await expect(tab.api.get("/api/dogs")).resolves.toEqual({ wert: 42 });

    expect(erneuerungen()).toBe(1);
    expect(werte.get(REFRESH_KEY)).toBe("refresh-neu");
  });

  it("mit Sperre und ohne anderen Tab geht die Erneuerung wie bisher zum Server", async () => {
    const { locks } = sperren();
    vi.stubGlobal("navigator", { locks });
    server(erneuernderServer);
    const tab = await neuerTab();

    await expect(tab.api.get("/api/dogs")).resolves.toEqual({ wert: 42 });

    expect(erneuerungen()).toBe(1);
    expect(werte.get(REFRESH_KEY)).toBe("refresh-neu");
  });

  it("mit Sperre beendet ein 401 der Erneuerung die Sitzung wie bisher", async () => {
    const { locks } = sperren();
    vi.stubGlobal("navigator", { locks });
    server((url) => (istErneuerung(url) ? json(401, {}) : json(401, { errors: ["Nicht angemeldet."] })));
    const tab = await neuerTab();

    await expect(tab.api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.size).toBe(0);
    expect(ort.href).toBe("/login");
  });

  it("hat ein anderer Tab abgemeldet (Refresh-Token weg), fragt der wartende Tab den Server nicht", async () => {
    const { locks } = sperren();
    vi.stubGlobal("navigator", { locks });
    server((url) => (istErneuerung(url) ? json(200, { token: "neu", refreshToken: "refresh-neu" }) : json(401, {})));
    const tab = await neuerTab();
    // Zwischen dem 401 und der Sperre meldet ein anderer Tab ab.
    const inSperre = locks.request;
    locks.request = (name, rueckruf) => {
      werte.delete(REFRESH_KEY);
      return inSperre(name, rueckruf);
    };

    await expect(tab.api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(erneuerungen()).toBe(0);
    expect(ort.href).toBe("/login");
  });
});

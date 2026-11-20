import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ABGEMELDET_KEY, ApiError, REFRESH_KEY, TOKEN_KEY, USER_KEY, ZUGRIFF_BIS_KEY, abmeldungNachholen, abmeldungSenden, api, zugriffAbgelaufen } from "@/lib/api";

// Die Anmeldung liegt in HttpOnly-Cookies; das Frontend sieht sie nie. Hier steht das Cookie-Glas des
// Browsers als Variable `cookie` (der "Access-Token", den der Browser mitschickt) und die Zeit bis zum
// Ablauf im localStorage. Die Token-Erneuerung hält die Sitzung am Leben: ein 401 bei angemeldeter Person
// heißt "Access-Token abgelaufen", einmal erneuern und wiederholen. Nur ein ausdrückliches "kenne ich nicht"
// beendet die Sitzung - Netzfehler und Drosselung nicht. Und laufen mehrere Aufrufe zugleich in
// den 401, darf nur EINE Erneuerung passieren (sonst löst die Wiederverwendungserkennung des
// Servers aus und wirft die Person hinaus).

let werte: Map<string, string>;
let ort: { protocol: string; hostname: string; pathname: string; href: string };
let aufrufe: { url: string; token: string | null; kopf: Record<string, string>; credentials: RequestCredentials | undefined }[];
// Was der Browser als Access-Token-Cookie mitschickt; eine erfolgreiche Erneuerung setzt es auf "neu".
let cookie: string | null;
let ablaufZaehler: number;

type Antwort = (url: string, token: string | null, rumpf: { refreshToken?: string }) => Promise<Response> | Response;

/** Eine frische Ablaufzeit je Erneuerung (die Antwort der API trägt sie, nie einen Schlüssel). */
const erneuert = () => json(200, { accessExpiresAt: new Date(Date.now() + 3_600_000 + ++ablaufZaehler).toISOString() });

const json = (status: number, rumpf: unknown) => new Response(JSON.stringify(rumpf), { status });
const istErneuerung = (url: string) => url.endsWith("/api/auth/refresh");
const erneuerungen = () => aufrufe.filter((a) => istErneuerung(a.url)).length;

function server(antwort: Antwort) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (url: string, init?: RequestInit) => {
      const kopf = (init?.headers ?? {}) as Record<string, string>;
      const token = cookie;
      aufrufe.push({ url, token, kopf, credentials: init?.credentials });
      const res = await antwort(url, token, typeof init?.body === "string" ? JSON.parse(init.body) : {});
      // Der Browser übernimmt das Set-Cookie der Erneuerung.
      if (istErneuerung(url) && res.status === 200) cookie = "neu";
      return res;
    }),
  );
}

beforeEach(() => {
  werte = new Map([
    [USER_KEY, "{}"],
    [ZUGRIFF_BIS_KEY, "2099-01-01T00:00:00.000Z"],
  ]);
  cookie = "alt";
  ablaufZaehler = 0;
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

/** Ein Server, der nur den Token "neu" annimmt und bei der Erneuerung neue Cookies setzt. */
const erneuernderServer: Antwort = (url, token) => {
  if (istErneuerung(url)) return erneuert();
  return token === "neu" ? json(200, { wert: 42 }) : json(401, { errors: ["Nicht angemeldet."] });
};

describe("Anfragen mit Cookies", () => {
  it("schickt bei jedem Aufruf Cookies mit und den Kopf X-Dogity, aber nie einen Authorization-Kopf", async () => {
    server(() => json(200, { wert: 1 }));

    await api.get("/api/dogs");
    await api.post("/api/dogs", { name: "Bello" });
    await api.post("/api/auth/login", { email: "a@b.de", password: "x" });

    expect(aufrufe).toHaveLength(3);
    for (const a of aufrufe) {
      expect(a.credentials).toBe("include");
      expect(a.kopf["X-Dogity"]).toBe("1");
      expect(a.kopf.Authorization).toBeUndefined();
    }
  });
});

describe("Token-Erneuerung: Erfolg", () => {
  it("erneuert nach einem 401 einmal, merkt sich die neue Ablaufzeit und wiederholt den Aufruf", async () => {
    server(erneuernderServer);

    await expect(api.get<{ wert: number }>("/api/dogs")).resolves.toEqual({ wert: 42 });

    expect(werte.get(ZUGRIFF_BIS_KEY)).not.toBe("2099-01-01T00:00:00.000Z");
    expect(aufrufe.filter((a) => !istErneuerung(a.url)).map((a) => a.token)).toEqual(["alt", "neu"]);
    expect(erneuerungen()).toBe(1);
    // Die Erneuerung geht mit Cookies hinaus: Der Refresh-Token kommt aus dem Cookie, nicht aus dem Rumpf.
    expect(aufrufe.find((a) => istErneuerung(a.url))?.credentials).toBe("include");
    expect(ort.href).toBe("http://localhost/dashboard");
  });

  it("Single-Flight: parallele 401 lösen nur eine Erneuerung aus, alle Aufrufe gelingen", async () => {
    let freigeben!: () => void;
    const warten = new Promise<void>((weiter) => (freigeben = weiter));
    server(async (url, token) => {
      if (istErneuerung(url)) {
        await warten; // die Erneuerung dauert, während die anderen Aufrufe schon im 401 stehen
        return erneuert();
      }
      return token === "neu" ? json(200, { wert: 1 }) : json(401, {});
    });

    const alle = Promise.all([api.get("/api/a"), api.get("/api/b"), api.get("/api/c")]);
    await vi.waitFor(() => expect(aufrufe.filter((a) => a.token === "alt" && !istErneuerung(a.url))).toHaveLength(3));
    freigeben();
    await alle;

    expect(erneuerungen()).toBe(1);
    expect(aufrufe.filter((a) => a.token === "neu")).toHaveLength(3);
  });

  it("erneuert beim nächsten Ablauf des Tokens wieder (die Merkung der laufenden Erneuerung ist frei)", async () => {
    server(erneuernderServer);
    await api.get("/api/dogs");

    cookie = "alt";
    await api.get("/api/dogs");

    expect(erneuerungen()).toBe(2);
  });

  it("ein 401, das erst nach einer fertigen Erneuerung ankommt, erneuert nicht noch einmal", async () => {
    // Der Aufruf ging mit dem alten Token hinaus; bis seine Ablehnung zurück ist, hat
    // eine andere Erneuerung (dieser oder ein anderer Tab) schon neue Cookies gesetzt.
    server((url, token) => {
      if (istErneuerung(url)) return erneuert();
      if (token === "alt") {
        cookie = "neu";
        werte.set(ZUGRIFF_BIS_KEY, "2099-06-01T00:00:00.000Z");
        return json(401, {});
      }
      return json(200, { wert: 7 });
    });

    await expect(api.get<{ wert: number }>("/api/dogs")).resolves.toEqual({ wert: 7 });

    expect(erneuerungen()).toBe(0);
    expect(aufrufe.map((a) => a.token)).toEqual(["alt", "neu"]);
  });

  it("wiederholt nur einmal: ein zweites 401 nach der Erneuerung ist ein Fehler, keine Schleife", async () => {
    server((url) => (istErneuerung(url) ? erneuert() : json(401, { errors: ["Nein."] })));

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

  it.each([429, 500, 503])("Erneuerung mit %i (Drosselung, Serverfehler) lässt die Sitzung bestehen", async (status) => {
    server((url) => (istErneuerung(url) ? json(status, {}) : json(401, { errors: ["Nicht angemeldet."] })));

    await expect(api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.has(USER_KEY)).toBe(true);
    expect(werte.get(ZUGRIFF_BIS_KEY)).toBe("2099-01-01T00:00:00.000Z");
    expect(ort.href).toBe("http://localhost/dashboard");
  });

  it("Netzfehler bei der Erneuerung lässt die Sitzung bestehen", async () => {
    server((url) => {
      if (istErneuerung(url)) throw new TypeError("Failed to fetch");
      return json(401, {});
    });

    await expect(api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.has(USER_KEY)).toBe(true);
    expect(ort.href).toBe("http://localhost/dashboard");
  });

  it("nach einer unerreichbaren Erneuerung versucht der nächste Aufruf sie erneut", async () => {
    let erreichbar = false;
    server((url, token) => {
      if (istErneuerung(url)) return erreichbar ? erneuert() : json(503, {});
      return token === "neu" ? json(200, { wert: 7 }) : json(401, {});
    });

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);
    erreichbar = true;

    await expect(api.get("/api/dogs")).resolves.toEqual({ wert: 7 });
    expect(cookie).toBe("neu");
  });

  it("ein 401 ohne Anmeldung (falsches Passwort beim Anmelden) löst keine Erneuerung aus", async () => {
    werte.delete(USER_KEY);
    server(() => json(401, { errors: ["E-Mail oder Passwort stimmt nicht."] }));

    await expect(api.post("/api/auth/login", { email: "a@b.de", password: "x" })).rejects.toMatchObject({ status: 401 });

    expect(erneuerungen()).toBe(0);
    expect(ort.href).toBe("http://localhost/dashboard");
  });
});

// Mehrere Tabs teilen sich localStorage, aber jeder hat seine eigene Single-Flight. Damit
// trotzdem nur EIN Tab erneuert, läuft die Erneuerung unter einer browserweiten Sperre (Web
// Locks); wer sie nach einem anderen Tab bekommt, findet die Ablaufzeit schon ersetzt und
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
    let verbraucht = false;
    server(async (url, token) => {
      if (istErneuerung(url)) {
        await freigabe;
        // Ein zweiter Aufruf mit dem alten Token wäre die Wiederverwendung.
        if (verbraucht) return json(401, {});
        verbraucht = true;
        return erneuert();
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
    await vi.waitFor(() => expect(aufrufe.filter((a) => a.token === "alt" && !istErneuerung(a.url))).toHaveLength(2));
    freigeben();

    await expect(beide).resolves.toEqual([{ wert: 1 }, { wert: 1 }]);
    expect(erneuerungen()).toBe(1);
    expect(gesperrt).toEqual(["dogity-token-erneuern", "dogity-token-erneuern"]);
    // Beide Seiten haben mit dem neuen Access-Token wiederholt.
    expect(aufrufe.filter((a) => a.token === "neu")).toHaveLength(2);
    expect(werte.get(ZUGRIFF_BIS_KEY)).not.toBe("2099-01-01T00:00:00.000Z");
  });

  it("ohne Web Locks erneuert der Tab wie bisher direkt, ohne Sperre", async () => {
    vi.stubGlobal("navigator", {});
    server(erneuernderServer);
    const tab = await neuerTab();

    await expect(tab.api.get("/api/dogs")).resolves.toEqual({ wert: 42 });

    expect(erneuerungen()).toBe(1);
    expect(werte.get(ZUGRIFF_BIS_KEY)).not.toBe("2099-01-01T00:00:00.000Z");
  });

  it("mit Sperre und ohne anderen Tab geht die Erneuerung wie bisher zum Server", async () => {
    const { locks } = sperren();
    vi.stubGlobal("navigator", { locks });
    server(erneuernderServer);
    const tab = await neuerTab();

    await expect(tab.api.get("/api/dogs")).resolves.toEqual({ wert: 42 });

    expect(erneuerungen()).toBe(1);
    expect(werte.get(ZUGRIFF_BIS_KEY)).not.toBe("2099-01-01T00:00:00.000Z");
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

  it("hat ein anderer Tab abgemeldet (Nutzerdaten weg), fragt der wartende Tab den Server nicht", async () => {
    const { locks } = sperren();
    vi.stubGlobal("navigator", { locks });
    server((url) => (istErneuerung(url) ? erneuert() : json(401, {})));
    const tab = await neuerTab();
    // Zwischen dem 401 und der Sperre meldet ein anderer Tab ab.
    const inSperre = locks.request;
    locks.request = (name, rueckruf) => {
      werte.delete(USER_KEY);
      return inSperre(name, rueckruf);
    };

    await expect(tab.api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(erneuerungen()).toBe(0);
    expect(ort.href).toBe("/login");
  });
});

describe("Token-Erneuerung: vorab bei abgelaufenem Token", () => {
  it("erkennt den Ablauf an der Ablaufzeit der API; ohne lesbare Zeit gilt der Token als gültig", () => {
    const jetzt = 1_800_000_000_000;
    const iso = (ms: number) => new Date(ms).toISOString();
    expect(zugriffAbgelaufen(iso(jetzt - 60_000), jetzt)).toBe(true);
    expect(zugriffAbgelaufen(iso(jetzt + 5_000), jetzt)).toBe(true); // läuft in den nächsten 10 s ab
    expect(zugriffAbgelaufen(iso(jetzt + 600_000), jetzt)).toBe(false);
    expect(zugriffAbgelaufen("kein-datum", jetzt)).toBe(false);
    expect(zugriffAbgelaufen(null, jetzt)).toBe(false);
  });

  it("erneuert vor dem ersten Abruf, schickt den alten Token nie und erneuert bei parallelen Abrufen nur einmal", async () => {
    werte.set(ZUGRIFF_BIS_KEY, new Date(Date.now() - 60_000).toISOString());
    server(erneuernderServer);

    await Promise.all([api.get("/api/dogs"), api.get("/api/preferences")]);

    expect(erneuerungen()).toBe(1);
    expect(aufrufe.filter((a) => !istErneuerung(a.url)).map((a) => a.token)).toEqual(["neu", "neu"]);
  });
});

// Anmeldungen aus der Zeit vor den Cookies liegen mit beiden Schlüsseln im localStorage. Die erste
// Anfrage nach dem Start übernimmt sie: einmal erneuern mit dem Token im Rumpf, dann sind die
// Cookies gesetzt und die alten Schlüssel gelöscht.
describe("Übernahme bestehender Sitzungen", () => {
  beforeEach(() => {
    werte = new Map([
      [USER_KEY, "{}"],
      [TOKEN_KEY, "alt-jwt"],
      [REFRESH_KEY, "refresh-alt"],
    ]);
    cookie = null;
  });

  it("erneuert vor dem ersten Abruf mit dem alten Token im Rumpf und löscht die alten Schlüssel", async () => {
    const rumpfe: unknown[] = [];
    server((url, token, rumpf) => {
      if (istErneuerung(url)) {
        rumpfe.push(rumpf);
        return erneuert();
      }
      return token === "neu" ? json(200, { wert: 5 }) : json(401, {});
    });

    await expect(api.get<{ wert: number }>("/api/dogs")).resolves.toEqual({ wert: 5 });

    expect(rumpfe).toEqual([{ refreshToken: "refresh-alt" }]);
    expect(werte.has(TOKEN_KEY)).toBe(false);
    expect(werte.has(REFRESH_KEY)).toBe(false);
    expect(werte.has(ZUGRIFF_BIS_KEY)).toBe(true);
    // Weitere Erneuerungen schicken keinen Schlüssel mehr im Rumpf.
    cookie = "alt";
    werte.set(ZUGRIFF_BIS_KEY, new Date(Date.now() - 60_000).toISOString());
    await api.get("/api/dogs");
    expect(rumpfe[1]).toEqual({});
  });

  it("lehnt der Server den alten Token ab, sind die alten Schlüssel und die Sitzung weg", async () => {
    server((url) => (istErneuerung(url) ? json(401, {}) : json(401, { errors: ["Nicht angemeldet."] })));

    await expect(api.get("/api/dogs")).rejects.toMatchObject({ status: 401 });

    expect(werte.size).toBe(0);
    expect(ort.href).toBe("/login");
  });

  it("ist der Server nicht erreichbar, bleiben die alten Schlüssel für den nächsten Versuch", async () => {
    server((url) => (istErneuerung(url) ? json(503, {}) : json(401, {})));

    await expect(api.get("/api/dogs")).rejects.toBeInstanceOf(ApiError);

    expect(werte.get(REFRESH_KEY)).toBe("refresh-alt");
    expect(ort.href).toBe("http://localhost/dashboard");
  });
});

// Meldet sich jemand ohne Netz ab, erreicht das den Server nicht: Die Cookies leben weiter. Der Merker
// sorgt dafür, dass der nächste Start mit Verbindung zuerst abmeldet.
describe("Abmelden", () => {
  it("meldet beim Server ab (mit Cookies und Kopf) und löscht den Merker", async () => {
    server(() => new Response(null, { status: 204 }));

    await abmeldungSenden();

    expect(aufrufe).toHaveLength(1);
    expect(aufrufe[0].url).toMatch(/\/api\/auth\/logout$/);
    expect(aufrufe[0].credentials).toBe("include");
    expect(aufrufe[0].kopf["X-Dogity"]).toBe("1");
    expect(werte.has(ABGEMELDET_KEY)).toBe(false);
  });

  it("merkt sich die Abmeldung, wenn der Server nicht erreichbar ist, und holt sie beim Start nach", async () => {
    server(() => {
      throw new TypeError("Failed to fetch");
    });
    await abmeldungSenden();
    expect(werte.get(ABGEMELDET_KEY)).toBe("1");

    aufrufe = [];
    server(() => new Response(null, { status: 204 }));
    await abmeldungNachholen();

    expect(aufrufe).toHaveLength(1);
    expect(werte.has(ABGEMELDET_KEY)).toBe(false);
  });

  it("holt beim Start nichts nach, wenn nichts offen ist", async () => {
    server(() => new Response(null, { status: 204 }));

    await abmeldungNachholen();

    expect(aufrufe).toHaveLength(0);
  });
});

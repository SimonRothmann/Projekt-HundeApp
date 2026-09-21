import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api, aufzeichnungBeendet, aufzeichnungGestartet, REFRESH_KEY, TOKEN_KEY } from "@/lib/api";

// Die Glocke fragt auch während einer Aufzeichnung jede Minute beim Server
// nach. Läuft dabei die Sitzung ab, darf die Weiterleitung zur Anmeldung die
// laufende Fährte nicht mitnehmen - sie wartet, bis die Aufzeichnung endet.

let werte: Map<string, string>;
let ort: { protocol: string; hostname: string; pathname: string; href: string };

beforeEach(() => {
  werte = new Map([
    [TOKEN_KEY, "abgelaufenes-jwt"],
    [REFRESH_KEY, "widerrufener-refresh-token"],
  ]);
  ort = { protocol: "http:", hostname: "localhost", pathname: "/dogs/hund-1", href: "http://localhost/dogs/hund-1" };
  vi.stubGlobal("window", {
    localStorage: {
      getItem: (k: string) => werte.get(k) ?? null,
      setItem: (k: string, v: string) => werte.set(k, v),
      removeItem: (k: string) => werte.delete(k),
    },
    location: ort,
  });
  // Erst lehnt der Server das Token ab, dann auch die Erneuerung: Die
  // Sitzung ist wirklich vorbei.
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(JSON.stringify({ errors: ["Nicht angemeldet."] }), { status: 401 })),
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("Abgelaufene Sitzung", () => {
  it("leitet ohne Aufzeichnung sofort zur Anmeldung", async () => {
    await api.get("/api/notifications/unread-count").catch(() => {});
    expect(ort.href).toBe("/login");
    expect(werte.has(TOKEN_KEY)).toBe(false);
  });

  it("wartet während einer Aufzeichnung, bis sie endet", async () => {
    aufzeichnungGestartet();
    await api.get("/api/notifications/unread-count").catch(() => {});
    expect(ort.href).toBe("http://localhost/dogs/hund-1");

    aufzeichnungBeendet();
    expect(ort.href).toBe("/login");
    expect(werte.has(TOKEN_KEY)).toBe(false);
  });

  it("leitet nach dem Beenden nicht weiter, wenn die Sitzung in Ordnung war", () => {
    aufzeichnungGestartet();
    aufzeichnungBeendet();
    expect(ort.href).toBe("http://localhost/dogs/hund-1");
  });
});

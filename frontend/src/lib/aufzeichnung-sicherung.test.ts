import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { GpsPoint, GpsPointType, GpsWalkPoint } from "@/lib/types";

// Nur api und Warteschlange ersetzen - ApiError muss die echte Klasse bleiben,
// weil sicherungSpeichern per instanceof zwischen Ablehnung und fehlendem Netz
// unterscheidet.
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  api: { post: vi.fn() },
}));
vi.mock("@/lib/offline-queue", () => ({ enqueueRequest: vi.fn() }));

import { api, ApiError } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import {
  ablaufSchluessel,
  alleSicherungenLoeschen,
  anfrageAusSicherung,
  aufzeichnungsEnde,
  faehrtenSchluessel,
  markerAnzahl,
  sicherungAnzeigen,
  sicherungenAbonnieren,
  sicherungenAuflisten,
  sicherungLesen,
  sicherungLoeschen,
  sicherungSchreiben,
  sicherungSpeichern,
  wirdAngezeigt,
  type AblaufSicherung,
  type FaehrtenSicherung,
} from "@/lib/aufzeichnung-sicherung";

class Speicher implements Storage {
  private werte = new Map<string, string>();
  get length() {
    return this.werte.size;
  }
  clear() {
    this.werte.clear();
  }
  getItem(k: string) {
    return this.werte.get(k) ?? null;
  }
  key(i: number) {
    return [...this.werte.keys()][i] ?? null;
  }
  removeItem(k: string) {
    this.werte.delete(k);
  }
  setItem(k: string, v: string) {
    this.werte.set(k, v);
  }
}

let speicher: Speicher;

beforeEach(() => {
  speicher = new Speicher();
  vi.stubGlobal("window", { localStorage: speicher });
  vi.mocked(api.post).mockReset();
  vi.mocked(enqueueRequest).mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function punkt(zeit: string, pointType: GpsPointType = 0): GpsPoint {
  return { latitude: 48.9, longitude: 8.5, timestamp: zeit, accuracy: 5, pointType, label: null, markerType: 0 };
}

function faehrte(ueber: Partial<FaehrtenSicherung> = {}): FaehrtenSicherung {
  return {
    art: "faehrte",
    userId: "nutzer-a",
    seite: "/dogs/hund-1",
    begonnen: Date.parse("2026-09-20T08:00:00Z"),
    trackId: "fährte-1",
    dogId: "hund-1",
    untergrund: ["Wiese", "Acker"],
    points: [punkt("2026-09-20T08:00:05Z"), punkt("2026-09-20T08:04:00Z", 1), punkt("2026-09-20T08:12:30Z")],
    ...ueber,
  };
}

function ablauf(ueber: Partial<AblaufSicherung> = {}): AblaufSicherung {
  const p: GpsWalkPoint = {
    latitude: 48.9,
    longitude: 8.5,
    timestamp: "2026-09-20T09:00:00Z",
    accuracy: 5,
    deviationMeters: null,
  };
  return {
    art: "ablauf",
    userId: "nutzer-a",
    seite: "/dashboard",
    begonnen: Date.parse("2026-09-20T09:00:00Z"),
    trackId: "fährte-1",
    kommentar: "  bei Wind ",
    points: [p],
    ...ueber,
  };
}

describe("Sicherung auf dem Gerät", () => {
  it("liest zurück, was geschrieben wurde", () => {
    sicherungSchreiben(faehrte());
    expect(sicherungLesen(faehrtenSchluessel("hund-1"), "nutzer-a")).toEqual(faehrte());
  });

  it("zeigt die Sicherung eines anderen Nutzers nicht - weder einzeln noch in der Liste", () => {
    sicherungSchreiben(faehrte({ userId: "nutzer-b" }));
    expect(sicherungLesen(faehrtenSchluessel("hund-1"), "nutzer-a")).toBeNull();
    expect(sicherungenAuflisten("nutzer-a")).toEqual([]);
  });

  it("hält Legen und Ablaufen derselben Fährte auseinander", () => {
    sicherungSchreiben(faehrte());
    sicherungSchreiben(ablauf());
    expect(sicherungenAuflisten("nutzer-a").map((s) => s.art)).toEqual(["faehrte", "ablauf"]);
    sicherungLoeschen(ablaufSchluessel("fährte-1"));
    expect(sicherungenAuflisten("nutzer-a").map((s) => s.art)).toEqual(["faehrte"]);
  });

  it("übersteht kaputte Einträge, statt die Seite zu sprengen", () => {
    speicher.setItem("dogity_aufzeichnung:faehrte:hund-1", "{kein json");
    speicher.setItem("dogity_aufzeichnung:faehrte:hund-2", JSON.stringify({ art: "faehrte", userId: "nutzer-a" }));
    expect(sicherungLesen(faehrtenSchluessel("hund-1"), "nutzer-a")).toBeNull();
    expect(sicherungenAuflisten("nutzer-a")).toEqual([]);
  });

  it("löscht beim Abmelden alle Sicherungen und sonst nichts", () => {
    sicherungSchreiben(faehrte());
    sicherungSchreiben(ablauf({ userId: "nutzer-b" }));
    speicher.setItem("dogity_kartenebene", "luftbild");
    alleSicherungenLoeschen();
    expect(speicher.length).toBe(1);
    expect(speicher.getItem("dogity_kartenebene")).toBe("luftbild");
  });

  it("arbeitet ohne Speicher einfach nicht, statt zu werfen", () => {
    vi.stubGlobal("window", {
      get localStorage(): Storage {
        throw new Error("SecurityError");
      },
    });
    expect(() => sicherungSchreiben(faehrte())).not.toThrow();
    expect(sicherungenAuflisten("nutzer-a")).toEqual([]);
  });
});

describe("Anfrage aus einer Sicherung", () => {
  it("Fährte: behält ihre Id, damit ein zweiter Versuch keine zweite Fährte anlegt", () => {
    const body = anfrageAusSicherung(faehrte()).body as Record<string, unknown>;
    expect(anfrageAusSicherung(faehrte()).path).toBe("/api/gps-tracks");
    expect(body.id).toBe("fährte-1");
    expect(body.dogId).toBe("hund-1");
    expect(body.surface).toBe("Wiese, Acker");
  });

  it("Fährte: Tag und Dauer kommen aus der Aufzeichnung, nicht vom Zeitpunkt des Speicherns", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-21T18:00:00Z"));
    try {
      const body = anfrageAusSicherung(faehrte()).body as Record<string, unknown>;
      expect(body.date).toBe("2026-09-20");
      // 08:00:00 bis zum letzten Punkt 08:12:30 - nicht bis zum nächsten Abend.
      expect(body.durationMinutes).toBe(13);
    } finally {
      vi.useRealTimers();
    }
  });

  it("zählt Marker getrennt und nimmt als Ende den letzten Punkt", () => {
    expect(markerAnzahl(faehrte())).toBe(1);
    expect(markerAnzahl(ablauf())).toBe(0);
    expect(aufzeichnungsEnde(faehrte())).toBe(Date.parse("2026-09-20T08:12:30Z"));
    expect(aufzeichnungsEnde(faehrte({ points: [] }))).toBe(faehrte().begonnen);
  });

  it("Ablauf: geht an die gelegte Fährte, Kommentar ohne Leerraum", () => {
    const { path, body } = anfrageAusSicherung(ablauf());
    expect(path).toBe("/api/gps-tracks/fährte-1/walk-runs");
    expect((body as { comment: string }).comment).toBe("bei Wind");
    expect((anfrageAusSicherung(ablauf({ kommentar: "  " })).body as { comment: null }).comment).toBeNull();
  });
});

describe("Speichern einer Sicherung", () => {
  it("löscht sie, sobald der Server sie hat", async () => {
    vi.mocked(api.post).mockResolvedValue({ id: "fährte-1" });
    sicherungSchreiben(faehrte());
    const ergebnis = await sicherungSpeichern(faehrte(), "Fährte");
    expect(ergebnis).toEqual({ ausgang: "gespeichert", faehrte: { id: "fährte-1" } });
    expect(sicherungenAuflisten("nutzer-a")).toEqual([]);
  });

  it("behält sie, wenn der Server ablehnt - vorher waren die Punkte dann weg", async () => {
    vi.mocked(api.post).mockRejectedValue(new ApiError(400, ["Hund nicht gefunden."]));
    sicherungSchreiben(faehrte());
    const ergebnis = await sicherungSpeichern(faehrte(), "Fährte");
    expect(ergebnis).toEqual({ ausgang: "fehler", meldung: "Hund nicht gefunden." });
    expect(sicherungenAuflisten("nutzer-a")).toHaveLength(1);
    expect(enqueueRequest).not.toHaveBeenCalled();
  });

  it("legt sie ohne Netz in die Warteschlange und löscht sie dann", async () => {
    vi.mocked(api.post).mockRejectedValue(new TypeError("Failed to fetch"));
    sicherungSchreiben(ablauf());
    const ergebnis = await sicherungSpeichern(ablauf(), "Ablauf-Versuch");
    expect(ergebnis).toEqual({ ausgang: "offline" });
    expect(enqueueRequest).toHaveBeenCalledWith(
      expect.objectContaining({ path: "/api/gps-tracks/fährte-1/walk-runs", method: "POST", label: "Ablauf-Versuch" }),
    );
    expect(sicherungenAuflisten("nutzer-a")).toEqual([]);
  });

  it("behält sie, wenn nicht einmal die Warteschlange geht", async () => {
    vi.mocked(api.post).mockRejectedValue(new TypeError("Failed to fetch"));
    vi.mocked(enqueueRequest).mockRejectedValue(new Error("IndexedDB gesperrt"));
    sicherungSchreiben(faehrte());
    expect(await sicherungSpeichern(faehrte(), "Fährte")).toEqual({ ausgang: "fehler", meldung: null });
    expect(sicherungenAuflisten("nutzer-a")).toHaveLength(1);
  });
});

describe("Welche Sicherung zeigt gerade ein Recorder?", () => {
  it("zählt mehrfach angezeigte Schlüssel und meldet jede Änderung", () => {
    const gemeldet = vi.fn();
    const abbestellen = sicherungenAbonnieren(gemeldet);
    const ersterWeg = sicherungAnzeigen("faehrte:hund-1");
    const zweiterWeg = sicherungAnzeigen("faehrte:hund-1");
    ersterWeg();
    expect(wirdAngezeigt("faehrte:hund-1")).toBe(true);
    zweiterWeg();
    expect(wirdAngezeigt("faehrte:hund-1")).toBe(false);
    sicherungLoeschen("faehrte:hund-1");
    expect(gemeldet).toHaveBeenCalledTimes(5);
    abbestellen();
  });
});

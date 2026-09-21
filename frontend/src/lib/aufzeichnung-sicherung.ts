import { api, ApiError } from "@/lib/api";
import { estimateLengthMeters } from "@/lib/geo";
import { enqueueRequest } from "@/lib/offline-queue";
import { untergrundAlsText } from "@/lib/untergrund";
import type { GpsPoint, GpsTrack, GpsWalkPoint } from "@/lib/types";

/**
 * Sicherung einer laufenden GPS-Aufzeichnung auf dem Gerät.
 *
 * Bis 2026-09-21 lebte eine Aufzeichnung nur im Arbeitsspeicher der Seite.
 * Alles, was die Seite neu aufbaut, nahm die Fährte mit: ein Neuladen, ein
 * iPhone, das die App im Hintergrund beendet, die Weiterleitung zur Anmeldung
 * nach abgelaufener Sitzung, ein Absturz. Eine gelegte Fährte lässt sich aber
 * nicht wiederholen - die Spur liegt, der Hund wartet.
 *
 * Deshalb wird jeder neue Punkt sofort hierher geschrieben, und zwar
 * synchron in den localStorage: Wird die Seite im nächsten Moment beendet,
 * ist der Punkt schon da. Eine Fährte von einem Kilometer sind rund 500
 * Punkte, gut 75 KB - weit unter dem, was der Speicher fasst.
 *
 * Gelöscht wird eine Sicherung erst, wenn die Aufzeichnung beim Server
 * angekommen ist (oder in der Offline-Warteschlange liegt), oder wenn jemand
 * sie ausdrücklich verwirft. Ein Fehler beim Speichern lässt sie stehen.
 */

const PREFIX = "dogity_aufzeichnung:";

type Gemeinsam = {
  /** Wem die Aufzeichnung gehört - auf einem geteilten Gerät sieht sie niemand sonst. */
  userId: string;
  /** Die Seite, auf der aufgezeichnet wurde. Dorthin führt der Hinweis zum Fortsetzen. */
  seite: string;
  /** Beginn der Aufzeichnung in ms seit 1970 - bleibt beim Fortsetzen erhalten. */
  begonnen: number;
};

export type FaehrtenSicherung = Gemeinsam & {
  art: "faehrte";
  /**
   * Die Id der künftigen Fährte, schon beim Start vergeben. Damit ist jedes
   * Speichern derselben Sicherung dieselbe Anfrage: Kam die Antwort beim
   * ersten Versuch nicht mehr an, legt der zweite keine zweite Fährte an.
   */
  trackId: string;
  dogId: string;
  untergrund: string[];
  points: GpsPoint[];
};

export type AblaufSicherung = Gemeinsam & {
  art: "ablauf";
  /** Die gelegte Fährte, zu der dieser Ablauf gehört. */
  trackId: string;
  kommentar: string;
  points: GpsWalkPoint[];
};

export type AufzeichnungsSicherung = FaehrtenSicherung | AblaufSicherung;

export function faehrtenSchluessel(dogId: string) {
  return `faehrte:${dogId}`;
}

export function ablaufSchluessel(trackId: string) {
  return `ablauf:${trackId}`;
}

export function schluesselVon(sicherung: AufzeichnungsSicherung) {
  return sicherung.art === "faehrte" ? faehrtenSchluessel(sicherung.dogId) : ablaufSchluessel(sicherung.trackId);
}

// Jeder Zugriff hinter try/catch: Im privaten Modus oder bei gesperrten
// Websitedaten wirft schon der Zugriff auf localStorage. Die Aufzeichnung
// läuft dann trotzdem - nur eben ohne Netz.
function speicher(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

function lesenRoh(vollerSchluessel: string): AufzeichnungsSicherung | null {
  try {
    const roh = speicher()?.getItem(vollerSchluessel);
    if (!roh) return null;
    const wert = JSON.parse(roh) as AufzeichnungsSicherung;
    return Array.isArray(wert?.points) && (wert.art === "faehrte" || wert.art === "ablauf") ? wert : null;
  } catch {
    return null;
  }
}

/** Die Sicherung unter diesem Schlüssel - nur, wenn sie diesem Nutzer gehört. */
export function sicherungLesen(schluessel: string, userId: string): AufzeichnungsSicherung | null {
  const wert = lesenRoh(PREFIX + schluessel);
  return wert && wert.userId === userId ? wert : null;
}

export function sicherungSchreiben(sicherung: AufzeichnungsSicherung) {
  try {
    speicher()?.setItem(PREFIX + schluesselVon(sicherung), JSON.stringify(sicherung));
  } catch {
    // Speicher voll oder gesperrt: Die Aufzeichnung läuft weiter, sie ist nur
    // nicht gegen ein Neuladen geschützt. Eine Fehlermeldung mitten im
    // Legen hülfe niemandem.
  }
}

export function sicherungLoeschen(schluessel: string) {
  try {
    speicher()?.removeItem(PREFIX + schluessel);
  } catch {
    // Siehe sicherungSchreiben.
  }
  melden();
}

/** Alle Sicherungen dieses Nutzers, älteste zuerst. */
export function sicherungenAuflisten(userId: string): AufzeichnungsSicherung[] {
  const s = speicher();
  if (!s) return [];
  const gefunden: AufzeichnungsSicherung[] = [];
  try {
    for (let i = 0; i < s.length; i++) {
      const schluessel = s.key(i);
      if (!schluessel?.startsWith(PREFIX)) continue;
      const wert = lesenRoh(schluessel);
      if (wert && wert.userId === userId) gefunden.push(wert);
    }
  } catch {
    return [];
  }
  return gefunden.sort((a, b) => a.begonnen - b.begonnen);
}

/**
 * Beim Abmelden: alle Sicherungen fort, gleich wessen.
 *
 * Sie enthalten Standorte, und die Datenschutzerklärung sagt zu, dass
 * Abmelden die lokal gespeicherten Werte entfernt. Nach einer abgelaufenen
 * Sitzung bleiben sie dagegen stehen - das ist kein Abmelden, sondern ein
 * Unfall, und nach der erneuten Anmeldung soll die Fährte noch da sein.
 */
export function alleSicherungenLoeschen() {
  const s = speicher();
  if (!s) return;
  try {
    const weg: string[] = [];
    for (let i = 0; i < s.length; i++) {
      const schluessel = s.key(i);
      if (schluessel?.startsWith(PREFIX)) weg.push(schluessel);
    }
    weg.forEach((schluessel) => s.removeItem(schluessel));
  } catch {
    // Siehe sicherungSchreiben.
  }
  melden();
}

// --- Wer zeigt eine Sicherung gerade an? -----------------------------------
//
// Eine unterbrochene Aufzeichnung erscheint dort, wo aufgezeichnet wurde: am
// Recorder selbst, mit "Weiter aufzeichnen". Nur ist der nicht immer zu
// sehen - ein iPhone, das die App beendet hat, öffnet sie beim nächsten Mal
// auf der Startseite, nicht auf der Hundeseite. Deshalb gibt es zusätzlich
// einen Hinweis in der App-Hülle, und der soll genau die Sicherungen zeigen,
// die gerade KEIN Recorder zeigt. Dafür melden sich Recorder hier an.
//
// Geschrieben wird bei jedem GPS-Punkt; gemeldet wird davon nichts - der
// Hinweis muss nur wissen, wann eine Sicherung verschwindet oder ein
// Recorder kommt oder geht, nicht jeden neuen Punkt.

const angezeigt = new Map<string, number>();
const hoerer = new Set<() => void>();
let stand = 0;

function melden() {
  stand++;
  hoerer.forEach((h) => h());
}

export function sicherungenAbonnieren(h: () => void) {
  hoerer.add(h);
  return () => {
    hoerer.delete(h);
  };
}

/** Zähler, der sich bei jeder gemeldeten Änderung erhöht - für useSyncExternalStore. */
export function sicherungenStand() {
  return stand;
}

/** Ein Recorder für diesen Schlüssel ist sichtbar; die Rückgabe meldet ihn wieder ab. */
export function sicherungAnzeigen(schluessel: string) {
  angezeigt.set(schluessel, (angezeigt.get(schluessel) ?? 0) + 1);
  melden();
  return () => {
    const n = (angezeigt.get(schluessel) ?? 1) - 1;
    if (n > 0) angezeigt.set(schluessel, n);
    else angezeigt.delete(schluessel);
    melden();
  };
}

export function wirdAngezeigt(schluessel: string) {
  return angezeigt.has(schluessel);
}

/** Anzahl der Marker (Gegenstände, Leckerlipots, ...) einer Sicherung. */
export function markerAnzahl(sicherung: AufzeichnungsSicherung) {
  return sicherung.art === "faehrte" ? sicherung.points.filter((p) => p.pointType === 1).length : 0;
}

/**
 * Wann zuletzt aufgezeichnet wurde: der Zeitstempel des letzten Punkts.
 *
 * Nicht "jetzt" - wer eine unterbrochene Fährte erst abends speichert, hat
 * sie nicht sechs Stunden lang gelegt.
 */
export function aufzeichnungsEnde(sicherung: AufzeichnungsSicherung): number {
  const zeiten = sicherung.points.map((p) => Date.parse(p.timestamp)).filter((z) => !Number.isNaN(z));
  return zeiten.length > 0 ? Math.max(...zeiten) : sicherung.begonnen;
}

/** Die Anfrage, mit der eine Sicherung beim Server ankommt. */
export function anfrageAusSicherung(sicherung: AufzeichnungsSicherung): { path: string; body: unknown } {
  if (sicherung.art === "ablauf") {
    return {
      path: `/api/gps-tracks/${sicherung.trackId}/walk-runs`,
      body: {
        lengthMeters: estimateLengthMeters(sicherung.points),
        comment: sicherung.kommentar.trim() || null,
        points: sicherung.points,
      },
    };
  }

  // EINE Anfrage mit Hund und Datum, statt erst eine Einheit und dann die
  // Fährte dazu anzulegen. Der Server hängt die Fährte an die Einheit des
  // Tages (siehe GpsTrackService.CreateAsync): Mehrere Fährten pro
  // Übungsstunde sind im Fährtensport üblich und gehören in dieselbe
  // Einheit. Die Id macht die Anfrage wiederholbar - aus der Warteschlange
  // wie aus einer Sicherung.
  return {
    path: "/api/gps-tracks",
    body: {
      id: sicherung.trackId,
      dogId: sicherung.dogId,
      // Der Tag, an dem gelegt wurde - nicht der, an dem gespeichert wird.
      date: new Date(sicherung.begonnen).toISOString().slice(0, 10),
      durationMinutes: Math.max(1, Math.round((aufzeichnungsEnde(sicherung) - sicherung.begonnen) / 60000)),
      lengthMeters: estimateLengthMeters(sicherung.points),
      ageMinutes: null,
      surface: untergrundAlsText(sicherung.untergrund),
      weather: null,
      wind: null,
      comment: null,
      points: sicherung.points,
    },
  };
}

export type SpeicherErgebnis =
  | { ausgang: "gespeichert"; faehrte: GpsTrack | null }
  | { ausgang: "offline" }
  /** meldung: die Begründung des Servers; null, wenn nicht einmal die Warteschlange ging. */
  | { ausgang: "fehler"; meldung: string | null };

/**
 * Schickt eine Sicherung an den Server - oder, ohne Netz, in die
 * Offline-Warteschlange. In beiden Fällen ist sie danach gelöscht.
 *
 * Lehnt der Server ab, bleibt sie stehen. Vorher war die Aufzeichnung in
 * diesem Fall verloren: Die Punkte wurden nach der Fehlermeldung verworfen.
 */
export async function sicherungSpeichern(
  sicherung: AufzeichnungsSicherung,
  warteschlangenLabel: string,
): Promise<SpeicherErgebnis> {
  const { path, body } = anfrageAusSicherung(sicherung);
  try {
    const antwort = await api.post<unknown>(path, body);
    sicherungLoeschen(schluesselVon(sicherung));
    return { ausgang: "gespeichert", faehrte: sicherung.art === "faehrte" ? (antwort as GpsTrack) : null };
  } catch (err) {
    if (err instanceof ApiError) return { ausgang: "fehler", meldung: err.message };
    try {
      await enqueueRequest({ path, method: "POST", body, label: warteschlangenLabel });
    } catch {
      return { ausgang: "fehler", meldung: null };
    }
    sicherungLoeschen(schluesselVon(sicherung));
    return { ausgang: "offline" };
  }
}

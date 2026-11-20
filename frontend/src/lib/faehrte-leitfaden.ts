import type { Goal } from "@/lib/types";
import { schritte, wirksameSchrittlaenge } from "@/lib/schritte";

/**
 * Leitfaden beim Fährtelegen (Paket FL1): was die angestrebte Prüfungsstufe von der Fährte verlangt.
 *
 * Die Werte stehen im Backend-Katalog nur als Text (Übungsbeschreibung "Fährtenarbeit", Description der
 * Ordnung in SportCatalogSeeder*.cs), nicht strukturiert - sie werden hier einmal von dort übernommen. Gegen
 * die Prüfungsordnung geprüft sind sie in docs/PO_VERIFIKATION.md (FCI-PO 2025, IGP S. 36/42, IFH S. 69-79;
 * SKG-NPO-BH). Was der Katalog für eine Stufe nicht nennt, bleibt weg (Feld fehlt, keine Zeile): Nichts
 * schätzen. Fehlt eine Stufe ganz (nationale Fährten von VDH/SV/DVG/ÖKV führt der Katalog nicht), gibt es
 * keinen Leitfaden.
 */
export type FaehrteLeitfaden = {
  id: string;
  /** Anzeigename, gilt in beiden Sprachen. */
  name: string;
  /** Katalogkennungen (Regulation.seedKey), für die der Leitfaden gilt - z. B. eine IGP und ihre Einzelprüfung. */
  seedKeys: readonly string[];
  schritte: number;
  /** "mind" (FCI: "min.") oder "ca" (SKG: "etwa"). */
  schritteArt: "mind" | "ca";
  schenkel?: number;
  winkel?: number;
  gegenstaende: number;
  eigene: boolean;
  /** Fährtenalter im Katalog = Mindest-Liegezeit in Minuten. */
  liegezeitMinuten: number;
};

// Quelle je Eintrag: Katalogzeile "Fährtenarbeit" der genannten Ordnungen (SportCatalogSeeder.cs) bzw. der
// SKG-BH-Ordnung (SportCatalogSeeder.Schweiz.cs).
export const FAEHRTE_LEITFAEDEN: readonly FaehrteLeitfaden[] = [
  // "IGP 1 - Fährte", "FCI-IGP 1", "FCI-FPr 1": Eigenfährte, min. 300 Schritte, 3 Schenkel, 2 Winkel, Fährtenalter min. 20 Min.; 3 eigene Gegenstände (je 7 Punkte).
  { id: "igp1", name: "IGP 1", seedKeys: ["IGP 1 - Fährte", "FCI-IGP 1", "FCI-FPr 1"], schritte: 300, schritteArt: "mind", schenkel: 3, winkel: 2, gegenstaende: 3, eigene: true, liegezeitMinuten: 20 },
  // "IGP 2 - Fährte", "FCI-IGP 2", "FCI-FPr 2": Fremdfährte, min. 400 Schritte, 3 Schenkel, 2 Winkel, min. 30 Min.; 3 fremde Gegenstände.
  { id: "igp2", name: "IGP 2", seedKeys: ["IGP 2 - Fährte", "FCI-IGP 2", "FCI-FPr 2"], schritte: 400, schritteArt: "mind", schenkel: 3, winkel: 2, gegenstaende: 3, eigene: false, liegezeitMinuten: 30 },
  // "IGP 3 - Fährte", "FCI-IGP 3", "FCI-FPr 3": Fremdfährte, min. 600 Schritte, 5 Schenkel, 4 Winkel, min. 60 Min.; 3 fremde Gegenstände.
  { id: "igp3", name: "IGP 3", seedKeys: ["IGP 3 - Fährte", "FCI-IGP 3", "FCI-FPr 3"], schritte: 600, schritteArt: "mind", schenkel: 5, winkel: 4, gegenstaende: 3, eigene: false, liegezeitMinuten: 60 },
  // "FCI-IFH 1": Eigenfährte, min. 800 Schritte, 5 Schenkel, 4 Winkel, min. 90 Min.; 3 eigene Gegenstände.
  { id: "ifh1", name: "IFH 1", seedKeys: ["FCI-IFH 1"], schritte: 800, schritteArt: "mind", schenkel: 5, winkel: 4, gegenstaende: 3, eigene: true, liegezeitMinuten: 90 },
  // "FCI-IFH 2": Fremdfährte, min. 1200 Schritte, 7 Schenkel, 6 Winkel, min. 120 Min.; 4 fremde Gegenstände (3 x 5 + 1 x 6 Punkte).
  { id: "ifh2", name: "IFH 2", seedKeys: ["FCI-IFH 2"], schritte: 1200, schritteArt: "mind", schenkel: 7, winkel: 6, gegenstaende: 4, eigene: false, liegezeitMinuten: 120 },
  // "FCI-IFH 3": Fremdfährte, min. 1800 Schritte, 8 Schenkel, 7 Winkel, min. 180 Min.; 7 fremde Gegenstände.
  { id: "ifh3", name: "IFH 3", seedKeys: ["FCI-IFH 3"], schritte: 1800, schritteArt: "mind", schenkel: 8, winkel: 7, gegenstaende: 7, eigene: false, liegezeitMinuten: 180 },
  // "FCI-IGP FH": je Fährte wie IFH 3 (Katalog: "Je Fährte: ... min. 1800 Schritte, 8 Schenkel ... 7 Winkel ... 180 Minuten", 7 Gegenstände je Fährte).
  { id: "igpfh", name: "IGP-FH", seedKeys: ["FCI-IGP FH"], schritte: 1800, schritteArt: "mind", schenkel: 8, winkel: 7, gegenstaende: 7, eigene: false, liegezeitMinuten: 180 },
  // "SKG BH 1": Eigenfährte, etwa 150 Schritte mit einem 90-Grad-Winkel, Liegezeit mind. 20 Min.; ein Gegenstand (der eigene). Die Schenkelzahl nennt der Katalog nicht.
  { id: "skgbh1", name: "SKG BH 1", seedKeys: ["SKG BH 1"], schritte: 150, schritteArt: "ca", winkel: 1, gegenstaende: 1, eigene: true, liegezeitMinuten: 20 },
  // "SKG BH 2": Fremdfährte, etwa 300 Schritte mit drei 90-Grad-Winkeln, mind. 30 Min.; zwei Gegenstände.
  { id: "skgbh2", name: "SKG BH 2", seedKeys: ["SKG BH 2"], schritte: 300, schritteArt: "ca", winkel: 3, gegenstaende: 2, eigene: false, liegezeitMinuten: 30 },
  // "SKG BH 3": Fremdfährte, etwa 400 Schritte mit drei 90-Grad-Winkeln, einem 45- und einem 135-Grad-Winkel (= 5 Winkel), mind. 60 Min.; drei Gegenstände.
  { id: "skgbh3", name: "SKG BH 3", seedKeys: ["SKG BH 3"], schritte: 400, schritteArt: "ca", winkel: 5, gegenstaende: 3, eigene: false, liegezeitMinuten: 60 },
];

export function leitfadenNachId(id: string | null | undefined): FaehrteLeitfaden | null {
  return FAEHRTE_LEITFAEDEN.find((l) => l.id === id) ?? null;
}

/**
 * Die Stufe, die der Leitfaden vorbelegt (das Ziel trägt den Katalognamen der Ordnung, der dem seedKey
 * entspricht; eine von Hand umbenannte Ordnung findet so keine Stufe und fällt auf die gemerkte zurück): die des aktiven Ziels (das mit dem nächsten Termin, wenn
 * mehrere eine Fährte haben), sonst die zuletzt gewählte dieses Hundes, sonst keine.
 */
export function vorgabeLeitfaden(
  ziele: readonly Pick<Goal, "status" | "regulationName" | "targetDate">[] | null | undefined,
  gemerktId: string | null | undefined,
): FaehrteLeitfaden | null {
  const treffer = (ziele ?? [])
    .filter((z) => z.status === 0)
    .sort((a, b) => a.targetDate.localeCompare(b.targetDate))
    .map((z) => FAEHRTE_LEITFAEDEN.find((l) => z.regulationName != null && l.seedKeys.includes(z.regulationName)))
    .find((l) => l !== undefined);
  return treffer ?? leitfadenNachId(gemerktId);
}

export type LeitfadenFortschritt = {
  schritteIst: number;
  schritteErreicht: boolean;
  gegenstaendeIst: number;
  gegenstaendeErreicht: boolean;
};

/** Stand beim Legen: gelaufene Meter in Schritten (auf 10 gerundet wie überall) und gesetzte Gegenstand-Marker. */
export function leitfadenFortschritt(
  leitfaden: FaehrteLeitfaden,
  meter: number,
  schrittlaengeCm: number | null | undefined,
  gegenstandMarker: number,
): LeitfadenFortschritt {
  const schritteIst = schritte(meter, wirksameSchrittlaenge(schrittlaengeCm)) ?? 0;
  return {
    schritteIst,
    schritteErreicht: schritteIst >= leitfaden.schritte,
    gegenstaendeIst: gegenstandMarker,
    gegenstaendeErreicht: gegenstandMarker >= leitfaden.gegenstaende,
  };
}

/** Minuten, die der gelegten Fährte noch bis zur Mindest-Liegezeit fehlen (0 = erreicht). */
export function liegezeitRest(leitfaden: FaehrteLeitfaden, alterMinuten: number): number {
  return Math.max(0, leitfaden.liegezeitMinuten - alterMinuten);
}

type TextUebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** "IGP 1: mind. 300 Schritte · 3 Schenkel · 2 Winkel · 3 Gegenstände · eigene Fährte · mind. 20 Min. Liegezeit" */
export function leitfadenZeile(l: FaehrteLeitfaden, t: TextUebersetzer): string {
  const teile = [
    l.schritteArt === "mind" ? t("mind. {n} Schritte", { n: l.schritte }) : t("ca. {n} Schritte", { n: l.schritte }),
    l.schenkel != null ? t("{n} Schenkel", { n: l.schenkel }) : null,
    l.winkel != null ? t("{n} Winkel", { n: l.winkel }) : null,
    l.gegenstaende === 1 ? t("1 Gegenstand") : t("{n} Gegenstände", { n: l.gegenstaende }),
    l.eigene ? t("eigene Fährte") : t("fremde Fährte"),
    t("mind. {n} Min. Liegezeit", { n: l.liegezeitMinuten }),
  ].filter((x): x is string => x !== null);
  return `${l.name}: ${teile.join(" · ")}`;
}

// Nur auf diesem Gerät gemerkt: die Stufe des Hundes beim letzten Legen (keine Migration nötig).
const gemerktSchluessel = (dogId: string) => `dogity.faehrte-leitfaden.${dogId}`;

export function leitfadenGemerkt(dogId: string): string | null {
  try {
    return window.localStorage.getItem(gemerktSchluessel(dogId));
  } catch {
    return null;
  }
}

export function leitfadenMerken(dogId: string, id: string | null): void {
  try {
    if (id) window.localStorage.setItem(gemerktSchluessel(dogId), id);
    else window.localStorage.removeItem(gemerktSchluessel(dogId));
  } catch {
    // Ohne Speicher (privates Fenster) bleibt es bei der Vorgabe aus dem Ziel.
  }
}

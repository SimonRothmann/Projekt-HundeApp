import { uebersetzbar } from "@/lib/i18n/sprachen";
import type { Goal } from "@/lib/types";

/**
 * Angaben und Leitfaden der Schweißfährte (Paket SF1). Die Fakten stammen aus den Prüfungsordnungen des
 * JGHV (VGPO, VSwPO/VFsPO), hier in eigenen Worten und als Zahlen.
 *
 * Gespeichert wird der deutsche Begriff (wie beim Untergrund); das Backend prüft gegen dieselben Listen
 * (Schweissangaben.cs, backend-kopplung.test.ts). Gemessen wird in Metern, nicht in Schritten.
 */

export const VERFAHREN_TUPF = "Tupf";
export const VERFAHREN_TROPF = "Tropf";
export const VERFAHREN_FAEHRTENSCHUH = "Fährtenschuh";

export const SCHWEISS_VERFAHREN: readonly string[] = [
  uebersetzbar("Tupf"),
  uebersetzbar("Tropf"),
  uebersetzbar("Fährtenschuh"),
];

export const SCHWEISS_WILDARTEN: readonly string[] = [
  uebersetzbar("Rotwild"),
  uebersetzbar("Damwild"),
  uebersetzbar("Schwarzwild"),
  uebersetzbar("Sikawild"),
  uebersetzbar("Muffelwild"),
  uebersetzbar("Gamswild"),
  uebersetzbar("Rehwild"),
  uebersetzbar("Sonstiges"),
];

/** Grenzen wie im Backend (Schweissangaben.MaxMl / MaxStehzeitStunden). */
export const MAX_SCHWEISS_ML = 1000;
export const MAX_STEHZEIT_STUNDEN = 48;

/**
 * Was der Leitfaden einer Prüfung verlangt. Ein Wundbett ist die Stelle, an der das Stück sich niedergetan
 * hat; ein Verweiserpunkt (beim Fährtenschuh: Tropfbett) eine kleine Schweißstelle, die der Hund verweisen
 * soll. Am Ende der Fährte liegt das Stück.
 */
export type SchweissLeitfaden = {
  id: string;
  /** Anzeigename, gilt in beiden Sprachen. */
  name: string;
  /** Katalogkennungen (Regulation.seedKey der Jagd, Paket JK1), für die der Leitfaden gilt. */
  seedKeys: readonly string[];
  /** Länge in Metern; "ca" (etwa) oder "mind" (mindestens). */
  laengeMeter: number;
  laengeArt: "ca" | "mind";
  haken: number;
  wundbetten: number;
  /** Verweiserpunkte bzw. Tropfbetten. */
  verweiserpunkte: number;
  /** Das Verfahren, das der Leitfaden vorsieht (der Fährtenschuh ist ein eigenes). */
  verfahren: readonly string[];
  maxMl: number;
  /** Mindest-Stehzeit in Stunden. */
  stehzeitStunden: number;
};

export const SCHWEISS_LEITFAEDEN: readonly SchweissLeitfaden[] = [
  { id: "vgp-tag", name: uebersetzbar("VGP Tagfährte"), seedKeys: ["JGHV VGP"], laengeMeter: 400, laengeArt: "ca", haken: 2, wundbetten: 1, verweiserpunkte: 0, verfahren: [VERFAHREN_TUPF, VERFAHREN_TROPF], maxMl: 250, stehzeitStunden: 2 },
  { id: "vgp-nacht", name: uebersetzbar("VGP Übernachtfährte"), seedKeys: ["JGHV VGP"], laengeMeter: 400, laengeArt: "ca", haken: 2, wundbetten: 1, verweiserpunkte: 0, verfahren: [VERFAHREN_TUPF, VERFAHREN_TROPF], maxMl: 250, stehzeitStunden: 14 },
  { id: "vswp-20", name: uebersetzbar("VSwP 20 h"), seedKeys: ["JGHV VSwP 20 h"], laengeMeter: 1000, laengeArt: "mind", haken: 3, wundbetten: 2, verweiserpunkte: 4, verfahren: [VERFAHREN_TUPF, VERFAHREN_TROPF], maxMl: 250, stehzeitStunden: 20 },
  { id: "vswp-40", name: uebersetzbar("VSwP 40 h"), seedKeys: ["JGHV VSwP 40 h"], laengeMeter: 1000, laengeArt: "mind", haken: 3, wundbetten: 2, verweiserpunkte: 4, verfahren: [VERFAHREN_TUPF, VERFAHREN_TROPF], maxMl: 250, stehzeitStunden: 40 },
  { id: "vfsp-20", name: uebersetzbar("VFsP 20 h"), seedKeys: ["JGHV VFsP 20 h"], laengeMeter: 1000, laengeArt: "mind", haken: 3, wundbetten: 2, verweiserpunkte: 4, verfahren: [VERFAHREN_FAEHRTENSCHUH], maxMl: 100, stehzeitStunden: 20 },
  { id: "vfsp-40", name: uebersetzbar("VFsP 40 h"), seedKeys: ["JGHV VFsP 40 h"], laengeMeter: 1000, laengeArt: "mind", haken: 3, wundbetten: 2, verweiserpunkte: 4, verfahren: [VERFAHREN_FAEHRTENSCHUH], maxMl: 100, stehzeitStunden: 40 },
];

export function schweissLeitfadenNachId(id: string | null | undefined): SchweissLeitfaden | null {
  return SCHWEISS_LEITFAEDEN.find((l) => l.id === id) ?? null;
}

/** Die Stufe, die vorbelegt wird: die des aktiven Ziels mit dem nächsten Termin, sonst die zuletzt gewählte, sonst keine. */
export function vorgabeSchweissLeitfaden(
  ziele: readonly Pick<Goal, "status" | "regulationName" | "targetDate">[] | null | undefined,
  gemerktId: string | null | undefined,
): SchweissLeitfaden | null {
  const treffer = (ziele ?? [])
    .filter((z) => z.status === 0)
    .sort((a, b) => a.targetDate.localeCompare(b.targetDate))
    .map((z) => SCHWEISS_LEITFAEDEN.find((l) => z.regulationName != null && l.seedKeys.includes(z.regulationName)))
    .find((l) => l !== undefined);
  return treffer ?? schweissLeitfadenNachId(gemerktId);
}

export type SchweissFortschritt = {
  meterIst: number;
  meterErreicht: boolean;
  wundbettenIst: number;
  wundbettenErreicht: boolean;
  verweiserIst: number;
  verweiserErreicht: boolean;
};

/** Stand beim Legen: gelaufene Meter (gerundet) und gesetzte Wundbetten und Verweiserpunkte. */
export function schweissFortschritt(
  leitfaden: SchweissLeitfaden,
  meter: number,
  wundbetten: number,
  verweiserpunkte: number,
): SchweissFortschritt {
  const meterIst = Math.round(meter);
  return {
    meterIst,
    meterErreicht: meterIst >= leitfaden.laengeMeter,
    wundbettenIst: wundbetten,
    wundbettenErreicht: wundbetten >= leitfaden.wundbetten,
    verweiserIst: verweiserpunkte,
    verweiserErreicht: verweiserpunkte >= leitfaden.verweiserpunkte,
  };
}

type TextUebersetzer = (text: string, werte?: Record<string, string | number>) => string;

/** "VSwP 20 h: mind. 1000 m · 3 Haken · 2 Wundbetten · 4 Verweiserpunkte · bis 250 ml · mind. 20 Std. Stehzeit" */
export function schweissLeitfadenZeile(l: SchweissLeitfaden, t: TextUebersetzer): string {
  const fuehrungName = l.verfahren.includes(VERFAHREN_FAEHRTENSCHUH) ? t("Tropfbetten") : t("Verweiserpunkte");
  const teile = [
    l.laengeArt === "mind" ? t("mind. {n} m", { n: l.laengeMeter }) : t("ca. {n} m", { n: l.laengeMeter }),
    t("{n} Haken", { n: l.haken }),
    l.wundbetten === 1 ? t("1 Wundbett") : t("{n} Wundbetten", { n: l.wundbetten }),
    l.verweiserpunkte > 0 ? `${l.verweiserpunkte} ${fuehrungName}` : null,
    t("bis {n} ml Schweiß", { n: l.maxMl }),
    t("mind. {n} Std. Stehzeit", { n: l.stehzeitStunden }),
  ].filter((x): x is string => x !== null);
  return `${t(l.name)}: ${teile.join(" · ")}`;
}

/** Schweißmenge aus dem Eingabefeld: null bei leerer Eingabe, undefined bei Ungültigem (keine ganze Zahl von 0 bis 1000). */
export function schweissMlAusText(text: string): number | null | undefined {
  const bereinigt = text.trim();
  if (bereinigt === "") return null;
  if (!/^\d{1,4}$/.test(bereinigt)) return undefined;
  const ml = Number(bereinigt);
  return ml <= MAX_SCHWEISS_ML ? ml : undefined;
}

/** Was vor dem Legen einer Schweißfährte gewählt wird - und mit der Aufzeichnung gesichert und gesendet wird. */
export type SchweissAngabenVorLegen = {
  verfahren: string | null;
  wildart: string | null;
  schweissMl: number | null;
  /** Mindest-Stehzeit des gewählten Leitfadens in Stunden. */
  stehzeitStunden: number | null;
  /** Der gewählte Leitfaden - nur, damit ein Fortsetzen den Fortschritt wieder zeigt. */
  leitfadenId: string | null;
};

export const LEERE_SCHWEISS_ANGABEN: SchweissAngabenVorLegen = {
  verfahren: null,
  wildart: null,
  schweissMl: null,
  stehzeitStunden: null,
  leitfadenId: null,
};

/** Wann die Stehzeit erreicht ist: Ende des Legens plus Stehzeit; null ohne Angabe. */
export function stehzeitErreichtAb(gelegtBis: Date, stehzeitStunden: number | null | undefined): Date | null {
  return stehzeitStunden == null ? null : new Date(gelegtBis.getTime() + stehzeitStunden * 3600_000);
}

// Nur auf diesem Gerät gemerkt: der Leitfaden des Hundes beim letzten Legen.
const gemerktSchluessel = (dogId: string) => `dogity.schweiss-leitfaden.${dogId}`;

export function schweissLeitfadenGemerkt(dogId: string): string | null {
  try {
    return window.localStorage.getItem(gemerktSchluessel(dogId));
  } catch {
    return null;
  }
}

export function schweissLeitfadenMerken(dogId: string, id: string | null): void {
  try {
    if (id) window.localStorage.setItem(gemerktSchluessel(dogId), id);
    else window.localStorage.removeItem(gemerktSchluessel(dogId));
  } catch {
    // Ohne Speicher bleibt es bei der Vorgabe aus dem Ziel.
  }
}

/** Das Verhalten am Stück (TrailIndication im Backend): Totverbellen = Verbellen (0), Totverweisen = Rückverweisen (4). */
export const STUECK_VERHALTEN = [
  { value: 0, label: uebersetzbar("Totverbellen") },
  { value: 4, label: uebersetzbar("Totverweisen") },
] as const;

export function stueckVerhaltenLabel(wert: number | null | undefined): string | null {
  return STUECK_VERHALTEN.find((v) => v.value === wert)?.label ?? null;
}

/**
 * Was beim Schließen des Ergebnis-Fensters an den Server geht, wenn sich "Zum Stück gekommen" oder das Verhalten
 * am Stück gegenüber dem Gespeicherten geändert hat (sonst null). Das Verhalten gilt nur bei "Zum Stück gekommen".
 */
export function stueckAenderung(
  run: { personFound?: boolean | null; indication?: number | null },
  eingabe: { gefunden: boolean | null; verhalten: number | null },
): { personFound: boolean | null; indication: number | null } | null {
  const verhalten = eingabe.gefunden === true ? eingabe.verhalten : null;
  return eingabe.gefunden !== (run.personFound ?? null) || verhalten !== (run.indication ?? null)
    ? { personFound: eingabe.gefunden, indication: verhalten }
    : null;
}

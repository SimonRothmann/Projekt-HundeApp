import { haversineMeters } from "@/lib/geo";

/**
 * Lücke im Ablauf: zwischen zwei aufeinanderfolgenden Punkten mehr als diese Zeit UND mehr als diese
 * Strecke (Bildschirm aus, das GPS lieferte nichts). Stehen am Gegenstand ergibt Zeit ohne Strecke,
 * langsames Gehen Strecke ohne Zeit - beides ist keine Lücke. Dieselbe Regel und dieselben Werte im
 * Backend (GpsTrackEvaluator.LueckeMinSekunden/LueckeMinMeter).
 */
export const LUECKE_MIN_SEKUNDEN = 30;
export const LUECKE_MIN_METER = 5;

type LueckenPunkt = { latitude: number; longitude: number; timestamp: string };

export function istLuecke(a: LueckenPunkt, b: LueckenPunkt): boolean {
  return lueckeSekunden(a, b) > LUECKE_MIN_SEKUNDEN && haversineMeters(a, b) > LUECKE_MIN_METER;
}

/** Zeit zwischen zwei Punkten in ganzen Sekunden. */
export function lueckeSekunden(a: LueckenPunkt, b: LueckenPunkt): number {
  return Math.round((Date.parse(b.timestamp) - Date.parse(a.timestamp)) / 1000);
}

/** "45 s" bzw. "3 Min." - für Hinweis und Kartentext. `minuten` übersetzt "{n} Min.". */
export function lueckeDauerText(sekunden: number, minuten: (n: number) => string): string {
  return sekunden < 60 ? `${sekunden} s` : minuten(Math.round(sekunden / 60));
}

/** Ein Stück einer Linie: durchgehend oder Lückenstück (der Punkt vor und der Punkt nach der Lücke). */
export type LinienStueck<T> = { punkte: T[]; luecke: boolean };

/**
 * Zerlegt eine Linie in durchgehende Stücke und Lückenstücke. `lueckeVor(i)` sagt, ob vor Punkt i eine
 * Lücke liegt - bei gespeicherten Abläufen das Feld des Servers (afterGap), bei der laufenden
 * Aufzeichnung die Regel auf den rohen Punkten.
 */
export function stueckeAnLuecken<T>(punkte: T[], lueckeVor: (i: number) => boolean): LinienStueck<T>[] {
  const stuecke: LinienStueck<T>[] = [];
  let aktuell: T[] = [];
  punkte.forEach((p, i) => {
    if (i > 0 && lueckeVor(i)) {
      stuecke.push({ punkte: aktuell, luecke: false }, { punkte: [punkte[i - 1], p], luecke: true });
      aktuell = [];
    }
    aktuell.push(p);
  });
  stuecke.push({ punkte: aktuell, luecke: false });
  return stuecke;
}

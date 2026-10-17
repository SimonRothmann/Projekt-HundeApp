import type { Regulation } from "@/lib/types";

/**
 * Welche Sportarten und Prüfungsordnungen eine Person je nach Land angeboten
 * bekommt - als reine Funktionen, damit die Regel an einer Stelle steht.
 *
 * Die Eingrenzung der Sportarten macht der Server (er kennt das gespeicherte
 * Land); hier steht nur, wie man sie bei der Katalogpflege aufhebt und wie
 * man Ordnungen für das Land filtert (siehe SportCatalogService).
 */

/** Alle Sportarten, ohne Eingrenzung nach Land - für Admin und Vereinskatalog-Pflege. */
export const SPORTARTEN_ALLE = "/api/sports?alle=true";

/** Die Sportarten-Adresse: mit `alle` ohne Eingrenzung nach Land. */
export function sportartenPfad(alle: boolean): string {
  return alle ? SPORTARTEN_ALLE : "/api/sports";
}

/** Die Ordnungen einer Sportart; mit Land nur die dort geltenden plus die international gültigen. */
export function ordnungenPfad(sportId: string, land: string | null): string {
  const basis = `/api/sports/${sportId}/regulations`;
  return land ? `${basis}?country=${encodeURIComponent(land)}` : basis;
}

/**
 * Ordnungen, die im Land gelten: ohne Länderkürzel (gilt überall) oder mit dem
 * Kürzel des Landes. `behalte` nimmt eine Ordnung aus, die trotzdem bleibt -
 * etwa die, die jemand über einen Link von einer Prüfungsordnungs-Seite
 * ausdrücklich angesteuert hat.
 */
export function ordnungenImLand(ordnungen: readonly Regulation[], land: string, behalte?: string): Regulation[] {
  return ordnungen.filter(
    (o) => o.id === behalte || o.countryCode == null || o.countryCode.toUpperCase() === land.toUpperCase(),
  );
}

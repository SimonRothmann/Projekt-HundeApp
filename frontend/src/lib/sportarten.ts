import { api } from "@/lib/api";
import type { Sport } from "@/lib/types";

/**
 * Die Sportarten-Liste (`/api/sports`, ohne `alle`) - pro Seitenaufruf nur
 * einmal geholt, statt von Dashboard, Profil, Hundeseite & Co. einzeln.
 *
 * Gemerkt wird das Promise: Gleichzeitige Aufrufe teilen sich einen Abruf.
 * Schlägt er fehl, wird es verworfen, der nächste Aufruf lädt neu.
 *
 * Die Liste hängt an der Person (Land, Vereinssportarten): Wer sich an- oder
 * abmeldet, das Land wechselt, einem Verein beitritt oder ihn verlässt oder
 * den Katalog ändert, ruft `sportartenCacheLeeren` auf. Was andere ändern
 * (etwa die Sportarten des Vereins), kommt spätestens nach fünf Minuten an -
 * die App bleibt auf dem Handy oft tagelang ohne Neuladen offen. Der Katalog
 * mit `?alle=true` (Pflege) läuft nicht hierüber.
 */
const GUELTIG_MS = 5 * 60_000;
let gemerkt: Promise<Sport[]> | null = null;
let geholtUm = 0;

export function ladeSportarten(): Promise<Sport[]> {
  if (!gemerkt || Date.now() - geholtUm > GUELTIG_MS) {
    const abruf = api.get<Sport[]>("/api/sports");
    gemerkt = abruf;
    geholtUm = Date.now();
    abruf.catch(() => {
      if (gemerkt === abruf) gemerkt = null;
    });
  }
  return gemerkt;
}

export function sportartenCacheLeeren(): void {
  gemerkt = null;
}

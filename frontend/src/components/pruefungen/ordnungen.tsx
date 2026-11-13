"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { ordnungenPfad } from "@/lib/katalog-land";
import { VORGABE_LAND } from "@/lib/i18n/laender";
import { usePreferences } from "@/lib/preferences-context";
import type { PruefungOrdnung, Regulation, Sport } from "@/lib/types";
import { Chip } from "@/components/ui/chip";

/** Eine Prüfungsordnung samt ihrer Sportart - die Auswahl gruppiert danach. */
export type OrdnungMitSport = PruefungOrdnung & { sportName: string; sportNameEn?: string | null };

// Je Land einmal pro Sitzung: Die Liste besteht aus einem Abruf je Sportart, das soll nicht bei jedem Öffnen eines Fensters neu laufen.
const gemerkt = new Map<string, OrdnungMitSport[]>();

/**
 * Die Prüfungsordnungen, aus denen die Person wählen kann - dieselbe Auswahl wie beim Anlegen eines
 * Ziels: die Sportarten der Person (ihr Land, die Sportarten ihrer Vereine) und deren Ordnungen im Land.
 * Geladen wird erst, wenn `aktiv` wahr ist (Fenster offen). `fehler`: der Abruf ist gescheitert.
 */
export function useOrdnungen(aktiv: boolean): { ordnungen: OrdnungMitSport[] | null; fehler: boolean } {
  const { preferences } = usePreferences();
  const land = preferences.country ?? VORGABE_LAND;
  const [geladen, setGeladen] = useState<{ land: string; ordnungen: OrdnungMitSport[] } | null>(null);
  const [fehler, setFehler] = useState(false);

  useEffect(() => {
    if (!aktiv || gemerkt.has(land)) return;
    let abgebrochen = false;
    (async () => {
      try {
        const sportarten = await api.get<Sport[]>("/api/sports");
        const jeSport = await Promise.all(sportarten.map((s) => api.get<Regulation[]>(ordnungenPfad(s.id, land))));
        if (abgebrochen) return;
        const liste = sportarten.flatMap((s, i) =>
          jeSport[i].map((r) => ({ id: r.id, name: r.name, nameEn: r.nameEn, sportName: s.name, sportNameEn: s.nameEn })),
        );
        gemerkt.set(land, liste);
        setGeladen({ land, ordnungen: liste });
        setFehler(false);
      } catch {
        if (!abgebrochen) setFehler(true);
      }
    })();
    return () => {
      abgebrochen = true;
    };
  }, [aktiv, land]);

  return { ordnungen: gemerkt.get(land) ?? (geladen?.land === land ? geladen.ordnungen : null), fehler };
}

/**
 * Auswahl von Ordnungen als Chips, nach Sportart gruppiert. Was ein Tipp bedeutet (hinzufügen, abwählen,
 * die eine wählen), entscheidet der Aufrufer in `onToggle`.
 */
export function OrdnungsWahl({
  ordnungen,
  gewaehlt,
  onToggle,
  gruppiert = true,
}: {
  ordnungen: readonly (PruefungOrdnung & { sportName?: string; sportNameEn?: string | null })[];
  gewaehlt: readonly string[];
  onToggle: (id: string) => void;
  /** Nach Sportart gruppieren; ohne Sportart (die Ordnungen eines Tags) steht alles in einem Block. */
  gruppiert?: boolean;
}) {
  const t = useT();
  const sprache = useSprache();

  const gruppen = new Map<string, { titel: string | null; eintraege: typeof ordnungen[number][] }>();
  for (const o of ordnungen) {
    const schluessel = gruppiert && o.sportName ? o.sportName : "";
    const gruppe = gruppen.get(schluessel) ?? {
      titel: gruppiert && o.sportName ? katalogName({ name: o.sportName, nameEn: o.sportNameEn }, sprache) : null,
      eintraege: [],
    };
    gruppe.eintraege.push(o);
    gruppen.set(schluessel, gruppe);
  }

  if (ordnungen.length === 0) return <p className="text-sm text-muted-foreground">{t("Keine Prüfungsordnungen gefunden.")}</p>;

  return (
    <div className="flex min-w-0 flex-col gap-3">
      {[...gruppen.entries()].map(([schluessel, gruppe]) => (
        <div key={schluessel} className="flex min-w-0 flex-col gap-1.5">
          {gruppe.titel && <p className="text-xs font-medium text-muted-foreground [overflow-wrap:anywhere]">{gruppe.titel}</p>}
          <div className="flex flex-wrap gap-2">
            {gruppe.eintraege.map((o) => (
              <Chip key={o.id} gewaehlt={gewaehlt.includes(o.id)} onClick={() => onToggle(o.id)}>
                {katalogName(o, sprache)}
              </Chip>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

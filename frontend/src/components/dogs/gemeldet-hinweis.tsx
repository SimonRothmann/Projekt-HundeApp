"use client";

import { useEffect, useState } from "react";
import { ClipboardCheck } from "lucide-react";
import { api } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { datumKurz } from "@/lib/pruefung";
import type { HundMeldung } from "@/lib/types";

/**
 * "Gemeldet: BH am 14.03.2027 (Hundesportverein Nord)" - solange die Prüfung ansteht. Die Prüfungsleitung
 * des Vereins trägt die Teams selbst ein; die Familie sieht hier, wofür ihr Hund gemeldet ist (und
 * bekommt dazu eine Benachrichtigung). Ohne Meldung, oder wenn der Abruf scheitert, bleibt die Stelle leer:
 * Es ist ein Hinweis, keine Voraussetzung.
 */
export function GemeldetHinweis({ dogId }: { dogId: string }) {
  const t = useT();
  const sprache = useSprache();
  const [meldungen, setMeldungen] = useState<HundMeldung[]>([]);

  useEffect(() => {
    let aktuell = true;
    api
      .get<HundMeldung[]>(`/api/dogs/${dogId}/club-exams`)
      .then((liste) => {
        if (aktuell) setMeldungen(liste);
      })
      .catch(() => {
        // Kein Hinweis ist besser als ein Fehler an einer Stelle, die niemand angefordert hat.
      });
    return () => {
      aktuell = false;
    };
  }, [dogId]);

  if (meldungen.length === 0) return null;

  return (
    <ul className="flex min-w-0 flex-col gap-2">
      {meldungen.map((m) => (
        <li
          key={m.entryId}
          className="flex min-w-0 items-start gap-2 rounded-lg border border-primary/25 bg-primary/6 px-3 py-2.5 text-sm dark:border-primary/35 dark:bg-primary/12"
        >
          <ClipboardCheck className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
          <span className="min-w-0 [overflow-wrap:anywhere]">
            {t("Gemeldet: {ordnung} am {datum} ({verein})", {
              ordnung: katalogName(m.regulation, sprache),
              datum: datumKurz(m.date, sprache),
              verein: m.clubName,
            })}
          </span>
        </li>
      ))}
    </ul>
  );
}

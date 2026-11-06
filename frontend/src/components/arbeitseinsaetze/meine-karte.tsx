"use client";

import { useEffect, useState } from "react";
import { HandHelping } from "lucide-react";
import { api } from "@/lib/api";
import { dauerText, stundenText } from "@/lib/arbeitseinsaetze";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { tagAlsDatum } from "@/lib/kalendertag";
import type { MeineArbeitseinsaetze } from "@/lib/types";

/** So viele Einsätze stehen gleich da, der Rest klappt auf. */
const VORSCHAU = 5;

/**
 * "Meine Arbeitseinsätze" auf der Vereinsseite: die Einsätze, bei denen die Verwaltung die Person abgehakt hat
 * (Datum, Titel, Dauer), und die Summe des laufenden Jahres. Nur, wenn es welche gibt - wer nie abgehakt wurde,
 * sieht nichts; ein Fehler beim Laden bleibt ebenfalls still, die Karte ist eine Zugabe. Abgehakt wird ohne
 * Benachrichtigung: Die Person erfährt es hier.
 */
export function MeineArbeitseinsaetzeKarte({ clubId }: { clubId: string }) {
  const t = useT();
  const sprache = useSprache();
  const [daten, setDaten] = useState<MeineArbeitseinsaetze | null>(null);

  useEffect(() => {
    let aktuell = true;
    api
      .get<MeineArbeitseinsaetze>(`/api/clubs/${clubId}/work-events/mine`)
      .then((d) => {
        if (aktuell) setDaten(d);
      })
      .catch(() => {
        // still: ohne Karte ist die Vereinsseite vollständig
      });
    return () => {
      aktuell = false;
    };
  }, [clubId]);

  if (!daten || daten.items.length === 0) return null;

  const zeile = (e: MeineArbeitseinsaetze["items"][number]) => (
    <li key={e.id} className="flex min-w-0 items-baseline justify-between gap-3 py-2 text-sm">
      <span className="min-w-0 [overflow-wrap:anywhere]">
        <span className="font-medium">{e.title}</span>
        <span className="block text-xs text-muted-foreground">
          {tagAlsDatum(e.date).toLocaleDateString(ortsformat(sprache), { day: "numeric", month: "long", year: "numeric" })}
        </span>
      </span>
      <span className="shrink-0 tabular-nums text-muted-foreground">{dauerText(e.minutes)}</span>
    </li>
  );
  const sichtbar = daten.items.slice(0, VORSCHAU);
  const rest = daten.items.slice(VORSCHAU);

  return (
    <section className="flex min-w-0 flex-col gap-2 rounded-md border px-3 py-3">
      <h3 className="flex items-center gap-2 text-sm font-medium">
        <HandHelping className="size-4 shrink-0 text-primary-text" aria-hidden />
        {t("Meine Arbeitseinsätze")}
      </h3>
      <p className="text-sm text-muted-foreground">
        {t("{jahr}: {einsaetze}, {stunden}", {
          jahr: daten.year,
          einsaetze: daten.yearEventCount === 1 ? t("1 Einsatz") : t("{n} Einsätze", { n: daten.yearEventCount }),
          stunden: stundenText(daten.yearMinutes, sprache),
        })}
      </p>
      <ul className="flex flex-col divide-y">{sichtbar.map(zeile)}</ul>
      {rest.length > 0 && (
        <details className="group">
          <summary className="flex min-h-11 cursor-pointer list-none items-center text-sm text-primary-text [&::-webkit-details-marker]:hidden">
            {t("Alle {n} anzeigen", { n: daten.items.length })}
          </summary>
          <ul className="flex flex-col divide-y">{rest.map(zeile)}</ul>
        </details>
      )}
    </section>
  );
}

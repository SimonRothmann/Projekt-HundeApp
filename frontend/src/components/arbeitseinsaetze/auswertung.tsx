"use client";

import { useEffect, useState } from "react";
import { Download } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { auswertungAlsCsv, csvDateiname, helferName, stundenText } from "@/lib/arbeitseinsaetze";
import { useSprache, useT } from "@/lib/i18n";
import type { ArbeitseinsatzAuswertung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Chip } from "@/components/ui/chip";

/**
 * Die Jahresauswertung (Verwaltung, Vorstand): je Person Einsätze und Stunden im Kalenderjahr, nach Stunden
 * sortiert, Personen ohne Dogity-Konto als eigene, gekennzeichnete Zeilen, ehemalige Mitglieder als eine Sammelzeile ohne Namen. Die Jahre, für die es Einsätze gibt, sind
 * wählbar; die CSV-Datei entsteht im Browser aus den geladenen Zahlen.
 */
export function Auswertung({ clubId }: { clubId: string }) {
  const t = useT();
  const sprache = useSprache();
  const [jahr, setJahr] = useState<number | null>(null);
  const [daten, setDaten] = useState<ArbeitseinsatzAuswertung | null>(null);
  const [fehler, setFehler] = useState(false);

  useEffect(() => {
    let aktuell = true;
    // Ohne Jahr wählt der Server das neueste mit Einsätzen.
    api
      .get<ArbeitseinsatzAuswertung>(`/api/clubs/${clubId}/work-events/summary${jahr === null ? "" : `?year=${jahr}`}`)
      .then((a) => {
        if (!aktuell) return;
        setDaten(a);
        setFehler(false);
      })
      .catch((err) => {
        if (!aktuell) return;
        setFehler(true);
        toast.error(err instanceof ApiError ? err.message : t("Die Auswertung konnte nicht geladen werden."));
      });
    return () => {
      aktuell = false;
    };
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clubId, jahr]);

  if (fehler && !daten) return <p className="text-sm text-muted-foreground">{t("Die Auswertung konnte nicht geladen werden.")}</p>;
  if (!daten) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  function herunterladen(a: ArbeitseinsatzAuswertung) {
    const datei = new Blob([auswertungAlsCsv(t, a, sprache)], { type: "text/csv;charset=utf-8" });
    const adresse = URL.createObjectURL(datei);
    const link = document.createElement("a");
    link.href = adresse;
    link.download = csvDateiname(a.year);
    // Safari lädt nur herunter, wenn das Element im Dokument hängt.
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(adresse);
  }

  return (
    <div className="flex min-w-0 flex-col gap-4">
      {daten.years.length > 0 && (
        <div role="group" aria-label={t("Jahr")} className="flex flex-wrap gap-2">
          {daten.years.map((j) => (
            <Chip key={j} gewaehlt={j === daten.year} onClick={() => setJahr(j)}>
              {j}
            </Chip>
          ))}
        </div>
      )}

      {daten.rows.length === 0 && !daten.former ? (
        <Card>
          <CardContent className="py-8 text-center text-muted-foreground">
            {t("Für {jahr} ist noch niemand abgehakt.", { jahr: daten.year })}
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent>
            <ul className="flex flex-col divide-y">
              {daten.rows.map((r, i) => (
                <Zeile key={i} name={helferName(t, r)} ohneKonto={r.guest} einsaetze={r.eventCount} minuten={r.minutes} sprache={sprache} />
              ))}
              {daten.former && <Zeile name={t("Ehemalige Mitglieder")} einsaetze={daten.former.eventCount} minuten={daten.former.minutes} sprache={sprache} gedaempft />}
            </ul>
          </CardContent>
        </Card>
      )}

      <p className="text-sm text-muted-foreground">
        {t("{jahr}: {einsaetze}, {helfer}, {stunden} Helferstunden insgesamt.", {
          jahr: daten.year,
          einsaetze: daten.eventCount === 1 ? t("1 Einsatz") : t("{n} Einsätze", { n: daten.eventCount }),
          helfer: daten.participationCount === 1 ? t("1 Teilnahme") : t("{n} Teilnahmen", { n: daten.participationCount }),
          stunden: stundenText(daten.totalMinutes, sprache),
        })}
      </p>

      <Button variant="outline" onClick={() => herunterladen(daten)} disabled={daten.eventCount === 0} className="self-start coarse:min-h-11">
        <Download className="size-4" />
        {t("Als CSV speichern")}
      </Button>
    </div>
  );
}

function Zeile({ name, ohneKonto, einsaetze, minuten, sprache, gedaempft }: { name: string; ohneKonto?: boolean; einsaetze: number; minuten: number; sprache: ReturnType<typeof useSprache>; gedaempft?: boolean }) {
  const t = useT();
  return (
    <li className="flex min-w-0 items-center gap-3 py-3 coarse:min-h-14">
      <span className={`min-w-0 flex-1 font-medium [overflow-wrap:anywhere] ${gedaempft ? "text-muted-foreground" : ""}`}>
        {name}
        {ohneKonto && <span className="ml-2 text-xs font-normal text-muted-foreground">{t("ohne Konto")}</span>}
      </span>
      <span className="shrink-0 text-right text-sm tabular-nums">
        <span className="block font-medium">{stundenText(minuten, sprache)}</span>
        <span className="block text-xs text-muted-foreground">
          {einsaetze === 1 ? t("1 Einsatz") : t("{n} Einsätze", { n: einsaetze })}
        </span>
      </span>
    </li>
  );
}

"use client";

import { useState, type ReactNode } from "react";
import { ChevronRight } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { Chip } from "@/components/ui/chip";
import { useT } from "@/lib/i18n";
import {
  betten,
  ergebnisseJeGegenstand,
  ergebnisUmschalten,
  GEGENSTANDS_ERGEBNISSE,
  gegenstaende,
  MATERIALIEN,
  materialUmschalten,
  type Gegenstand,
} from "@/lib/gegenstaende";
import type { GegenstandsErgebnis, GpsPoint, GpsWalkRun } from "@/lib/types";

/** Der Rahmen der beiden aufklappbaren Bereiche: wie "Winkel und Schenkel" in walk-run-evaluation.tsx. */
function Aufklapper({ titel, rechts, offen, children }: { titel: string; rechts?: string; offen?: boolean; children: ReactNode }) {
  return (
    <details open={offen} className="group min-w-0 rounded-md border border-input px-2.5 text-sm">
      <summary className="flex min-h-10 cursor-pointer list-none items-center justify-between gap-2 font-medium [&::-webkit-details-marker]:hidden">
        <span className="min-w-0 [overflow-wrap:anywhere]">{titel}</span>
        <span className="flex shrink-0 items-center gap-1.5">
          {rechts && <span className="text-xs font-normal text-muted-foreground">{rechts}</span>}
          <ChevronRight className="size-4 transition-transform group-open:rotate-90" aria-hidden />
        </span>
      </summary>
      {children}
    </details>
  );
}

/** "Gegenstand 1", bei Betten "Wundbett 1" / "Verweiserpunkt 2" (beim Fährtenschuh "Tropfbett 2"). */
function bezeichnung(g: Gegenstand, t: ReturnType<typeof useT>, tropfbett: boolean): string {
  if (g.art === 4) return t("Wundbett {nr}", { nr: g.nummer });
  if (g.art === 5) return tropfbett ? t("Tropfbett {nr}", { nr: g.nummer }) : t("Verweiserpunkt {nr}", { nr: g.nummer });
  return t("Gegenstand {nr}", { nr: g.nummer });
}

/** "Gegenstand 1" mit dem Label des Markers dahinter, falls er eines hat. */
function Gegenstandsname({ g, t, tropfbett = false }: { g: Gegenstand; t: ReturnType<typeof useT>; tropfbett?: boolean }) {
  return (
    <span className="min-w-0 font-medium [overflow-wrap:anywhere]">
      {bezeichnung(g, t, tropfbett)}
      {g.label ? ` · ${g.label}` : ""}
    </span>
  );
}

/**
 * Das Material der Gegenstände einer gelegten Fährte, eine Zeile je Gegenstand mit den Chips zum Antippen
 * (eine Wahl; erneut tippen wählt ab). Nicht beim Legen - dort bleibt es bei einem Tipp je Marker -,
 * sondern danach: direkt nach dem Speichern und später in den Fährtendetails. Jeder Tipp wird sofort
 * gespeichert; ohne Gegenstände mit Id (Fährte nicht gespeichert, älterer Stand) erscheint nichts.
 *
 * darfAendern: Das Material ändert, wer die Fährte bearbeiten darf; der Server prüft das ohnehin.
 */
export function GegenstandsMaterial({
  trackId,
  punkte,
  darfAendern = true,
  offen = false,
  onGeaendert,
}: {
  trackId: string;
  punkte: readonly GpsPoint[];
  darfAendern?: boolean;
  offen?: boolean;
  /** Mit dem gespeicherten Marker; der Aufrufer übernimmt ihn oder lädt neu. */
  onGeaendert: (punkt: GpsPoint) => void | Promise<void>;
}) {
  const t = useT();
  const [speichert, setSpeichert] = useState(false);
  const liste = gegenstaende(punkte);
  if (liste.length === 0) return null;

  async function waehlen(g: Gegenstand, material: string) {
    setSpeichert(true);
    try {
      const punkt = await api.put<GpsPoint>(`/api/gps-tracks/${trackId}/markers/${g.id}`, {
        material: materialUmschalten(g.material, material),
      });
      await onGeaendert(punkt);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das Material konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <Aufklapper titel={t("Gegenstände")} rechts={t("Material eintragen")} offen={offen}>
      <ul className="flex flex-col divide-y pb-1">
        {liste.map((g) => (
          <li key={g.id} className="flex min-w-0 flex-col gap-1.5 py-2">
            <Gegenstandsname g={g} t={t} />
            <div role="group" aria-label={t("Material von Gegenstand {nr}", { nr: g.nummer })} className="flex flex-wrap gap-1.5">
              {MATERIALIEN.map((m) => (
                <Chip key={m} kompakt haken gewaehlt={g.material === m} disabled={speichert || !darfAendern} onClick={() => void waehlen(g, m)}>
                  {t(m)}
                </Chip>
              ))}
            </div>
          </li>
        ))}
      </ul>
    </Aufklapper>
  );
}

/**
 * Je Gegenstand eine Zeile mit den drei Chips verwiesen / aufgenommen / überlaufen und daneben, dezent, was
 * GPS meint (erreicht = das Gespann kam auf 8 m heran). Keine Vorbelegung: Was der Hund getan hat, weiß nur
 * die Hundeführer:in. Die Chips brechen um, die Zeile bleibt bei 375 px ohne seitliches Scrollen.
 *
 * erreicht: die Marker-Ids, denen GPS nahe kam; ohne Angabe (älterer Stand) fehlt der Hinweis.
 */
export function GegenstandsErgebnisZeilen({
  liste,
  werte,
  erreicht,
  ohneAufzeichnung,
  disabled,
  tropfbett = false,
  onWahl,
}: {
  /** Beim Fährtenschuh heißt der Verweiserpunkt Tropfbett. */
  tropfbett?: boolean;
  liste: readonly Gegenstand[];
  werte: Readonly<Record<string, GegenstandsErgebnis | null | undefined>>;
  erreicht?: readonly string[] | null;
  /** Nicht erreichte Gegenstände, die in einer Lücke der Aufzeichnung liegen: kein "nicht erreicht", denn der Hund war wohl dort. */
  ohneAufzeichnung?: readonly string[] | null;
  disabled?: boolean;
  onWahl: (id: string, ergebnis: GegenstandsErgebnis | null) => void;
}) {
  const t = useT();
  return (
    <ul className="flex min-w-0 flex-col divide-y">
      {liste.map((g) => (
        <li key={g.id} className="flex min-w-0 flex-col gap-1.5 py-2">
          <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-sm">
            <Gegenstandsname g={g} t={t} tropfbett={tropfbett} />
            {g.material && <span className="[overflow-wrap:anywhere]">· {t(g.material)}</span>}
            {erreicht && (
              <span className="text-xs text-muted-foreground">
                {erreicht.includes(g.id)
                  ? t("GPS: erreicht")
                  : ohneAufzeichnung?.includes(g.id)
                    ? t("GPS: keine Aufzeichnung")
                    : t("GPS: nicht erreicht")}
              </span>
            )}
          </div>
          <div role="group" aria-label={t("Ergebnis von {name}", { name: bezeichnung(g, t, tropfbett) })} className="flex flex-wrap gap-1.5">
            {/* Ein Bett wird verwiesen oder überlaufen, nicht aufgenommen. */}
            {GEGENSTANDS_ERGEBNISSE.filter((e) => g.art == null || e.wert !== 1).map((e) => (
              <Chip
                key={e.wert}
                kompakt
                haken
                gewaehlt={werte[g.id] === e.wert}
                disabled={disabled}
                onClick={() => onWahl(g.id, ergebnisUmschalten(werte[g.id], e.wert))}
              >
                {t(e.label)}
              </Chip>
            ))}
          </div>
        </li>
      ))}
    </ul>
  );
}

/**
 * Die Angaben zu den Gegenständen eines Ablaufs in den Fährtendetails, nachträglich änderbar: dieselben
 * Zeilen wie im Ergebnis-Fenster, kompakt aufklappbar. Jeder Tipp wird sofort gespeichert.
 */
export function AblaufGegenstaende({
  trackId,
  punkte,
  run,
  darfAendern = true,
  schweiss = false,
  tropfbett = false,
  onSaved,
}: {
  /** Schweißfährte: die Betten statt der Gegenstände. */
  schweiss?: boolean;
  tropfbett?: boolean;
  trackId: string;
  punkte: readonly GpsPoint[];
  run: GpsWalkRun;
  darfAendern?: boolean;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [speichert, setSpeichert] = useState(false);
  const liste = schweiss ? betten(punkte) : gegenstaende(punkte);
  if (liste.length === 0) return null;

  const werte = ergebnisseJeGegenstand(run.articles);

  async function waehlen(id: string, ergebnis: GegenstandsErgebnis | null) {
    setSpeichert(true);
    try {
      await api.put(`/api/gps-tracks/${trackId}/walk-runs/${run.id}/articles`, [{ pointId: id, result: ergebnis }]);
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Angabe konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <Aufklapper
      titel={schweiss ? t("Ergebnis je Bett") : t("Ergebnis je Gegenstand")}
      rechts={t("{angegeben} von {gesamt} angegeben", { angegeben: Object.keys(werte).length, gesamt: liste.length })}
    >
      <GegenstandsErgebnisZeilen
        liste={liste}
        werte={werte}
        erreicht={run.reachedArticleIds}
        ohneAufzeichnung={run.unrecordedArticleIds}
        tropfbett={tropfbett}
        disabled={speichert || !darfAendern}
        onWahl={(id, ergebnis) => void waehlen(id, ergebnis)}
      />
    </Aufklapper>
  );
}

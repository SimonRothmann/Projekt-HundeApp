"use client";

import { useState } from "react";
import { toast } from "sonner";
import { ChevronDown, Pencil, Plus, Trash2 } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { GESUNDHEIT_ART, type Dog, type DogHealthRecord } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { AktionenMenue } from "@/components/ui/aktionen-menue";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Zuklappbar } from "@/components/ui/zuklappbar";
import { useRueckfrage } from "@/components/ui/rueckfrage";
import { gewichte, kgText, kurvenPunkte, parseKg, pruefeGewicht, veraenderung, veraenderungText } from "@/lib/gewicht";
import { datumKurz } from "@/lib/pruefung";
import { datumTagMonat } from "@/lib/pausen";
import { heuteIso } from "@/lib/kalendertag";
import { ortsformat } from "@/lib/ortsformat";
import { useSprache, useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Der Abschnitt "Gewicht" im Bereich Gesundheit: letzter Wert mit Veränderung, eine schlichte
 * Linienkurve (ab zwei Einträgen) und die Einträge zum Ändern und Löschen. Wägungen sind kein
 * Merkzettel mit Frist: Sie erscheinen nirgends unter "Als Nächstes" und lösen keine Hinweise aus.
 */
export function GewichtSection({
  dog,
  eintraege,
  onChanged,
}: {
  dog: Dog;
  eintraege: readonly DogHealthRecord[];
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();
  const rueckfrage = useRueckfrage();
  // null = kein Formular; "neu" = neue Wägung; sonst die, die bearbeitet wird.
  const [formular, setFormular] = useState<"neu" | DogHealthRecord | null>(null);
  const [alleOffen, setAlleOffen] = useState(false);

  const sortiert = gewichte(eintraege);
  const letzter = sortiert[sortiert.length - 1];
  const diff = veraenderung(sortiert);
  const neuesteZuerst = [...sortiert].reverse();

  async function nachAenderung() {
    setFormular(null);
    await onChanged();
  }

  async function loeschen(eintrag: DogHealthRecord) {
    if (!(await rueckfrage({ titel: t("Diesen Eintrag wirklich löschen?"), bestaetigen: t("Löschen"), gefaehrlich: true }))) return;
    try {
      await api.delete(`/api/dogs/${dog.id}/gesundheit/${eintrag.id}`);
      toast.success(t("Eintrag gelöscht."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    }
  }

  return (
    <Card size="sm" aria-label={t("Gewicht")}>
      <CardContent className="flex min-w-0 flex-col gap-3">
        <h3 className="text-sm font-medium">{t("Gewicht")}</h3>

        {letzter ? (
          <div className="flex min-w-0 flex-col gap-0.5">
            <p className="text-lg font-semibold [overflow-wrap:anywhere]">
              {t("{kg} · am {datum}", { kg: kgText(letzter.weightKg!, sprache), datum: datumTagMonat(letzter.date, sprache) })}
            </p>
            {diff !== null && (
              <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
                {t("{diff} zum vorigen Eintrag", { diff: veraenderungText(diff, sprache) })}
              </p>
            )}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{t("Noch kein Gewicht eingetragen.")}</p>
        )}

        {sortiert.length >= 2 && <Kurve sortiert={sortiert} />}

        {formular === null ? (
          <Button type="button" variant="outline" className="min-h-11 w-full" onClick={() => setFormular("neu")}>
            <Plus className="size-4" aria-hidden />
            {t("Gewicht eintragen")}
          </Button>
        ) : (
          <GewichtFormular
            key={formular === "neu" ? "neu" : formular.id}
            dog={dog}
            eintrag={formular === "neu" ? null : formular}
            onGespeichert={nachAenderung}
            onAbbrechen={() => setFormular(null)}
          />
        )}

        {neuesteZuerst.length > 0 && (
          <div className="flex min-w-0 flex-col">
            <button
              type="button"
              aria-expanded={alleOffen}
              onClick={() => setAlleOffen((offen) => !offen)}
              className="flex min-h-11 items-center gap-1 self-start rounded-md text-sm text-primary-text underline-offset-4 hover:underline"
            >
              <ChevronDown className={cn("size-4 shrink-0 transition-transform", !alleOffen && "-rotate-90")} aria-hidden />
              <span className="min-w-0 [overflow-wrap:anywhere]">{t("Alle Einträge ({n})", { n: neuesteZuerst.length })}</span>
            </button>
            <Zuklappbar offen={alleOffen} className="pt-1">
              <ul className="flex flex-col divide-y">
                {neuesteZuerst.map((e) => (
                  <li key={e.id} className="flex min-w-0 items-center gap-1 py-1">
                    <div className="flex min-w-0 flex-1 flex-col">
                      <span className="font-medium [overflow-wrap:anywhere]">{kgText(e.weightKg!, sprache)}</span>
                      <span className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{datumKurz(e.date, sprache)}</span>
                    </div>
                    <AktionenMenue
                      label={t("Aktionen für diesen Eintrag")}
                      aktionen={[
                        { label: t("Bearbeiten"), icon: Pencil, onSelect: () => setFormular(e) },
                        { label: t("Löschen"), icon: Trash2, onSelect: () => void loeschen(e), destruktiv: true },
                      ]}
                    />
                  </li>
                ))}
              </ul>
            </Zuklappbar>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// Zeichenfläche der Kurve in SVG-Einheiten; das SVG skaliert mit der Breite (viewBox), ohne Überlauf.
const B = 300;
const H = 110;
const RAND_L = 34;
const RAND_O = 8;
const RAND_U = 20;
const RAND_R = 10;

/** Die Linienkurve: Zeitachse nach Datum, Y-Achse nur über den Bereich der Werte (nicht ab 0). Nur eine Linie, deshalb reicht ein Ton. */
function Kurve({ sortiert }: { sortiert: DogHealthRecord[] }) {
  const t = useT();
  const sprache = useSprache();
  const punkte = kurvenPunkte(sortiert, B - RAND_L - RAND_R, H - RAND_O - RAND_U);
  const werte = sortiert.map((e) => e.weightKg!);
  const lo = Math.min(...werte);
  const hi = Math.max(...werte);
  const pfad = punkte.map((p) => `${p.x + RAND_L},${p.y + RAND_O}`).join(" ");
  const beschreibung = t("Gewichtskurve von {von} bis {bis}, zwischen {min} und {max}", {
    von: datumKurz(sortiert[0].date, sprache),
    bis: datumKurz(sortiert[sortiert.length - 1].date, sprache),
    min: kgText(lo, sprache),
    max: kgText(hi, sprache),
  });

  return (
    <svg viewBox={`0 0 ${B} ${H}`} className="h-auto w-full text-primary-text" role="img" aria-label={beschreibung}>
      <g className="fill-muted-foreground text-[10px]">
        <text x={RAND_L - 4} y={RAND_O + 3} textAnchor="end">{hi.toLocaleString(ortsformat(sprache), { maximumFractionDigits: 1 })}</text>
        <text x={RAND_L - 4} y={H - RAND_U + 3} textAnchor="end">{lo.toLocaleString(ortsformat(sprache), { maximumFractionDigits: 1 })}</text>
        <text x={RAND_L} y={H - 4} textAnchor="start">{datumTagMonat(sortiert[0].date, sprache)}</text>
        <text x={B - RAND_R} y={H - 4} textAnchor="end">{datumTagMonat(sortiert[sortiert.length - 1].date, sprache)}</text>
      </g>
      <line x1={RAND_L} y1={H - RAND_U} x2={B - RAND_R} y2={H - RAND_U} stroke="currentColor" className="opacity-20" />
      <polyline points={pfad} fill="none" stroke="currentColor" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
      {punkte.map((p) => (
        <circle key={p.eintrag.id} cx={p.x + RAND_L} cy={p.y + RAND_O} r={3} fill="currentColor" />
      ))}
    </svg>
  );
}

/** Datum (Vorgabe heute) und Gewicht in kg mit Komma-Eingabe; der Titel ist beim Gewicht fest und steht nicht im Formular. */
function GewichtFormular({
  dog,
  eintrag,
  onGespeichert,
  onAbbrechen,
}: {
  dog: Dog;
  eintrag: DogHealthRecord | null;
  onGespeichert: () => Promise<void>;
  onAbbrechen: () => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const kennung = eintrag?.id ?? "neu";
  const [datum, setDatum] = useState(eintrag?.date ?? heuteIso());
  const [kg, setKg] = useState(eintrag?.weightKg != null ? eintrag.weightKg.toLocaleString(ortsformat(sprache), { minimumFractionDigits: 1, maximumFractionDigits: 1, useGrouping: false }) : "");
  const [versucht, setVersucht] = useState(false);
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefeGewicht(t, { datum, kg });

  async function speichern() {
    setVersucht(true);
    if (fehler !== null) return;
    setSpeichert(true);
    try {
      const nutzlast = {
        kind: GESUNDHEIT_ART.Gewicht,
        title: "Gewicht",
        date: datum,
        time: null,
        dueDate: null,
        note: eintrag?.note ?? null,
        weightKg: parseKg(kg),
      };
      if (eintrag) await api.put(`/api/dogs/${dog.id}/gesundheit/${eintrag.id}`, nutzlast);
      else await api.post(`/api/dogs/${dog.id}/gesundheit`, nutzlast);
      toast.success(t("Eintrag gespeichert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Eintrag konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-4 rounded-md border border-primary/30 p-3">
      <p className="font-medium">{eintrag ? t("Gewicht bearbeiten") : t("Gewicht eintragen")}</p>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`gewicht-datum-${kennung}`}>{t("Gewogen am")}</Label>
        <Input
          id={`gewicht-datum-${kennung}`}
          type="date"
          required
          value={datum}
          onChange={(e) => setDatum(e.target.value)}
          className="h-11 min-w-0"
        />
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`gewicht-kg-${kennung}`}>{t("Gewicht in kg")}</Label>
        <Input
          id={`gewicht-kg-${kennung}`}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          maxLength={6}
          placeholder="24,3"
          value={kg}
          onChange={(e) => setKg(e.target.value)}
          className="h-11 min-w-0"
        />
      </div>

      {versucht && fehler && <p className="text-sm text-destructive">{fehler}</p>}

      <div className="flex flex-wrap gap-2">
        <Button type="button" className="min-h-11 flex-1" disabled={speichert} onClick={speichern}>
          {speichert ? t("Speichert…") : t("Speichern")}
        </Button>
        <Button type="button" variant="ghost" className="min-h-11" onClick={onAbbrechen}>
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

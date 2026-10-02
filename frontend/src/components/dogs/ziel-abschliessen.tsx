"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, NextStage } from "@/lib/types";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { datumKurz, morgenIso, pruefungsName, punkteText, vorgabePruefungstag } from "@/lib/pruefung";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useSprache, useT } from "@/lib/i18n";

/**
 * Die Felder eines Prüfungsergebnisses: Tag, Punkte, Notiz. Gemeinsam genutzt
 * vom Abschluss-Dialog hier und vom Nachtragen/Bearbeiten in der Karte
 * "Leistungen" - beide prüfen dieselben Grenzen wie der Server.
 *
 * Das Punktefeld gibt es nur, wenn die Prüfungsordnung eine Höchstpunktzahl
 * hat (BH/VT, Agility und Co. werden nicht nach Punkten gewertet) - oder wenn
 * schon ein Wert drinsteht, den man sonst nicht mehr korrigieren könnte.
 */
export type ErgebnisEingabe = { datum: string; punkte: string; notiz: string };

/** Fehlermeldung zur Eingabe, sonst null - die Regeln des Servers, damit man sie vor dem Senden sieht. */
export function pruefeErgebnis(
  t: ReturnType<typeof useT>,
  eingabe: ErgebnisEingabe,
  hoechstens: number | null | undefined,
): string | null {
  if (!eingabe.datum) return t("Bitte den Prüfungstag angeben.");
  if (eingabe.datum > morgenIso()) return t("Der Prüfungstag darf nicht in der Zukunft liegen.");
  const text = eingabe.punkte.trim();
  if (text === "") return null;
  const punkte = Number(text);
  if (!Number.isInteger(punkte) || punkte < 0) return t("Die Punktzahl muss eine ganze Zahl ab 0 sein.");
  if (hoechstens != null && punkte > hoechstens) return t("Die Punktzahl darf höchstens {max} betragen.", { max: hoechstens });
  return null;
}

export function ErgebnisFelder({
  eingabe,
  onChange,
  hoechstens,
  idPrefix,
}: {
  eingabe: ErgebnisEingabe;
  onChange: (neu: ErgebnisEingabe) => void;
  hoechstens: number | null | undefined;
  idPrefix: string;
}) {
  const t = useT();
  const zeigePunkte = hoechstens != null || eingabe.punkte !== "";

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`${idPrefix}-tag`}>{t("Prüfungstag")}</Label>
        <Input
          id={`${idPrefix}-tag`}
          type="date"
          required
          max={morgenIso()}
          value={eingabe.datum}
          onChange={(e) => onChange({ ...eingabe, datum: e.target.value })}
          className="min-w-0"
        />
      </div>
      {zeigePunkte && (
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor={`${idPrefix}-punkte`}>{t("Punkte")}</Label>
          <div className="flex items-center gap-2">
            <Input
              id={`${idPrefix}-punkte`}
              type="number"
              inputMode="numeric"
              min={0}
              max={hoechstens ?? undefined}
              value={eingabe.punkte}
              onChange={(e) => onChange({ ...eingabe, punkte: e.target.value })}
              className="w-28 min-w-0"
            />
            {hoechstens != null && (
              <span className="text-sm text-muted-foreground">{t("von {max}", { max: hoechstens })}</span>
            )}
            <span className="text-xs text-muted-foreground">{t("(optional)")}</span>
          </div>
        </div>
      )}
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`${idPrefix}-notiz`}>{t("Notiz")}</Label>
        <Input
          id={`${idPrefix}-notiz`}
          value={eingabe.notiz}
          maxLength={TEXTLAENGE.pruefungsNotiz}
          placeholder={t("(optional)")}
          onChange={(e) => onChange({ ...eingabe, notiz: e.target.value })}
          className="min-w-0"
        />
      </div>
    </div>
  );
}

/**
 * "Ziel abschließen": Die Prüfung ist gelaufen - bestanden oder nicht.
 *
 * Ersetzt das bloße "Als erreicht markieren", bei dem Tag, Ergebnis und Punkte
 * verloren gingen und ein Folgeziel von Hand angelegt werden musste.
 *
 * - Bestanden: Ziel erreicht, Tag/Punkte/Notiz werden gespeichert. Danach bietet
 *   der Dialog die Folgestufe(n) als neues Ziel an.
 * - Nicht bestanden: Das Ziel bleibt aktiv, die Person wählt einen neuen Termin
 *   (der Plan wird dafür vom Server verlängert). Es wird nichts als Leistung
 *   festgehalten.
 *
 * Nach "bestanden" meldet sich die Hundeseite erst beim Schließen neu: Das
 * Ziel wandert dort von den Plänen zu den Leistungen, und mit seiner Karte
 * verschwände sonst dieser Dialog, bevor man das Folgeziel sehen konnte.
 */
export function ZielAbschliessen({
  goal,
  hundName,
  onChanged,
  onFolgeziel,
}: {
  goal: Goal;
  hundName: string;
  onChanged: () => Promise<void>;
  onFolgeziel?: (stufe: NextStage) => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const [offen, setOffen] = useState(false);
  const [bestanden, setBestanden] = useState(true);
  const [eingabe, setEingabe] = useState<ErgebnisEingabe>({ datum: "", punkte: "", notiz: "" });
  const [neuerTermin, setNeuerTermin] = useState("");
  const [speichert, setSpeichert] = useState(false);
  // Gesetzt, sobald "bestanden" gespeichert ist: zeigt dann das Ergebnis und die Folgeziele.
  const [ergebnis, setErgebnis] = useState<Goal | null>(null);

  const pruefung = pruefungsName(goal);

  function oeffnen() {
    setBestanden(true);
    setEingabe({ datum: vorgabePruefungstag(goal.targetDate), punkte: "", notiz: "" });
    setNeuerTermin("");
    setErgebnis(null);
    setOffen(true);
  }

  async function schliessen() {
    setOffen(false);
    if (ergebnis) await onChanged();
  }

  const fehler = bestanden
    ? pruefeErgebnis(t, eingabe, goal.maxPoints)
    : neuerTermin === ""
      ? t("Bitte den neuen Prüfungstermin angeben.")
      : neuerTermin < morgenIso()
        ? t("Der neue Termin muss in der Zukunft liegen.")
        : null;

  async function speichern() {
    if (fehler) return;
    setSpeichert(true);
    try {
      if (bestanden) {
        const punkte = eingabe.punkte.trim();
        const aktualisiert = await api.post<Goal>(`/api/goals/${goal.id}/complete`, {
          passed: true,
          examDate: eingabe.datum,
          score: punkte === "" ? null : Number(punkte),
          note: eingabe.notiz.trim() || null,
          newTargetDate: null,
        });
        setErgebnis(aktualisiert);
      } else {
        await api.post<Goal>(`/api/goals/${goal.id}/complete`, {
          passed: false,
          examDate: null,
          score: null,
          note: null,
          newTargetDate: neuerTermin,
        });
        toast.success(t("Neuer Prüfungstermin gespeichert."));
        setOffen(false);
        await onChanged();
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ergebnis konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  async function folgezielAnlegen(stufe: NextStage) {
    onFolgeziel?.(stufe);
    setOffen(false);
    await onChanged();
  }

  const stufen = ergebnis?.nextStages ?? [];

  return (
    <>
      <Button size="sm" variant="outline" className="coarse:min-h-11" onClick={oeffnen}>
        {t("Ziel abschließen")}
      </Button>
      <Sheet open={offen} onOpenChange={(neu) => (neu ? setOffen(true) : void schliessen())}>
        <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto">
          {ergebnis ? (
            <>
              <SheetHeader>
                <SheetTitle className="flex items-center gap-2">
                  <CheckCircle2 className="size-5 shrink-0 text-emerald-600 dark:text-emerald-500" />
                  {t("Geschafft!")}
                </SheetTitle>
                <SheetDescription className="[overflow-wrap:anywhere]">
                  {[
                    t("{pruefung} bestanden", { pruefung }),
                    ergebnis.examScore != null ? punkteText(t, ergebnis.examScore, ergebnis.maxPoints) : null,
                    datumKurz(ergebnis.examDate ?? eingabe.datum, sprache),
                  ]
                    .filter(Boolean)
                    .join(" · ")}
                </SheetDescription>
              </SheetHeader>
              <div className="flex min-w-0 flex-col gap-2 p-4 pt-0">
                {stufen.length > 1 && <p className="text-sm text-muted-foreground">{t("Wie geht es weiter?")}</p>}
                {stufen.map((stufe, index) => (
                  <Button
                    key={stufe.regulationId}
                    variant={stufen.length === 1 || index === 0 ? "default" : "outline"}
                    className="h-auto min-h-9 whitespace-normal py-1.5 text-left coarse:min-h-11"
                    onClick={() => folgezielAnlegen(stufe)}
                  >
                    {t("{name} als Ziel anlegen", { name: stufe.name })}
                  </Button>
                ))}
                <Button
                  variant={stufen.length === 0 ? "default" : "ghost"}
                  className="coarse:min-h-11"
                  onClick={() => void schliessen()}
                >
                  {stufen.length === 0 ? t("Fertig") : t("Später")}
                </Button>
              </div>
            </>
          ) : (
            <>
              <SheetHeader>
                <SheetTitle>{t("Wie lief die Prüfung?")}</SheetTitle>
                <SheetDescription className="[overflow-wrap:anywhere]">
                  {t("{pruefung} mit {hund}", { pruefung, hund: hundName })}
                </SheetDescription>
              </SheetHeader>
              <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
                <div className="grid grid-cols-2 gap-2" role="group" aria-label={t("Ergebnis")}>
                  {[
                    { wert: true, label: t("Bestanden") },
                    { wert: false, label: t("Nicht bestanden") },
                  ].map(({ wert, label }) => (
                    <button
                      key={String(wert)}
                      type="button"
                      aria-pressed={bestanden === wert}
                      onClick={() => setBestanden(wert)}
                      className={cn(
                        "flex min-h-10 items-center justify-center rounded-md border px-2 text-sm font-medium transition-colors coarse:min-h-11",
                        bestanden === wert
                          ? "border-primary bg-primary/10 text-primary-text"
                          : "border-input text-muted-foreground hover:bg-muted",
                      )}
                    >
                      {label}
                    </button>
                  ))}
                </div>

                {bestanden ? (
                  <ErgebnisFelder eingabe={eingabe} onChange={setEingabe} hoechstens={goal.maxPoints} idPrefix="abschluss" />
                ) : (
                  <div className="flex min-w-0 flex-col gap-2">
                    <Label htmlFor="abschluss-termin">{t("Neues Prüfungsdatum")}</Label>
                    <Input
                      id="abschluss-termin"
                      type="date"
                      required
                      min={morgenIso()}
                      value={neuerTermin}
                      onChange={(e) => setNeuerTermin(e.target.value)}
                      className="min-w-0"
                    />
                  </div>
                )}

                {bestanden && (
                  <p className="text-xs text-muted-foreground">
                    {t("Nicht bestanden? Dann bleibt das Ziel aktiv und du wählst einen neuen Termin.")}
                  </p>
                )}
                {/* Ein noch leeres Pflichtfeld sperrt nur den Knopf - erst eine
                    falsche Eingabe bekommt einen Vorwurf. */}
                {fehler && (bestanden ? eingabe.datum : neuerTermin) !== "" && (
                  <p className="text-sm text-destructive">{fehler}</p>
                )}

                <Button disabled={speichert || fehler !== null} onClick={speichern} className="coarse:min-h-11">
                  {speichert ? t("Speichert…") : t("Ergebnis speichern")}
                </Button>
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}

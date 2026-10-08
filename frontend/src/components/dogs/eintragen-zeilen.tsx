"use client";

import { Check, ChevronDown, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Abschnittsname } from "@/components/dogs/eintragen-chip";
import { BEWERTUNGEN, type AufgeloesteZeile, type Bewertung, type EintragZeile, type Werte } from "@/lib/eintragen";
import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { Exercise } from "@/lib/types";
import { cn } from "@/lib/utils";

const BEWERTUNG_TEXT: Record<Bewertung, string> = {
  maessig: uebersetzbar("Mäßig"),
  gut: uebersetzbar("Gut"),
  top: uebersetzbar("Top"),
};

/**
 * "Gewählt": die Frage "Wie lief es?" und je gewählter Übung eine kompakte
 * Zeile (Name, ggf. "zählt für den Plan", genauer, Entfernen).
 *
 * Die Frage gilt für alle Zeilen; wer unter "genauer" etwas ändert,
 * überschreibt sie nur für diese Zeile (siehe EintragZeile.eigeneWerte).
 */
export function EintragenZeilen({
  zeilen,
  bewertung,
  onBewertung,
  offen,
  onUmschalten,
  onAendere,
  onWerte,
  onNeutral,
  onEntferne,
  uebungen,
}: {
  zeilen: readonly AufgeloesteZeile[];
  bewertung: Bewertung;
  onBewertung: (bewertung: Bewertung) => void;
  /** Kennungen der Zeilen, deren "genauer" aufgeklappt ist. */
  offen: ReadonlySet<string>;
  onUmschalten: (schluessel: string) => void;
  onAendere: (schluessel: string, aenderung: Partial<EintragZeile>) => void;
  onWerte: (schluessel: string, aenderung: Partial<Werte>) => void;
  /** Eigene Werte der Zeile verwerfen - sie folgt wieder der Frage oben. */
  onNeutral: (schluessel: string) => void;
  onEntferne: (schluessel: string) => void;
  /** Katalog-Übungen nach Kennung - für die Bewertungskriterien. */
  uebungen: ReadonlyMap<string, Exercise>;
}) {
  const t = useT();

  if (zeilen.length === 0) {
    return (
      <p className="rounded-lg border border-dashed px-3 py-3 text-sm text-muted-foreground">
        {t("Tippe oben auf die Übungen, die ihr gemacht habt.")}
      </p>
    );
  }

  return (
    <section className="flex flex-col gap-3" aria-labelledby="eintragen-gewaehlt">
      <Abschnittsname id="eintragen-gewaehlt">{t("Gewählt")}</Abschnittsname>

      <div className="flex flex-col gap-2">
        <p id="eintragen-wie-lief-es" className="text-base font-medium">
          {t("Wie lief es?")}
        </p>
        <div role="group" aria-labelledby="eintragen-wie-lief-es" className="grid grid-cols-3 gap-2">
          {BEWERTUNGEN.map((stufe) => (
            <button
              key={stufe}
              type="button"
              aria-pressed={bewertung === stufe}
              onClick={() => onBewertung(stufe)}
              className={cn(
                "min-h-12 rounded-xl border px-2 text-base font-medium transition-colors",
                bewertung === stufe
                  ? "border-primary bg-primary text-primary-foreground"
                  : "border-input text-foreground hover:border-primary/50 hover:bg-accent/30",
              )}
            >
              {t(BEWERTUNG_TEXT[stufe])}
            </button>
          ))}
        </div>
      </div>

      <ul className="flex flex-col gap-2">
        {zeilen.map(({ zeile, rating, success, plan }) => {
          const aufgeklappt = offen.has(zeile.schluessel);
          const kriterien = zeile.exerciseId ? uebungen.get(zeile.exerciseId)?.scoringCriteria : null;
          const name = zeile.name.trim() || t("Eigene Übung");
          return (
            <li key={zeile.schluessel} className="min-w-0 rounded-lg border border-surface-border bg-surface">
              <div className="flex items-center gap-1 pl-3">
                <div className="min-w-0 flex-1 py-2">
                  <p className="font-medium [overflow-wrap:anywhere]">{name}</p>
                  {/* Steht die Übung diese Woche offen im Plan, zählt der
                      Eintrag dafür - die Zeile sagt es und lässt es ausschalten
                      (z. B. für eine Übung, die nur zum Spaß lief). Bleibt
                      sichtbar, auch nach dem Abwählen, damit man es zurücknehmen
                      kann. */}
                  {plan && (
                    <p className="flex min-w-0 flex-wrap items-center gap-x-1 text-xs text-muted-foreground">
                      <Check
                        className={cn("size-3.5 shrink-0", plan.gezaehlt ? "text-emerald-600 dark:text-emerald-500" : "text-muted-foreground")}
                        aria-hidden
                      />
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        {plan.gezaehlt
                          ? t("zählt für den Plan · {erledigt}/{ziel} diese Woche", {
                              erledigt: plan.erledigt,
                              ziel: plan.item.repetitionsTarget,
                            })
                          : t("zählt nicht für den Plan")}
                      </span>
                      <button
                        type="button"
                        className="-my-1.5 min-h-11 px-2 text-xs font-medium text-primary-text underline-offset-4 hover:underline"
                        onClick={() => onAendere(zeile.schluessel, { planGeloest: plan.gezaehlt })}
                      >
                        {plan.gezaehlt ? t("nicht zählen") : t("doch zählen")}
                      </button>
                    </p>
                  )}
                </div>
                <button
                  type="button"
                  aria-expanded={aufgeklappt}
                  onClick={() => onUmschalten(zeile.schluessel)}
                  className="flex min-h-11 shrink-0 items-center gap-0.5 rounded-md px-2 text-xs text-muted-foreground hover:text-foreground"
                >
                  {t("genauer")}
                  <ChevronDown className={cn("size-3.5 transition-transform", aufgeklappt && "rotate-180")} aria-hidden />
                </button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="size-11 shrink-0"
                  aria-label={t("Entfernen")}
                  onClick={() => onEntferne(zeile.schluessel)}
                >
                  <X className="size-4" />
                </Button>
              </div>

              {aufgeklappt && (
                <div className="flex flex-col gap-3 border-t border-surface-border p-3">
                  {zeile.exerciseId === null && (
                    <div className="flex flex-col gap-1.5">
                      <Label htmlFor={`name-${zeile.schluessel}`} className="text-xs text-muted-foreground">
                        {t("Eigene Übung")}
                      </Label>
                      <Input
                        id={`name-${zeile.schluessel}`}
                        value={zeile.name}
                        maxLength={TEXTLAENGE.eigeneUebung}
                        className="h-11"
                        onChange={(e) => onAendere(zeile.schluessel, { name: e.target.value })}
                      />
                    </div>
                  )}

                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs text-muted-foreground">{t("Bewertung")}</span>
                    <div className="flex gap-1.5" role="group" aria-label={t("Bewertung, 1 bis 5")}>
                      {[1, 2, 3, 4, 5].map((wert) => (
                        <button
                          key={wert}
                          type="button"
                          onClick={() => onWerte(zeile.schluessel, { rating: wert })}
                          aria-label={t("{wert} von 5", { wert })}
                          aria-pressed={rating === wert}
                          className={cn(
                            "flex size-11 items-center justify-center rounded-md border text-sm",
                            rating >= wert ? "border-accent bg-accent text-accent-foreground" : "border-input text-muted-foreground",
                          )}
                        >
                          {wert}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* Die ganze Zeile ist die Tippfläche, nicht nur das winzige Kästchen. */}
                  <label className="flex min-h-11 items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      className="size-5 shrink-0 accent-primary"
                      checked={success}
                      onChange={(e) => onWerte(zeile.schluessel, { success: e.target.checked })}
                    />
                    {t("Erfolgreich")}
                  </label>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor={`notiz-${zeile.schluessel}`} className="text-xs text-muted-foreground">
                      {t("Kommentar zur Übung (optional)")}
                    </Label>
                    <Input
                      id={`notiz-${zeile.schluessel}`}
                      placeholder={t("z.B. Ablenkung durch Jogger, zweiter Versuch sauber")}
                      value={zeile.notes}
                      maxLength={TEXTLAENGE.uebungsNotiz}
                      className="h-11"
                      onChange={(e) => onAendere(zeile.schluessel, { notes: e.target.value })}
                    />
                  </div>

                  {kriterien && (
                    <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground [overflow-wrap:anywhere]">
                      <strong className="text-foreground">{t("Bewertungskriterien")}:</strong> {kriterien}
                    </p>
                  )}

                  {zeile.eigeneWerte && (
                    <Button type="button" variant="ghost" size="sm" className="min-h-11 self-start text-xs" onClick={() => onNeutral(zeile.schluessel)}>
                      {t("Wie oben übernehmen")}
                    </Button>
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

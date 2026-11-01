"use client";

import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import { AMPEL_WORT, ZUSTAND_WORT, ampelSatz } from "@/lib/pruefungsreife";
import { hundName, teamName } from "@/lib/pruefungen";
import type { PruefungTeam } from "@/lib/types";
import { cn } from "@/lib/utils";
import { AMPEL, ZUSTAND } from "@/components/dogs/pruefungsreife-karte";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Die Prüfungsreife eines Teams für die Prüfungsleitung: Ampel, Zählung und was noch fehlt - sonst
 * nichts. Kein Tagebuch, keine Notizen, keine Bewertungen, keine Theoriezeile (die bleibt der Person
 * selbst). Dieselbe Darstellung wie auf der Hundeseite (AMPEL, ZUSTAND), nur ohne Einzelheiten.
 */
export function ReifeFenster({ team, onOpenChange }: { team: PruefungTeam | null; onOpenChange: (offen: boolean) => void }) {
  const t = useT();
  const sprache = useSprache();
  const reife = team?.reife ?? null;
  const ampel = reife ? AMPEL[reife.ampel] : null;

  return (
    <Sheet open={team !== null && reife !== null} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{t("Prüfungsreife")}</SheetTitle>
          {team && (
            <SheetDescription className="[overflow-wrap:anywhere]">
              {t("{fuehrer} mit {hund}", { fuehrer: teamName(t, team), hund: hundName(team) })}
            </SheetDescription>
          )}
        </SheetHeader>
        {reife && (
          <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
            <div className={cn("flex min-w-0 items-start gap-3 rounded-lg border px-3 py-3", ampel?.klassen)}>
              {ampel && <ampel.symbol className="mt-0.5 size-8 shrink-0" aria-hidden />}
              <div className="flex min-w-0 flex-col gap-0.5">
                <p className="text-lg font-semibold">{t(AMPEL_WORT[reife.ampel])}</p>
                <p className="[overflow-wrap:anywhere]">{ampelSatz(t, { ...reife, theorie: null })}</p>
                <p className="text-sm">
                  {t("{sicher} sicher, {wackelig} wackelig, {offen} offen", { sicher: reife.sicher, wackelig: reife.wackelig, offen: reife.offen })}
                </p>
              </div>
            </div>

            {reife.fehlend.length > 0 && (
              <section className="flex min-w-0 flex-col gap-1.5">
                <h3 className="text-sm font-semibold">
                  {t("Das fehlt noch")} <span className="font-normal text-muted-foreground">({reife.fehlend.length})</span>
                </h3>
                <ul className="flex flex-col gap-1.5">
                  {reife.fehlend.map((u) => {
                    const { symbol: Zeichen, klassen } = ZUSTAND[u.zustand];
                    return (
                      <li key={u.exerciseId} className="flex min-w-0 items-start gap-2 rounded-lg border border-surface-border bg-surface px-3 py-2 text-sm">
                        <Zeichen className={cn("mt-0.5 size-4 shrink-0", klassen)} aria-hidden />
                        <div className="flex min-w-0 flex-1 flex-col">
                          <span className="font-medium [overflow-wrap:anywhere]">
                            {katalogName({ name: u.name, nameEn: u.nameEn }, sprache)}
                            {!u.isMandatory && <span className="font-normal text-muted-foreground"> · {t("Wahlübung")}</span>}
                          </span>
                          <span className={cn("text-xs font-medium", klassen)}>{t(ZUSTAND_WORT[u.zustand])}</span>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              </section>
            )}

            <p className="text-xs text-muted-foreground">
              {t("Das ist eine Faustregel aus dem Trainingstagebuch der Familie, kein Urteil der Richter:in. Du siehst nur Ampel und was noch fehlt – keine Einträge, Notizen oder Bewertungen.")}
            </p>
          </div>
        )}
      </BottomSheetContent>
    </Sheet>
  );
}

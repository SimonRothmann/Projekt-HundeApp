"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, NextStage } from "@/lib/types";
import { pruefungsName } from "@/lib/pruefung";
import { tageText } from "@/components/dogs/plan-edit-tools";
import { ExerciseWeightingSheet } from "@/components/dogs/exercise-weighting-sheet";
import { ZielAbschliessen } from "@/components/dogs/ziel-abschliessen";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { CheckCircle2, ChevronDown, MoreHorizontal, Pencil, SlidersHorizontal, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

const ZEILE =
  "flex min-h-12 w-full items-center gap-3 rounded-lg border border-surface-border bg-surface px-3 text-left text-sm font-medium transition-colors hover:bg-muted";

/**
 * "Ziel verwalten": alles, was das Ziel selbst betrifft und nicht das tägliche
 * Abhaken - Plan-Einstellungen anpassen, Übungen gewichten, das Prüfungsergebnis
 * eintragen und, abgesetzt, das Ziel beenden. Früher standen die Knöpfe dafür
 * als Zeile und als Knopfpaar mitten in der Plankarte, "Abbrechen" direkt neben
 * "Ziel abschließen".
 *
 * Die beiden Dialoge (Gewichten, Ergebnis) öffnen sich über diesem Menü. Wird
 * etwas geändert, schließt sich das Menü mit, bevor die Karte neu lädt.
 */
export function GoalManageSheet({
  goal,
  dogName,
  geplanteUebungen,
  onChanged,
  onFolgeziel,
}: {
  goal: Goal;
  dogName: string;
  /** Geplante Übungen der laufenden Woche; null in einer Pausenwoche oder ohne Plan. */
  geplanteUebungen: number | null;
  onChanged: () => Promise<void>;
  onFolgeziel?: (stufe: NextStage) => void;
}) {
  const t = useT();
  const [offen, setOffen] = useState(false);
  const [anpassenOffen, setAnpassenOffen] = useState(false);
  // Plan-Konfiguration (Übungen/Woche, Trainingstage) des adaptiven Generators.
  const [wochenUebungen, setWochenUebungen] = useState(goal.weeklyExerciseCount);
  const [tage, setTage] = useState(goal.trainingDaysPerWeek);
  const [speichert, setSpeichert] = useState(false);

  const aktiv = goal.status === 0;
  const name = pruefungsName(goal);
  // Plan-Einstellungen und Gewichtung gibt es nur für den adaptiven Plan -
  // individuelle Ziele legen ihre Übungen von Hand an.
  const adaptiv = aktiv && !goal.isCustom && goal.trainingPlan !== null;

  function oeffnen() {
    setWochenUebungen(goal.weeklyExerciseCount);
    setTage(goal.trainingDaysPerWeek);
    setAnpassenOffen(false);
    setOffen(true);
  }

  async function nachAenderung() {
    setOffen(false);
    await onChanged();
  }

  async function einstellungenSpeichern() {
    setSpeichert(true);
    try {
      await api.put(`/api/goals/${goal.id}/config`, { weeklyExerciseCount: wochenUebungen, trainingDaysPerWeek: tage });
      toast.success(t("Plan-Einstellungen gespeichert."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einstellungen konnten nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  // Erreicht wird ein Ziel über "Ergebnis eintragen" (mit Prüfungsergebnis),
  // nicht über den bloßen Statuswechsel - nur das Beenden läuft noch hierüber.
  // Status "abgebrochen" ändert sonst nichts: Der Plan und alle Einträge bleiben,
  // aber nur aktive Ziele werden wöchentlich angepasst und auf der Startseite
  // gezeigt. Eine Rückkehr in den Status "aktiv" bietet die App nicht an - darum
  // die ausdrückliche Rückfrage.
  async function beenden() {
    if (
      !window.confirm(
        t(
          "Ziel „{name}“ beenden? Der Plan bleibt hier sichtbar, wird aber nicht mehr fortgeführt und erscheint nicht mehr auf der Startseite. Das Ziel lässt sich danach nur noch löschen.",
          { name },
        ),
      )
    ) {
      return;
    }
    try {
      await api.put<Goal>(`/api/goals/${goal.id}/status`, { status: 2 });
      toast.success(t("Ziel beendet."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Status konnte nicht aktualisiert werden."));
    }
  }

  async function loeschen() {
    if (!window.confirm(t("Ziel inkl. Trainingsplan endgültig löschen? Bereits erfasste Trainingseinträge bleiben im Tagebuch erhalten."))) {
      return;
    }
    try {
      await api.delete(`/api/goals/${goal.id}`);
      toast.success(t("Ziel gelöscht."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ziel konnte nicht gelöscht werden."));
    }
  }

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="icon"
        className="size-11 shrink-0"
        aria-label={t("Ziel verwalten")}
        aria-haspopup="dialog"
        onClick={oeffnen}
      >
        <MoreHorizontal className="size-5" />
      </Button>
      <Sheet open={offen} onOpenChange={setOffen}>
        <SheetContent side="bottom" className="max-h-[85vh] overflow-y-auto pb-[max(1rem,env(safe-area-inset-bottom))]">
          <SheetHeader>
            <SheetTitle>{t("Ziel verwalten")}</SheetTitle>
            <SheetDescription className="[overflow-wrap:anywhere]">{t("{pruefung} mit {hund}", { pruefung: name, hund: dogName })}</SheetDescription>
          </SheetHeader>
          <div className="flex min-w-0 flex-col gap-2 px-4">
            {adaptiv && (
              <>
                <button type="button" className={ZEILE} aria-expanded={anpassenOffen} onClick={() => setAnpassenOffen((v) => !v)}>
                  <Pencil className="size-4 shrink-0" aria-hidden />
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span>{t("Anpassen")}</span>
                    {/* Der Generator plant nicht immer genau so viele Übungen, wie
                        als Wunsch eingestellt sind (kleiner Katalog, Mindestpensum
                        von vier) - deshalb "Zielwert" und, wo es abweicht, die
                        geplante Zahl der laufenden Woche dazu. */}
                    <span className="text-xs font-normal text-muted-foreground">
                      {goal.weeklyExerciseCount === 1
                        ? t("Zielwert: {anzahl} Übung/Woche", { anzahl: goal.weeklyExerciseCount })
                        : t("Zielwert: {anzahl} Übungen/Woche", { anzahl: goal.weeklyExerciseCount })}{" "}
                      · {tageText(t, goal.trainingDaysPerWeek)}
                      {geplanteUebungen != null &&
                        geplanteUebungen !== goal.weeklyExerciseCount &&
                        ` · ${t("diese Woche geplant: {anzahl}", { anzahl: geplanteUebungen })}`}
                    </span>
                  </span>
                  <ChevronDown className={cn("size-4 shrink-0 text-muted-foreground transition-transform", anpassenOffen && "rotate-180")} aria-hidden />
                </button>
                {anpassenOffen && (
                  <div className="flex flex-wrap items-end gap-3 rounded-md border bg-muted/30 p-3">
                    <div className="flex flex-col gap-1">
                      <Label className="text-xs">{t("Übungen/Woche")}</Label>
                      <Input type="number" min={1} max={12} className="w-20" value={wochenUebungen} onChange={(e) => setWochenUebungen(Number(e.target.value))} />
                    </div>
                    <div className="flex flex-col gap-1">
                      <Label className="text-xs">{t("Trainingstage")}</Label>
                      <Input type="number" min={1} max={7} className="w-20" value={tage} onChange={(e) => setTage(Number(e.target.value))} />
                    </div>
                    <div className="flex gap-2">
                      <Button type="button" size="sm" disabled={speichert} onClick={einstellungenSpeichern}>
                        {speichert ? t("Speichert…") : t("Speichern")}
                      </Button>
                      <Button type="button" size="sm" variant="ghost" onClick={() => setAnpassenOffen(false)}>
                        {t("Abbrechen")}
                      </Button>
                    </div>
                  </div>
                )}
                <ExerciseWeightingSheet
                  goalId={goal.id}
                  ausloeser={(oeffnenGewichten) => (
                    <button type="button" className={ZEILE} onClick={oeffnenGewichten}>
                      <SlidersHorizontal className="size-4 shrink-0" aria-hidden />
                      {t("Übungen gewichten")}
                    </button>
                  )}
                />
              </>
            )}

            {aktiv && (
              <ZielAbschliessen
                goal={goal}
                hundName={dogName}
                onChanged={nachAenderung}
                onFolgeziel={onFolgeziel}
                ausloeser={(oeffnenErgebnis) => (
                  <button type="button" className={ZEILE} onClick={oeffnenErgebnis}>
                    <CheckCircle2 className="size-4 shrink-0" aria-hidden />
                    {t("Ergebnis eintragen")}
                  </button>
                )}
              />
            )}

            {/* Beenden und Löschen: klar abgesetzt, beides mit Rückfrage. */}
            <div className="mt-2 flex flex-col gap-2 rounded-lg border border-destructive/30 p-3">
              <p className="text-sm text-muted-foreground">
                {aktiv
                  ? t("Wurde keine Prüfung abgelegt, beendest du das Ziel hier. Der Plan bleibt im Verlauf, wird aber nicht mehr fortgeführt.")
                  : t("Entfernt das Ziel samt Trainingsplan. Bereits erfasste Trainingseinträge bleiben im Tagebuch erhalten.")}
              </p>
              <Button
                type="button"
                variant="outline"
                className="w-full text-destructive hover:text-destructive"
                onClick={aktiv ? beenden : loeschen}
              >
                <Trash2 className="size-4" aria-hidden />
                {aktiv ? t("Ziel beenden ohne Ergebnis") : t("Ziel löschen")}
              </Button>
            </div>
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}

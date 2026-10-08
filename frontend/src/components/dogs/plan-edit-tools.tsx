"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, Goal, TrainingPlanItem } from "@/lib/types";
import { planItemName, trainingstageDerWoche } from "@/lib/trainingsplan";
import { PlanAddForm } from "@/components/dogs/plan-item-forms";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Pencil, Plus, RefreshCw, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Die Werkzeuge der Plankarte im Bearbeitungsmodus. Im Lesemodus (Standard)
 * kommt nichts davon auf den Bildschirm - die Karte soll dort zum Abhaken
 * einladen und nicht zum Umbauen.
 */

export function tageText(t: ReturnType<typeof useT>, anzahl: number): string {
  return anzahl === 1 ? t("1 Trainingstag") : t("{n} Trainingstage", { n: anzahl });
}

/**
 * Werkzeugzeile einer Woche: Trainingstage nur für diese Woche, adaptives
 * Neu-Generieren und eine Übung direkt in diese Woche.
 */
export function WochenWerkzeuge({
  goal,
  weekNumber,
  uebungen,
  onChanged,
}: {
  goal: Goal;
  weekNumber: number;
  uebungen: Exercise[] | null;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const [tageBearbeiten, setTageBearbeiten] = useState(false);
  const [tageEntwurf, setTageEntwurf] = useState(2);
  const [speichertTage, setSpeichertTage] = useState(false);
  const [generiert, setGeneriert] = useState(false);
  const [hinzufuegen, setHinzufuegen] = useState(false);

  function tageStart() {
    setTageEntwurf(trainingstageDerWoche(goal, weekNumber));
    setTageBearbeiten(true);
  }

  async function tageSpeichern() {
    setSpeichertTage(true);
    try {
      await api.put(`/api/goals/${goal.id}/weeks/${weekNumber}/config`, { trainingDaysPerWeek: tageEntwurf });
      toast.success(t("Trainingstage für Woche {nummer} gespeichert.", { nummer: weekNumber }));
      setTageBearbeiten(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Trainingstage konnten nicht gespeichert werden."));
    } finally {
      setSpeichertTage(false);
    }
  }

  // Adaptive Neugenerierung einer Woche (siehe docs/SMART_TRAINING_PLAN.md):
  // ersetzt nur fortschrittslose Auto-Übungen, manuelle Einträge und bereits
  // trainierte Übungen bleiben erhalten.
  async function neuGenerieren() {
    setGeneriert(true);
    try {
      await api.put(`/api/goals/${goal.id}/regenerate-week`, { weekNumber });
      toast.success(t("Woche {nummer} neu generiert.", { nummer: weekNumber }));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Woche konnte nicht neu generiert werden."));
    } finally {
      setGeneriert(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
        {/* Pro-Woche abweichende Trainingstage (überschreibt den Plan-Standard
            nur für diese Woche). */}
        {tageBearbeiten ? (
          <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
            <span>{t("Trainingstage:")}</span>
            <Input
              type="number"
              min={1}
              max={7}
              className="w-16"
              value={tageEntwurf}
              onChange={(e) => setTageEntwurf(Number(e.target.value))}
            />
            <Button type="button" size="sm" disabled={speichertTage} onClick={tageSpeichern}>
              {speichertTage ? "…" : "OK"}
            </Button>
            <Button type="button" size="sm" variant="ghost" onClick={() => setTageBearbeiten(false)}>
              {t("Abbrechen")}
            </Button>
          </div>
        ) : (
          <Button
            type="button"
            size="sm"
            variant="ghost"
            className="text-xs text-muted-foreground"
            onClick={tageStart}
            title={t("Trainingstage dieser Woche anpassen")}
          >
            {tageText(t, trainingstageDerWoche(goal, weekNumber))}
            <Pencil className="size-3" />
          </Button>
        )}
        <div className="flex items-center gap-1">
          {!goal.isCustom && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              className="text-xs"
              disabled={generiert}
              onClick={neuGenerieren}
              title={t("Diese Woche adaptiv neu generieren (erhält manuelle & bereits trainierte Übungen)")}
            >
              <RefreshCw className={cn("size-3", generiert && "animate-spin")} />
              {generiert ? t("Generiere…") : t("Neu generieren")}
            </Button>
          )}
          <Button type="button" size="sm" variant="ghost" className="text-xs" onClick={() => setHinzufuegen((v) => !v)}>
            <Plus className="size-3" />
            {t("Übung")}
          </Button>
        </div>
      </div>
      {hinzufuegen && (
        <PlanAddForm
          goal={goal}
          uebungen={uebungen}
          startWoche={weekNumber}
          mitWochenfeld={false}
          onAdded={async () => {
            setHinzufuegen(false);
            await onChanged();
          }}
          onCancel={() => setHinzufuegen(false)}
        />
      )}
    </div>
  );
}

/**
 * Stift und Entfernen einer Planübung. Entfernt wird erst nach einer Rückfrage:
 * Ein Tipp daneben soll keine Übung samt Ziel verschwinden lassen. Bewusst
 * keine Rückgängig-Meldung statt der Rückfrage - die Übung ließe sich nur neu
 * anlegen, ihre bisherigen Einträge hingen dann nicht mehr an ihr, und der
 * Fortschritt der Woche stünde wieder bei null.
 */
export function ItemWerkzeuge({
  goalId,
  item,
  onEdit,
  onChanged,
}: {
  goalId: string;
  item: TrainingPlanItem;
  onEdit: () => void;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const name = planItemName(item) ?? t("(Übung nicht mehr verfügbar)");

  async function entfernen() {
    const frage = t("Übung „{name}“ aus dem Plan entfernen?", { name });
    const mitEintraegen = item.completedCount > 0 ? ` ${t("Bereits eingetragene Trainings bleiben im Tagebuch erhalten.")}` : "";
    if (!window.confirm(frage + mitEintraegen)) return;
    try {
      await api.delete(`/api/goals/${goalId}/plan-items/${item.id}`);
      toast.success(t("Übung aus dem Plan entfernt."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Übung konnte nicht entfernt werden."));
    }
  }

  return (
    <div className="flex shrink-0 gap-0.5">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="size-11"
        onClick={onEdit}
        aria-label={t("Übung, Woche oder Zielwert bearbeiten")}
        title={t("Übung, Woche oder Zielwert bearbeiten")}
      >
        <Pencil className="size-4 text-muted-foreground" />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="size-11"
        onClick={entfernen}
        aria-label={t("Aus dem Plan entfernen")}
        title={t("Aus dem Plan entfernen")}
      >
        <Trash2 className="size-4 text-muted-foreground" />
      </Button>
    </div>
  );
}

/** "Übung hinzufügen (freie Woche)": eine Übung in eine frei gewählte Woche, mit Wochenfeld. */
export function FreieUebungHinzufuegen({
  goal,
  uebungen,
  onChanged,
}: {
  goal: Goal;
  uebungen: Exercise[] | null;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const [offen, setOffen] = useState(false);

  return (
    <>
      <Button type="button" size="sm" variant="outline" className="self-start" onClick={() => setOffen((v) => !v)}>
        <Plus className="size-4" />
        {t("Übung hinzufügen (freie Woche)")}
      </Button>
      {offen && (
        <PlanAddForm
          goal={goal}
          uebungen={uebungen}
          startWoche={1}
          mitWochenfeld
          onAdded={async () => {
            setOffen(false);
            await onChanged();
          }}
          onCancel={() => setOffen(false)}
        />
      )}
    </>
  );
}

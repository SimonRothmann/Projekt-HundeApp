"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, NextStage } from "@/lib/types";
import { computeCurrentWeek, groupByWeek, istPausenwoche } from "@/lib/trainingsplan";
import { pruefungsName, restzeitText, tageBisPruefung } from "@/lib/pruefung";
import { GoalManageSheet } from "@/components/dogs/goal-manage-sheet";
import { PlanWeekList } from "@/components/dogs/plan-week-list";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Check, Pencil, UserCog } from "lucide-react";
import { toast } from "sonner";

import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { uebersetzbar } from "@/lib/i18n/sprachen";

const statusLabel: Record<Goal["status"], string> = { 0: uebersetzbar("Aktiv"), 1: uebersetzbar("Erreicht"), 2: uebersetzbar("Beendet") };
const statusVariant: Record<Goal["status"], "default" | "secondary" | "outline"> = { 0: "default", 1: "secondary", 2: "outline" };

/**
 * Ein einzelnes Ziel mit seinem Wochenplan.
 *
 * Die Karte ist standardmäßig zum Lesen und Abhaken da: Prüfung und Restzeit,
 * die laufende Woche mit Fortschritt und die Übungen, ein Tipp öffnet den
 * Schnelleintrag. Umbauen (Trainingstage, Neu generieren, Übungen ändern oder
 * entfernen) steht hinter "Plan bearbeiten", alles am Ziel selbst hinter dem
 * Menü "Ziel verwalten". Früher stand die Karte dauerhaft im Bearbeitungsmodus -
 * mit mehr als zwanzig Tippzielen für vier Übungen.
 *
 * Jede Karte hält ihren eigenen Zustand, es gibt keine ziel-übergreifende
 * Kopplung. onChanged lädt die Ziele der Seite neu, sobald sich am
 * Plan/Fortschritt etwas ändert.
 */
export function GoalPlanCard({
  goal,
  dogId,
  dogName,
  onChanged,
  onFolgeziel,
}: {
  goal: Goal;
  dogId: string;
  dogName: string;
  onChanged: () => Promise<void>;
  onFolgeziel?: (stufe: NextStage) => void;
}) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const [bearbeiten, setBearbeiten] = useState(false);
  const [switchingAuto, setSwitchingAuto] = useState(false);

  const aktiv = goal.status === 0;
  const weeks = goal.trainingPlan ? groupByWeek(goal.trainingPlan.items) : [];
  // Aktuelle Trainingswoche kalendarisch bestimmen: Woche 1 beginnt mit der
  // Plan-Erstellung (generatedAt), danach zählt jede angebrochene 7-Tage-Woche
  // hoch. Nur die aktuelle Woche ist standardmäßig aufgeklappt (nicht immer
  // Woche 1). Auf die tatsächlich vorhandenen Wochennummern begrenzt.
  const currentWeek = computeCurrentWeek(weeks, goal.trainingPlan?.generatedAt);
  // Geplante Übungen der laufenden Woche; null in einer Pausenwoche oder ohne Plan.
  const wocheItems = weeks.find(([nummer]) => nummer === currentWeek)?.[1] ?? [];
  const geplanteUebungen = wocheItems.length > 0 && !istPausenwoche(wocheItems) ? wocheItems.length : null;

  // Hat eine Trainer:in den Plan in der Hand, baut der Generator ihn nicht mehr
  // wöchentlich um - sonst würde er die Absicht hinter dem Plan Woche für Woche
  // unterlaufen. Zurückschalten darf der Besitzer: es ist sein Hund.
  async function enableAutoRegeneration() {
    setSwitchingAuto(true);
    try {
      await api.put(`/api/goals/${goal.id}/plan-auto-regeneration`, { enabled: true });
      toast.success(t("Automatische Anpassung wieder eingeschaltet."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Konnte nicht umgeschaltet werden."));
    } finally {
      setSwitchingAuto(false);
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-2 space-y-0">
        <div className="min-w-0">
          <CardTitle className="text-base break-words">{pruefungsName(goal)}</CardTitle>
          <p className="text-sm text-muted-foreground">
            {t("Ziel: {datum}", { datum: new Date(goal.targetDate).toLocaleDateString(ort) })}
            {aktiv && ` · ${restzeitText(t, tageBisPruefung(goal.targetDate))}`}
          </p>
          {!aktiv && (
            <Badge className="mt-1" variant={statusVariant[goal.status]}>
              {t(statusLabel[goal.status])}
            </Badge>
          )}
        </div>
        <GoalManageSheet goal={goal} dogName={dogName} geplanteUebungen={geplanteUebungen} onChanged={onChanged} onFolgeziel={onFolgeziel} />
      </CardHeader>
      <CardContent className="flex min-w-0 flex-col gap-3">
        {goal.notes && <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{goal.notes}</p>}

        {goal.planManagedByTrainer && (
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md bg-muted px-3 py-2 text-xs">
            <UserCog className="size-3.5 shrink-0 text-muted-foreground" />
            <span className="min-w-0">{t("Dein Trainer betreut diesen Plan – er wird nicht mehr automatisch angepasst.")}</span>
            <Button type="button" size="sm" variant="ghost" className="text-xs" disabled={switchingAuto} onClick={enableAutoRegeneration}>
              {switchingAuto ? t("Schaltet um…") : t("Automatik wieder einschalten")}
            </Button>
          </div>
        )}

        <PlanWeekList
          goal={goal}
          dogId={dogId}
          weeks={weeks}
          currentWeek={currentWeek}
          bearbeiten={bearbeiten}
          onChanged={onChanged}
        />

        {aktiv && (
          <Button
            type="button"
            size="sm"
            variant={bearbeiten ? "default" : "outline"}
            className="self-start"
            aria-pressed={bearbeiten}
            onClick={() => setBearbeiten((v) => !v)}
          >
            {bearbeiten ? <Check className="size-4" /> : <Pencil className="size-4" />}
            {bearbeiten ? t("Fertig") : t("Plan bearbeiten")}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

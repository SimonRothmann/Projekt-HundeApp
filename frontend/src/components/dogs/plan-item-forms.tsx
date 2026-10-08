"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, Goal, TrainingPlanItem } from "@/lib/types";
import { trainingstageDerWoche } from "@/lib/trainingsplan";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Die Übungen der Ziel-Sportart für die Auswahl beim Hinzufügen und Bearbeiten.
 * Geladen wird erst, wenn die Plankarte in den Bearbeitungsmodus geht - im
 * Lesemodus braucht sie niemand. null heißt: noch nicht (oder gerade) geladen.
 */
export function useUebungKatalog(sportId: string, aktiv: boolean): Exercise[] | null {
  const t = useT();
  const [uebungen, setUebungen] = useState<Exercise[] | null>(null);

  useEffect(() => {
    if (!aktiv || uebungen !== null) return;
    let abgebrochen = false;
    api
      .get<Exercise[]>(`/api/sports/${sportId}/exercises`)
      .then((daten) => {
        if (!abgebrochen) setUebungen(daten);
      })
      .catch((err) => {
        if (abgebrochen) return;
        toast.error(err instanceof ApiError ? err.message : t("Übungen konnten nicht geladen werden."));
        setUebungen([]);
      });
    return () => {
      abgebrochen = true;
    };
  }, [aktiv, sportId, uebungen, t]);

  return uebungen;
}

/**
 * Katalog-Übung oder Freitext: Schalter, Eingabe und Auswahl, die Hinzufügen
 * und Bearbeiten gemeinsam haben.
 */
function UebungFelder({
  frei,
  onFrei,
  uebungId,
  onUebungId,
  text,
  onText,
  uebungen,
  kompakt,
}: {
  frei: boolean;
  onFrei: (frei: boolean) => void;
  uebungId: string;
  onUebungId: (id: string) => void;
  text: string;
  onText: (text: string) => void;
  uebungen: Exercise[] | null;
  kompakt: boolean;
}) {
  const t = useT();
  return (
    <>
      <label className={cn("flex min-h-11 items-center gap-2", kompakt ? "text-xs" : "text-sm")}>
        <input type="checkbox" className="size-4 accent-primary" checked={frei} onChange={(e) => onFrei(e.target.checked)} />
        <span>{kompakt ? t("Freitext-Übung") : t("Freitext-Übung (nicht aus dem Katalog)")}</span>
      </label>
      <div className="flex min-w-0 flex-col gap-2">
        <Label className={kompakt ? "text-xs" : undefined}>{frei ? t("Freitext") : t("Übung")}</Label>
        {frei ? (
          <Input
            value={text}
            onChange={(e) => onText(e.target.value)}
            placeholder={t("z.B. Kopfarbeit ausprobieren")}
            maxLength={150}
          />
        ) : (
          <Select value={uebungId} onValueChange={(wert) => onUebungId(wert ?? "")}>
            <SelectTrigger>
              <SelectValue placeholder={t("Auswählen…")} />
            </SelectTrigger>
            {/* max-h-[60vh] + touch-pan-y: Base-UI errechnet die max-height
                aus der Trigger-Position; bei weit unten sitzendem Trigger
                auf iOS Safari wird das zu klein und wirkt nicht scrollbar. */}
            <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
              {(uebungen ?? []).map((ex) => (
                <SelectItem key={ex.id} value={ex.id}>
                  {ex.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>
    </>
  );
}

/**
 * Übung zum Plan hinzufügen: zentral mit Wochenfeld ("Übung hinzufügen (freie
 * Woche)") oder direkt in einer Woche ohne.
 */
export function PlanAddForm({
  goal,
  uebungen,
  startWoche,
  mitWochenfeld,
  onAdded,
  onCancel,
}: {
  goal: Goal;
  uebungen: Exercise[] | null;
  startWoche: number;
  mitWochenfeld: boolean;
  onAdded: () => Promise<void>;
  onCancel: () => void;
}) {
  const t = useT();
  const [frei, setFrei] = useState(false);
  const [uebungId, setUebungId] = useState("");
  const [text, setText] = useState("");
  const [woche, setWoche] = useState(startWoche);
  const [ziel, setZiel] = useState(2);
  const [tag, setTag] = useState(1);
  const [speichert, setSpeichert] = useState(false);

  async function hinzufuegen() {
    if (frei ? !text.trim() : !uebungId) {
      toast.error(frei ? t("Freitext eingeben.") : t("Übung auswählen."));
      return;
    }
    setSpeichert(true);
    try {
      await api.post(`/api/goals/${goal.id}/plan-items`, {
        weekNumber: woche,
        exerciseId: frei ? null : uebungId,
        freeTextLabel: frei ? text.trim() : null,
        repetitionsTarget: ziel,
        dayIndex: tag,
      });
      toast.success(t("Übung zum Plan hinzugefügt."));
      await onAdded();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Übung konnte nicht hinzugefügt werden."));
    } finally {
      setSpeichert(false);
    }
  }

  const tage = trainingstageDerWoche(goal, woche);
  return (
    <div className="flex min-w-0 flex-col gap-3 rounded-md border bg-muted/30 p-3">
      <UebungFelder
        frei={frei}
        onFrei={setFrei}
        uebungId={uebungId}
        onUebungId={setUebungId}
        text={text}
        onText={setText}
        uebungen={uebungen}
        kompakt={false}
      />
      <div className="grid gap-3 sm:grid-cols-3">
        {mitWochenfeld && (
          <div className="flex flex-col gap-2">
            <Label>{t("Woche")}</Label>
            <Input type="number" min={1} max={12} value={woche} onChange={(e) => setWoche(Number(e.target.value))} />
          </div>
        )}
        <div className="flex flex-col gap-2">
          <Label>{t("Zielwert (x diese Woche)")}</Label>
          <Input type="number" min={1} max={10} value={ziel} onChange={(e) => setZiel(Number(e.target.value))} />
        </div>
        {tage > 1 && (
          <div className="flex flex-col gap-2">
            <Label>{t("Trainingstag")}</Label>
            <Input type="number" min={1} max={tage} value={tag} onChange={(e) => setTag(Number(e.target.value))} />
          </div>
        )}
      </div>
      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={speichert} onClick={hinzufuegen}>
          {speichert ? t("Wird hinzugefügt…") : t("Hinzufügen")}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

/** Eine Planübung ändern: Übung oder Freitext, Woche, Zielwert, Trainingstag. */
export function PlanItemEditForm({
  goal,
  item,
  uebungen,
  onSaved,
  onCancel,
}: {
  goal: Goal;
  item: TrainingPlanItem;
  uebungen: Exercise[] | null;
  onSaved: () => Promise<void>;
  onCancel: () => void;
}) {
  const t = useT();
  const [frei, setFrei] = useState(item.freeTextLabel !== null);
  const [uebungId, setUebungId] = useState(item.exerciseId ?? "");
  const [text, setText] = useState(item.freeTextLabel ?? "");
  const [woche, setWoche] = useState(item.weekNumber);
  const [ziel, setZiel] = useState(item.repetitionsTarget);
  const [tag, setTag] = useState(item.dayIndex);
  const [speichert, setSpeichert] = useState(false);

  async function speichern() {
    if (frei ? !text.trim() : !uebungId) {
      toast.error(frei ? t("Freitext eingeben.") : t("Übung auswählen."));
      return;
    }
    setSpeichert(true);
    try {
      await api.put(`/api/goals/${goal.id}/plan-items/${item.id}`, {
        weekNumber: woche,
        exerciseId: frei ? null : uebungId,
        freeTextLabel: frei ? text.trim() : null,
        repetitionsTarget: ziel,
        dayIndex: tag,
      });
      toast.success(t("Plan-Ziel aktualisiert."));
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Plan-Ziel konnte nicht aktualisiert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  const tage = trainingstageDerWoche(goal, woche);
  return (
    <div className="flex min-w-0 flex-col gap-2 rounded-md border bg-muted/40 p-2.5">
      <UebungFelder
        frei={frei}
        onFrei={setFrei}
        uebungId={uebungId}
        onUebungId={setUebungId}
        text={text}
        onText={setText}
        uebungen={uebungen}
        kompakt
      />
      <div className="grid grid-cols-2 gap-2">
        <div className="flex flex-col gap-1">
          <Label className="text-xs">{t("Woche")}</Label>
          <Input type="number" min={1} max={12} value={woche} onChange={(e) => setWoche(Number(e.target.value))} />
        </div>
        <div className="flex flex-col gap-1">
          <Label className="text-xs">{t("Zielwert (x diese Woche)")}</Label>
          <Input type="number" min={1} max={10} value={ziel} onChange={(e) => setZiel(Number(e.target.value))} />
        </div>
        {tage > 1 && (
          <div className="flex flex-col gap-1">
            <Label className="text-xs">{t("Trainingstag")}</Label>
            <Input type="number" min={1} max={tage} value={tag} onChange={(e) => setTag(Number(e.target.value))} />
          </div>
        )}
      </div>
      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={speichert} onClick={speichern}>
          {speichert ? t("Wird gespeichert…") : t("Speichern")}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

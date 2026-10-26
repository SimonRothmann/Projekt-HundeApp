"use client";

import { useState } from "react";
import { CalendarClock } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { Goal } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { morgenIso, pruefungsName } from "@/lib/pruefung";
import { vorschlagZieldatum } from "@/lib/pausen";
import { useSprache, useT } from "@/lib/i18n";

/**
 * Der Hinweis "Zieldatum verschieben?" - erscheint nur, wenn eine
 * Trainingspause die Zeit bis zur Prüfung so verkürzt hat, dass der Plan nicht
 * mehr ganz hineinpasst (Goal.zieldatumZuKnapp, vom Server berechnet). Der
 * Knopf öffnet ein kleines Fenster mit dem neuen Datum; verschoben wird nie von
 * selbst, und der Plan wird danach um die fehlenden Wochen verlängert.
 *
 * Mit `anlass="pruefungsreife"` erscheint er auch ohne Pause: wenn die
 * Prüfungsreife rot steht und nur noch wenige Wochen bleiben (der Server sagt
 * das in Goal.pruefungsreife.zieldatumVerschieben). Derselbe Hinweis und dasselbe
 * Fenster, nur ein anderer Satz.
 */
export function ZieldatumHinweis({
  goal,
  onChanged,
  anlass = "pause",
}: {
  goal: Goal;
  onChanged: () => Promise<void>;
  anlass?: "pause" | "pruefungsreife";
}) {
  const t = useT();
  const sprache = useSprache();
  const [offen, setOffen] = useState(false);
  const [datum, setDatum] = useState("");
  const [speichert, setSpeichert] = useState(false);

  if (goal.status !== 0 || (anlass === "pause" && !goal.zieldatumZuKnapp)) return null;

  function oeffnen() {
    // Um die Tage verschoben, die dem Plan fehlen - ein Vorschlag, den man ändert.
    setDatum(vorschlagZieldatum(goal));
    setOffen(true);
  }

  const fehler = datum === "" ? t("Bitte den neuen Prüfungstermin angeben.") : datum < morgenIso() ? t("Der neue Termin muss in der Zukunft liegen.") : null;

  async function speichern() {
    if (fehler) return;
    setSpeichert(true);
    try {
      await api.put<Goal>(`/api/goals/${goal.id}/target-date`, { targetDate: datum });
      toast.success(t("Zieldatum verschoben."));
      setOffen(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Zieldatum konnte nicht verschoben werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <>
      <div className="flex flex-col gap-2 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm">
        <p className="flex min-w-0 items-start gap-2 text-amber-800 dark:text-amber-300">
          <CalendarClock className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span className="min-w-0 [overflow-wrap:anywhere]">
            {anlass === "pause"
              ? t("Durch die Pause reicht die Zeit bis zur Prüfung nicht mehr für den ganzen Plan. Zieldatum verschieben?")
              : t("Bis zur Prüfung bleibt nicht mehr viel Zeit, und noch fehlt einiges. Zieldatum verschieben?")}
          </span>
        </p>
        <Button type="button" size="sm" variant="outline" className="self-start coarse:min-h-11" aria-haspopup="dialog" onClick={oeffnen}>
          {t("Zieldatum verschieben")}
        </Button>
      </div>

      <Sheet open={offen} onOpenChange={setOffen}>
        <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto pb-[max(1rem,env(safe-area-inset-bottom))]">
          <SheetHeader>
            <SheetTitle>{t("Zieldatum verschieben")}</SheetTitle>
            <SheetDescription className="[overflow-wrap:anywhere]">{pruefungsName(goal, sprache)}</SheetDescription>
          </SheetHeader>
          <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
            <div className="flex min-w-0 flex-col gap-2">
              <Label htmlFor={`zieldatum-${goal.id}`}>{t("Neues Prüfungsdatum")}</Label>
              <Input
                id={`zieldatum-${goal.id}`}
                type="date"
                required
                min={morgenIso()}
                value={datum}
                onChange={(e) => setDatum(e.target.value)}
                className="h-11 min-w-0"
              />
              <p className="text-xs text-muted-foreground">
                {t("Der Trainingsplan wird bis zum neuen Datum verlängert. Was schon im Plan steht, bleibt.")}
              </p>
            </div>
            {fehler && datum !== "" && <p className="text-sm text-destructive">{fehler}</p>}
            <Button disabled={speichert || fehler !== null} onClick={speichern} className="min-h-11">
              {speichert ? t("Speichert…") : t("Zieldatum speichern")}
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}

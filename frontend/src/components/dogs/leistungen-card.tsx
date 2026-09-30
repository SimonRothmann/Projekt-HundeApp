"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal } from "@/lib/types";
import { datumKurz, leistungen, pruefungsName, punkteText, vorgabePruefungstag } from "@/lib/pruefung";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SectionHeading } from "@/components/ui/section-heading";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Medal, Pencil, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useSprache, useT } from "@/lib/i18n";
import { ErgebnisFelder, pruefeErgebnis, type ErgebnisEingabe } from "@/components/dogs/ziel-abschliessen";

/**
 * "Leistungen": die bestandenen Prüfungen eines Hundes, neueste zuerst.
 *
 * Erreichte Ziele stehen nur noch hier und nicht mehr zusätzlich als Plan-Karte
 * bei den Zielen: Dort zeigte die Karte bisher bloß ein "Erreicht" und einen
 * abgelaufenen Wochenplan, während Tag, Punkte und Notiz fehlten. Die Karte hier
 * trägt dieselbe Aktion "Ziel löschen" weiter, damit nichts unerreichbar wird.
 *
 * Alte Einträge, die vor den Ergebnisfeldern als erreicht markiert wurden,
 * haben weder Tag noch Punkte - sie lassen sich hier nachtragen.
 */
export function LeistungenCard({ goals, onChanged }: { goals: Goal[] | null; onChanged: () => Promise<void> }) {
  const t = useT();
  const sprache = useSprache();
  const [bearbeitet, setBearbeitet] = useState<Goal | null>(null);

  const eintraege = leistungen(goals ?? []);
  if (eintraege.length === 0) return null;

  async function loeschen(goal: Goal) {
    // Hier steht eine bestandene Prüfung auf dem Spiel, nicht nur ein Plan -
    // die Rückfrage sagt das ausdrücklich, damit niemand sie mit "Ergebnis
    // bearbeiten" verwechselt und eine Leistung verliert.
    if (!window.confirm(t("Diese Leistung samt Prüfungsergebnis und Trainingsplan endgültig löschen? Bereits erfasste Trainingseinträge bleiben im Tagebuch erhalten."))) {
      return;
    }
    try {
      await api.delete(`/api/goals/${goal.id}`);
      toast.success(t("Ziel gelöscht."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ziel konnte nicht gelöscht werden."));
    }
  }

  return (
    <div id="leistungen" className="flex scroll-mt-20 flex-col gap-3">
      <SectionHeading icon={Medal} title={t("Leistungen")} />
      <Card>
        <CardContent className="flex flex-col divide-y">
          {eintraege.map((goal) => (
            <div key={goal.id} className="flex min-w-0 flex-col gap-1.5 py-3 first:pt-0 last:pb-0">
              <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                <span className="min-w-0 font-medium [overflow-wrap:anywhere]">{pruefungsName(goal)}</span>
                <Badge className="shrink-0" variant="secondary">
                  {t("bestanden")}
                </Badge>
              </div>
              <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
                {[
                  goal.examDate ? datumKurz(goal.examDate, sprache) : t("Ohne Datum"),
                  goal.examScore != null ? punkteText(t, goal.examScore, goal.maxPoints) : null,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
              {goal.examNote && (
                <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{goal.examNote}</p>
              )}
              <div className="flex flex-wrap items-center gap-x-1 gap-y-1">
                <Button
                  size="sm"
                  variant="ghost"
                  className="-ml-2 coarse:min-h-11"
                  onClick={() => setBearbeitet(goal)}
                >
                  <Pencil className="size-3.5" />
                  {goal.examDate ? t("Ergebnis bearbeiten") : t("Ergebnis nachtragen")}
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="text-destructive hover:text-destructive coarse:min-h-11"
                  onClick={() => loeschen(goal)}
                >
                  <Trash2 className="size-3.5" />
                  {t("Leistung löschen")}
                </Button>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      {bearbeitet && (
        <ErgebnisBearbeiten
          key={bearbeitet.id}
          goal={bearbeitet}
          onClose={() => setBearbeitet(null)}
          onSaved={async () => {
            setBearbeitet(null);
            await onChanged();
          }}
        />
      )}
    </div>
  );
}

function ErgebnisBearbeiten({
  goal,
  onClose,
  onSaved,
}: {
  goal: Goal;
  onClose: () => void;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [eingabe, setEingabe] = useState<ErgebnisEingabe>({
    datum: goal.examDate ?? vorgabePruefungstag(goal.targetDate),
    punkte: goal.examScore != null ? String(goal.examScore) : "",
    notiz: goal.examNote ?? "",
  });
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefeErgebnis(t, eingabe, goal.maxPoints);

  async function speichern() {
    if (fehler) return;
    setSpeichert(true);
    try {
      const punkte = eingabe.punkte.trim();
      await api.put<Goal>(`/api/goals/${goal.id}/exam-result`, {
        examDate: eingabe.datum,
        score: punkte === "" ? null : Number(punkte),
        note: eingabe.notiz.trim() || null,
      });
      toast.success(t("Ergebnis gespeichert."));
      await onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ergebnis konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <Sheet open onOpenChange={(offen) => !offen && onClose()}>
      <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{goal.examDate ? t("Ergebnis bearbeiten") : t("Ergebnis nachtragen")}</SheetTitle>
          <SheetDescription className="[overflow-wrap:anywhere]">{pruefungsName(goal)}</SheetDescription>
        </SheetHeader>
        <div className="flex min-w-0 flex-col gap-4 p-4 pt-0">
          <ErgebnisFelder eingabe={eingabe} onChange={setEingabe} hoechstens={goal.maxPoints} idPrefix="ergebnis" />
          {fehler && eingabe.datum !== "" && <p className="text-sm text-destructive">{fehler}</p>}
          <Button disabled={speichert || fehler !== null} onClick={speichern} className="coarse:min-h-11">
            {speichert ? t("Speichert…") : t("Ergebnis speichern")}
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}

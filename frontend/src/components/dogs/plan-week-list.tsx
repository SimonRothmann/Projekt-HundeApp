"use client";

import { useState } from "react";
import type { Goal, PlanItemReason, TrainingPlanItem } from "@/lib/types";
import { fortschrittDerWoche, istPausenwoche, planItemName, sichtbareWochen } from "@/lib/trainingsplan";
import { FreieUebungHinzufuegen, ItemWerkzeuge, WochenWerkzeuge } from "@/components/dogs/plan-edit-tools";
import { PlanItemEditForm, useUebungKatalog } from "@/components/dogs/plan-item-forms";
import { ExerciseNotes } from "@/components/dogs/exercise-notes";
import { Button } from "@/components/ui/button";
import { Check, ChevronDown, ChevronRight, Plus } from "lucide-react";
import { cn } from "@/lib/utils";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { uebersetzbar } from "@/lib/i18n/sprachen";

// Warum der adaptive Generator eine Übung geplant hat (siehe PlanItemReason).
const reasonLabel: Record<PlanItemReason, string> = { 0: uebersetzbar("Schwäche"), 1: uebersetzbar("Wiederholung"), 2: uebersetzbar("Neu") };

// Dieselbe ruhige Zeile wie die Übungen auf der Startseite (dashboard/ziel-karten.tsx).
const ZEILE =
  "flex min-h-11 min-w-0 flex-1 items-center gap-3 rounded-lg border border-surface-border bg-surface px-3 py-1.5 text-left text-sm transition-colors hover:border-primary/40";

// Innerhalb einer Woche nach Trainingstag gruppieren (aufsteigend). Wird nur
// als sichtbare "Tag N"-Struktur genutzt, wenn eine Woche tatsächlich mehr als
// einen Trainingstag hat (Alt-Pläne liegen alle auf Tag 1 -> flache Ansicht).
function groupByDay(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byDay = new Map<number, TrainingPlanItem[]>();
  for (const item of items) {
    const group = byDay.get(item.dayIndex);
    if (group) group.push(item);
    else byDay.set(item.dayIndex, [item]);
  }
  return [...byDay.entries()].sort(([a], [b]) => a - b);
}

/**
 * Eine Übung der Woche. Im Lesemodus eine einzeilige Zeile (Name, x/y, Plus),
 * die das Eintragen-Fenster mit dieser Übung öffnet; im Bearbeitungsmodus kommen Stift und
 * Entfernen dazu. Jede Zeile hält ihren Auf-/Zu-Zustand selbst - so gibt es
 * keine kartenweite Kopplung (früher quickLogItemId/editItemId als Einzelwerte).
 */
function PlanUebung({
  goal,
  item,
  bearbeiten,
  uebungen,
  onEintragen,
  onChanged,
}: {
  goal: Goal;
  item: TrainingPlanItem;
  bearbeiten: boolean;
  uebungen: ReturnType<typeof useUebungKatalog>;
  onEintragen: (item: TrainingPlanItem) => void;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const [formOffen, setFormOffen] = useState(false);
  // Beim Verlassen des Bearbeitungsmodus klappt ein offenes Formular zu, damit
  // es beim nächsten "Plan bearbeiten" nicht von gestern übrig ist. (Zustand
  // beim Rendern angleichen statt per Effekt - kein zusätzlicher Durchlauf.)
  const [warBearbeiten, setWarBearbeiten] = useState(bearbeiten);
  if (warBearbeiten !== bearbeiten) {
    setWarBearbeiten(bearbeiten);
    if (!bearbeiten) setFormOffen(false);
  }

  const name = planItemName(item) ?? t("(Übung nicht mehr verfügbar)");
  const zusatz = [
    item.freeTextLabel && !item.exerciseName ? t("Freitext") : null,
    item.reason !== null ? t(reasonLabel[item.reason]) : null,
  ].filter(Boolean);

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <div className="flex items-center gap-1">
        <button
          type="button"
          onClick={() => onEintragen(item)}
          aria-haspopup="dialog"
          aria-label={`${name} · ${t("{erledigt}/{ziel}× erledigt", { erledigt: item.completedCount, ziel: item.repetitionsTarget })} · ${t("Eintragen")}`}
          className={ZEILE}
        >
          <span className={cn("min-w-0 flex-1 break-words font-medium", item.isComplete && "text-muted-foreground")}>
            {name}
            {zusatz.length > 0 && <span className="ml-1.5 text-xs font-normal text-muted-foreground">{zusatz.join(" · ")}</span>}
          </span>
          <span className="shrink-0 text-sm tabular-nums text-muted-foreground">
            {item.completedCount}/{item.repetitionsTarget}
          </span>
          {!bearbeiten &&
            (item.isComplete ? (
              <Check className="size-4 shrink-0 text-emerald-600 dark:text-emerald-500" aria-hidden />
            ) : (
              <Plus className="size-4 shrink-0 text-primary-text" aria-hidden />
            ))}
        </button>
        {bearbeiten && <ItemWerkzeuge goalId={goal.id} item={item} onEdit={() => setFormOffen((offen) => !offen)} onChanged={onChanged} />}
      </div>
      {bearbeiten && formOffen && (
        <PlanItemEditForm
          goal={goal}
          item={item}
          uebungen={uebungen}
          onSaved={async () => {
            setFormOffen(false);
            await onChanged();
          }}
          onCancel={() => setFormOffen(false)}
        />
      )}
      {item.logs.length > 0 && (
        <ul className="ml-3 flex flex-col gap-1 border-l pl-2.5">
          {item.logs.map((log) => (
            // Eine Zeile pro Log als reiner Textfluss: Meta (Datum/
            // Sterne) nowrap, dann der Kommentar im selben Fluss -
            // kurze Kommentare stehen neben der Meta, lange brechen um
            // und sind voll lesbar (Stift folgt am Textende). Das
            // overflow-wrap:anywhere deckelt die min-content (bricht
            // notfalls überlange Wörter), sodass die nowrap-Meta das
            // einzige Breiten-Minimum ist und nichts die Seite aufbläht
            // (Mobile-App-first, kein horizontaler Scroll).
            <li key={log.trainingExerciseId} className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
              <span className="whitespace-nowrap">
                {new Date(log.date).toLocaleDateString(ort)} · {"★".repeat(log.rating)}
                {"☆".repeat(5 - log.rating)} {log.success ? "✓" : "✗"}
              </span>{" "}
              <ExerciseNotes exerciseId={log.trainingExerciseId} notes={log.notes} onSaved={onChanged} compact />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * Die Wochen des Plans als Akkordeon: offen ist standardmäßig die laufende
 * Woche, die übrigen zeigt "Alle Wochen anzeigen" (ebenfalls im Lesemodus).
 * Mit `bearbeiten` kommen die Werkzeuge der Woche und der Übungen dazu.
 */
export function PlanWeekList({
  goal,
  weeks,
  currentWeek,
  bearbeiten,
  onEintragen,
  onChanged,
}: {
  goal: Goal;
  weeks: [number, TrainingPlanItem[]][];
  currentWeek: number | undefined;
  bearbeiten: boolean;
  /** Eine Übung wurde angetippt: das Eintragen-Fenster mit ihr öffnen. */
  onEintragen: (item: TrainingPlanItem) => void;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  // Wochen-Akkordeon: leer heißt "Standard" - offen ist dann nur die laufende
  // Woche (erste noch nicht vollständig erledigte, Nicht-Pause-Woche).
  const [openWeeks, setOpenWeeks] = useState<Set<number>>(new Set());
  // Der Plan stand mit allen Wochen über dem Tagebuch - bei zwölf Wochen mal
  // Anzahl der Ziele scrollt man lange, ehe man zum Erfassen kommt. Gebraucht
  // wird beim Öffnen genau eine Woche: die laufende.
  const [alleWochenZeigen, setAlleWochenZeigen] = useState(false);
  // Bearbeiten gibt es nur für aktive Ziele - ein beendetes Ziel bleibt Lesestoff.
  const aktiv = bearbeiten && goal.status === 0;
  const uebungen = useUebungKatalog(goal.sportId, aktiv);

  const effectiveOpenWeeks = openWeeks.size === 0 && currentWeek != null ? new Set([currentWeek]) : openWeeks;
  const gezeigteWochen = sichtbareWochen(weeks, currentWeek, alleWochenZeigen);
  // Ob das Verkürzen überhaupt etwas verbirgt. Bei einem Zwei-Wochen-Plan oder
  // einem abgeschlossenen Ziel tut es das nicht - dann wäre der Knopf ein
  // Versprechen ohne Wirkung.
  const kannVerkuerzen = sichtbareWochen(weeks, currentWeek, false).length < weeks.length;

  function toggleWeek(week: number) {
    setOpenWeeks((prev) => {
      const next = new Set(prev.size === 0 && currentWeek != null ? [currentWeek] : prev);
      if (next.has(week)) next.delete(week);
      else next.add(week);
      return next;
    });
  }

  return (
    <div className="flex min-w-0 flex-col gap-3">
      {gezeigteWochen.map(([weekNumber, items]) => {
        const isOpen = effectiveOpenWeeks.has(weekNumber);
        const isRest = istPausenwoche(items);
        const { geplant, erledigt } = fortschrittDerWoche(items);
        return (
          <div key={weekNumber} className="rounded-lg border border-surface-border">
            <button
              type="button"
              onClick={() => toggleWeek(weekNumber)}
              aria-expanded={isOpen}
              className="flex min-h-11 w-full items-center gap-2 px-3 py-2 text-left text-sm text-muted-foreground"
            >
              {isOpen ? <ChevronDown className="size-4 shrink-0" aria-hidden /> : <ChevronRight className="size-4 shrink-0" aria-hidden />}
              {isRest ? (
                <span className="font-medium">{`${t("Woche {nummer}", { nummer: weekNumber })} · ${t("Pause")}`}</span>
              ) : (
                <>
                  {geplant > 0 && erledigt >= geplant && <Check className="size-4 shrink-0 text-emerald-600 dark:text-emerald-500" aria-hidden />}
                  <span className="shrink-0 font-medium">
                    {t("Woche {woche} · {erledigt}/{geplant}", { woche: weekNumber, erledigt, geplant })}
                  </span>
                  <span className="flex min-w-0 flex-1 gap-1" aria-hidden>
                    {Array.from({ length: geplant }, (_, i) => (
                      <span key={i} className={cn("h-1.5 min-w-0 flex-1 rounded-full", i < erledigt ? "bg-primary" : "bg-primary/15")} />
                    ))}
                  </span>
                </>
              )}
            </button>
            {isOpen && (
              <div className="flex min-w-0 flex-col gap-2 border-t border-surface-border p-2.5">
                {aktiv && !isRest && <WochenWerkzeuge goal={goal} weekNumber={weekNumber} uebungen={uebungen} onChanged={onChanged} />}
                {isRest ? (
                  <span className="text-sm text-muted-foreground">{t("Pause")}</span>
                ) : (
                  groupByDay(items).map(([dayNumber, dayItems], _dayIdx, dayGroups) => (
                    <div key={dayNumber} className="flex min-w-0 flex-col gap-1.5">
                      {dayGroups.length > 1 && (
                        <span className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                          {t("Tag {nummer}", { nummer: dayNumber })}
                        </span>
                      )}
                      {dayItems.map((item) => (
                        <PlanUebung
                          key={item.id}
                          goal={goal}
                          item={item}
                          bearbeiten={aktiv}
                          uebungen={uebungen}
                          onEintragen={onEintragen}
                          onChanged={onChanged}
                        />
                      ))}
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
        );
      })}

      {kannVerkuerzen && (
        <Button
          type="button"
          size="sm"
          variant="ghost"
          className="self-start text-xs text-muted-foreground"
          onClick={() => setAlleWochenZeigen((v) => !v)}
        >
          {alleWochenZeigen ? <ChevronRight className="size-3.5" /> : <ChevronDown className="size-3.5" />}
          {alleWochenZeigen ? t("Nur die laufende Woche") : t("Alle {anzahl} Wochen anzeigen", { anzahl: weeks.length })}
        </Button>
      )}

      {aktiv && <FreieUebungHinzufuegen goal={goal} uebungen={uebungen} onChanged={onChanged} />}
    </div>
  );
}

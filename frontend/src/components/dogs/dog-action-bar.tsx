"use client";

import { NotebookPen, Route, StickyNote } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Die Wege, für die man die Hundeseite am häufigsten öffnet: Training erfassen
 * (primär), Fährte legen und - für Besitzer:innen - Merken (Merkzettel fürs nächste
 * Training). Gleich breite Knöpfe, 44 px hoch, in zwei Spalten.
 * "Fährte legen" nur, wenn das Fährte-Modul an ist und der Hund Fährte läuft.
 * Sind es drei Knöpfe, nimmt "Training erfassen" die erste Zeile ganz: Der Text
 * "Training erfassen" fände bei 375 px in einem Drittel der Breite keinen Platz.
 */
export function DogActionBar({
  zeigtFaehrte,
  nurTrail = false,
  zeigtMerken,
  onTraining,
  onFaehrte,
  onMerken,
}: {
  zeigtFaehrte: boolean;
  /** Der Hund trailt (Mantrailing), läuft aber keine Fährte: Der Knopf heißt dann "Trail legen". */
  nurTrail?: boolean;
  /** Ob "Merken" dabei ist (Besitzer:innen; der Merkzettel gehört der Familie, nicht den Trainer:innen). */
  zeigtMerken: boolean;
  onTraining: () => void;
  onFaehrte: () => void;
  onMerken: () => void;
}) {
  const t = useT();
  const dreiKnoepfe = zeigtFaehrte && zeigtMerken;

  return (
    <div className={cn("grid gap-2", zeigtFaehrte || zeigtMerken ? "grid-cols-2" : "grid-cols-1")}>
      <Button className={cn("h-11 min-w-0 text-sm", dreiKnoepfe && "col-span-2")} onClick={onTraining}>
        <NotebookPen className="size-4" />
        <span className="truncate">{t("Training erfassen")}</span>
      </Button>
      {zeigtFaehrte && (
        <Button variant="outline" className="h-11 min-w-0 text-sm" onClick={onFaehrte}>
          <Route className="size-4" />
          <span className="truncate">{nurTrail ? t("Trail legen") : t("Fährte legen")}</span>
        </Button>
      )}
      {zeigtMerken && (
        <Button variant="outline" className="h-11 min-w-0 text-sm" onClick={onMerken}>
          <StickyNote className="size-4" />
          <span className="truncate">{t("Merken")}</span>
        </Button>
      )}
    </div>
  );
}

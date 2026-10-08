"use client";

import { NotebookPen, Route } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Die zwei Wege, für die man die Hundeseite am häufigsten öffnet: Training
 * erfassen (primär) und Fährte legen. Zwei gleich breite Knöpfe, 44 px hoch.
 * "Fährte legen" nur, wenn das Fährte-Modul an ist und der Hund Fährte läuft -
 * dann steht "Training erfassen" allein über die ganze Breite.
 */
export function DogActionBar({
  zeigtFaehrte,
  onTraining,
  onFaehrte,
}: {
  zeigtFaehrte: boolean;
  onTraining: () => void;
  onFaehrte: () => void;
}) {
  const t = useT();

  return (
    <div className={cn("grid gap-2", zeigtFaehrte ? "grid-cols-2" : "grid-cols-1")}>
      <Button className="h-11 min-w-0 text-sm" onClick={onTraining}>
        <NotebookPen className="size-4" />
        <span className="truncate">{t("Training erfassen")}</span>
      </Button>
      {zeigtFaehrte && (
        <Button variant="outline" className="h-11 min-w-0 text-sm" onClick={onFaehrte}>
          <Route className="size-4" />
          <span className="truncate">{t("Fährte legen")}</span>
        </Button>
      )}
    </div>
  );
}

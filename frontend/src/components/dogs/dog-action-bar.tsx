"use client";

import { NotebookPen, Route, StickyNote, type LucideIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";

/**
 * Die Wege, für die man die Hundeseite am häufigsten öffnet: Training erfassen
 * (primär, volle Breite), darunter ohne Rahmen Fährte legen und - für
 * Besitzer:innen - Merken (Merkzettel fürs nächste Training), alle 44 px hoch.
 * "Fährte legen" nur, wenn das Fährte-Modul an ist und der Hund Fährte läuft.
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

  return (
    <div className="flex flex-col gap-1">
      <Button className="h-11 min-w-0 text-sm" onClick={onTraining}>
        <NotebookPen className="size-4" />
        <span className="truncate">{t("Training eintragen")}</span>
      </Button>
      {(zeigtFaehrte || zeigtMerken) && (
        <div className="flex flex-wrap justify-center gap-x-2">
          {zeigtFaehrte && (
            <NebenKnopf icon={Route} text={nurTrail ? t("Trail legen") : t("Fährte legen")} onClick={onFaehrte} />
          )}
          {zeigtMerken && <NebenKnopf icon={StickyNote} text={t("Merken")} onClick={onMerken} />}
        </div>
      )}
    </div>
  );
}

/** Nebenweg ohne Rahmen: Text in Primärfarbe mit Symbol, 44 px hoch. */
function NebenKnopf({ icon: Icon, text, onClick }: { icon: LucideIcon; text: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="inline-flex min-h-11 min-w-0 items-center gap-1.5 rounded-full px-4 text-sm font-medium text-primary-text transition-colors hover:bg-muted active:bg-muted"
    >
      <Icon className="size-4 shrink-0" aria-hidden />
      <span className="truncate">{text}</span>
    </button>
  );
}

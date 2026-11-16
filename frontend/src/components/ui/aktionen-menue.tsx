"use client";

import { useState } from "react";
import { MoreHorizontal, type LucideIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

export type Aktion = {
  label: string;
  icon: LucideIcon;
  onSelect: () => void;
  /** Löschen, Verlassen o. Ä.: in der Warnfarbe. */
  destruktiv?: boolean;
};

/**
 * Das kleine ⋯-Menü: Bearbeiten, Löschen und Verlassen stehen nicht offen als
 * Knöpfe auf der Seite, sondern hinter einem Knopf. Ein Antippen wählt den
 * Eintrag, schließt das Menü und ruft `onSelect` - Rückfragen stellt der
 * Aufrufer selbst.
 */
export function AktionenMenue({
  aktionen,
  label,
  disabled,
  className,
}: {
  aktionen: Aktion[];
  /** Zugänglicher Name des Knopfes (er zeigt nur ein Symbol). */
  label: string;
  disabled?: boolean;
  className?: string;
}) {
  const [offen, setOffen] = useState(false);

  return (
    <Popover open={offen} onOpenChange={setOffen}>
      <PopoverTrigger
        render={
          <Button
            variant="ghost"
            size="icon"
            disabled={disabled}
            className={cn("shrink-0 rounded-full text-muted-foreground", className)}
            aria-label={label}
          />
        }
      >
        <MoreHorizontal className="size-4" />
      </PopoverTrigger>
      <PopoverContent className="w-56 p-1">
        {aktionen.map(({ label: text, icon: Icon, onSelect, destruktiv }) => (
          <Button
            key={text}
            variant="ghost"
            className={cn("h-10 w-full justify-start gap-2", destruktiv && "text-destructive hover:text-destructive")}
            onClick={() => {
              setOffen(false);
              onSelect();
            }}
          >
            <Icon className="size-4" />
            {text}
          </Button>
        ))}
      </PopoverContent>
    </Popover>
  );
}

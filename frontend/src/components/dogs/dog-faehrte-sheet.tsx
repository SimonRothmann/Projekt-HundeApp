"use client";

import { useState } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { FahrteRecorder } from "@/components/tracking/fahrte-recorder";
import { useT } from "@/lib/i18n";
import type { SpurArten } from "@/lib/faehrte";
import { cn } from "@/lib/utils";

/**
 * Das Fenster "Fährte legen": der Recorder (Untergrund wählen, "Legen starten")
 * in einem Sheet statt als Karte mitten auf der Seite. Das Vollbild beim
 * Aufzeichnen kommt vom Recorder selbst und liegt über allem.
 *
 * Solange aufgezeichnet wird, lässt sich das Fenster nicht schließen - weder
 * per Tipp daneben noch per Escape: Mit dem Fenster verschwände der Recorder
 * und mit ihm die laufende Aufzeichnung. Nicht modal, damit Fokusfalle und
 * Bedienungssperre nicht gegen das Vollbild arbeiten, das als Portal am Body
 * hängt und für das Fenster "außerhalb" liegt. Das gilt bewusst auch vor dem
 * Start: Ein Wechsel von modal zu nicht modal mitten im Betrieb (beim ersten
 * Aufzeichnen) ist nicht erprobt, und das Fenster trägt vorher nur die Wahl
 * des Untergrunds.
 */
export function DogFaehrteSheet({
  dogId,
  open,
  onOpenChange,
  onSaved,
  arten = { faehrte: true, mantrailing: false },
}: {
  /** Was der Hund legen kann. Kann er beides, wählt man im Fenster; sonst steht die eine Art fest. */
  arten?: SpurArten;
  dogId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [aufzeichnet, setAufzeichnet] = useState(false);
  // Abgeleitet statt einmal beim ersten Rendern gemerkt: Auf der Hundeseite steht
  // das Fenster schon, bevor die Sportarten des Hundes geladen sind. Gewählt wird
  // nur, wenn der Hund beides legen kann.
  const [trailGewaehlt, setTrail] = useState(false);
  const trail = arten.faehrte && arten.mantrailing ? trailGewaehlt : arten.mantrailing;

  return (
    <Sheet
      open={open}
      modal={false}
      onOpenChange={(offen) => {
        if (!offen && aufzeichnet) return;
        onOpenChange(offen);
      }}
    >
      {/* Das X würde während der Aufzeichnung ins Leere tippen (onOpenChange
          verwirft das Schließen) - dann gar nicht erst anbieten. */}
      <SheetContent side="bottom" showCloseButton={!aufzeichnet} className="max-h-[85vh] overflow-y-auto pb-[max(1rem,env(safe-area-inset-bottom))]">
        <SheetHeader>
          <SheetTitle>{trail ? t("Trail legen") : t("Fährte legen")}</SheetTitle>
        </SheetHeader>
        <div className="flex flex-col gap-3 px-4">
          {/* Beides geht nur bei einem Hund, der Fährte läuft UND trailt. Während der Aufzeichnung
              verschwindet der Recorder-Inhalt ohnehin hinter dem Vollbild; gewechselt wird davor. */}
          {arten.faehrte && arten.mantrailing && !aufzeichnet && (
            <div role="group" aria-label={t("Art der Spur")} className="grid grid-cols-2 gap-1.5">
              {[
                { istTrail: false, label: t("Fährte") },
                { istTrail: true, label: t("Trail") },
              ].map((wahl) => (
                <button
                  key={String(wahl.istTrail)}
                  type="button"
                  aria-pressed={trail === wahl.istTrail}
                  onClick={() => setTrail(wahl.istTrail)}
                  className={cn(
                    "min-h-11 rounded-lg border px-3 text-sm font-medium transition-colors",
                    trail === wahl.istTrail
                      ? "border-primary bg-primary/15 text-primary-text"
                      : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
                  )}
                >
                  {wahl.label}
                </button>
              ))}
            </div>
          )}
          {/* key: Ein anderer Spur-Typ bekommt einen frischen Recorder statt des Zustands des vorigen. */}
          <FahrteRecorder key={String(trail)} dogId={dogId} onSaved={onSaved} onAufzeichnung={setAufzeichnet} mantrailing={trail} />
        </div>
      </SheetContent>
    </Sheet>
  );
}

"use client";

import { useState } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { FahrteRecorder } from "@/components/tracking/fahrte-recorder";
import { useT } from "@/lib/i18n";

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
}: {
  dogId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [aufzeichnet, setAufzeichnet] = useState(false);

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
          <SheetTitle>{t("Fährte legen")}</SheetTitle>
        </SheetHeader>
        <div className="px-4">
          <FahrteRecorder dogId={dogId} onSaved={onSaved} onAufzeichnung={setAufzeichnet} />
        </div>
      </SheetContent>
    </Sheet>
  );
}

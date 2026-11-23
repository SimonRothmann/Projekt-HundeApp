"use client";

import { useState } from "react";
import { Sheet, BottomSheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { FahrteRecorder } from "@/components/tracking/fahrte-recorder";
import { useT } from "@/lib/i18n";
import { hauptSpurArt, type SpurArten } from "@/lib/faehrte";
import type { SpurArtName } from "@/lib/spurart";
import { Chip } from "@/components/ui/chip";

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
  arten = { faehrte: true, mantrailing: false, schweiss: false },
}: {
  /** Was der Hund legen kann. Kann er mehrere Arten, wählt man im Fenster; sonst steht die eine Art fest. */
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
  // nur, wenn der Hund mehrere Arten legen kann.
  const moegliche = (
    [
      { art: "faehrte", label: t("Fährte"), moeglich: arten.faehrte },
      { art: "trail", label: t("Trail"), moeglich: arten.mantrailing },
      { art: "schweiss", label: t("Schweißfährte"), moeglich: arten.schweiss },
    ] as const
  ).filter((wahl) => wahl.moeglich);
  const [gewaehlt, setGewaehlt] = useState<SpurArtName>("faehrte");
  const art = moegliche.length > 1 && moegliche.some((m) => m.art === gewaehlt) ? gewaehlt : hauptSpurArt(arten);

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
      <BottomSheetContent showCloseButton={!aufzeichnet}>
        <SheetHeader>
          <SheetTitle>{art === "trail" ? t("Trail legen") : art === "schweiss" ? t("Schweißfährte legen") : t("Fährte legen")}</SheetTitle>
        </SheetHeader>
        <div className="flex flex-col gap-3 px-4">
          {/* Mehrere Arten gibt es nur bei einem Hund, der z. B. Fährte läuft UND trailt. Während der
              Aufzeichnung verschwindet der Recorder-Inhalt ohnehin hinter dem Vollbild; gewechselt wird davor.
              Drei Chips nebeneinander: bei 375 px bricht "Schweißfährte" im Chip um statt zu scrollen. */}
          {moegliche.length > 1 && !aufzeichnet && (
            <div role="group" aria-label={t("Art der Spur")} className={moegliche.length === 3 ? "grid grid-cols-3 gap-1.5" : "grid grid-cols-2 gap-1.5"}>
              {moegliche.map((wahl) => (
                <Chip
                  key={wahl.art}
                  gewaehlt={art === wahl.art}
                  onClick={() => setGewaehlt(wahl.art)}
                  className="justify-center px-2 text-center"
                >
                  {wahl.label}
                </Chip>
              ))}
            </div>
          )}
          {/* key: Ein anderer Spur-Typ bekommt einen frischen Recorder statt des Zustands des vorigen. */}
          <FahrteRecorder key={art} dogId={dogId} onSaved={onSaved} onAufzeichnung={setAufzeichnet} art={art} />
        </div>
      </BottomSheetContent>
    </Sheet>
  );
}

"use client";

import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { dauerText, helferName, MAX_MINUTEN, MIN_MINUTEN, minutenAusEingabe, pruefeMinuten } from "@/lib/arbeitseinsaetze";
import { useT } from "@/lib/i18n";
import type { Arbeitseinsatz, ArbeitseinsatzHelfer } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Abweichende Minuten einer Person (Verwaltung): wenn jemand kürzer oder länger da war als der Einsatz
 * dauert. "Wie der Einsatz" nimmt die Abweichung zurück - dann folgt die Person künftig der Dauer des Einsatzes.
 */
export function MinutenSheet({
  clubId,
  einsatz,
  helfer,
  onOpenChange,
  onGespeichert,
}: {
  clubId: string;
  einsatz: Arbeitseinsatz;
  /** Gesetzt: das Fenster ist offen. */
  helfer: ArbeitseinsatzHelfer | null;
  onOpenChange: (offen: boolean) => void;
  onGespeichert: () => Promise<void>;
}) {
  const t = useT();
  return (
    <Sheet open={helfer !== null} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{helfer ? helferName(t, helfer) : ""}</SheetTitle>
          <SheetDescription>{t("Der Einsatz dauert {dauer}.", { dauer: dauerText(einsatz.durationMinutes) })}</SheetDescription>
        </SheetHeader>
        {helfer && <Formular key={helfer.id} clubId={clubId} einsatz={einsatz} helfer={helfer} onGespeichert={onGespeichert} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Formular({ clubId, einsatz, helfer, onGespeichert }: { clubId: string; einsatz: Arbeitseinsatz; helfer: ArbeitseinsatzHelfer; onGespeichert: () => Promise<void> }) {
  const t = useT();
  const [minuten, setMinuten] = useState(String(helfer.minutes));
  const [speichert, setSpeichert] = useState(false);
  const fehler = pruefeMinuten(t, minutenAusEingabe(minuten));

  async function senden(wert: number | null) {
    setSpeichert(true);
    try {
      await api.put(`/api/clubs/${clubId}/work-events/${einsatz.id}/participants/${helfer.id}`, { minutes: wert });
      toast.success(t("Dauer gespeichert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Dauer konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  function speichern(e: FormEvent) {
    e.preventDefault();
    if (!fehler) void senden(minutenAusEingabe(minuten));
  }

  return (
    <form onSubmit={speichern} className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="helfer-minuten">{t("Dauer in Minuten")}</Label>
        <Input id="helfer-minuten" inputMode="numeric" value={minuten} maxLength={3} onChange={(e) => setMinuten(e.target.value)} className="min-w-0" />
        <p className="text-xs text-muted-foreground">{t("{min} bis {max} Minuten.", { min: MIN_MINUTEN, max: MAX_MINUTEN })}</p>
      </div>
      <Button type="submit" disabled={speichert || fehler !== null} className="coarse:min-h-11">
        {speichert ? t("Speichert…") : t("Speichern")}
      </Button>
      {helfer.custom && (
        <Button type="button" variant="outline" disabled={speichert} onClick={() => void senden(null)} className="coarse:min-h-11">
          {t("Wie der Einsatz")}
        </Button>
      )}
    </form>
  );
}

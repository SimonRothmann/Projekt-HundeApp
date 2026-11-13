"use client";

import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { MAX_MINUTEN, MIN_MINUTEN, minutenAusEingabe, pruefeEinsatz, spaetesterTag, STANDARD_MINUTEN } from "@/lib/arbeitseinsaetze";
import { useT } from "@/lib/i18n";
import { vereinszeitTag } from "@/lib/kalendertag";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { Arbeitseinsatz } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Arbeitseinsatz anlegen oder bearbeiten (Verwaltung): Datum, Titel, Dauer für alle Helfer:innen und eine
 * Notiz. Vorab anlegen darf man bis zu 30 Tage; abgehakt wird erst ab dem Tag selbst (das prüft der Server).
 */
export function EinsatzSheet({
  clubId,
  einsatz,
  open,
  onOpenChange,
  onGespeichert,
}: {
  clubId: string;
  /** Gesetzt: bearbeiten; sonst neu anlegen. */
  einsatz?: Arbeitseinsatz;
  open: boolean;
  onOpenChange: (offen: boolean) => void;
  onGespeichert: (gespeichert: Arbeitseinsatz) => void;
}) {
  const t = useT();
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{einsatz ? t("Einsatz bearbeiten") : t("Neuer Arbeitseinsatz")}</SheetTitle>
          <SheetDescription>{t("Platzpflege, Aufbau, Vereinsheim … Danach hakst du ab, wer geholfen hat.")}</SheetDescription>
        </SheetHeader>
        {/* Erst beim Öffnen aufgebaut: So startet das Formular jedes Mal mit den aktuellen Werten. */}
        {open && <Formular clubId={clubId} einsatz={einsatz} onGespeichert={onGespeichert} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Formular({ clubId, einsatz, onGespeichert }: { clubId: string; einsatz?: Arbeitseinsatz; onGespeichert: (gespeichert: Arbeitseinsatz) => void }) {
  const t = useT();
  const heute = vereinszeitTag();
  const [datum, setDatum] = useState(einsatz?.date ?? heute);
  const [titel, setTitel] = useState(einsatz?.title ?? "");
  const [minuten, setMinuten] = useState(String(einsatz?.durationMinutes ?? STANDARD_MINUTEN));
  const [notiz, setNotiz] = useState(einsatz?.note ?? "");
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefeEinsatz(t, { datum, titel, minuten }, heute);

  async function speichern(e: FormEvent) {
    e.preventDefault();
    if (fehler) return;
    setSpeichert(true);
    try {
      const body = { date: datum, title: titel.trim(), note: notiz.trim() || null, durationMinutes: minutenAusEingabe(minuten) };
      const gespeichert = einsatz
        ? await api.put<Arbeitseinsatz>(`/api/clubs/${clubId}/work-events/${einsatz.id}`, body)
        : await api.post<Arbeitseinsatz>(`/api/clubs/${clubId}/work-events`, body);
      toast.success(einsatz ? t("Einsatz gespeichert.") : t("Einsatz angelegt."));
      onGespeichert(gespeichert);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einsatz konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={speichern} className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="einsatz-titel">{t("Titel")}</Label>
        <Input
          id="einsatz-titel"
          required
          value={titel}
          maxLength={TEXTLAENGE.arbeitseinsatzTitel}
          placeholder={t("z. B. Platzpflege")}
          onChange={(e) => setTitel(e.target.value)}
        />
      </div>
      {/* Datum und Dauer untereinander am Telefon: Nebeneinander können die Felder in Safari über den Rand laufen. */}
      <div className="grid min-w-0 grid-cols-1 gap-3 min-[420px]:grid-cols-2">
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="einsatz-datum">{t("Datum")}</Label>
          <Input
            id="einsatz-datum"
            type="date"
            required
            value={datum}
            max={spaetesterTag(heute)}
            onChange={(e) => setDatum(e.target.value)}
            className="min-w-0"
          />
        </div>
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="einsatz-dauer">{t("Dauer in Minuten")}</Label>
          <Input
            id="einsatz-dauer"
            inputMode="numeric"
            required
            value={minuten}
            maxLength={3}
            onChange={(e) => setMinuten(e.target.value)}
            className="min-w-0"
          />
        </div>
      </div>
      <p className="text-xs text-muted-foreground">
        {t("Die Dauer gilt für alle Helfer:innen; bei einzelnen lässt sie sich ändern ({min} bis {max} Minuten). Vorab anlegen kannst du bis zu 30 Tage, abhaken erst ab dem Tag des Einsatzes.", {
          min: MIN_MINUTEN,
          max: MAX_MINUTEN,
        })}
      </p>
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="einsatz-notiz">{t("Notiz")}</Label>
        <Input
          id="einsatz-notiz"
          value={notiz}
          maxLength={TEXTLAENGE.arbeitseinsatzNotiz}
          placeholder={t("(optional)")}
          onChange={(e) => setNotiz(e.target.value)}
        />
      </div>
      <Button type="submit" disabled={speichert || fehler !== null} className="coarse:min-h-11">
        {speichert ? t("Speichert…") : t("Speichern")}
      </Button>
    </form>
  );
}

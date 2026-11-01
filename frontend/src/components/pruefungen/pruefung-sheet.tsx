"use client";

import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { hatErgebnisse, pruefeTag } from "@/lib/pruefungen";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { VereinsPruefung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, BottomSheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { OrdnungsWahl, useOrdnungen } from "@/components/pruefungen/ordnungen";

/**
 * Prüfungstag anlegen oder bearbeiten (Ausbildungswart:in, Verwaltung): Datum, Uhrzeit, Ort,
 * Richter:in, Meldeschluss (nur zur Anzeige), die angebotenen Ordnungen und eine Bemerkung. Die
 * angebotenen Ordnungen steuern nur die Auswahl beim Eintragen eines Teams; ohne Auswahl
 * gibt es keine Vorgabe.
 */
export function PruefungSheet({
  clubId,
  pruefung,
  open,
  onOpenChange,
  onGespeichert,
}: {
  clubId: string;
  /** Gesetzt: bearbeiten; sonst neu anlegen. */
  pruefung?: VereinsPruefung;
  open: boolean;
  onOpenChange: (offen: boolean) => void;
  onGespeichert: (gespeichert: VereinsPruefung) => void;
}) {
  const t = useT();
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <BottomSheetContent>
        <SheetHeader>
          <SheetTitle>{pruefung ? t("Prüfung bearbeiten") : t("Neue Prüfung")}</SheetTitle>
          <SheetDescription>{t("Datum, Ort und die Ordnungen, die an diesem Tag geprüft werden.")}</SheetDescription>
        </SheetHeader>
        {/* Erst beim Öffnen aufgebaut: So startet das Formular jedes Mal mit den aktuellen Werten. */}
        {open && <Formular clubId={clubId} pruefung={pruefung} onGespeichert={onGespeichert} />}
      </BottomSheetContent>
    </Sheet>
  );
}

function Formular({
  clubId,
  pruefung,
  onGespeichert,
}: {
  clubId: string;
  pruefung?: VereinsPruefung;
  onGespeichert: (gespeichert: VereinsPruefung) => void;
}) {
  const t = useT();
  const { ordnungen, fehler: ordnungenFehler } = useOrdnungen(true);
  const [datum, setDatum] = useState(pruefung?.date ?? "");
  const [uhrzeit, setUhrzeit] = useState(pruefung?.startTime?.slice(0, 5) ?? "");
  const [ort, setOrt] = useState(pruefung?.location ?? "");
  const [richter, setRichter] = useState(pruefung?.judgeName ?? "");
  const [meldeschluss, setMeldeschluss] = useState(pruefung?.registrationDeadline ?? "");
  const [bemerkung, setBemerkung] = useState(pruefung?.note ?? "");
  const [gewaehlt, setGewaehlt] = useState<string[]>(pruefung?.offeredRegulations.map((o) => o.id) ?? []);
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefeTag(t, { datum, ort });
  const datumGesperrt = pruefung !== undefined && hatErgebnisse(pruefung.entries);
  // Auch eine schon angebotene Ordnung bleibt wählbar, wenn der Katalog sie (noch) nicht geliefert hat.
  const auswahl = [
    ...(ordnungen ?? []),
    ...(pruefung?.offeredRegulations ?? []).filter((o) => !(ordnungen ?? []).some((x) => x.id === o.id)),
  ];

  async function speichern(e: FormEvent) {
    e.preventDefault();
    if (fehler) return;
    setSpeichert(true);
    try {
      const body = {
        date: datum,
        startTime: uhrzeit ? `${uhrzeit}:00` : null,
        location: ort.trim(),
        judgeName: richter.trim() || null,
        registrationDeadline: meldeschluss || null,
        note: bemerkung.trim() || null,
        regulationIds: gewaehlt,
      };
      const gespeichert = pruefung
        ? await api.put<VereinsPruefung>(`/api/clubs/${clubId}/exams/${pruefung.id}`, body)
        : await api.post<VereinsPruefung>(`/api/clubs/${clubId}/exams`, body);
      toast.success(pruefung ? t("Prüfung gespeichert.") : t("Prüfung angelegt."));
      onGespeichert(gespeichert);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Prüfung konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <form onSubmit={speichern} className="flex min-w-0 flex-col gap-4 p-4 pt-0">
      {/* Datum und Uhrzeit untereinander am Telefon: Nebeneinander können die Felder in Safari über den Rand laufen. */}
      <div className="grid min-w-0 grid-cols-1 gap-3 min-[420px]:grid-cols-2">
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="pruefung-datum">{t("Datum")}</Label>
          <Input
            id="pruefung-datum"
            type="date"
            required
            value={datum}
            onChange={(e) => setDatum(e.target.value)}
            disabled={datumGesperrt}
            className="min-w-0"
          />
          {/* Der Server lehnt eine Datumsänderung mit Ergebnissen ab: die Oberfläche sperrt das Feld gleich. */}
          {datumGesperrt && <p className="text-xs text-muted-foreground">{t("Das Datum lässt sich nicht mehr ändern, sobald Ergebnisse eingetragen sind.")}</p>}
        </div>
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor="pruefung-zeit">{t("Uhrzeit")}</Label>
          <Input id="pruefung-zeit" type="time" value={uhrzeit} onChange={(e) => setUhrzeit(e.target.value)} className="min-w-0" />
        </div>
      </div>
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="pruefung-ort">{t("Ort")}</Label>
        <Input id="pruefung-ort" required value={ort} maxLength={TEXTLAENGE.pruefungsOrt} onChange={(e) => setOrt(e.target.value)} />
      </div>
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="pruefung-richter">{t("Richter:in")}</Label>
        <Input
          id="pruefung-richter"
          value={richter}
          maxLength={TEXTLAENGE.pruefungsRichter}
          placeholder={t("(optional)")}
          onChange={(e) => setRichter(e.target.value)}
        />
      </div>
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="pruefung-meldeschluss">{t("Meldeschluss")}</Label>
        <Input
          id="pruefung-meldeschluss"
          type="date"
          value={meldeschluss}
          onChange={(e) => setMeldeschluss(e.target.value)}
          className="min-w-0"
        />
        <p className="text-xs text-muted-foreground">{t("Optional. Nur zur Anzeige – gesperrt wird nach dem Meldeschluss nichts.")}</p>
      </div>
      <div className="flex min-w-0 flex-col gap-2">
        <Label>{t("Angebotene Ordnungen")}</Label>
        {ordnungenFehler ? (
          <p className="text-sm text-destructive">{t("Die Prüfungsordnungen konnten nicht geladen werden.")}</p>
        ) : ordnungen === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <OrdnungsWahl
            ordnungen={auswahl}
            gewaehlt={gewaehlt}
            onToggle={(id) => setGewaehlt((alt) => (alt.includes(id) ? alt.filter((x) => x !== id) : [...alt, id]))}
          />
        )}
        <p className="text-xs text-muted-foreground">
          {t("Beim Eintragen eines Teams stehen nur diese Ordnungen zur Wahl. Ohne Auswahl gibt es keine Vorgabe.")}
        </p>
      </div>
      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor="pruefung-bemerkung">{t("Bemerkung")}</Label>
        <Input
          id="pruefung-bemerkung"
          value={bemerkung}
          maxLength={TEXTLAENGE.pruefungsBemerkung}
          placeholder={t("(optional)")}
          onChange={(e) => setBemerkung(e.target.value)}
        />
      </div>
      <Button type="submit" disabled={speichert || fehler !== null} className="coarse:min-h-11">
        {speichert ? t("Speichert…") : t("Speichern")}
      </Button>
    </form>
  );
}

"use client";

import { useState } from "react";
import { CheckCircle2, CircleDashed, FileUp, Pencil, Phone, Plus, QrCode as QrIcon, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { telefonLink } from "@/lib/anmeldung";
import { api, ApiError } from "@/lib/api";
import { formatDogAge } from "@/lib/dog-age";
import { useT } from "@/lib/i18n";
import type { GroupRegistration } from "@/lib/types";
import { Chip } from "@/components/ui/chip";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { AktionenMenue } from "@/components/ui/aktionen-menue";

type Filter = "alle" | "offen";

/**
 * Die Anmeldungen einer Gruppe als Liste: neueste oben, "bezahlt" als Umschalter,
 * Bearbeiten und Löschen im Menü.
 *
 * Die Angemeldeten haben kein Dogity-Konto - das steht auch im Hinweis, damit
 * niemand nach ihnen in der Mitgliederliste sucht.
 */
export function RegistrationList({
  groupId,
  registrierungen,
  onGeaendert,
  onNeu,
  onBearbeiten,
  onImport,
  onLink,
}: {
  groupId: string;
  registrierungen: GroupRegistration[];
  /** Eine Anmeldung wurde ersetzt (bezahlt umgeschaltet) bzw. entfernt (null). */
  onGeaendert: (id: string, neu: GroupRegistration | null) => void;
  onNeu: () => void;
  onBearbeiten: (r: GroupRegistration) => void;
  onImport: () => void;
  /** Öffnet Anmeldelink und QR-Code. */
  onLink: () => void;
}) {
  const t = useT();
  const [filter, setFilter] = useState<Filter>("alle");
  const [beschaeftigt, setBeschaeftigt] = useState<string | null>(null);

  const offene = registrierungen.filter((r) => r.paidAt === null).length;
  const sichtbar = filter === "offen" ? registrierungen.filter((r) => r.paidAt === null) : registrierungen;

  async function bezahltUmschalten(r: GroupRegistration) {
    const bezahlt = r.paidAt === null;
    // Die Rücknahme ist die gefährliche Richtung: Wer sich vertippt, würde
    // sonst eine Zahlung "vergessen". Das Setzen braucht keine Rückfrage.
    if (!bezahlt && !window.confirm(t("„Bezahlt“ für {name} zurücknehmen?", { name: r.dogName }))) return;
    setBeschaeftigt(r.id);
    try {
      const neu = await api.put<GroupRegistration>(`/api/groups/${groupId}/registrations/${r.id}/paid`, { paid: bezahlt });
      onGeaendert(r.id, neu);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setBeschaeftigt(null);
    }
  }

  async function loeschen(r: GroupRegistration) {
    if (
      !window.confirm(
        t("Anmeldung von {name} wirklich löschen? Die Angaben und die Anwesenheit werden endgültig entfernt.", { name: r.dogName }),
      )
    )
      return;
    setBeschaeftigt(r.id);
    try {
      await api.delete(`/api/groups/${groupId}/registrations/${r.id}`);
      onGeaendert(r.id, null);
      toast.success(t("Anmeldung gelöscht."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Anmeldung konnte nicht gelöscht werden."));
    } finally {
      setBeschaeftigt(null);
    }
  }

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="text-base">{t("Anmeldungen ({n})", { n: registrierungen.length })}</CardTitle>
        <p className="text-sm text-muted-foreground">{t("Über das Anmeldeformular - ohne Dogity-Konto")}</p>
      </CardHeader>
      <CardContent className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-wrap gap-2">
          <Button variant="ghost" className="text-primary-text" onClick={onLink}>
            <QrIcon className="size-4" />
            {t("Anmeldelink & QR-Code")}
          </Button>
          <Button variant="outline" onClick={onNeu}>
            <Plus className="size-4" />
            {t("Anmeldung hinzufügen")}
          </Button>
          <Button variant="ghost" className="text-primary-text" onClick={onImport}>
            <FileUp className="size-4" />
            {t("Aus Google-Formular importieren")}
          </Button>
        </div>

        {registrierungen.length > 0 && (
          <div className="flex flex-wrap gap-2" role="group" aria-label={t("Anmeldungen filtern")}>
            {(
              [
                ["alle", t("Alle ({n})", { n: registrierungen.length })],
                ["offen", t("Unbezahlt ({n})", { n: offene })],
              ] as const
            ).map(([wert, label]) => (
              <Chip key={wert} kompakt gewaehlt={filter === wert} onClick={() => setFilter(wert)}>
                {label}
              </Chip>
            ))}
          </div>
        )}

        {registrierungen.length === 0 ? (
          <p className="py-4 text-center text-sm text-muted-foreground">{t("Noch keine Anmeldungen.")}</p>
        ) : sichtbar.length === 0 ? (
          <p className="py-4 text-center text-sm text-muted-foreground">{t("Keine unbezahlten Anmeldungen.")}</p>
        ) : (
          <ul className="flex min-w-0 flex-col divide-y">
            {sichtbar.map((r) => {
              const alter = formatDogAge(r.dogBirthDate, new Date(), t);
              const bezahlt = r.paidAt !== null;
              return (
                <li key={r.id} className="flex min-w-0 flex-col gap-2 py-3 first:pt-0 last:pb-0">
                  <div className="flex min-w-0 items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="font-medium [overflow-wrap:anywhere]">{r.dogName}</p>
                      <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
                        {[r.dogBreed, alter].filter(Boolean).join(" · ")}
                      </p>
                      <p className="mt-1 text-sm [overflow-wrap:anywhere]">{`${r.firstName} ${r.lastName}`}</p>
                      <a
                        href={telefonLink(r.phone)}
                        className="inline-flex min-h-9 items-center gap-1.5 text-sm text-primary-text underline-offset-4 hover:underline coarse:min-h-11"
                      >
                        <Phone className="size-3.5 shrink-0" />
                        <span className="[overflow-wrap:anywhere]">{r.phone}</span>
                      </a>
                      {r.notes && <p className="mt-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">{r.notes}</p>}
                    </div>
                    <AktionenMenue
                      label={t("Menü für {name}", { name: r.dogName })}
                      aktionen={[
                        { label: t("Bearbeiten"), icon: Pencil, onSelect: () => onBearbeiten(r) },
                        { label: t("Löschen"), icon: Trash2, onSelect: () => void loeschen(r), destruktiv: true },
                      ]}
                      className="coarse:size-11"
                    />
                  </div>

                  <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                    <button
                      type="button"
                      onClick={() => void bezahltUmschalten(r)}
                      disabled={beschaeftigt === r.id}
                      aria-pressed={bezahlt}
                      aria-label={bezahlt ? t("Bezahlt - zum Zurücknehmen tippen") : t("Nicht bezahlt - als bezahlt markieren")}
                      className="inline-flex min-h-9 items-center coarse:min-h-11 disabled:opacity-50"
                    >
                      <Badge variant={bezahlt ? "default" : "secondary"} className="h-6 gap-1 px-2.5 text-xs">
                        {bezahlt ? <CheckCircle2 /> : <CircleDashed />}
                        {bezahlt ? t("bezahlt") : t("nicht bezahlt")}
                      </Badge>
                    </button>
                    <span className="text-sm text-muted-foreground">{t("{n}× da", { n: r.attendanceCount })}</span>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

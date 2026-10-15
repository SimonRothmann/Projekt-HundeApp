"use client";

import { useState } from "react";
import { Mail } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { DashboardGruppeneinladung, DashboardVereinseinladung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";

/** Eine Zeile der Karte - gleich gebaut für Gruppe und Verein, nur Text und Endpunkt unterscheiden sich. */
type Einladung = {
  schluessel: string;
  /** Der Endpunkt ohne "/accept" bzw. "/decline". */
  pfad: string;
  bereich: string;
  titel: string;
  hinweis: string;
  zugesagt: string;
};

/**
 * Offene Einladungen in eine Trainingsgruppe oder einen Verein, direkt unter
 * dem Kopf der Startseite - damit ein neues Mitglied nach der Vereinsfreigabe
 * nicht erst auf /clubs suchen muss, wo es annimmt.
 *
 * Mitglied wird nur, wer selbst zustimmt: Die Einladung nimmt niemanden auf,
 * erst "Annehmen" tut es. Der Hinweis, was das bedeutet (bei der Gruppe: die
 * Trainer:innen sehen dann Tagebuch, Ziele und Fährten; beim Verein: sie sehen
 * Namen und E-Mail-Adresse), steht deshalb gleich dabei und nicht erst hinter
 * einem zweiten Schritt. Nach dem Annehmen lädt die Startseite neu - dann
 * erscheinen ggf. die Termine der Gruppe.
 *
 * Ohne offene Einladung erscheint nichts.
 */
export function EinladungenKarte({
  gruppen,
  vereine,
  onChanged,
}: {
  gruppen: DashboardGruppeneinladung[] | undefined;
  vereine: DashboardVereinseinladung[] | undefined;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const [laeuft, setLaeuft] = useState<string | null>(null);

  // Vereine zuerst: Wer in einen Verein eingeladen ist, wird oft gleich danach
  // in eine seiner Gruppen eingeladen - die Reihenfolge entspricht dem Weg.
  const einladungen: Einladung[] = [
    ...(vereine ?? []).map((e) => ({
      schluessel: `verein-${e.clubId}`,
      pfad: `/api/clubs/${e.clubId}/invitation`,
      bereich: t("Einladung in einen Verein"),
      titel: t("Einladung in den Verein {verein}", { verein: e.clubName }),
      hinweis: t("Nimmst du an, bist du Mitglied im Verein - die Trainer:innen sehen dann deinen Namen und deine E-Mail-Adresse."),
      zugesagt: t("Du bist jetzt Mitglied im Verein {verein}.", { verein: e.clubName }),
    })),
    ...(gruppen ?? []).map((e) => ({
      schluessel: `gruppe-${e.groupId}`,
      pfad: `/api/groups/${e.groupId}/invitation`,
      bereich: t("Einladung in eine Gruppe"),
      titel: e.clubName
        ? t("Einladung in die Gruppe {gruppe} ({verein})", { gruppe: e.groupName, verein: e.clubName })
        : t("Einladung in die Gruppe {gruppe}", { gruppe: e.groupName }),
      hinweis: t(
        "Nimmst du an, können die Trainer:innen der Gruppe deine Hunde betreuen - sie sehen dann Tagebuch, Ziele und Fährten.",
      ),
      zugesagt: t("Du bist jetzt Mitglied von {gruppe}.", { gruppe: e.groupName }),
    })),
  ];

  if (einladungen.length === 0) return null;

  async function antworten(einladung: Einladung, annehmen: boolean) {
    setLaeuft(einladung.schluessel);
    try {
      await api.post(`${einladung.pfad}/${annehmen ? "accept" : "decline"}`);
      toast.success(annehmen ? einladung.zugesagt : t("Einladung abgelehnt."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setLaeuft(null);
    }
  }

  return (
    <>
      {einladungen.map((einladung) => (
        <section
          key={einladung.schluessel}
          aria-label={einladung.bereich}
          className="flex min-w-0 flex-col gap-2 rounded-xl border border-l-2 border-primary/25 border-l-primary bg-primary/6 p-3 dark:border-primary/35 dark:bg-primary/12"
        >
          <p className="flex min-w-0 items-start gap-2 text-sm font-medium">
            <Mail className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
            <span className="min-w-0 [overflow-wrap:anywhere]">{einladung.titel}</span>
          </p>
          <p className="text-xs text-muted-foreground">{einladung.hinweis}</p>
          <div className="flex flex-wrap gap-3">
            <Button size="sm" disabled={laeuft === einladung.schluessel} onClick={() => antworten(einladung, true)}>
              {t("Annehmen")}
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={laeuft === einladung.schluessel}
              onClick={() => antworten(einladung, false)}
            >
              {t("Ablehnen")}
            </Button>
          </div>
        </section>
      ))}
    </>
  );
}

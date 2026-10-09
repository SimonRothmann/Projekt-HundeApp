"use client";

import { useState } from "react";
import { Mail } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { DashboardGruppeneinladung } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";

/**
 * Offene Einladungen in eine Trainingsgruppe, direkt unter dem Kopf der
 * Startseite - damit ein neues Mitglied nach der Vereinsfreigabe nicht erst
 * auf /clubs suchen muss, wo es annimmt.
 *
 * Mitglied wird nur, wer selbst zustimmt: Die Einladung nimmt niemanden auf,
 * erst "Annehmen" tut es. Der Hinweis, was das bedeutet (die Trainer:innen
 * sehen dann Tagebuch, Ziele und Fährten), steht deshalb gleich dabei und
 * nicht erst hinter einem zweiten Schritt. Nach dem Annehmen lädt die
 * Startseite neu - dann erscheinen ggf. die Termine der Gruppe.
 *
 * Ohne offene Einladung erscheint nichts.
 */
export function GruppeneinladungKarte({
  einladungen,
  onChanged,
}: {
  einladungen: DashboardGruppeneinladung[] | undefined;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const [laeuft, setLaeuft] = useState<string | null>(null);

  if (!einladungen || einladungen.length === 0) return null;

  async function antworten(einladung: DashboardGruppeneinladung, annehmen: boolean) {
    setLaeuft(einladung.groupId);
    try {
      await api.post(`/api/groups/${einladung.groupId}/invitation/${annehmen ? "accept" : "decline"}`);
      toast.success(
        annehmen
          ? t("Du bist jetzt Mitglied von {gruppe}.", { gruppe: einladung.groupName })
          : t("Einladung abgelehnt."),
      );
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
          key={einladung.groupId}
          aria-label={t("Einladung in eine Gruppe")}
          className="flex min-w-0 flex-col gap-2 rounded-xl border border-l-2 border-primary/25 border-l-primary bg-primary/6 p-3 dark:border-primary/35 dark:bg-primary/12"
        >
          <p className="flex min-w-0 items-start gap-2 text-sm font-medium">
            <Mail className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
            <span className="min-w-0 [overflow-wrap:anywhere]">
              {einladung.clubName
                ? t("Einladung in die Gruppe {gruppe} ({verein})", { gruppe: einladung.groupName, verein: einladung.clubName })
                : t("Einladung in die Gruppe {gruppe}", { gruppe: einladung.groupName })}
            </span>
          </p>
          <p className="text-xs text-muted-foreground">
            {t(
              "Nimmst du an, können die Trainer:innen der Gruppe deine Hunde betreuen - sie sehen dann Tagebuch, Ziele und Fährten.",
            )}
          </p>
          <div className="flex flex-wrap gap-3">
            <Button size="sm" disabled={laeuft === einladung.groupId} onClick={() => antworten(einladung, true)}>
              {t("Annehmen")}
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={laeuft === einladung.groupId}
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

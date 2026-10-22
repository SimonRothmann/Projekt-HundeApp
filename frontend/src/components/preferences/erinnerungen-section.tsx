"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { usePreferences } from "@/lib/preferences-context";
import {
  STUNDEN,
  WOCHENTAGE,
  gueltigeStunde,
  stundeText,
  tagGewaehlt,
  tagUmschalten,
} from "@/lib/erinnerungen";
import { SchalterZeile } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import { useT } from "@/lib/i18n";

/**
 * Die Erinnerungen, die Dogity von sich aus schickt: Termin am Vortag, Fährte
 * noch nicht abgelaufen und - nur für Trainer:innen - die Zusammenfassung offener
 * Rückmeldungen. Jede lässt sich einzeln abschalten; die Zusammenfassung hat
 * einen eigenen Rhythmus (Wochentage und Uhrzeit), den die Trainer:in selbst wählt.
 *
 * Erinnerungen erscheinen in der Glocke und, wo Push eingeschaltet ist, als
 * Mitteilung - deshalb steht dieser Abschnitt unabhängig davon da, ob der Server
 * Push eingerichtet hat. Alle Werte gehen gemeinsam an den Server (ein Aufruf,
 * eine Prüfung); die Regeln dazu stehen in lib/erinnerungen.ts.
 */
export function ErinnerungenSection() {
  const t = useT();
  const { isTrainer } = useAuth();
  const { preferences, reload } = usePreferences();
  const [speichert, setSpeichert] = useState(false);

  const werte = {
    sessions: preferences.reminderSessions,
    tracks: preferences.reminderTracks,
    trainerSummary: preferences.reminderTrainerSummary,
    trainerSummaryDays: preferences.trainerSummaryDays,
    trainerSummaryHour: gueltigeStunde(preferences.trainerSummaryHour),
  };

  async function speichern(aenderung: Partial<typeof werte>) {
    setSpeichert(true);
    try {
      await api.put("/api/preferences/reminders", { ...werte, ...aenderung });
      await reload();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einstellung konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  function tagWechseln(bit: number) {
    const neu = tagUmschalten(werte.trainerSummaryDays, bit);
    if (neu !== werte.trainerSummaryDays) void speichern({ trainerSummaryDays: neu });
  }

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <div className="px-1">
        <h3 className="text-sm font-medium">{t("Erinnerungen")}</h3>
        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          {t("Erinnerungen kommen als Meldung in der Glocke und als Push, wenn du Push eingeschaltet hast. Zwischen 22 und 8 Uhr ist Ruhe.")}
        </p>
      </div>
      <ListenGruppe>
        <SchalterZeile
          an={werte.sessions}
          disabled={speichert}
          onChange={(an) => void speichern({ sessions: an })}
          titel={t("Erinnerung an Termine am Vortag")}
          beschreibung={t("Ab 18 Uhr für Gruppentermine am nächsten Tag")}
        />
        <SchalterZeile
          an={werte.tracks}
          disabled={speichert}
          onChange={(an) => void speichern({ tracks: an })}
          titel={t("Erinnerung an noch nicht abgelaufene Fährten")}
          beschreibung={t("Etwa drei Stunden nach dem Legen, solange der Ablauf noch möglich ist")}
        />
        {isTrainer && (
          <SchalterZeile
            an={werte.trainerSummary}
            disabled={speichert}
            onChange={(an) => void speichern({ trainerSummary: an })}
            titel={t("Zusammenfassung offener Rückmeldungen")}
            beschreibung={t("Wie viele Trainings auf deine Rückmeldung warten")}
          />
        )}
        {isTrainer && werte.trainerSummary && (
          <div className="flex min-w-0 flex-col gap-3 px-4 py-3">
            <div className="flex min-w-0 flex-col gap-2">
              <span className="text-xs text-muted-foreground">{t("An diesen Tagen")}</span>
              <div className="flex flex-wrap gap-2">
                {WOCHENTAGE.map((tag) => {
                  const aktiv = tagGewaehlt(werte.trainerSummaryDays, tag.bit);
                  return (
                    <button
                      key={tag.bit}
                      type="button"
                      disabled={speichert}
                      aria-pressed={aktiv}
                      aria-label={t(tag.lang)}
                      onClick={() => tagWechseln(tag.bit)}
                      className={cn(
                        "flex size-11 shrink-0 items-center justify-center rounded-full border text-sm transition-colors disabled:opacity-50",
                        aktiv
                          ? "border-primary bg-primary/15 font-medium text-primary-text"
                          : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
                      )}
                    >
                      {t(tag.kurz)}
                    </button>
                  );
                })}
              </div>
              <span className="text-xs text-muted-foreground">{t("Mindestens ein Tag bleibt gewählt.")}</span>
            </div>
            <div className="flex min-w-0 flex-col gap-2">
              <span className="text-xs text-muted-foreground">{t("Uhrzeit")}</span>
              <Select
                value={String(werte.trainerSummaryHour)}
                disabled={speichert}
                onValueChange={(v) => {
                  const stunde = Number(v);
                  if (v && stunde !== werte.trainerSummaryHour) void speichern({ trainerSummaryHour: stunde });
                }}
              >
                <SelectTrigger className="h-11 w-32 max-w-full" aria-label={t("Uhrzeit")}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {STUNDEN.map((h) => (
                    <SelectItem key={h} value={String(h)}>
                      {stundeText(h)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        )}
      </ListenGruppe>
    </div>
  );
}

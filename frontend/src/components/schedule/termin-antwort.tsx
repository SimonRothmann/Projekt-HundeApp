"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { GroupTrainingSession } from "@/lib/types";
import { mitAntwort } from "@/lib/termin-zusage";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Zu- und Absage zu Gruppenterminen ("Ich komme" / "Kann nicht") - die Logik
 * hinter der Terminkarte der Startseite (siehe docs/GROUP_TRAINING_SCHEDULE.md).
 *
 * Die Antwort erscheint sofort und wird bei einem Fehler zurückgenommen - auf
 * dem Platz hat man schlechten Empfang, und ein Knopf, der erst nach einer
 * Sekunde reagiert, wird doppelt getippt. Mitglieder sehen Zahlen, keine
 * Namen; wer kommt, sehen nur die Trainer:innen.
 *
 * Eigene Antworten eilen der Anzeige voraus. Kommen neue Termine von der
 * Startseite (Nachladen), gelten wieder deren Werte - siehe die Anpassung
 * während des Renderns.
 */
export function useTerminAntworten(sessions: GroupTrainingSession[]) {
  const t = useT();
  const [basis, setBasis] = useState(sessions);
  const [lokal, setLokal] = useState<Record<string, GroupTrainingSession>>({});
  const [sendet, setSendet] = useState<string | null>(null);
  if (basis !== sessions) {
    setBasis(sessions);
    setLokal({});
  }

  const angezeigt = (s: GroupTrainingSession) => lokal[s.id] ?? s;

  async function antworten(s: GroupTrainingSession, kommt: boolean) {
    const vorher = angezeigt(s);
    if (vorher.myResponse === kommt || sendet) return;
    setSendet(s.id);
    setLokal((l) => ({ ...l, [s.id]: mitAntwort(vorher, kommt) }));
    try {
      const neu = await api.put<GroupTrainingSession>(`/api/group-training/schedule/sessions/${s.id}/response`, { attending: kommt });
      setLokal((l) => ({ ...l, [s.id]: neu }));
    } catch (err) {
      setLokal((l) => ({ ...l, [s.id]: vorher }));
      toast.error(err instanceof ApiError ? err.message : t("Deine Antwort konnte nicht gespeichert werden."));
    } finally {
      setSendet(null);
    }
  }

  return { angezeigt, antworten, sendet };
}

/** Die beiden Antwortknöpfe zu einem Termin. */
export function AntwortKnoepfe({
  termin,
  gesperrt,
  onAntwort,
}: {
  termin: GroupTrainingSession;
  gesperrt: boolean;
  onAntwort: (kommt: boolean) => void;
}) {
  const t = useT();
  return (
    <div role="group" aria-label={t("Kommst du zum Training?")} className="flex flex-wrap gap-1.5">
      {[
        { kommt: true, text: t("Ich komme") },
        { kommt: false, text: t("Kann nicht") },
      ].map((wahl) => {
        const aktiv = termin.myResponse === wahl.kommt;
        return (
          <button
            key={wahl.text}
            type="button"
            aria-pressed={aktiv}
            disabled={gesperrt}
            onClick={() => onAntwort(wahl.kommt)}
            className={cn(
              "rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-11 disabled:opacity-60",
              aktiv
                ? wahl.kommt
                  ? "border-primary bg-primary/15 font-medium text-primary-text"
                  : "border-destructive/50 bg-destructive/10 font-medium text-destructive"
                : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
            )}
          >
            {wahl.text}
          </button>
        );
      })}
    </div>
  );
}

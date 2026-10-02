"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import type { GroupTrainingCategory, GroupTrainingSession } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { CalendarDays, MapPin } from "lucide-react";
import { KalenderKnopf } from "@/components/schedule/kalender-knopf";
import { kannAntworten, mitAntwort } from "@/lib/termin-zusage";
import { cn } from "@/lib/utils";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { uebersetzbar } from "@/lib/i18n/sprachen";

const categoryLabel: Record<GroupTrainingCategory, string> = { 0: uebersetzbar("Welpen"), 1: uebersetzbar("Junghunde"), 2: uebersetzbar("Basis") };
const fmt = (iso: string, ort: string) =>
  new Date(iso).toLocaleString(ort, { weekday: "short", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/**
 * Die nächsten Gruppentrainings der eigenen Gruppen (siehe
 * docs/GROUP_TRAINING_SCHEDULE.md), mit Zu- und Absage: "Ich komme" / "Kann
 * nicht". Rendert nichts, wenn es keine kommenden Termine gibt – dann bleibt
 * das Dashboard unverändert.
 *
 * Die Termine lädt die Startseite zusammen mit ihren übrigen Daten, damit sie
 * in einem Zug erscheint statt Abschnitt für Abschnitt.
 *
 * Die Antwort erscheint sofort und wird bei einem Fehler zurückgenommen - auf
 * dem Platz hat man schlechten Empfang, und ein Knopf, der erst nach einer
 * Sekunde reagiert, wird doppelt getippt. Mitglieder sehen Zahlen, keine
 * Namen; wer kommt, sehen nur die Trainer:innen.
 */
export function UpcomingTrainingsSection({ sessions }: { sessions: GroupTrainingSession[] }) {
  const t = useT();
  const ort = ortsformat(useSprache());
  // Eigene Antworten, die der Anzeige vorauseilen. Kommen neue Termine von der
  // Startseite (Nachladen), gelten wieder deren Werte - siehe die Anpassung
  // während des Renderns direkt darunter.
  const [basis, setBasis] = useState(sessions);
  const [lokal, setLokal] = useState<Record<string, GroupTrainingSession>>({});
  const [sendet, setSendet] = useState<string | null>(null);
  if (basis !== sessions) {
    setBasis(sessions);
    setLokal({});
  }

  if (sessions.length === 0) return null;

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

  return (
    <Card>
      <CardHeader className="p-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <CalendarDays className="size-5 text-primary-text" />
          {t("Nächste Gruppentrainings")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2 p-3 pt-0">
        {sessions.slice(0, 5).map((quelle) => {
          const s = angezeigt(quelle);
          return (
          // Abgesagt: kein Abdunkeln des ganzen Termins (das drückte auch die
          // Schrift unter die Lesbarkeitsgrenze), sondern Titel durchgestrichen
          // und gedämpft, dazu das Abzeichen in voller Deckkraft.
          <div key={s.id} className="rounded-md border p-2.5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className={cn("text-sm font-medium [overflow-wrap:anywhere]", s.status === 1 && "text-muted-foreground line-through")}>
                {fmt(s.startsAt, ort)} · {s.groupName}
              </span>
              <span className="flex shrink-0 items-center gap-1">
                <Badge variant="secondary">{t(categoryLabel[s.category])}</Badge>
                {s.status === 1 && <Badge variant="destructive">{t("Abgesagt")}</Badge>}
              </span>
            </div>
            {s.location && (
              <p className="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
                <MapPin className="size-3" />
                {s.location}
              </p>
            )}
            {s.items.length > 0 && (
              <p className="mt-0.5 text-xs text-muted-foreground [overflow-wrap:anywhere]">
                {s.items.map((i) => (i.exercise ? i.exercise.title : i.freeText)).join(" · ")}
              </p>
            )}
            {s.status === 0 && (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                {kannAntworten(s) && (
                  <div role="group" aria-label={t("Kommst du zum Training?")} className="flex flex-wrap gap-1.5">
                    {[
                      { kommt: true, text: t("Ich komme") },
                      { kommt: false, text: t("Kann nicht") },
                    ].map((wahl) => {
                      const aktiv = s.myResponse === wahl.kommt;
                      return (
                        <button
                          key={wahl.text}
                          type="button"
                          aria-pressed={aktiv}
                          disabled={sendet === s.id}
                          onClick={() => antworten(s, wahl.kommt)}
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
                )}
                <span className="text-xs text-muted-foreground">
                  {s.attendingCount === 1 ? t("1 Zusage") : t("{n} Zusagen", { n: s.attendingCount })}
                </span>
                <KalenderKnopf termin={s} className="ml-auto" />
              </div>
            )}
          </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainerSessionToRate } from "@/lib/types";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Check, CheckCheck, ClipboardCheck, MessageSquarePlus, Pencil } from "lucide-react";
import { toast } from "sonner";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { istDurchBewertungErledigt, kannSelbsteinschaetzungUebernehmen } from "@/lib/trainer-bewertung";

/**
 * Trainerseite: alle offenen Trainings der betreuten Hunde in EINER Ansicht -
 * je Trainingstag das Gesamt-Feedback und alle Übungen, ohne ins Tagebuch des
 * jeweiligen Hundes zu wechseln. Ein Training verschwindet, sobald die
 * Trainer:in es abgehakt hat ("Fertig" / "Passt so"), Feedback gegeben hat oder
 * alle Übungen bewertet sind - Feedback ist freiwillig, die Definition steht
 * im Backend (TrainerSessionQueries) und gilt auch für den Zähler.
 *
 * Auf Überblick gebaut, nicht auf Vollständigkeit:
 * - nach Hund gruppiert, der Name steht einmal statt über jedem Training;
 * - je Training ein Zähler "2/3 bewertet", je Übung EINE Zeile;
 * - fertig Bewertetes tritt zurück (gedämpft), Offenes bleibt sichtbar.
 * Vorher stand der Hundename über jeder Karte, jede Übung brauchte zwei Zeilen
 * plus einen Knopf, und nirgends stand, wie viel überhaupt noch offen ist.
 */

/** Was an einem Training noch fehlt - Grundlage für Zähler und Sortierung. */
function openCount(session: TrainerSessionToRate): number {
  const unrated = session.exercises.filter((e) => e.trainerRating === null).length;
  return unrated + (session.trainerFeedback ? 0 : 1);
}

export function TrainerReviewSection() {
  const t = useT();
  const sprache = useSprache();
  const [sessions, setSessions] = useState<TrainerSessionToRate[] | null>(null);
  const [openFeedbackId, setOpenFeedbackId] = useState<string | null>(null);
  const [feedbackText, setFeedbackText] = useState("");
  const [savingFeedback, setSavingFeedback] = useState(false);
  // Das Training, dessen Knopf gerade arbeitet - sperrt beide Knöpfe der Karte
  // gegen Doppeltippen, ohne die anderen Karten festzuhalten.
  const [arbeitetId, setArbeitetId] = useState<string | null>(null);
  // Karten, die erst durch einen Stern erledigt wurden: der Server führt sie
  // nicht mehr als offen, sie bleiben aber bis zum Neuladen der Seite stehen.
  // Sonst verschwände die Karte unter dem Finger - ein Fehltipp auf der letzten
  // Übung ließe sich nicht mehr korrigieren und Notiz oder Feedback gingen nicht
  // mehr. Abhaken ("Fertig" / "Passt so") oder Feedback räumt sie weg.
  const bleibtStehen = useRef(new Set<string>());

  async function load() {
    try {
      const data = await api.get<TrainerSessionToRate[]>("/api/trainings/trainer/sessions");
      setSessions((alt) => {
        if (!alt) return data;
        const imServer = new Set(data.map((x) => x.sessionId));
        return [...data, ...alt.filter((x) => bleibtStehen.current.has(x.sessionId) && !imServer.has(x.sessionId))];
      });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Trainings konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, []);

  function startFeedback(sessionId: string, current: string | null) {
    setOpenFeedbackId(sessionId);
    setFeedbackText(current ?? "");
  }

  async function saveFeedback(sessionId: string) {
    if (!feedbackText.trim()) return;
    setSavingFeedback(true);
    try {
      await api.put(`/api/trainings/${sessionId}/feedback`, { feedback: feedbackText });
      toast.success(t("Feedback gespeichert."));
      setOpenFeedbackId(null);
      // Mit Feedback ist das Training bewusst abgeschlossen - es darf gehen.
      bleibtStehen.current.delete(sessionId);
      await load();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Feedback konnte nicht gespeichert werden."));
    } finally {
      setSavingFeedback(false);
    }
  }

  // Die Karte verschwindet sofort; der Abruf danach gleicht mit dem Server ab
  // (auch wenn die Aktion scheiterte, dann steht sie wieder da).
  function ausblenden(sessionId: string) {
    bleibtStehen.current.delete(sessionId);
    setSessions((aktuell) => (aktuell ? aktuell.filter((x) => x.sessionId !== sessionId) : aktuell));
  }

  // Die Übung lokal nachziehen statt neu zu laden: Der Abruf liefert ein
  // Training, dessen letzte Übung gerade Sterne bekam, nicht mehr mit.
  function sterneGespeichert(sessionId: string, exerciseId: string, rating: number, note: string | null) {
    bleibtStehen.current.add(sessionId);
    setSessions((aktuell) =>
      aktuell
        ? aktuell.map((x) =>
            x.sessionId !== sessionId
              ? x
              : {
                  ...x,
                  exercises: x.exercises.map((e) =>
                    e.exerciseId === exerciseId ? { ...e, trainerRating: rating, trainerNote: note } : e,
                  ),
                },
          )
        : aktuell,
    );
    return Promise.resolve();
  }

  async function rueckgaengig(sessionId: string) {
    try {
      await api.delete(`/api/trainings/${sessionId}/trainer-reviewed`);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das ging nicht zurück."));
    }
    await load();
  }

  async function fertig(sessionId: string) {
    setArbeitetId(sessionId);
    ausblenden(sessionId);
    try {
      await api.put(`/api/trainings/${sessionId}/trainer-reviewed`);
      toast.success(t("Training abgehakt."), {
        action: { label: t("Rückgängig"), onClick: () => void rueckgaengig(sessionId) },
        duration: 8000,
      });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das Training konnte nicht abgehakt werden."));
    } finally {
      setArbeitetId(null);
    }
    await load();
  }

  async function passtSo(sessionId: string) {
    setArbeitetId(sessionId);
    ausblenden(sessionId);
    try {
      await api.put(`/api/trainings/${sessionId}/accept-self-ratings`);
      toast.success(t("Selbsteinschätzung übernommen."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Selbsteinschätzung konnte nicht übernommen werden."));
    } finally {
      setArbeitetId(null);
    }
    await load();
  }

  // Nach Hund gruppieren, Reihenfolge der Hunde nach dem ältesten offenen
  // Training: was am längsten wartet, steht oben.
  // Nach dogId gruppieren, nicht nach dem Namen: zwei Hunde dürfen "Bella"
  // heißen, und dann liefen die Trainings zweier Teams unter einer Überschrift
  // zusammen - mit dem Namen des erstbesten Hundeführers daneben.
  const byDog = new Map<string, { dogName: string; handlerName: string; sessions: TrainerSessionToRate[] }>();
  for (const s of sessions ?? []) {
    const entry = byDog.get(s.dogId) ?? { dogName: s.dogName, handlerName: s.handlerName, sessions: [] };
    entry.sessions.push(s);
    byDog.set(s.dogId, entry);
  }
  for (const entry of byDog.values()) entry.sessions.sort((a, b) => a.date.localeCompare(b.date));

  // Anzahl Trainings, nicht offener Einzelpunkte: dieselbe Einheit wie die
  // Kachel "Zu erledigen" auf der Übersicht, von der man hierher kommt.
  // Stehengebliebene, schon erledigte Karten zählen nicht mit.
  const totalOpen = (sessions ?? []).filter((x) => !istDurchBewertungErledigt(x)).length;

  return (
    <Card>
      <CardHeader className="items-center">
        <CardTitle className="flex items-center gap-2 text-base">
          <ClipboardCheck className="size-5" />
{t("Trainings bewerten")}
        </CardTitle>
        {totalOpen > 0 && (
          <CardAction>
            <Badge variant="secondary">{t("{n} offen", { n: totalOpen })}</Badge>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        {sessions === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">
{t("Alles erledigt – im Moment ist nichts mehr zu bewerten.")}
          </p>
        ) : (
          <div className="flex flex-col gap-4">
            {Array.from(byDog.entries()).map(([dogId, { dogName, handlerName, sessions: dogSessions }]) => (
              <div key={dogId} className="flex min-w-0 flex-col gap-2">
                <p className="flex flex-wrap items-baseline gap-x-1.5 text-sm font-semibold">
                  <span className="[overflow-wrap:anywhere]">{dogName}</span>
                  <span className="text-xs font-normal text-muted-foreground [overflow-wrap:anywhere]">
                    {handlerName}
                  </span>
                </p>

                {dogSessions.map((s) => {
                  const rated = s.exercises.filter((e) => e.trainerRating !== null).length;
                  const open = openCount(s);
                  const uebernehmbar = kannSelbsteinschaetzungUebernehmen(s);
                  return (
                    <div key={s.sessionId} className="min-w-0 rounded-md border p-2.5">
                      <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
                        <span className="text-xs text-muted-foreground">
                          {new Date(s.date).toLocaleDateString(ortsformat(sprache))} · {t("{n} Min.", { n: s.durationMinutes })}
                        </span>
                        <Badge variant={open === 0 ? "secondary" : "outline"} className="shrink-0">
                          {s.exercises.length > 0
                            ? t("{bewertet}/{gesamt} bewertet", { bewertet: rated, gesamt: s.exercises.length })
                            : t("nur Feedback")}
                        </Badge>
                      </div>

                      {s.exercises.length > 0 && (
                        <ul className="mt-2 flex flex-col gap-2">
                          {s.exercises.map((ex) => (
                            <li
                              key={ex.exerciseId}
                              // Bewertetes tritt zurück, damit das Auge beim
                              // Offenen hängen bleibt.
                              className={`flex min-w-0 flex-col gap-0.5 ${ex.trainerRating !== null ? "opacity-60" : ""}`}
                            >
                              <span className="flex flex-wrap items-baseline justify-between gap-x-2 text-sm">
                                <span className="min-w-0 [overflow-wrap:anywhere]">{ex.exerciseName}</span>
                                {/* Selbsteinschätzung des Hundeführers - klein
                                    und gedämpft, sie ist hier nur Kontext. */}
                                <span className="shrink-0 text-xs text-muted-foreground">
                                  {t("Selbst:")} {"★".repeat(ex.rating)}
                                  {"☆".repeat(5 - ex.rating)} {ex.success ? "✓" : "✗"}
                                </span>
                              </span>
                              <ExerciseTrainerRating
                                exerciseId={ex.exerciseId}
                                rating={ex.trainerRating}
                                note={ex.trainerNote}
                                canEdit
                                onSaved={(rating, note) => sterneGespeichert(s.sessionId, ex.exerciseId, rating, note)}
                              />
                            </li>
                          ))}
                        </ul>
                      )}

                      <div className="mt-2 border-t pt-2">
                        {openFeedbackId === s.sessionId ? (
                          <div className="flex flex-col gap-2">
                            <textarea
                              className="min-h-16 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
                              value={feedbackText}
                              maxLength={TEXTLAENGE.trainerRueckmeldung}
                              onChange={(e) => setFeedbackText(e.target.value)}
                              placeholder={t("Gesamt-Feedback zu diesem Training…")}
                              autoFocus
                            />
                            <div className="flex gap-2 self-start">
                              <Button size="sm" onClick={() => saveFeedback(s.sessionId)} disabled={savingFeedback}>
{t("Speichern")}
                              </Button>
                              <Button size="sm" variant="ghost" onClick={() => setOpenFeedbackId(null)}>
{t("Abbrechen")}
                              </Button>
                            </div>
                          </div>
                        ) : s.trainerFeedback ? (
                          <div className="flex min-w-0 items-start gap-1 text-xs opacity-60">
                            <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">„{s.trainerFeedback}“</span>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="size-6 shrink-0"
                              onClick={() => startFeedback(s.sessionId, s.trainerFeedback)}
                              title={t("Feedback bearbeiten")}
                            >
                              <Pencil className="size-3" />
                            </Button>
                          </div>
                        ) : (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 px-2 text-xs text-muted-foreground"
                            onClick={() => startFeedback(s.sessionId, null)}
                          >
                            <MessageSquarePlus className="size-3.5" />
                            {t("Gesamt-Feedback geben")}
                          </Button>
                        )}
                      </div>

                      {/* Feedback und Sterne sind freiwillig: Wer nichts zu sagen
                          hat, hakt das Training ab. "Passt so" nur, wenn es
                          Selbsteinschätzungen zu übernehmen gibt. */}
                      <div className="mt-2 flex flex-col gap-1.5 border-t pt-2">
                        <div className="flex flex-wrap gap-2">
                          {uebernehmbar && (
                            <Button
                              variant="secondary"
                              className="min-w-0 flex-1"
                              disabled={arbeitetId === s.sessionId}
                              onClick={() => void passtSo(s.sessionId)}
                            >
                              <CheckCheck />
                              {t("Passt so")}
                            </Button>
                          )}
                          <Button
                            variant="outline"
                            className="min-w-0 flex-1"
                            disabled={arbeitetId === s.sessionId}
                            onClick={() => void fertig(s.sessionId)}
                          >
                            <Check />
                            {t("Fertig")}
                          </Button>
                        </div>
                        {uebernehmbar && (
                          <p className="text-xs text-muted-foreground">
                            {t("„Passt so“ übernimmt die Selbsteinschätzung der Hundeführer:in als deine Bewertung.")}
                          </p>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

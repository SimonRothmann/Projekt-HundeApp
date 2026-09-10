"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { BlockLabel } from "@/components/ui/block-label";
import { Input } from "@/components/ui/input";
import {
  CalendarDays,
  Check,
  ChevronDown,
  ChevronRight,
  Clock,
  History,
  ListChecks,
  MessageSquarePlus,
  Pencil,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import { GpsTrackSection } from "@/components/tracking/gps-track-section";
import { SessionContextEditor } from "@/components/dogs/session-context-editor";
import { TrainerFeedback } from "@/components/dogs/trainer-feedback";
import { ExerciseNotes } from "@/components/dogs/exercise-notes";
import { ExerciseRating } from "@/components/dogs/exercise-rating";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

import { useT } from "@/lib/i18n";
// Monatsschlüssel im Format "2026-07" für die Gruppierung; toLocaleDateString
// mit month:"long" liefert die Anzeige-Version ("Juli 2026").
function monthKey(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, "0")}`;
}
function monthLabel(iso: string): string {
  return new Date(iso).toLocaleDateString("de-DE", { month: "long", year: "numeric" });
}

/**
 * Ob eine Einheit Angaben zu Ort, Zeit, Wetter oder Verfassung trägt.
 *
 * Ein Trainingstag zeigt diese Angaben EINMAL. Liegen an einem Tag mehrere
 * Einheiten (Fährtenaufnahmen älterer Stände bekamen je eine eigene), steht
 * dort die, in der tatsächlich etwas eingetragen ist - sonst die erste.
 */
function hatKontext(s: TrainingSession): boolean {
  return !!s.startTime || !!s.locationName || s.latitude != null || s.condition != null || s.temperatureC != null;
}

// Ein Trainingstag gilt als abgeschlossen, sobald sein Datum in der
// Vergangenheit liegt (vor dem heutigen Tag) - dann entfällt z.B. das
// erneute Ablaufen der Fährte.
function isCompletedDay(iso: string): boolean {
  const d = new Date(iso);
  d.setHours(0, 0, 0, 0);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return d.getTime() < today.getTime();
}

/**
 * Tages-Kommentar: zeigt die Notizen aller Trainingseinheiten des Tages als
 * EINEN Text und speichert Änderungen als Tages-Kommentar (auf der ersten
 * Einheit; Notizen weiterer Alt-Einheiten desselben Tages werden dabei
 * konsolidiert, damit es künftig nur noch einen Text pro Tag gibt).
 */
function DayNotes({ sessions, onChanged }: { sessions: TrainingSession[]; onChanged: () => Promise<void> }) {
  const t = useT();
  // Gleiche Notizen nur einmal: Jede Fährtenaufnahme älterer Stände legte
  // eine eigene Einheit mit der Notiz "Fährtenaufnahme" an - zwei Fährten an
  // einem Tag ergaben "Fährtenaufnahme / Fährtenaufnahme".
  const joined = [
    ...new Set(sessions.map((s) => s.notes?.trim()).filter((n): n is string => !!n)),
  ].join("\n");
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(joined);
  const [saving, setSaving] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Textarea mit dem Inhalt mitwachsen lassen - langer Tages-Kommentar bricht um
  // statt einzeilig gequetscht zu werden (Mobile-App-first, kein H-Scroll).
  function autoGrow() {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`;
  }
  useEffect(() => {
    if (editing) autoGrow();
  }, [editing]);

  async function save() {
    setSaving(true);
    try {
      // Kompletter Tagestext auf die erste Einheit, Notizen der übrigen
      // Einheiten leeren - konsolidiert Alt-Daten mit mehreren Einheiten/Tag.
      await api.put(`/api/trainings/${sessions[0].id}/notes`, { notes: value.trim() || null });
      for (const s of sessions.slice(1)) {
        if (s.notes) await api.put(`/api/trainings/${s.id}/notes`, { notes: null });
      }
      setEditing(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Kommentar konnte nicht gespeichert werden."));
    } finally {
      setSaving(false);
    }
  }

  if (editing) {
    return (
      <div className="flex w-full min-w-0 flex-col gap-1.5">
        <textarea
          ref={textareaRef}
          className="max-h-[200px] min-h-16 w-full min-w-0 resize-none rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
          placeholder={t("Kommentar zum Trainingstag")}
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            autoGrow();
          }}
          rows={2}
          autoFocus
        />
        <div className="flex items-center justify-end gap-2">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setValue(joined);
              setEditing(false);
            }}
          >
{t("Abbrechen")}
          </Button>
          <Button size="sm" onClick={save} disabled={saving}>
            <Check className="size-3.5" />
{t("Speichern")}
          </Button>
        </div>
      </div>
    );
  }

  if (!joined) {
    return (
      <Button
        size="sm"
        variant="ghost"
        className="h-7 self-start px-2 text-xs text-muted-foreground"
        onClick={() => {
          setValue("");
          setEditing(true);
        }}
      >
        <MessageSquarePlus className="size-3.5" />
        Tages-Kommentar
      </Button>
    );
  }

  // Abgesetzt als Zitatblock: der Tages-Kommentar ist ein eigener Gedanke und
  // stand bisher als grauer Fließtext zwischen Kopfdaten und Übungsliste,
  // ohne erkennbar zu einem der beiden zu gehören oder eben nicht.
  return (
    <div className="flex items-start gap-1 rounded-lg border border-l-2 border-surface-border border-l-primary/70 bg-surface px-2.5 py-2">
      <p className="min-w-0 flex-1 text-sm whitespace-pre-line [overflow-wrap:anywhere]">{joined}</p>
      <Button
        size="icon"
        variant="ghost"
        className="size-6 shrink-0"
        onClick={() => {
          setValue(joined);
          setEditing(true);
        }}
        title={t("Tages-Kommentar bearbeiten")}
      >
        <Pencil className="size-3" />
      </Button>
    </div>
  );
}

/**
 * Datum eines Trainingstags korrigieren.
 *
 * Trainings werden oft erst abends oder Tage später nachgetragen und landen
 * dann auf dem Tag, an dem man sie eingetippt hat.
 *
 * Verschoben wird der GANZE Trainingstag - eine Karte kann mehrere Einheiten
 * enthalten (eine Fährtenaufnahme bekommt eine eigene). Darum kümmert sich der
 * Server in einem Rutsch, der zieht auch das Wetter neu: der gespeicherte Wert
 * gehörte zum alten Tag (siehe TrainingService.MoveTrainingDayAsync).
 */
function DayDate({ sessions, onChanged }: { sessions: TrainingSession[]; onChanged: () => Promise<void> }) {
  const t = useT();
  const date = sessions[0].date;
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(date);
  const [saving, setSaving] = useState(false);

  async function save() {
    // Datum weggewischt oder unverändert: einfach zumachen, statt für einen
    // abgebrochenen Versuch eine Fehlermeldung zu zeigen.
    if (!value || value === date) {
      setEditing(false);
      return;
    }
    setSaving(true);
    try {
      // Ein Request für den ganzen Tag: der Server nimmt alle Einheiten dieses
      // Tages mit (siehe MoveTrainingDayAsync). Selbst zu schleifen hieße, den
      // Tag bei einem Fehler auf halbem Weg zerrissen zu hinterlassen.
      await api.put(`/api/trainings/${sessions[0].id}/date`, { date: value });
      // Neues Datum nennen: die Hundeseite lädt nur die letzten drei Monate,
      // ein weiter zurück verschobenes Training verschwindet sonst wortlos aus
      // der Liste und sieht wie gelöscht aus.
      toast.success(`Training auf den ${new Date(value).toLocaleDateString("de-DE")} verschoben.`);
      setEditing(false);
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Datum konnte nicht geändert werden."));
    } finally {
      setSaving(false);
    }
  }

  if (!editing) {
    return (
      <CardTitle className="flex min-w-0 items-center gap-0.5 text-base font-semibold tracking-tight">
        <CalendarDays className="mr-1 size-4 shrink-0 text-primary" />
        <span className="truncate">{new Date(date).toLocaleDateString("de-DE")}</span>
        <Button
          size="icon"
          variant="ghost"
          className="size-6 shrink-0"
          onClick={() => {
            // Frisch aus der Einheit füllen: die Hundeseite rendert erst aus
            // dem Lesecache und reicht die Netzantwort nach - useState hätte
            // sonst noch den Stand von vorhin.
            setValue(date);
            setEditing(true);
          }}
          title={t("Datum ändern")}
        >
          <Pencil className="size-3" />
        </Button>
      </CardTitle>
    );
  }

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1.5">
      <Input
        type="date"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        // Auf dem Handy eine eigene Zeile: neben den beiden Knöpfen bleiben
        // sonst gut 80px übrig und das Feld zeigt nur noch "19.0".
        className="w-full sm:w-44"
        aria-label={t("Datum des Trainingstags")}
        autoFocus
      />
      <Button size="sm" onClick={save} disabled={saving}>
        <Check className="size-3.5" />
        {saving ? "Speichert…" : t("Speichern")}
      </Button>
      <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
{t("Abbrechen")}
      </Button>
    </div>
  );
}

/**
 * Trainingstagebuch: pro TRAININGSTAG eine Karte (nicht pro Einheit) - alle
 * an einem Tag erfassten Übungen, Fährten und Kommentare in einem Feld.
 * Neue Einträge desselben Tages hängt das Backend ohnehin an die bestehende
 * Einheit an (siehe TrainingService "Tages-Zusammenfassung"); Alt-Daten mit
 * mehreren Einheiten pro Tag werden hier clientseitig zusammengeführt.
 * Monats-Accordion bleibt (neuester Monat automatisch aufgeklappt).
 *
 * onLoadOlder: lädt die komplette Historie nach (initial sind nur die
 * letzten 3 Monate geladen) - null, wenn bereits alles geladen ist.
 */
export function SessionHistory({
  sessions,
  isOwner,
  onChanged,
  onLoadOlder,
}: {
  sessions: TrainingSession[] | null;
  isOwner: boolean;
  onChanged: () => Promise<void>;
  onLoadOlder: (() => Promise<void>) | null;
}) {
  const t = useT();
  const [openMonths, setOpenMonths] = useState<Set<string>>(new Set());
  const [loadingOlder, setLoadingOlder] = useState(false);

  async function deleteDay(daySessions: TrainingSession[]) {
    if (!confirm(t("Trainingstag wirklich löschen? Alle Übungen und Fährten dieses Tages werden entfernt."))) return;
    try {
      for (const s of daySessions) {
        await api.delete(`/api/trainings/${s.id}`);
      }
      toast.success(t("Trainingstag gelöscht."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Löschen fehlgeschlagen."));
    }
  }

  async function handleLoadOlder() {
    if (!onLoadOlder) return;
    setLoadingOlder(true);
    try {
      await onLoadOlder();
    } finally {
      setLoadingOlder(false);
    }
  }

  if (sessions === null) return <p className="text-muted-foreground">{t("Lädt…")}</p>;

  if (sessions.length === 0) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-muted-foreground">
{t("Noch keine Trainingseinheiten erfasst.")}
        </CardContent>
      </Card>
    );
  }

  // Erst nach Monat, darin nach Tag gruppieren. Sessions kommen nach Datum
  // absteigend vom Backend, die Gruppen erben diese Reihenfolge.
  const monthGroups = new Map<string, Map<string, TrainingSession[]>>();
  for (const s of sessions) {
    const mKey = monthKey(s.date);
    const days = monthGroups.get(mKey) ?? new Map<string, TrainingSession[]>();
    const list = days.get(s.date) ?? [];
    list.push(s);
    days.set(s.date, list);
    monthGroups.set(mKey, days);
  }
  const orderedKeys = Array.from(monthGroups.keys());
  // Neuesten Monat automatisch aufklappen, sofern der Nutzer die
  // Sichtbarkeit noch nicht selbst gesteuert hat.
  const effectiveOpen = openMonths.size === 0 && orderedKeys.length > 0 ? new Set([orderedKeys[0]]) : openMonths;

  function toggleMonth(key: string) {
    setOpenMonths((prev) => {
      const next = new Set(prev.size === 0 ? [orderedKeys[0]] : prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <div className="flex flex-col gap-3">
      {orderedKeys.map((mKey) => {
        const days = monthGroups.get(mKey)!;
        const isOpen = effectiveOpen.has(mKey);
        const firstDate = days.keys().next().value as string;
        const dayCount = days.size;
        return (
          // Der Monat ist die oberste Ebene der Liste und bekommt deshalb eine
          // eigene Fläche: eingefärbte Kopfzeile, ruhiger Grund darunter. So
          // ist auf einen Blick zu sehen, welche Trainingstage zusammengehören.
          <div key={mKey} className="overflow-hidden rounded-xl border border-border">
            <button
              type="button"
              onClick={() => toggleMonth(mKey)}
              aria-expanded={isOpen}
              className="flex w-full items-center justify-between gap-2 bg-secondary px-3 py-3 text-left transition-colors hover:bg-secondary/70 coarse:min-h-11"
            >
              <span className="flex min-w-0 items-center gap-2 font-heading font-semibold tracking-tight capitalize">
                {isOpen ? (
                  <ChevronDown className="size-4 shrink-0 text-primary" />
                ) : (
                  <ChevronRight className="size-4 shrink-0 text-primary" />
                )}
                <span className="truncate">{monthLabel(firstDate)}</span>
              </span>
              {/* Die nackte Zahl ließ offen, was sie zählt. */}
              <Badge variant="secondary" className="shrink-0">
                {dayCount === 1 ? t("1 Tag") : t("{anzahl} Tage", { anzahl: dayCount })}
              </Badge>
            </button>
            {isOpen && (
              // Im Dark Mode ohne eigenen Grund: dort gilt "höher = heller"
              // (Material 3, Apple HIG) - die Trainingstage heben sich durch
              // ihre hellere Kartenfläche ab. Ein abgedunkelter Grund darunter
              // war genau das "Dunkelblau auf Dunkelgrau auf Schwarz".
              <div className="flex flex-col gap-4 border-t border-border bg-muted/40 p-3 dark:bg-transparent">
                {Array.from(days.entries()).map(([date, daySessions]) => {
                  const completed = isCompletedDay(date);
                  const totalMinutes = daySessions.reduce((sum, s) => sum + s.durationMinutes, 0);
                  const exercises = daySessions.flatMap((s) => s.exercises);
                  const gpsSessions = daySessions.filter((s) => s.hasGpsTrack);
                  const feedbackSessions = daySessions.filter((s) => s.trainerFeedback);
                  const kontextEinheit = daySessions.find(hatKontext) ?? daySessions[0];
                  return (
                    <Card key={date} className="dark:ring-white/15">
                      {/* Eigene Kopfzeile mit Trennlinie: Datum und Dauer sind
                          die Kennung des Trainingstags, nicht sein erster
                          Inhalt. Der innere flex-Container, weil CardHeader
                          ein Grid ist - "justify-between" darauf hat die
                          beiden nie nebeneinander gebracht, sie standen
                          untereinander. */}
                      <CardHeader className="-mt-4 border-b bg-primary/6 pt-4 dark:bg-white/6">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <DayDate sessions={daySessions} onChanged={onChanged} />
                          <div className="flex shrink-0 items-center gap-1">
                            <Badge variant="outline" className="gap-1 text-muted-foreground">
                              <Clock />
                              {totalMinutes} Min.
                            </Badge>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="size-7 text-muted-foreground hover:text-destructive"
                              onClick={() => deleteDay(daySessions)}
                              title={t("Trainingstag löschen")}
                            >
                              <Trash2 className="size-4" />
                            </Button>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className="flex flex-col gap-3">
                        {/* Ort, Zeit, Wetter, Verfassung sind Angaben ZUM Tag -
                            deshalb direkt unter der Kopfzeile und nicht
                            zwischen den inhaltlichen Blöcken. */}
                        <SessionContextEditor
                          key={`ctx-${kontextEinheit.id}`}
                          session={kontextEinheit}
                          onSaved={onChanged}
                        />
                        <DayNotes sessions={daySessions} onChanged={onChanged} />
                        {exercises.length > 0 && (
                          <section className="flex flex-col gap-1.5">
                            <BlockLabel icon={ListChecks}>{t("Übungen")}</BlockLabel>
                            <ul className="flex flex-col gap-1.5">
                              {exercises.map((ex) => (
                                <li
                                  key={ex.id}
                                  className="flex flex-col gap-1 rounded-lg border border-surface-border bg-surface px-2.5 py-2"
                                >
                                  <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1 text-sm">
                                    <span className="min-w-0 font-medium [overflow-wrap:anywhere]">
                                      {ex.exerciseName}
                                    </span>
                                    <ExerciseRating
                                      exerciseId={ex.id}
                                      rating={ex.rating}
                                      success={ex.success}
                                      notes={ex.notes}
                                      onSaved={onChanged}
                                    />
                                  </div>
                                  <ExerciseNotes exerciseId={ex.id} notes={ex.notes} onSaved={onChanged} />
                                  <ExerciseTrainerRating
                                    exerciseId={ex.id}
                                    rating={ex.trainerRating}
                                    note={ex.trainerNote}
                                    canEdit={!isOwner}
                                    onSaved={onChanged}
                                  />
                                </li>
                              ))}
                            </ul>
                          </section>
                        )}
                        {/* Bereits aufgezeichnete Fährten bleiben sichtbar,
                            auch wenn das Modul aus ist: Sie gehören zum
                            Tagebuch, und wer die Aufzeichnung abschaltet,
                            will seine Aufzeichnungen nicht verlieren.
                            Ausgeblendet wird nur der Einstieg ins NEUE
                            Aufnehmen - siehe GpsTrackSection. */}
                        {gpsSessions.length > 0 && (
                          <GpsTrackSection trainingSessionIds={gpsSessions.map((s) => s.id)} readOnly={completed} />
                        )}
                        {(feedbackSessions.length > 0 ? feedbackSessions : [daySessions[0]]).map((s) => (
                          <TrainerFeedback key={s.id} session={s} isOwner={isOwner} onUpdated={onChanged} />
                        ))}
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            )}
          </div>
        );
      })}
      {onLoadOlder && (
        <Button variant="outline" size="sm" className="self-center" onClick={handleLoadOlder} disabled={loadingOlder}>
          <History className="size-4" />
          {loadingOlder ? t("Lädt…") : t("Ältere Trainings anzeigen")}
        </Button>
      )}
    </div>
  );
}

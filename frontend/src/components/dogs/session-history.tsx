"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Check, ChevronDown, ChevronRight, History, MessageSquarePlus, Pencil, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { GpsTrackSection } from "@/components/tracking/gps-track-section";
import { TrainerFeedback } from "@/components/dogs/trainer-feedback";
import { ExerciseNotes } from "@/components/dogs/exercise-notes";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

// Monatsschlüssel im Format "2026-07" für die Gruppierung; toLocaleDateString
// mit month:"long" liefert die Anzeige-Version ("Juli 2026").
function monthKey(iso: string): string {
  const d = new Date(iso);
  return `${d.getFullYear()}-${(d.getMonth() + 1).toString().padStart(2, "0")}`;
}
function monthLabel(iso: string): string {
  return new Date(iso).toLocaleDateString("de-DE", { month: "long", year: "numeric" });
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
  const joined = sessions
    .map((s) => s.notes)
    .filter((n): n is string => !!n)
    .join("\n");
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(joined);
  const [saving, setSaving] = useState(false);

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
      toast.error(err instanceof ApiError ? err.message : "Kommentar konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  if (editing) {
    return (
      <div className="flex items-center gap-1">
        <Input
          className="h-8 text-sm"
          placeholder="Kommentar zum Trainingstag"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          autoFocus
        />
        <Button size="icon" variant="ghost" className="size-7" onClick={save} disabled={saving} title="Speichern">
          <Check className="size-3.5" />
        </Button>
        <Button
          size="icon"
          variant="ghost"
          className="size-7"
          onClick={() => {
            setValue(joined);
            setEditing(false);
          }}
          title="Abbrechen"
        >
          <X className="size-3.5" />
        </Button>
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

  return (
    <div className="flex items-start gap-1 text-sm text-muted-foreground">
      <p className="whitespace-pre-line">{joined}</p>
      <Button
        size="icon"
        variant="ghost"
        className="size-6 shrink-0"
        onClick={() => {
          setValue(joined);
          setEditing(true);
        }}
        title="Tages-Kommentar bearbeiten"
      >
        <Pencil className="size-3" />
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
  const [openMonths, setOpenMonths] = useState<Set<string>>(new Set());
  const [loadingOlder, setLoadingOlder] = useState(false);

  async function deleteDay(daySessions: TrainingSession[]) {
    if (!confirm("Trainingstag wirklich löschen? Alle Übungen und Fährten dieses Tages werden entfernt.")) return;
    try {
      for (const s of daySessions) {
        await api.delete(`/api/trainings/${s.id}`);
      }
      toast.success("Trainingstag gelöscht.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
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

  if (sessions === null) return <p className="text-muted-foreground">Lädt…</p>;

  if (sessions.length === 0) {
    return (
      <Card>
        <CardContent className="py-10 text-center text-muted-foreground">
          Noch keine Trainingseinheiten erfasst.
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
    <div className="flex flex-col gap-2">
      {orderedKeys.map((mKey) => {
        const days = monthGroups.get(mKey)!;
        const isOpen = effectiveOpen.has(mKey);
        const firstDate = days.keys().next().value as string;
        const dayCount = days.size;
        return (
          <div key={mKey} className="rounded-md border">
            <button
              type="button"
              onClick={() => toggleMonth(mKey)}
              className="flex w-full items-center justify-between px-3 py-2 text-left"
            >
              <span className="flex items-center gap-2 font-medium capitalize">
                {isOpen ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                {monthLabel(firstDate)}
              </span>
              <Badge variant="secondary">{dayCount}</Badge>
            </button>
            {isOpen && (
              <div className="flex flex-col gap-3 border-t p-3">
                {Array.from(days.entries()).map(([date, daySessions]) => {
                  const completed = isCompletedDay(date);
                  const totalMinutes = daySessions.reduce((sum, s) => sum + s.durationMinutes, 0);
                  const exercises = daySessions.flatMap((s) => s.exercises);
                  const gpsSessions = daySessions.filter((s) => s.hasGpsTrack);
                  const feedbackSessions = daySessions.filter((s) => s.trainerFeedback);
                  return (
                    <Card key={date}>
                      <CardHeader className="flex-row items-center justify-between space-y-0">
                        <CardTitle className="text-base">{new Date(date).toLocaleDateString("de-DE")}</CardTitle>
                        <div className="flex items-center gap-2">
                          <Badge variant="secondary">{totalMinutes} Min.</Badge>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="text-destructive hover:text-destructive"
                            onClick={() => deleteDay(daySessions)}
                            title="Trainingstag löschen"
                          >
                            <Trash2 className="size-4" />
                          </Button>
                        </div>
                      </CardHeader>
                      <CardContent className="flex flex-col gap-2">
                        <DayNotes sessions={daySessions} onChanged={onChanged} />
                        {exercises.length > 0 && (
                          <ul className="flex flex-col gap-2">
                            {exercises.map((ex) => (
                              <li key={ex.id} className="flex flex-col gap-0.5">
                                <div className="flex items-center justify-between text-sm">
                                  <span>{ex.exerciseName}</span>
                                  <span className="text-muted-foreground">
                                    {"★".repeat(ex.rating)}
                                    {"☆".repeat(5 - ex.rating)} {ex.success ? "✓" : "✗"}
                                  </span>
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
                        )}
                        {gpsSessions.map((s) => (
                          <GpsTrackSection key={s.id} trainingSessionId={s.id} readOnly={completed} />
                        ))}
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
          {loadingOlder ? "Lädt…" : "Ältere Trainings anzeigen"}
        </Button>
      )}
    </div>
  );
}

"use client";

import { useEffect, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TrainingSession } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { BlockLabel } from "@/components/ui/block-label";
import { Input } from "@/components/ui/input";
import { Check, ListChecks, MessageSquarePlus, Pencil, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { GpsTrackSection } from "@/components/tracking/gps-track-section";
import { SessionContextEditor } from "@/components/dogs/session-context-editor";
import { TrainerFeedback } from "@/components/dogs/trainer-feedback";
import { ExerciseNotes } from "@/components/dogs/exercise-notes";
import { ExerciseRating } from "@/components/dogs/exercise-rating";
import { ExerciseTrainerRating } from "@/components/dogs/exercise-trainer-rating";

import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { istNeuesFeedback } from "@/lib/feedback-gesehen";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { cn } from "@/lib/utils";

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
  // Nur die ANZEIGE übersetzen: die Notiz "Fährtenaufnahme" hat der alte App-
  // Stand selbst geschrieben, sie ist kein Nutzertext. Gespeichert und im
  // Editor gezeigt wird weiter der Rohtext, damit ein Speichern nichts
  // umschreibt.
  const anzeige = joined
    .split("\n")
    .map((zeile) => (zeile === "Fährtenaufnahme" ? t("Fährtenaufnahme") : zeile))
    .join("\n");
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
          className="max-h-[200px] min-h-16 w-full min-w-0 resize-none rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/85 dark:focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
          placeholder={t("Kommentar zum Trainingstag")}
          value={value}
          maxLength={TEXTLAENGE.trainingsNotiz}
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
        {t("Tages-Kommentar")}
      </Button>
    );
  }

  // Abgesetzt als Zitatblock: der Tages-Kommentar ist ein eigener Gedanke und
  // stand bisher als grauer Fließtext zwischen Kopfdaten und Übungsliste,
  // ohne erkennbar zu einem der beiden zu gehören oder eben nicht.
  return (
    <div className="flex items-start gap-1 rounded-lg border border-l-2 border-surface-border border-l-primary/70 bg-surface px-2.5 py-2">
      <p className="min-w-0 flex-1 text-sm whitespace-pre-line [overflow-wrap:anywhere]">{anzeige}</p>
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
 *
 * Als Knopf in der Leiste der aufgeklappten Tageskarte: das Datum selbst steht
 * schon in der Zeile darüber.
 */
function DayDate({ sessions, onChanged }: { sessions: TrainingSession[]; onChanged: () => Promise<void> }) {
  const t = useT();
  const ort = ortsformat(useSprache());
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
      toast.success(t("Training auf den {datum} verschoben.", { datum: new Date(value).toLocaleDateString(ort) }));
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
      <Button
        size="sm"
        variant="ghost"
        className="text-muted-foreground"
        onClick={() => {
          // Frisch aus der Einheit füllen: die Hundeseite rendert erst aus
          // dem Lesecache und reicht die Netzantwort nach - useState hätte
          // sonst noch den Stand von vorhin.
          setValue(date);
          setEditing(true);
        }}
      >
        <Pencil className="size-3.5" />
        {t("Datum ändern")}
      </Button>
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
        {saving ? t("Speichert…") : t("Speichern")}
      </Button>
      <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
{t("Abbrechen")}
      </Button>
    </div>
  );
}

/**
 * Die volle Karte eines aufgeklappten Trainingstags: alles, was zum Tag gehört -
 * Datum ändern und Tag löschen, Ort/Zeit/Verfassung, Kommentar, Übungen mit
 * Bewertung, Fährten mit Karte und Auswertung, Trainer-Feedback.
 *
 * Wird erst gemountet, wenn der Tag aufgeklappt ist (siehe SessionHistory):
 * Die Karte der Fährte und der Abruf der GPS-Daten gehören damit nicht mehr zum
 * Öffnen der Seite, sondern zum Aufschlagen eines Tages. Datum und Dauer stehen
 * schon in der Zeile darüber (SessionDayRow).
 */
export function SessionDayCard({
  tag,
  isOwner,
  dogName,
  fokusId,
  gesehenBeiStart,
  onChanged,
}: {
  /** Alle Einheiten dieses Trainingstags. */
  tag: TrainingSession[];
  isOwner: boolean;
  /** Für das Fährtenbild zum Teilen (Schalter "Hundename zeigen"). */
  dogName?: string;
  /** Der Eintrag, auf den ein Link zeigt: bekommt eine kurze Hervorhebung. */
  fokusId: string | null;
  /** Welches Feedback beim Öffnen der Seite schon gesehen war (für "Neu"). */
  gesehenBeiStart: ReadonlySet<string>;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const datum = tag[0].date;
  const completed = isCompletedDay(datum);
  const exercises = tag.flatMap((s) => s.exercises);
  const gpsSessions = tag.filter((s) => s.hasGpsTrack);
  const feedbackSessions = tag.filter((s) => s.trainerFeedback);
  const kontextEinheit = tag.find(hatKontext) ?? tag[0];

  async function deleteDay() {
    if (!confirm(t("Trainingstag wirklich löschen? Alle Übungen und Fährten dieses Tages werden entfernt."))) return;
    try {
      for (const s of tag) {
        await api.delete(`/api/trainings/${s.id}`);
      }
      toast.success(t("Trainingstag gelöscht."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Löschen fehlgeschlagen."));
    }
  }

  return (
    <div className="flex flex-col gap-3 p-3">
      {/* Datum ändern und löschen gibt es nur im aufgeklappten Tag: in der
          Zeile wären es zwei winzige Knöpfe neben einer Fläche, die man
          zum Aufklappen antippt. */}
      {/* Nur für Besitzer:innen: Datum ändern und löschen prüft der Server
          über die Besitzerschaft, Trainer:innen eines betreuten Hundes
          bekämen nur eine Fehlermeldung. */}
      {isOwner && (
        <div className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
          <DayDate sessions={tag} onChanged={onChanged} />
          <Button
            size="sm"
            variant="ghost"
            className="text-muted-foreground hover:text-destructive"
            onClick={deleteDay}
          >
            <Trash2 className="size-3.5" />
            {t("Trainingstag löschen")}
          </Button>
        </div>
      )}
      {/* Ort, Zeit, Wetter, Verfassung sind Angaben ZUM Tag -
          deshalb direkt unter der Leiste und nicht zwischen den
          inhaltlichen Blöcken. */}
      <SessionContextEditor key={`ctx-${kontextEinheit.id}`} session={kontextEinheit} onSaved={onChanged} />
      <DayNotes sessions={tag} onChanged={onChanged} />
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
                  <span className="min-w-0 font-medium [overflow-wrap:anywhere]">{ex.exerciseName}</span>
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
      {/* Bereits aufgezeichnete Fährten bleiben sichtbar, auch wenn das Modul
          aus ist: Sie gehören zum Tagebuch, und wer die Aufzeichnung
          abschaltet, will seine Aufzeichnungen nicht verlieren. Ausgeblendet
          wird nur der Einstieg ins NEUE Aufnehmen - siehe GpsTrackSection. */}
      {gpsSessions.length > 0 && (
        <GpsTrackSection
          trainingSessionIds={gpsSessions.map((s) => s.id)}
          readOnly={completed}
          hundeName={dogName}
          onChanged={onChanged}
          darfLoeschen={isOwner}
        />
      )}
      {(feedbackSessions.length > 0 ? feedbackSessions : [tag[0]]).map((s) => (
        // Die Kennung, auf die ?eintrag= zeigt. Hervorgehoben wird nur der
        // Eintrag, auf den der Link zeigt.
        <div key={s.id} id={`eintrag-${s.id}`} className={cn("scroll-mt-20 rounded-lg", s.id === fokusId && "eintrag-hervorgehoben")}>
          <TrainerFeedback
            session={s}
            isOwner={isOwner}
            onUpdated={onChanged}
            neu={isOwner && istNeuesFeedback(s, gesehenBeiStart)}
          />
        </div>
      ))}
    </div>
  );
}

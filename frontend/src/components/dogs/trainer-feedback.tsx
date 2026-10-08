"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { FEEDBACK_REACTION, type FeedbackReaction, type TrainingSession } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { BlockLabel } from "@/components/ui/block-label";
import { MessageSquare } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
import { TEXTLAENGE } from "@/lib/textlaengen";

const REAKTIONEN: { wert: FeedbackReaction; text: string }[] = [
  { wert: FEEDBACK_REACTION.Thanks, text: uebersetzbar("Danke") },
  { wert: FEEDBACK_REACTION.Understood, text: uebersetzbar("Verstanden") },
];

const chipKlasse = (aktiv: boolean) =>
  cn(
    "rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-11 disabled:opacity-60",
    aktiv
      ? "border-primary bg-primary/15 font-medium text-primary-text"
      : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
  );

/**
 * Die Antwort der Besitzer:in unter dem Feedback: Danke, Verstanden und eine
 * Rückfrage - mehr nicht. Kein Gespräch und kein Verlauf: je Eintrag eine
 * Reaktion und eine Rückfrage, die beim Ändern überschrieben werden. Schreibt
 * die Trainer:in neues Feedback, sind beide weg (sie bezogen sich auf den alten
 * Text, siehe TrainingService.SetFeedbackAsync).
 *
 * Die Antwort erscheint sofort und wird bei einem Fehler zurückgenommen.
 * Auch die Feedback-Karte der Startseite nutzt sie.
 */
export function FeedbackAntwort({
  session,
  onUpdated,
}: {
  // Nur das, was die Antwort braucht - die Startseite kennt zum Feedback nicht die ganze Einheit.
  session: Pick<TrainingSession, "id" | "ownerReaction" | "ownerReply">;
  onUpdated: () => Promise<void>;
}) {
  const t = useT();
  const [lokal, setLokal] = useState<{ reaction: FeedbackReaction | null; reply: string | null } | null>(null);
  const [fragen, setFragen] = useState(false);
  const [text, setText] = useState("");
  const [sendet, setSendet] = useState(false);

  const reaktion = lokal ? lokal.reaction : (session.ownerReaction ?? null);
  const rueckfrage = lokal ? lokal.reply : (session.ownerReply ?? null);

  async function speichern(neu: { reaction: FeedbackReaction | null; reply: string | null }) {
    setLokal(neu);
    setSendet(true);
    try {
      await api.put(`/api/trainings/${session.id}/feedback-reply`, { reaction: neu.reaction, reply: neu.reply });
      await onUpdated();
      return true;
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Deine Antwort konnte nicht gespeichert werden."));
      return false;
    } finally {
      // Nach dem Neuladen gilt wieder der Stand vom Server; bei einem Fehler
      // ist das der alte - die Anzeige springt also zurück.
      setLokal(null);
      setSendet(false);
    }
  }

  async function senden(e: React.FormEvent) {
    e.preventDefault();
    const neu = text.trim();
    if (!neu) return;
    if (await speichern({ reaction: reaktion, reply: neu })) setFragen(false);
  }

  return (
    <div className="mt-1 flex flex-col gap-2 border-t border-primary/15 pt-2">
      <div role="group" aria-label={t("Antwort auf das Feedback")} className="flex flex-wrap gap-1.5">
        {REAKTIONEN.map((r) => (
          <button
            key={r.wert}
            type="button"
            aria-pressed={reaktion === r.wert}
            disabled={sendet}
            onClick={() => speichern({ reaction: reaktion === r.wert ? null : r.wert, reply: rueckfrage })}
            className={chipKlasse(reaktion === r.wert)}
          >
            {t(r.text)}
          </button>
        ))}
        <button
          type="button"
          aria-pressed={fragen || rueckfrage !== null}
          aria-expanded={fragen}
          disabled={sendet}
          onClick={() => {
            // Mit der bisherigen Rückfrage füllen: Wer sie ändern will, soll
            // nicht alles neu tippen.
            setText(rueckfrage ?? "");
            setFragen((offen) => !offen);
          }}
          className={chipKlasse(fragen || rueckfrage !== null)}
        >
          {t("Rückfrage")}
        </button>
      </div>

      {fragen ? (
        <form onSubmit={senden} className="flex flex-col gap-2">
          <textarea
            className="min-h-16 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-2 text-base md:text-sm"
            value={text}
            maxLength={TEXTLAENGE.feedbackRueckfrage}
            onChange={(e) => setText(e.target.value)}
            placeholder={t("Deine Rückfrage zum Feedback…")}
            aria-label={t("Rückfrage")}
            autoFocus
          />
          <div className="flex flex-wrap gap-2">
            <Button type="submit" size="sm" disabled={sendet || !text.trim()}>
              {t("Senden")}
            </Button>
            {rueckfrage !== null && (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                disabled={sendet}
                onClick={async () => {
                  if (await speichern({ reaction: reaktion, reply: null })) setFragen(false);
                }}
              >
                {t("Zurücknehmen")}
              </Button>
            )}
          </div>
        </form>
      ) : (
        rueckfrage !== null && (
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
            <span className="font-medium">{t("Deine Rückfrage")}: </span>
            {rueckfrage}
          </p>
        )
      )}
    </div>
  );
}

/**
 * Was die Besitzer:in geantwortet hat - für die Trainer:in, unter ihrem
 * Feedback. Nur zum Lesen.
 */
function GegebeneAntwort({ session }: { session: TrainingSession }) {
  const t = useT();
  const reaktion = REAKTIONEN.find((r) => r.wert === session.ownerReaction);
  if (!reaktion && !session.ownerReply) return null;
  return (
    <div className="mt-1 flex flex-col gap-1.5 border-t border-primary/15 pt-2">
      {reaktion && (
        <Badge variant="secondary" className="self-start">
          {t(reaktion.text)}
        </Badge>
      )}
      {session.ownerReply && (
        <p className="text-sm [overflow-wrap:anywhere]">
          <span className="font-medium">{t("Rückfrage")}: </span>
          {session.ownerReply}
        </p>
      )}
    </div>
  );
}

export function TrainerFeedback({
  session,
  isOwner,
  onUpdated,
  neu = false,
}: {
  session: TrainingSession;
  isOwner: boolean;
  onUpdated: () => Promise<void>;
  /** Feedback, das auf diesem Gerät noch nicht gesehen wurde (nur für Besitzer:innen). */
  neu?: boolean;
}) {
  const t = useT();
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(session.trainerFeedback ?? "");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!text.trim()) return;
    setSubmitting(true);
    try {
      await api.put(`/api/trainings/${session.id}/feedback`, { feedback: text });
      toast.success(t("Feedback gespeichert."));
      setEditing(false);
      await onUpdated();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Feedback konnte nicht gespeichert werden."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    // Vorhandenes Feedback ist eine fremde Stimme im eigenen Tagebuch und
    // bekommt deshalb eine eigene, farbig abgesetzte Fläche. Fehlt es, bleibt
    // der gestrichelte Rahmen: dann ist der Block eine Leerstelle, kein Inhalt.
    <div
      className={
        session.trainerFeedback && !editing
          ? "flex flex-col gap-1.5 rounded-lg border border-l-2 border-primary/25 border-l-primary bg-primary/6 p-3 dark:border-primary/35 dark:bg-primary/12"
          : "rounded-lg border border-dashed border-surface-border p-3"
      }
    >
      {session.trainerFeedback && !editing ? (
        <>
          <div className="flex items-start justify-between gap-2">
            <span className="flex min-w-0 items-center gap-2">
              <BlockLabel icon={MessageSquare}>{t("Trainer-Feedback")}</BlockLabel>
              {neu && <Badge>{t("Neu")}</Badge>}
            </span>
            {!isOwner && (
              <Button size="sm" variant="ghost" className="h-6 shrink-0 px-2 text-xs" onClick={() => setEditing(true)}>
{t("Bearbeiten")}
              </Button>
            )}
          </div>
          <p className="text-sm [overflow-wrap:anywhere]">{session.trainerFeedback}</p>
          {isOwner ? <FeedbackAntwort session={session} onUpdated={onUpdated} /> : <GegebeneAntwort session={session} />}
        </>
      ) : !isOwner ? (
        <form onSubmit={handleSubmit} className="flex flex-col gap-2">
          <BlockLabel icon={MessageSquare}>{t("Feedback geben")}</BlockLabel>
          <textarea
            className="min-h-16 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
            value={text}
            maxLength={TEXTLAENGE.trainerRueckmeldung}
            onChange={(e) => setText(e.target.value)}
            placeholder={t("Rückmeldung zu diesem Training…")}
          />
          <div className="flex gap-2 self-start">
            <Button type="submit" size="sm" disabled={submitting}>
{t("Speichern")}
            </Button>
            {editing && (
              <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
{t("Abbrechen")}
              </Button>
            )}
          </div>
        </form>
      ) : (
        <p className="text-sm text-muted-foreground">{t("Noch kein Trainer-Feedback zu diesem Training.")}</p>
      )}
    </div>
  );
}

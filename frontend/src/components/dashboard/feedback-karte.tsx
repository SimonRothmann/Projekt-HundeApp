"use client";

import { useState } from "react";
import Link from "next/link";
import { MessageSquare } from "lucide-react";
import type { OffenesFeedback } from "@/lib/types";
import { FeedbackAntwort } from "@/components/dogs/trainer-feedback";
import { feedbackKarte } from "@/lib/startseite";
import { cn } from "@/lib/utils";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";

/**
 * Das neueste Trainer-Feedback, auf das noch nicht reagiert wurde - über alle
 * Hunde, auch die mitbesessenen. Aufgeklappt stehen Danke / Verstanden /
 * Rückfrage darunter (dieselbe Antwort wie im Tagebuch, nichts doppelt
 * gebaut). Nach der Reaktion lädt die Startseite neu: Die Karte verschwindet
 * oder zeigt das nächste Feedback. Gibt es mehrere, führt "+n weitere" zum
 * nächsten Eintrag auf der Hundeseite.
 *
 * Ohne offenes Feedback erscheint nichts.
 */
export function FeedbackKarte({
  eintraege,
  onChanged,
}: {
  eintraege: OffenesFeedback[] | undefined;
  onChanged: () => Promise<void>;
}) {
  const { aktuell, weitere } = feedbackKarte(eintraege);
  if (!aktuell) return null;

  // key: Beim Wechsel auf das nächste Feedback beginnt die Karte wieder zugeklappt.
  return <Karte key={aktuell.sessionId} feedback={aktuell} weitere={weitere} onChanged={onChanged} />;
}

function Karte({
  feedback,
  weitere,
  onChanged,
}: {
  feedback: OffenesFeedback;
  weitere: OffenesFeedback[];
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const ort = ortsformat(useSprache());
  // Zugeklappt nur Titel und zwei Zeilen Text: Mit ganzem Text, drei
  // Antwortknöpfen und "+n weitere" war die Karte der größte Block der
  // Startseite. Aufgeklappt steht alles wie im Tagebuch.
  const [offen, setOffen] = useState(false);
  const datum = feedback.feedbackAt
    ? new Date(feedback.feedbackAt).toLocaleDateString(ort, { day: "numeric", month: "short" })
    : null;
  const titel = feedback.trainerName
    ? t("Feedback von {trainer} zu {hund}", { trainer: feedback.trainerName, hund: feedback.dogName })
    : t("Feedback zu {hund}", { hund: feedback.dogName });
  const naechstes = weitere[0];

  return (
    <section
      aria-label={t("Trainer-Feedback")}
      className="flex min-w-0 flex-col gap-1.5 rounded-xl border border-l-2 border-primary/25 border-l-primary bg-primary/6 p-3 dark:border-primary/35 dark:bg-primary/12"
    >
      <p className="flex min-w-0 items-start gap-2 text-sm font-medium">
        <MessageSquare className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
        <span className="min-w-0 [overflow-wrap:anywhere]">
          {titel}
          {datum && <span className="font-normal text-muted-foreground"> · {datum}</span>}
        </span>
      </p>

      <p className={cn("text-sm whitespace-pre-line [overflow-wrap:anywhere]", !offen && "line-clamp-2")}>{feedback.feedback}</p>

      {offen && <FeedbackAntwort session={{ id: feedback.sessionId, ownerReaction: null, ownerReply: null }} onUpdated={onChanged} />}

      <div className="-mb-1 flex flex-wrap items-center gap-x-4">
        <button
          type="button"
          aria-expanded={offen}
          onClick={() => setOffen((o) => !o)}
          className="rounded-md py-1.5 text-sm font-medium text-primary-text underline-offset-4 hover:underline coarse:min-h-11"
        >
          {offen ? t("weniger") : t("Lesen & antworten")}
        </button>
        {offen && naechstes && (
          <Link
            href={`/dogs/${naechstes.dogId}?eintrag=${naechstes.sessionId}`}
            className="rounded-md py-1.5 text-sm text-primary-text underline-offset-4 hover:underline coarse:flex coarse:min-h-11 coarse:items-center"
          >
            {t("+{n} weitere", { n: weitere.length })}
          </Link>
        )}
      </div>
    </section>
  );
}

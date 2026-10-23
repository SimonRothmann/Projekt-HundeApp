"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import type { QuizExamAttempt, QuizExamHistory } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * Die letzten Ergebnisse der Übungsprüfung (nur angemeldet - ohne Konto wird
 * nichts gespeichert).
 *
 * Lädt für sich und schweigt bei einem Fehler: Der Verlauf ist Beiwerk, an
 * einer Netzstörung soll weder die Auswertung noch die Katalogseite hängen.
 */
export function useVerlauf(katalogCode: string, angemeldet: boolean): QuizExamHistory | null {
  const [verlauf, setVerlauf] = useState<QuizExamHistory | null>(null);

  useEffect(() => {
    if (!angemeldet) return;
    let aktuell = true;
    api
      .get<QuizExamHistory>(`/api/sachkunde/catalogs/${katalogCode}/exams`)
      .then((antwort) => {
        if (aktuell) setVerlauf(antwort);
      })
      .catch(() => {
        // Beiwerk - siehe oben.
      });
    return () => {
      aktuell = false;
    };
  }, [katalogCode, angemeldet]);

  return angemeldet ? verlauf : null;
}

/** "Übung bestanden" / "Übung nicht bestanden" - nie nur "Bestanden", es ist keine offizielle Prüfung. */
export function useErgebnisWort(): (bestanden: boolean) => string {
  const t = useT();
  return (bestanden) => (bestanden ? t("Übung bestanden") : t("Übung nicht bestanden"));
}

export function PruefungsVerlauf({ verlauf }: { verlauf: QuizExamHistory | null }) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const wort = useErgebnisWort();

  if (!verlauf || verlauf.attempts.length === 0) return null;

  return (
    <section className="min-w-0 rounded-xl border border-border/60 bg-card p-4">
      <h3 className="text-sm font-semibold">{t("Deine letzten Übungsprüfungen")}</h3>
      <p className="mt-0.5 text-xs text-muted-foreground">
        {t("{bestanden} von den letzten {gesamt} als Übung bestanden", {
          bestanden: verlauf.passedOfLastFive,
          gesamt: verlauf.lastFiveCount,
        })}
      </p>
      <ul className="mt-3 flex flex-col divide-y divide-border/60">
        {verlauf.attempts.map((versuch) => (
          <VerlaufZeile key={versuch.id} versuch={versuch} ort={ort} wort={wort} />
        ))}
      </ul>
    </section>
  );
}

function VerlaufZeile({
  versuch,
  ort,
  wort,
}: {
  versuch: QuizExamAttempt;
  ort: string;
  wort: (bestanden: boolean) => string;
}) {
  const t = useT();
  return (
    <li className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5 py-2 text-sm">
      <span className="text-xs text-muted-foreground tabular-nums">
        {new Date(versuch.submittedAt).toLocaleDateString(ort, { day: "2-digit", month: "2-digit", year: "numeric" })}
      </span>
      <span className="min-w-0 font-medium tabular-nums">
        {t("{punkte} von {max} Punkten", { punkte: versuch.points, max: versuch.maxPoints })}
      </span>
      <span
        className={cn(
          "text-xs font-medium",
          versuch.passed ? "text-emerald-600 dark:text-emerald-400" : "text-destructive",
        )}
      >
        {wort(versuch.passed)}
      </span>
    </li>
  );
}

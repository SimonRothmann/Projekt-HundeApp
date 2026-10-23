"use client";

import Link from "next/link";
import { Check, ListChecks, RotateCcw, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PruefungHinweis } from "@/components/sachkunde/pruefung-hinweis";
import { PruefungsVerlauf, useErgebnisWort, useVerlauf } from "@/components/sachkunde/pruefung-verlauf";
import { mitBausteinen, useT } from "@/lib/i18n";
import { balkenBreite, falscheFragen, type Antworten, type FalscheFrage } from "@/lib/sachkunde-pruefung";
import type { QuizExam, QuizExamQuestion, QuizExamResult } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * Die Auswertung einer abgegebenen Übungsprüfung: groß "Übung bestanden" bzw.
 * "Übung nicht bestanden", die Punkte, je Komplex ein Balken und die Liste der
 * falschen Fragen mit eigener und richtiger Antwort.
 *
 * Das Wort ist absichtlich "Übung ...": die Wertungsregel ist die auf
 * Vereinsseiten verbreitete, keine amtliche (siehe PruefungHinweis).
 */
export function PruefungsErgebnis({
  katalogCode,
  exam,
  antworten,
  ergebnis,
  angemeldet,
  beschaeftigt,
  onNeu,
  onFehlerUeben,
  onZurueck,
}: {
  katalogCode: string;
  exam: QuizExam;
  antworten: Antworten;
  ergebnis: QuizExamResult;
  angemeldet: boolean;
  beschaeftigt: boolean;
  onNeu: () => void;
  onFehlerUeben: () => void;
  onZurueck: () => void;
}) {
  const t = useT();
  const wort = useErgebnisWort();
  const falsche = falscheFragen(exam, antworten, ergebnis);
  const verlauf = useVerlauf(katalogCode, angemeldet && ergebnis.saved);

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div
        className={cn(
          "min-w-0 rounded-xl border p-5 text-center",
          ergebnis.passed ? "border-emerald-500/40 bg-emerald-500/10" : "border-destructive/40 bg-destructive/5",
        )}
      >
        <p
          className={cn(
            "flex items-center justify-center gap-2 text-2xl font-extrabold tracking-tight text-balance",
            ergebnis.passed ? "text-emerald-700 dark:text-emerald-300" : "text-destructive",
          )}
        >
          {ergebnis.passed ? <Check className="size-6 shrink-0" /> : <X className="size-6 shrink-0" />}
          {wort(ergebnis.passed)}
        </p>
        <p className="mt-2 text-lg font-semibold tabular-nums">
          {t("{punkte} von {max} Punkten ({prozent} %)", {
            punkte: ergebnis.points,
            max: ergebnis.maxPoints,
            prozent: ergebnis.percent,
          })}
        </p>
        <p className="mt-0.5 text-sm text-muted-foreground tabular-nums">
          {t("Bestanden ab {grenze} Punkten ({prozent} %)", {
            grenze: ergebnis.passPoints,
            prozent: ergebnis.passPercent,
          })}
        </p>
        <p className="mt-0.5 text-sm text-muted-foreground tabular-nums">
          {t("{richtig} von {gesamt} Fragen voll richtig", {
            richtig: ergebnis.correctCount,
            gesamt: ergebnis.questionCount,
          })}
        </p>
      </div>

      <PruefungHinweis />

      <section className="min-w-0 rounded-xl border border-border/60 bg-card p-4">
        <h3 className="text-sm font-semibold">{t("Punkte je Komplex")}</h3>
        <ul className="mt-3 flex flex-col gap-3">
          {ergebnis.sections.map((abschnitt) => (
            <li key={abschnitt.key} className="min-w-0">
              <div className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
                <span className="min-w-0 [overflow-wrap:anywhere]">{abschnitt.name}</span>
                <span className="text-xs text-muted-foreground tabular-nums">
                  {abschnitt.points}/{abschnitt.maxPoints}
                </span>
              </div>
              <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-muted">
                <div
                  className="h-full rounded-full bg-primary"
                  style={{ width: `${balkenBreite(abschnitt.points, abschnitt.maxPoints)}%` }}
                />
              </div>
            </li>
          ))}
        </ul>
      </section>

      {angemeldet ? (
        <p className="text-xs text-muted-foreground">
          {ergebnis.saved
            ? falsche.length > 0
              ? t("Ergebnis gespeichert. Die falschen Fragen liegen jetzt im Fehlerspeicher und kommen beim Üben wieder.")
              : t("Ergebnis gespeichert.")
            : t("Das Ergebnis konnte nicht gespeichert werden.")}
        </p>
      ) : (
        <p className="rounded-md border border-border/60 bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
          {mitBausteinen(
            t("Ohne Anmeldung wird dein Ergebnis nicht gespeichert. {anmelden}, damit falsche Fragen im Fehlerspeicher landen und gezielt wiederkommen."),
            {
              anmelden: (
                <Link href="/register" className="font-medium text-primary-text underline-offset-2 hover:underline">
                  {t("Kostenlos anmelden")}
                </Link>
              ),
            },
          )}
        </p>
      )}

      <div className="flex flex-wrap gap-2">
        {angemeldet && falsche.length > 0 && (
          <Button size="sm" onClick={onFehlerUeben}>
            <ListChecks className="size-4" />
            {t("Falsche Fragen üben")}
          </Button>
        )}
        <Button size="sm" variant={angemeldet && falsche.length > 0 ? "outline" : "default"} disabled={beschaeftigt} onClick={onNeu}>
          <RotateCcw className="size-4" />
          {t("Neue Übungsprüfung")}
        </Button>
        <Button size="sm" variant="ghost" onClick={onZurueck}>
          {t("Zurück zum Üben")}
        </Button>
      </div>

      {falsche.length > 0 && (
        <section className="min-w-0">
          <h3 className="text-sm font-semibold">{t("Falsch beantwortete Fragen")}</h3>
          <ul className="mt-2 flex flex-col gap-3">
            {falsche.map((eintrag) => (
              <FalscheFrageKarte key={eintrag.frage.id} eintrag={eintrag} />
            ))}
          </ul>
        </section>
      )}

      <PruefungsVerlauf verlauf={verlauf} />
    </div>
  );
}

function FalscheFrageKarte({ eintrag }: { eintrag: FalscheFrage }) {
  const t = useT();
  const { frage, ergebnis, antwort } = eintrag;

  return (
    <li className="min-w-0 rounded-xl border border-border/60 bg-card p-4">
      <p className="text-xs text-muted-foreground">
        {frage.number} · {frage.sectionName}
      </p>
      <p className="mt-1.5 text-sm font-medium [overflow-wrap:anywhere]">{frage.text}</p>

      {frage.imageName && (
        // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public.
        <img
          src={`/sachkunde/${frage.imageName}`}
          alt={t("Zeichnung mit fünf Körperhaltungen, von 1 bis 5 nummeriert")}
          width={664}
          height={562}
          className="mt-3 h-auto w-full max-w-md rounded-md border border-border/60 bg-white"
        />
      )}

      {frage.terms.length > 0 ? (
        <ul className="mt-3 flex flex-col gap-1.5 text-sm">
          {frage.terms.map((begriff) => {
            const gewaehlt = antwort.assignments[begriff.id];
            const richtig = ergebnis.correctAssignments[begriff.id] ?? "";
            const stimmt = gewaehlt?.toUpperCase() === richtig.toUpperCase();
            return (
              <li key={begriff.id} className="min-w-0 [overflow-wrap:anywhere]">
                <span className="font-medium">{begriff.text}: </span>
                <span className={stimmt ? "text-emerald-700 dark:text-emerald-300" : "text-destructive"}>
                  {gewaehlt ? schluesselText(frage, gewaehlt) : t("nicht zugeordnet")}
                </span>
                {!stimmt && (
                  <span className="text-muted-foreground">
                    {" · "}
                    {t("richtig: {antwort}", { antwort: schluesselText(frage, richtig) })}
                  </span>
                )}
              </li>
            );
          })}
        </ul>
      ) : (
        <dl className="mt-3 flex flex-col gap-2 text-sm">
          <div className="min-w-0">
            <dt className="text-xs text-muted-foreground">{t("Deine Antwort")}</dt>
            <dd className="mt-0.5 min-w-0 text-destructive [overflow-wrap:anywhere]">
              {antwort.selectedOptionIds.length === 0
                ? t("nicht beantwortet")
                : antwort.selectedOptionIds.map((id) => antwortText(frage, id, t)).join(" · ")}
            </dd>
          </div>
          <div className="min-w-0">
            <dt className="text-xs text-muted-foreground">{t("Richtige Antwort")}</dt>
            <dd className="mt-0.5 min-w-0 text-emerald-700 [overflow-wrap:anywhere] dark:text-emerald-300">
              {ergebnis.correctOptionIds.map((id) => antwortText(frage, id, t)).join(" · ")}
            </dd>
          </div>
        </dl>
      )}
    </li>
  );
}

/** Der Text einer Antwort; bei einer Bildantwort ihre Nummer ("Zeichnung 2"). */
function antwortText(frage: QuizExamQuestion, optionId: string, t: ReturnType<typeof useT>): string {
  const option = frage.options.find((o) => o.id === optionId);
  if (!option) return "";
  return option.imageName ? t("Zeichnung {text}", { text: option.text }) : option.text;
}

/** "E kurzköpfig" bei beschrifteten Schlüsseln, sonst nur "3". */
function schluesselText(frage: QuizExamQuestion, schluessel: string): string {
  const eintrag = frage.keys.find((k) => k.key.toUpperCase() === schluessel.toUpperCase());
  return eintrag?.label ? `${eintrag.key} ${eintrag.label}` : (eintrag?.key ?? schluessel);
}

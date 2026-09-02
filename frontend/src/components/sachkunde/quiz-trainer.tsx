"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import type {
  QuizAnswerResult,
  QuizCatalog,
  QuizMode,
  QuizProgress,
  QuizQuestion,
  QuizSession,
} from "@/lib/types";
import { QuizAssignment } from "@/components/sachkunde/quiz-assignment";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Check, Eye, RotateCcw, X } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

/**
 * Fragentrainer nach dem Muster der Führerschein-Apps.
 *
 * Der Ablauf, den man von dort kennt und der hier nachgebaut ist:
 * - eine Frage je Bildschirm, Antwort antippen, sofort auflösen;
 * - falsch beantwortet heißt: die Frage kommt WIEDER, und zwar noch in
 *   derselben Runde (siehe WIEDERVORLAGE_ABSTAND) - zusätzlich setzt der
 *   Server das Leitner-Fach zurück, damit sie auch an den Folgetagen
 *   wiederkommt;
 * - ist alles durch, endet die Runde ausdrücklich und man kann von vorne
 *   anfangen, statt vor einem leeren Bildschirm zu stehen.
 *
 * Ohne Anmeldung läuft alles außer dem Speichern: der Katalog ist
 * veröffentlichtes Lernmaterial, und wer sich auf die BH vorbereitet, hat oft
 * noch gar keinen Zugang. Die Auswertung passiert dann im Browser.
 */

/** Um wie viele Fragen eine falsch beantwortete Frage nach hinten rutscht. */
const WIEDERVORLAGE_ABSTAND = 4;

/** Wie viele Fragen eine Runde vorlegt. */
const RUNDENGROESSE = 20;

const MODI: { key: QuizMode; label: string }[] = [
  { key: "learn", label: "Lernen" },
  { key: "mistakes", label: "Fehler" },
  { key: "all", label: "Alle" },
];

type Auswertung = { correct: boolean; correctOptionIds: string[]; termResults: Record<string, boolean> };

export function QuizTrainer({ catalog }: { catalog: QuizCatalog }) {
  const { user } = useAuth();
  const angemeldet = user !== null;

  const [mode, setMode] = useState<QuizMode>("learn");
  const [queue, setQueue] = useState<QuizQuestion[] | null>(null);
  const [index, setIndex] = useState(0);
  const [progress, setProgress] = useState<QuizProgress | null>(null);
  const [rundeDurch, setRundeDurch] = useState(false);
  const [gewaehlt, setGewaehlt] = useState<string[]>([]);
  const [auswertung, setAuswertung] = useState<Auswertung | null>(null);
  const [loesungOffen, setLoesungOffen] = useState(false);
  const [beschaeftigt, setBeschaeftigt] = useState(false);
  const [bilanz, setBilanz] = useState({ richtig: 0, falsch: 0 });
  // Zuordnungsaufgaben: je Begriffs-Id der gewählte Schlüssel.
  const [belegung, setBelegung] = useState<Record<string, string>>({});

  const laden = useCallback(
    async (gewuenschterModus: QuizMode) => {
      setQueue(null);
      setIndex(0);
      setGewaehlt([]);
      setBelegung({});
      setAuswertung(null);
      setLoesungOffen(false);
      setBilanz({ richtig: 0, falsch: 0 });

      try {
        if (angemeldet) {
          const session = await api.get<QuizSession>(
            `/api/sachkunde/catalogs/${catalog.code}/session?mode=${gewuenschterModus}&limit=${RUNDENGROESSE}`,
          );
          setQueue(session.questions);
          setProgress(session.progress);
          setRundeDurch(session.roundComplete);
          return;
        }

        // Ohne Anmeldung gibt es keinen Lernstand - also auch keinen
        // Fehlerspeicher und keine Wiedervorlage. Der Katalog wird der Reihe
        // nach durchgegangen.
        const alle = await api.get<QuizQuestion[]>(
          `/api/sachkunde/catalogs/${catalog.code}/questions`,
        );
        setQueue(alle);
        setProgress(null);
        setRundeDurch(false);
      } catch (err) {
        setQueue([]);
        toast.error(err instanceof ApiError ? err.message : "Die Fragen konnten nicht geladen werden.");
      }
    },
    [angemeldet, catalog.code],
  );

  useEffect(() => {
    // Erster Abruf beim Aufbau der Seite (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden(mode);
  }, [laden, mode]);

  const frage = queue?.[index] ?? null;
  const zuordnung = frage?.kind === "Assignment" && frage.terms.length > 0;
  // Nur die offenen Fragen bleiben Selbsteinschätzung - Zuordnungen prüft der
  // Server anhand der Schlüssel.
  const selbstEinschaetzung = frage?.kind === "FreeText" || (frage?.kind === "Assignment" && !zuordnung);
  const mehrfach = frage?.kind === "MultipleChoice";

  /**
   * Eine Antwort auswerten und den Lernstand fortschreiben.
   *
   * Angemeldet entscheidet der Server über richtig/falsch - er kennt die
   * Lösung und schreibt zugleich das Leitner-Fach fort. Ohne Anmeldung wird
   * im Browser nach derselben Regel ausgewertet und nichts gespeichert.
   */
  async function bewerten(
    payload: {
      selectedOptionIds?: string[];
      selfAssessedCorrect?: boolean;
      assignments?: Record<string, string>;
    },
    korrektOhneAnmeldung: boolean,
    begriffErgebnisse: Record<string, boolean> = {},
  ) {
    if (!frage || beschaeftigt || auswertung) return;

    const richtigeIds = frage.options.filter((o) => o.isCorrect).map((o) => o.id);
    setBeschaeftigt(true);

    try {
      const ergebnis: Auswertung = angemeldet
        ? await api
            .post<QuizAnswerResult>(`/api/sachkunde/questions/${frage.id}/answer`, payload)
            .then((a) => {
              // Der Server schickt den Stand nach dieser Antwort mit - ohne das
              // blieb der Balken die ganze Runde stehen.
              if (a.progress) setProgress(a.progress);
              return {
                correct: a.correct,
                correctOptionIds: a.correctOptionIds,
                termResults: a.termResults ?? {},
              };
            })
        : { correct: korrektOhneAnmeldung, correctOptionIds: richtigeIds, termResults: begriffErgebnisse };

      setAuswertung(ergebnis);
      setBilanz((b) => ({
        richtig: b.richtig + (ergebnis.correct ? 1 : 0),
        falsch: b.falsch + (ergebnis.correct ? 0 : 1),
      }));

      // Falsch beantwortet: die Frage noch einmal einreihen. Genau das ist mit
      // "kommt immer wieder" gemeint - nicht erst morgen, sondern gleich. Der
      // Server setzt zusätzlich das Leitner-Fach zurück, damit sie auch an den
      // Folgetagen wieder auftaucht.
      if (!ergebnis.correct) {
        setQueue((bisher) => {
          if (!bisher) return bisher;
          const naechste = [...bisher];
          naechste.splice(Math.min(index + WIEDERVORLAGE_ABSTAND, naechste.length), 0, frage);
          return naechste;
        });
      }
    } catch (err) {
      setGewaehlt([]);
      toast.error(err instanceof ApiError ? err.message : "Die Antwort konnte nicht gespeichert werden.");
    } finally {
      setBeschaeftigt(false);
    }
  }

  function weiter() {
    setGewaehlt([]);
    setBelegung({});
    setAuswertung(null);
    setLoesungOffen(false);
    setIndex((i) => i + 1);
  }

  /** Zuordnung abgeben - richtig ist sie nur, wenn ALLE Begriffe stimmen. */
  function zuordnungPruefen() {
    if (!frage) return;
    const ergebnisse = Object.fromEntries(
      frage.terms.map((term) => [term.id, belegung[term.id] === term.solutionKey]),
    );
    void bewerten(
      { assignments: belegung },
      Object.values(ergebnisse).every(Boolean),
      ergebnisse,
    );
  }

  function antippen(optionId: string) {
    if (auswertung || !frage) return;

    if (mehrfach) {
      setGewaehlt((bisher) =>
        bisher.includes(optionId) ? bisher.filter((id) => id !== optionId) : [...bisher, optionId],
      );
      return;
    }

    // Einfachauswahl: ein Tipp genügt, wie bei den Führerschein-Trainern -
    // antippen ist zugleich abgeben.
    setGewaehlt([optionId]);
    const richtigeIds = frage.options.filter((o) => o.isCorrect).map((o) => o.id);
    void bewerten({ selectedOptionIds: [optionId] }, richtigeIds.length === 1 && richtigeIds[0] === optionId);
  }

  /** Mehrfachauswahl: die Auswahl muss die richtigen Antworten genau treffen. */
  function pruefen() {
    if (!frage) return;
    const richtigeIds = frage.options.filter((o) => o.isCorrect).map((o) => o.id);
    void bewerten(
      { selectedOptionIds: gewaehlt },
      gewaehlt.length === richtigeIds.length && gewaehlt.every((id) => richtigeIds.includes(id)),
    );
  }

  async function vonVorne() {
    if (!angemeldet) {
      await laden(mode);
      return;
    }
    try {
      await api.post(`/api/sachkunde/catalogs/${catalog.code}/reset`);
      toast.success("Lernstand zurückgesetzt – es geht von vorne los.");
      await laden("learn");
      setMode("learn");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Der Lernstand konnte nicht zurückgesetzt werden.");
    }
  }

  // ---- Darstellung ----

  if (queue === null) {
    return <p className="py-10 text-sm text-muted-foreground">Lädt…</p>;
  }

  const durch = index >= queue.length;

  return (
    <div className="flex min-w-0 flex-col gap-4">
      {angemeldet && (
        <div className="flex flex-wrap gap-1.5">
          {MODI.map((m) => (
            <Button
              key={m.key}
              size="sm"
              variant={mode === m.key ? "default" : "outline"}
              onClick={() => setMode(m.key)}
            >
              {m.label}
              {m.key === "mistakes" && progress && progress.inMistakes > 0 && (
                <span className="ml-1 tabular-nums">{progress.inMistakes}</span>
              )}
            </Button>
          ))}
        </div>
      )}

      {progress && <Lernstand progress={progress} />}

      {!angemeldet && (
        <p className="rounded-md border border-border/60 bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
          Du übst ohne Anmeldung – dein Lernstand wird nicht gespeichert.{" "}
          <Link href="/register" className="font-medium text-primary underline-offset-2 hover:underline">
            Kostenlos anmelden
          </Link>
          , damit falsche Fragen gezielt wiederkommen.
        </p>
      )}

      {durch ? (
        <Rundenende
          bilanz={bilanz}
          rundeDurch={rundeDurch && mode === "learn"}
          mode={mode}
          angemeldet={angemeldet}
          onWeiter={() => laden(mode)}
          onVonVorne={vonVorne}
        />
      ) : frage ? (
        <div className="min-w-0 rounded-xl border border-border/60 bg-card p-4 sm:p-5">
          <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
            <span className="text-xs text-muted-foreground">
              Frage {index + 1} von {queue.length} · {frage.sectionName}
            </span>
            <div className="flex items-center gap-1.5">
              {frage.state && frage.state.box > 1 && (
                <Badge variant="secondary" className="text-xs">
                  Fach {frage.state.box}
                </Badge>
              )}
              <Badge variant="outline" className="text-xs">
                {frage.number}
              </Badge>
            </div>
          </div>

          <p className="mt-3 text-base font-medium [overflow-wrap:anywhere]">{frage.text}</p>

          {frage.imageName && (
            // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public, der Optimierer bringt hier nichts.
            <img
              src={`/sachkunde/${frage.imageName}`}
              alt="Zeichnung mit fünf Körperhaltungen, von 1 bis 5 nummeriert"
              width={664}
              height={562}
              className="mt-3 h-auto w-full max-w-md rounded-md border border-border/60 bg-white"
            />
          )}

          {zuordnung ? (
            <QuizAssignment
              terms={frage.terms}
              keys={frage.keys}
              belegung={belegung}
              ergebnisse={auswertung?.termResults ?? null}
              aufgeloest={auswertung !== null}
              beschaeftigt={beschaeftigt}
              onWaehlen={(termId, key) => setBelegung((b) => ({ ...b, [termId]: key }))}
              onPruefen={zuordnungPruefen}
            />
          ) : selbstEinschaetzung ? (
            <SelbstKarte
              solution={frage.sampleSolution}
              offen={loesungOffen}
              auswertung={auswertung}
              beschaeftigt={beschaeftigt}
              onZeigen={() => setLoesungOffen(true)}
              onEinschaetzen={(gewusst) => bewerten({ selfAssessedCorrect: gewusst }, gewusst)}
            />
          ) : (
            <ul className="mt-4 flex flex-col gap-2">
              {frage.options.map((option) => (
                <li key={option.id}>
                  <button
                    type="button"
                    disabled={beschaeftigt || auswertung !== null}
                    onClick={() => antippen(option.id)}
                    aria-pressed={gewaehlt.includes(option.id)}
                    className={cn(
                      "flex w-full min-w-0 items-start gap-2.5 rounded-lg border px-3 py-3 text-left text-sm transition-colors",
                      "coarse:min-h-12 disabled:cursor-default",
                      antwortKlasse(option.id, option.isCorrect, gewaehlt, auswertung),
                    )}
                  >
                    <span className="mt-0.5 shrink-0">
                      {auswertung && option.isCorrect ? (
                        <Check className="size-4" />
                      ) : auswertung && gewaehlt.includes(option.id) ? (
                        <X className="size-4" />
                      ) : (
                        <span
                          className={cn(
                            "block size-4 rounded-full border",
                            gewaehlt.includes(option.id) ? "border-primary bg-primary/30" : "border-muted-foreground/40",
                          )}
                        />
                      )}
                    </span>
                    {option.imageName ? (
                      // Bildantwort: die Zeichnung IST die Antwort, der Text ist
                      // nur ihre Nummer (siehe Jugendfrage 30).
                      // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public.
                      <img
                        src={`/sachkunde/${option.imageName}`}
                        alt={`Zeichnung ${option.text}`}
                        className="h-auto w-full max-w-[14rem] rounded border border-border/40 bg-white"
                      />
                    ) : (
                      <span className="min-w-0 [overflow-wrap:anywhere]">{option.text}</span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}

          {mehrfach && !auswertung && (
            <Button className="mt-3" size="sm" disabled={gewaehlt.length === 0 || beschaeftigt} onClick={pruefen}>
              Prüfen
            </Button>
          )}

          {auswertung && (
            <div className="mt-4 flex flex-wrap items-center justify-between gap-2">
              <span
                className={cn(
                  "text-sm font-medium",
                  auswertung.correct ? "text-emerald-600 dark:text-emerald-400" : "text-destructive",
                )}
              >
                {auswertung.correct ? "Richtig" : "Falsch – die Frage kommt gleich noch einmal."}
              </span>
              <Button size="sm" onClick={weiter} autoFocus>
                Weiter
              </Button>
            </div>
          )}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
        <span className="tabular-nums">
          Diese Runde: {bilanz.richtig} richtig · {bilanz.falsch} falsch
        </span>
        {angemeldet && (
          <Button size="sm" variant="ghost" className="h-7 px-2 text-xs" onClick={vonVorne}>
            <RotateCcw className="size-3.5" />
            Von vorne anfangen
          </Button>
        )}
      </div>
    </div>
  );
}

function antwortKlasse(
  optionId: string,
  istRichtig: boolean,
  gewaehlt: string[],
  auswertung: Auswertung | null,
): string {
  if (!auswertung) {
    return gewaehlt.includes(optionId)
      ? "border-primary bg-primary/10"
      : "border-border/60 hover:border-primary/50 hover:bg-accent/30";
  }
  if (istRichtig) return "border-emerald-500/60 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300";
  if (gewaehlt.includes(optionId)) return "border-destructive/60 bg-destructive/10 text-destructive";
  return "border-border/40 opacity-60";
}

/**
 * Der Lernstand über den Katalog.
 *
 * Vorn steht, wie viele Fragen gerade sitzen - diese Zahl bewegt sich mit jeder
 * Antwort. Dahinter, wie viele sicher sitzen (Leitner-Fach 4 aufwärts, also
 * mehrfach richtig an verschiedenen Tagen); die braucht Zeit.
 *
 * Anfangs stand nur die zweite Zahl da. Wer zwanzig Fragen richtig beantwortet
 * hatte, las weiter "0 von 72" - rechnerisch richtig, als Rückmeldung
 * unbrauchbar. Der Balken zeigt jetzt beides: hell, was sitzt, kräftig, was
 * sicher sitzt.
 */
function Lernstand({ progress }: { progress: QuizProgress }) {
  return (
    <div className="min-w-0 rounded-lg border border-border/60 bg-card p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <span className="text-sm font-medium">
          <span className="tabular-nums">{progress.correct}</span> von{" "}
          <span className="tabular-nums">{progress.total}</span> richtig
        </span>
        <span className="text-xs text-muted-foreground tabular-nums">
          {progress.mastered > 0 && <>{progress.mastered} sitzen sicher</>}
          {progress.inMistakes > 0 && (
            <>
              {progress.mastered > 0 && " · "}
              {progress.inMistakes} im Fehlerspeicher
            </>
          )}
        </span>
      </div>

      <div className="relative mt-2 h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          className="absolute inset-y-0 left-0 rounded-full bg-primary/35 transition-[width] duration-300"
          style={{ width: `${progress.percentCorrect}%` }}
        />
        <div
          className="absolute inset-y-0 left-0 rounded-full bg-primary transition-[width] duration-300"
          style={{ width: `${progress.percentMastered}%` }}
        />
      </div>

      <ul className="mt-2.5 flex flex-wrap gap-x-3 gap-y-1">
        {progress.sections.map((section) => (
          <li key={section.key} className="text-xs text-muted-foreground">
            {section.name}
            <span className="ml-1 tabular-nums">
              {section.correct}/{section.total}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function SelbstKarte({
  solution,
  offen,
  auswertung,
  beschaeftigt,
  onZeigen,
  onEinschaetzen,
}: {
  solution: string | null;
  offen: boolean;
  auswertung: Auswertung | null;
  beschaeftigt: boolean;
  onZeigen: () => void;
  onEinschaetzen: (gewusst: boolean) => void;
}) {
  return (
    <div className="mt-4 flex flex-col gap-3">
      {!offen ? (
        <Button size="sm" variant="outline" className="self-start" onClick={onZeigen}>
          <Eye className="size-4" />
          Lösung zeigen
        </Button>
      ) : (
        <>
          <p className="rounded-lg border border-border/60 bg-muted/40 px-3 py-2.5 text-sm [overflow-wrap:anywhere]">
            {solution}
          </p>
          {!auswertung && (
            <div className="flex flex-wrap gap-2">
              <Button size="sm" disabled={beschaeftigt} onClick={() => onEinschaetzen(true)}>
                <Check className="size-4" />
                Gewusst
              </Button>
              <Button size="sm" variant="outline" disabled={beschaeftigt} onClick={() => onEinschaetzen(false)}>
                <X className="size-4" />
                Nicht gewusst
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function Rundenende({
  bilanz,
  rundeDurch,
  mode,
  angemeldet,
  onWeiter,
  onVonVorne,
}: {
  bilanz: { richtig: number; falsch: number };
  rundeDurch: boolean;
  mode: QuizMode;
  angemeldet: boolean;
  onWeiter: () => void;
  onVonVorne: () => void;
}) {
  const gesamt = bilanz.richtig + bilanz.falsch;

  return (
    <div className="min-w-0 rounded-xl border border-border/60 bg-card p-5 text-center">
      <p className="text-lg font-semibold">
        {rundeDurch ? "Alles durch." : mode === "mistakes" ? "Fehlerspeicher leer." : "Runde geschafft."}
      </p>
      <p className="mt-1 text-sm text-muted-foreground">
        {gesamt === 0
          ? rundeDurch
            ? "Im Moment ist keine Frage zur Wiederholung fällig."
            : "Hier ist gerade nichts zu tun."
          : `${bilanz.richtig} von ${gesamt} richtig.`}
      </p>

      <div className="mt-4 flex flex-wrap justify-center gap-2">
        {!rundeDurch && (
          <Button size="sm" onClick={onWeiter}>
            Weiter üben
          </Button>
        )}
        <Button size="sm" variant={rundeDurch ? "default" : "outline"} onClick={onVonVorne}>
          <RotateCcw className="size-4" />
          Von vorne anfangen
        </Button>
        {!angemeldet && (
          <Link href="/register" className={buttonVariants({ size: "sm", variant: "outline" })}>
            Lernstand speichern
          </Link>
        )}
      </div>
    </div>
  );
}

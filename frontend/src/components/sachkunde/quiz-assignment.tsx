"use client";

import type { QuizKey, QuizTerm } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * Eine Zuordnungsaufgabe: je Begriff ein Schlüssel.
 *
 * Der erste Anlauf hat diese Fragen als Karte zum Selbsteinschätzen gebaut -
 * Lösung aufdecken, "gewusst"/"nicht gewusst". Das war falsch: die
 * Fragestellung lautet "Ordnen Sie den aufgelisteten Stimmungen die
 * abgebildeten Körperhaltungen zu", und aufgelistet war nichts. Man konnte die
 * Aufgabe gar nicht versuchen, nur die Lösung ansehen.
 *
 * Jetzt steht je Begriff eine Zeile mit den wählbaren Schlüsseln. Bei A2 sind
 * das die Ziffern aus der Abbildung, bei A18/A23 die Buchstaben mit ihrer
 * Beschriftung. Geprüft wird erst auf Knopfdruck und nur ganz: eine Zuordnung
 * stimmt, wenn alle Begriffe stimmen.
 *
 * Bewusst Knöpfe statt Ziehen-und-Ablegen: auf dem Telefon ist Antippen
 * treffsicher, Ziehen nicht.
 */
export function QuizAssignment({
  terms,
  keys,
  belegung,
  ergebnisse,
  aufgeloest,
  beschaeftigt,
  onWaehlen,
  onPruefen,
}: {
  terms: QuizTerm[];
  keys: QuizKey[];
  belegung: Record<string, string>;
  /** Nach dem Prüfen: je Begriffs-Id, ob die Zuordnung stimmte. */
  ergebnisse: Record<string, boolean> | null;
  aufgeloest: boolean;
  beschaeftigt: boolean;
  onWaehlen: (termId: string, key: string) => void;
  onPruefen: () => void;
}) {
  const vollstaendig = terms.every((term) => belegung[term.id]);
  const beschriftet = keys.some((k) => k.label);

  return (
    <div className="mt-4 flex flex-col gap-3">
      {beschriftet && (
        <ul className="flex flex-col gap-1 rounded-lg border border-border/60 bg-muted/40 px-3 py-2.5 text-sm">
          {keys.map((k) => (
            <li key={k.key} className="min-w-0 [overflow-wrap:anywhere]">
              <span className="font-semibold">{k.key}</span> {k.label}
            </li>
          ))}
        </ul>
      )}

      <ul className="flex flex-col gap-2.5">
        {terms.map((term) => {
          const gewaehlt = belegung[term.id];
          const ergebnis = ergebnisse?.[term.id];

          return (
            <li key={term.id} className="flex min-w-0 flex-col gap-1.5">
              <span className="flex flex-wrap items-baseline gap-x-2 text-sm">
                <span className="min-w-0 font-medium [overflow-wrap:anywhere]">{term.text}</span>
                {aufgeloest && ergebnis === false && (
                  <span className="text-xs text-muted-foreground">richtig: {term.solutionKey}</span>
                )}
              </span>

              <div className="flex flex-wrap gap-1.5">
                {keys.map((k) => {
                  const aktiv = gewaehlt === k.key;
                  return (
                    <button
                      key={k.key}
                      type="button"
                      disabled={beschaeftigt || aufgeloest}
                      aria-pressed={aktiv}
                      aria-label={k.label ? `${term.text}: ${k.key} – ${k.label}` : `${term.text}: ${k.key}`}
                      onClick={() => onWaehlen(term.id, k.key)}
                      className={cn(
                        "min-w-10 rounded-md border px-3 py-2 text-sm font-medium transition-colors",
                        "coarse:min-h-11 disabled:cursor-default",
                        schluesselKlasse(aktiv, aufgeloest, ergebnis, k.key === term.solutionKey),
                      )}
                    >
                      {k.key}
                    </button>
                  );
                })}
              </div>
            </li>
          );
        })}
      </ul>

      {!aufgeloest && (
        <Button size="sm" className="self-start" disabled={!vollstaendig || beschaeftigt} onClick={onPruefen}>
          Prüfen
        </Button>
      )}
    </div>
  );
}

function schluesselKlasse(
  aktiv: boolean,
  aufgeloest: boolean,
  ergebnis: boolean | undefined,
  istLoesung: boolean,
): string {
  if (!aufgeloest) {
    return aktiv
      ? "border-primary bg-primary/15 text-primary"
      : "border-border/60 hover:border-primary/50 hover:bg-accent/30";
  }
  // Nach dem Auflösen: die richtige Zuordnung immer grün, eine falsch gewählte
  // rot. So sieht man in einem Blick, was man verwechselt hat.
  if (istLoesung) return "border-emerald-500/60 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300";
  if (aktiv && ergebnis === false) return "border-destructive/60 bg-destructive/10 text-destructive";
  return "border-border/40 text-muted-foreground opacity-60";
}

"use client";

import type { QuizQuestion } from "@/lib/types";
import { nachAbschnitten } from "@/lib/public-sachkunde";
import { useT } from "@/lib/i18n";

/**
 * Alle Fragen des Katalogs am Stück, mit Lösung. Wird auch serverseitig
 * gerendert (Client-Komponente, aber mit fertigem HTML).
 *
 * Zwei Gründe, und der zweite wiegt schwerer:
 *
 * 1. Zum Nachlesen. Wer sich auf die Sachkundeprüfung vorbereitet, will den
 *    Stoff auch einmal am Stück durchgehen und nicht nur abgefragt werden.
 *
 * 2. Damit die Seite überhaupt jemand findet. Der Trainer darüber holt seine
 *    Fragen erst im Browser nach; im ausgelieferten HTML stand an dieser
 *    Stelle wörtlich "Lädt…". Für Suchmaschinen war die Seite damit leer -
 *    und die 112 Fragen beider Kataloge, der einzige Inhalt dieser Seite, den
 *    es sonst nirgends in dieser Form gibt, unsichtbar.
 *
 * Die Lösungen stehen offen dabei. Sie sind kein Geheimnis: der Trainer löst
 * nach jeder Antwort sofort auf, eine zugeklappte Liste würde also nichts
 * verbergen, was einen Fingertipp weiter ohnehin offenliegt - sie würde nur
 * den Text wieder aus dem Blickfeld nehmen, um dessentwillen es sie gibt.
 *
 * Übersetzt wird nur, was die Seite bedient (Überschrift, Hinweise, "richtig",
 * Bildbeschreibungen). Die Fragen und Antworten selbst bleiben deutsch - sie
 * sind die Inhalte des deutschen Fragenkatalogs. Im ausgelieferten HTML gilt
 * die Vorgabe Deutsch, die Suchmaschine sieht also dieselbe Seite wie zuvor.
 */
export function FragenUebersicht({ fragen }: { fragen: QuizQuestion[] }) {
  const t = useT();

  // Backend nicht erreichbar (siehe getQuizQuestions): dann trägt die Seite
  // den Trainer allein, statt eine leere Überschrift zu zeigen.
  if (fragen.length === 0) return null;

  return (
    <section className="mt-12 min-w-0 border-t border-border/60 pt-8">
      <h2 className="text-xl font-bold tracking-tight">{t("Alle {n} Fragen mit Lösung", { n: fragen.length })}</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        {t("Zum Durchlesen und Nachschlagen. Abgefragt wird im Trainer weiter oben.")}
      </p>

      {nachAbschnitten(fragen).map((abschnitt) => (
        <div key={abschnitt.key} className="mt-8 min-w-0">
          <h3 className="text-base font-bold [overflow-wrap:anywhere]">{abschnitt.name}</h3>

          <ol className="mt-3 flex flex-col gap-3">
            {abschnitt.fragen.map((frage) => (
              <li key={frage.id} className="min-w-0 rounded-lg border border-border/60 px-3 py-2.5">
                <p className="font-medium [overflow-wrap:anywhere]">
                  <span className="text-muted-foreground">{frage.number}</span> {frage.text}
                </p>

                {frage.imageName && (
                  // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public, der Optimierer bringt hier nichts.
                  <img
                    src={`/sachkunde/${frage.imageName}`}
                    alt={t("Zeichnung zu Frage {n}", { n: frage.number })}
                    className="mt-2 h-auto w-full max-w-md rounded-md border border-border/60 bg-white"
                  />
                )}

                {frage.options.length > 0 && (
                  <ul className="mt-2 flex flex-col gap-1.5">
                    {frage.options.map((option) => (
                      <li key={option.id} className="min-w-0 text-sm">
                        <span className="flex min-w-0 items-baseline gap-2">
                          <span aria-hidden className="text-muted-foreground">
                            –
                          </span>
                          <span className="min-w-0 [overflow-wrap:anywhere]">
                            {/* Bildantwort: die Zeichnung IST die Antwort, der Text
                                ist nur ihre Nummer (siehe Jugendfrage 30). Ohne das
                                Wort davor stünde hier eine nackte Ziffer. */}
                            {option.imageName ? t("Antwort {text}", { text: option.text }) : option.text}
                          </span>
                          {option.isCorrect && (
                            <span className="shrink-0 rounded bg-primary/10 px-1.5 py-0.5 text-xs font-semibold text-primary-text">
                              {t("richtig")}
                            </span>
                          )}
                        </span>
                        {option.imageName && (
                          // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public.
                          <img
                            src={`/sachkunde/${option.imageName}`}
                            alt={t("Zeichnung {text}", { text: option.text })}
                            className="mt-1 ml-4 h-auto w-full max-w-[12rem] rounded border border-border/40 bg-white"
                          />
                        )}
                      </li>
                    ))}
                  </ul>
                )}

                {/* Zuordnungs- und Freitextfragen haben keine ankreuzbaren
                    Antworten - bei ihnen steht die Musterlösung. */}
                {frage.sampleSolution && (
                  <p className="mt-2 text-sm [overflow-wrap:anywhere]">
                    <span className="font-semibold">{t("Lösung:")} </span>
                    <span className="text-muted-foreground">{frage.sampleSolution}</span>
                  </p>
                )}
              </li>
            ))}
          </ol>
        </div>
      ))}
    </section>
  );
}

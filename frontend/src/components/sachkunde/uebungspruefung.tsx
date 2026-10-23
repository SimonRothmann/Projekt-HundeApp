"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { ChevronLeft, ChevronRight, Clock } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import {
  alsAbgabe,
  antwortUmschalten,
  anzahlOffen,
  dauerText,
  istBeantwortet,
  leereAntwort,
  loescheStand,
  schreibeStand,
  zuordnungUmschalten,
  type Antworten,
  type PruefungsStand,
} from "@/lib/sachkunde-pruefung";
import type { QuizCatalog, QuizExam, QuizExamAnswer, QuizExamQuestion, QuizExamResult } from "@/lib/types";
import { QuizAssignment } from "@/components/sachkunde/quiz-assignment";
import { PruefungsErgebnis } from "@/components/sachkunde/pruefung-ergebnis";
import { PruefungHinweis } from "@/components/sachkunde/pruefung-hinweis";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Die Übungsprüfung: Fragen zum Ankreuzen am Stück, ausgewertet erst bei der
 * Abgabe - wie die Prüfungssimulationen der Führerschein-Apps.
 *
 * Anders als im Lernmodus gibt es KEINE Auflösung nach jeder Frage und keinen
 * Hinweis, wie viele Antworten richtig sind. Eine Frage je Bildschirm, vor und
 * zurück, eine Fragenleiste zum Springen, "Abgeben". Eine laufende Prüfung
 * übersteht ein Neuladen (sessionStorage, siehe lib/sachkunde-pruefung.ts).
 *
 * Gewertet wird im Backend. Dort steht auch, dass anonyme Abgaben nichts
 * speichern; die Oberfläche zeigt das auf dem Ergebnis an.
 */

type Auswertung = { exam: QuizExam; antworten: Antworten; ergebnis: QuizExamResult };

/**
 * Bringt den Anfang der Prüfung ins Bild, wenn sich die Ansicht ändert (nächste
 * Frage, Ergebnis). Auf dem Telefon steht der "Weiter"-Knopf unter den
 * Antworten - ohne das läge die neue Frage nach dem Antippen oberhalb des
 * sichtbaren Bereichs, und man müsste jedes Mal hochscrollen.
 */
function useNachObenBeiWechsel(ansicht: string) {
  const oben = useRef<HTMLDivElement>(null);
  const erste = useRef(true);

  useEffect(() => {
    // Beim ersten Aufbau (auch nach einem Neuladen) nichts verschieben.
    if (erste.current) {
      erste.current = false;
      return;
    }
    const kasten = oben.current?.getBoundingClientRect();
    if (kasten && (kasten.top < 0 || kasten.top > window.innerHeight)) oben.current?.scrollIntoView({ block: "start" });
  }, [ansicht]);

  return oben;
}

export function Uebungspruefung({
  catalog,
  anfangsStand,
  onVerlassen,
}: {
  catalog: QuizCatalog;
  /** Eine beim Laden der Seite gefundene laufende Prüfung (aus dem Browserspeicher). */
  anfangsStand: PruefungsStand | null;
  /** Zurück zum Üben; mit `fehlerspeicher` direkt in den Modus "Fehler". */
  onVerlassen: (fehlerspeicher: boolean) => void;
}) {
  const t = useT();
  const { user } = useAuth();
  const angemeldet = user !== null;

  const [stand, setStand] = useState<PruefungsStand | null>(anfangsStand);
  // Spiegel des Stands für die Handler: zwei Tipps kurz hintereinander (vor dem
  // nächsten Zeichnen) sähen mit dem Wert aus dem Rendern sonst beide den alten
  // Stand, und der zweite überschriebe den ersten.
  const standRef = useRef<PruefungsStand | null>(anfangsStand);
  const [auswertung, setAuswertung] = useState<Auswertung | null>(null);
  const [beschaeftigt, setBeschaeftigt] = useState(false);
  const [rueckfrage, setRueckfrage] = useState<"abgeben" | "abbrechen" | null>(null);

  const einzeln = catalog.audience === "Youth";
  const oben = useNachObenBeiWechsel(auswertung ? "ergebnis" : stand ? `frage-${stand.index}` : "start");
  const umhuellen = (inhalt: ReactNode) => (
    <div ref={oben} className="min-w-0 scroll-mt-20">
      {inhalt}
    </div>
  );

  /** Neuen Stand setzen und für ein Neuladen festhalten. */
  function festhalten(neu: PruefungsStand) {
    standRef.current = neu;
    setStand(neu);
    schreibeStand(catalog.code, neu);
  }

  function verwerfen() {
    standRef.current = null;
    loescheStand(catalog.code);
    setStand(null);
  }

  async function starten() {
    if (beschaeftigt) return;
    setBeschaeftigt(true);
    try {
      const exam = await api.post<QuizExam>(`/api/sachkunde/catalogs/${catalog.code}/exam`);
      setAuswertung(null);
      festhalten({ exam, antworten: {}, index: 0, gestartetAm: Date.now() });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Übungsprüfung konnte nicht geladen werden."));
    } finally {
      setBeschaeftigt(false);
    }
  }

  async function abgeben() {
    if (!stand || beschaeftigt) return;
    setBeschaeftigt(true);
    try {
      const ergebnis = await api.post<QuizExamResult>(
        `/api/sachkunde/catalogs/${catalog.code}/exam/submit`,
        alsAbgabe(stand.exam, stand.antworten),
      );
      // Abgegeben ist abgegeben: der Stand muss weg, sonst öffnete ein
      // Neuladen dieselbe Prüfung noch einmal zum Abgeben.
      verwerfen();
      setAuswertung({ exam: stand.exam, antworten: stand.antworten, ergebnis });
      setRueckfrage(null);
    } catch (err) {
      // Die Prüfung bleibt erhalten - nichts ist verloren, man kann es noch einmal versuchen.
      toast.error(err instanceof ApiError ? err.message : t("Die Übungsprüfung konnte nicht abgegeben werden."));
    } finally {
      setBeschaeftigt(false);
    }
  }

  function abbrechen() {
    verwerfen();
    setRueckfrage(null);
    onVerlassen(false);
  }

  function zurueck(fehlerspeicher: boolean) {
    verwerfen();
    onVerlassen(fehlerspeicher);
  }

  // ---- Ergebnis ----

  if (auswertung) {
    return umhuellen(
      <PruefungsErgebnis
        katalogCode={catalog.code}
        exam={auswertung.exam}
        antworten={auswertung.antworten}
        ergebnis={auswertung.ergebnis}
        angemeldet={angemeldet}
        beschaeftigt={beschaeftigt}
        onNeu={starten}
        onFehlerUeben={() => zurueck(true)}
        onZurueck={() => zurueck(false)}
      />,
    );
  }

  // ---- Vor dem Start ----

  if (!stand) {
    return umhuellen(
      <Startseite catalog={catalog} beschaeftigt={beschaeftigt} onStart={starten} onZurueck={() => zurueck(false)} />,
    );
  }

  // ---- Während der Prüfung ----

  const { exam, antworten, index } = stand;
  const frage = exam.questions[index];
  const offen = anzahlOffen(exam, antworten);

  function gehZu(neuerIndex: number) {
    const aktuell = standRef.current;
    if (!aktuell) return;
    festhalten({ ...aktuell, index: Math.min(Math.max(neuerIndex, 0), aktuell.exam.questions.length - 1) });
  }

  function antwortAendern(frageId: string, neu: (alt: QuizExamAnswer) => QuizExamAnswer) {
    const aktuell = standRef.current;
    if (!aktuell) return;
    festhalten({
      ...aktuell,
      antworten: { ...aktuell.antworten, [frageId]: neu(aktuell.antworten[frageId] ?? leereAntwort(frageId)) },
    });
  }

  function abgabeAnstossen() {
    if (offen > 0) setRueckfrage("abgeben");
    else void abgeben();
  }

  return umhuellen(
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
        <Badge variant="secondary">{t("Übungsprüfung")}</Badge>
        <Laufzeit seit={stand.gestartetAm} />
      </div>

      <div className="min-w-0 rounded-xl border border-border/60 bg-card p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
          <span className="text-xs text-muted-foreground">
            {t("Frage {n} von {gesamt}", { n: index + 1, gesamt: exam.questions.length })} · {frage.sectionName}
          </span>
          <Badge variant="outline" className="text-xs">
            {frage.number}
          </Badge>
        </div>

        <p className="mt-3 text-base font-medium [overflow-wrap:anywhere]">{frage.text}</p>

        {frage.imageName && (
          // eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public, der Optimierer bringt hier nichts.
          <img
            src={`/sachkunde/${frage.imageName}`}
            alt={t("Zeichnung mit fünf Körperhaltungen, von 1 bis 5 nummeriert")}
            width={664}
            height={562}
            className="mt-3 h-auto w-full max-w-md rounded-md border border-border/60 bg-white"
          />
        )}

        {frage.terms.length > 0 ? (
          <QuizAssignment
            terms={frage.terms}
            keys={frage.keys}
            belegung={antworten[frage.id]?.assignments ?? {}}
            ergebnisse={null}
            aufgeloest={false}
            beschaeftigt={beschaeftigt}
            onWaehlen={(begriffId, schluessel) =>
              antwortAendern(frage.id, (alt) => zuordnungUmschalten(alt, begriffId, schluessel))
            }
          />
        ) : (
          <AntwortListe
            frage={frage}
            gewaehlt={antworten[frage.id]?.selectedOptionIds ?? []}
            einzeln={einzeln}
            onUmschalten={(optionId) =>
              antwortAendern(frage.id, (alt) => antwortUmschalten(alt, optionId, einzeln))
            }
          />
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button size="sm" variant="outline" disabled={index === 0} onClick={() => gehZu(index - 1)}>
          <ChevronLeft className="size-4" />
          {t("Zurück")}
        </Button>
        <Button size="sm" variant="outline" disabled={index >= exam.questions.length - 1} onClick={() => gehZu(index + 1)}>
          {t("Weiter")}
          <ChevronRight className="size-4" />
        </Button>
        <Button size="sm" className="ml-auto" disabled={beschaeftigt} onClick={abgabeAnstossen}>
          {t("Abgeben")}
        </Button>
      </div>

      {rueckfrage === "abgeben" && (
        <Rueckfrage
          text={
            offen === 1
              ? t("Eine Frage ist noch offen – trotzdem abgeben?")
              : t("{n} Fragen sind noch offen – trotzdem abgeben?", { n: offen })
          }
          bestaetigen={t("Trotzdem abgeben")}
          zurueck={t("Zurück zur Prüfung")}
          beschaeftigt={beschaeftigt}
          onBestaetigen={() => void abgeben()}
          onZurueck={() => setRueckfrage(null)}
        />
      )}

      <Fragenleiste
        fragen={exam.questions}
        antworten={antworten}
        aktuell={index}
        onWaehlen={gehZu}
      />

      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
        <span className="tabular-nums">
          {t("{beantwortet} von {gesamt} beantwortet", {
            beantwortet: exam.questions.length - offen,
            gesamt: exam.questions.length,
          })}
        </span>
        <Button size="sm" variant="ghost" className="h-7 px-2 text-xs" onClick={() => setRueckfrage("abbrechen")}>
          {t("Prüfung abbrechen")}
        </Button>
      </div>

      {rueckfrage === "abbrechen" && (
        <Rueckfrage
          text={t("Prüfung abbrechen? Deine Antworten gehen verloren.")}
          bestaetigen={t("Prüfung abbrechen")}
          zurueck={t("Zurück zur Prüfung")}
          beschaeftigt={beschaeftigt}
          onBestaetigen={abbrechen}
          onZurueck={() => setRueckfrage(null)}
        />
      )}
    </div>,
  );
}

function Startseite({
  catalog,
  beschaeftigt,
  onStart,
  onZurueck,
}: {
  catalog: QuizCatalog;
  beschaeftigt: boolean;
  onStart: () => void;
  onZurueck: () => void;
}) {
  const t = useT();
  const info = catalog.exam;

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="min-w-0 rounded-xl border border-border/60 bg-card p-5">
        <h2 className="text-lg font-bold tracking-tight">{t("Übungsprüfung")}</h2>

        {info && (
          <dl className="mt-3 grid grid-cols-1 gap-x-6 gap-y-2 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-xs text-muted-foreground">{t("Fragen")}</dt>
              <dd className="font-semibold tabular-nums">{info.questionCount}</dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t("Mögliche Punkte")}</dt>
              <dd className="font-semibold tabular-nums">{info.maxPoints}</dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t("Bestanden ab")}</dt>
              <dd className="font-semibold tabular-nums">
                {t("{grenze} Punkten ({prozent} %)", { grenze: info.passPoints, prozent: info.passPercent })}
              </dd>
            </div>
          </dl>
        )}

        <ul className="mt-4 flex list-disc flex-col gap-1 pl-5 text-sm text-muted-foreground">
          <li>{t("Eine Frage je Bildschirm, ausgewertet wird erst bei der Abgabe.")}</li>
          <li>{t("Du kannst vor- und zurückblättern und Antworten ändern. Es gibt keine Zeitbegrenzung.")}</li>
          {catalog.audience === "Youth" && <li>{t("Bei jeder Frage ist genau eine Antwort richtig.")}</li>}
          <li>{t("Freitextfragen sind nicht dabei, sie lassen sich nicht automatisch werten.")}</li>
        </ul>

        <div className="mt-4">
          <PruefungHinweis />
        </div>

        <div className="mt-5 flex flex-wrap gap-2">
          <Button disabled={beschaeftigt} onClick={onStart}>
            {beschaeftigt ? t("Lädt…") : t("Prüfung starten")}
          </Button>
          <Button variant="ghost" onClick={onZurueck}>
            {t("Zurück zum Üben")}
          </Button>
        </div>
      </div>
    </div>
  );
}

/** Die Antworten zum Ankreuzen. Erwachsene: Kästchen (mehrere möglich), Jugend: runde Auswahl (genau eine). */
function AntwortListe({
  frage,
  gewaehlt,
  einzeln,
  onUmschalten,
}: {
  frage: QuizExamQuestion;
  gewaehlt: string[];
  einzeln: boolean;
  onUmschalten: (optionId: string) => void;
}) {
  const t = useT();

  return (
    <ul className="mt-4 flex flex-col gap-2">
      {frage.options.map((option) => {
        const aktiv = gewaehlt.includes(option.id);
        return (
          <li key={option.id}>
            <button
              type="button"
              onClick={() => onUmschalten(option.id)}
              aria-pressed={aktiv}
              className={cn(
                "flex w-full min-w-0 items-start gap-2.5 rounded-lg border px-3 py-3 text-left text-sm transition-colors",
                "coarse:min-h-12",
                aktiv ? "border-primary bg-primary/10" : "border-input hover:border-primary/50 hover:bg-accent/30",
              )}
            >
              <span
                className={cn(
                  "mt-0.5 flex size-4 shrink-0 items-center justify-center border",
                  einzeln ? "rounded-full" : "rounded-[4px]",
                  aktiv ? "border-primary bg-primary text-primary-foreground" : "border-muted-foreground",
                )}
              >
                {aktiv && !einzeln && <Check className="size-3" strokeWidth={3} />}
                {aktiv && einzeln && <span className="block size-1.5 rounded-full bg-primary-foreground" />}
              </span>
              {option.imageName ? (
                // Bildantwort: die Zeichnung ist die Antwort, daneben ihre Nummer (siehe Lernmodus, Jugendfrage 30).
                <span className="flex min-w-0 items-center gap-2.5">
                  <span className="flex size-6 shrink-0 items-center justify-center rounded-full border text-xs font-semibold">
                    {option.text}
                  </span>
                  {/* eslint-disable-next-line @next/next/no-img-element -- feste Zeichnung aus /public. */}
                  <img
                    src={`/sachkunde/${option.imageName}`}
                    alt={t("Zeichnung {text}", { text: option.text })}
                    className="h-auto w-full max-w-[12rem] rounded border border-border/40 bg-white"
                  />
                </span>
              ) : (
                <span className="min-w-0 [overflow-wrap:anywhere]">{option.text}</span>
              )}
            </button>
          </li>
        );
      })}
    </ul>
  );
}

/**
 * Nummern aller Fragen zum Springen: beantwortete gefüllt, offene leer, die
 * aktuelle umrandet. Umbrechend statt seitlich scrollend, damit auch 40
 * Fragen bei 375 px ohne horizontalen Scroll passen.
 */
function Fragenleiste({
  fragen,
  antworten,
  aktuell,
  onWaehlen,
}: {
  fragen: QuizExamQuestion[];
  antworten: Antworten;
  aktuell: number;
  onWaehlen: (index: number) => void;
}) {
  const t = useT();

  return (
    <ul className="flex flex-wrap gap-1.5" aria-label={t("Fragenleiste")}>
      {fragen.map((frage, i) => {
        const beantwortet = istBeantwortet(antworten[frage.id]);
        return (
          <li key={frage.id}>
            <button
              type="button"
              onClick={() => onWaehlen(i)}
              aria-current={i === aktuell ? "step" : undefined}
              aria-label={beantwortet ? t("Frage {n}, beantwortet", { n: i + 1 }) : t("Frage {n}, offen", { n: i + 1 })}
              className={cn(
                "flex size-9 items-center justify-center rounded-md border text-xs font-medium tabular-nums transition-colors coarse:size-11",
                beantwortet
                  ? "border-primary/60 bg-primary/15 text-primary-text"
                  : "border-input text-muted-foreground hover:border-primary/50",
                i === aktuell && "ring-2 ring-primary ring-offset-1 ring-offset-background",
              )}
            >
              {i + 1}
            </button>
          </li>
        );
      })}
    </ul>
  );
}

function Rueckfrage({
  text,
  bestaetigen,
  zurueck,
  beschaeftigt,
  onBestaetigen,
  onZurueck,
}: {
  text: string;
  bestaetigen: string;
  zurueck: string;
  beschaeftigt: boolean;
  onBestaetigen: () => void;
  onZurueck: () => void;
}) {
  return (
    <div role="alert" className="min-w-0 rounded-xl border border-primary/40 bg-primary/5 p-4">
      <p className="text-sm font-medium">{text}</p>
      <div className="mt-3 flex flex-wrap gap-2">
        <Button size="sm" disabled={beschaeftigt} onClick={onBestaetigen}>
          {bestaetigen}
        </Button>
        <Button size="sm" variant="outline" onClick={onZurueck}>
          {zurueck}
        </Button>
      </div>
    </div>
  );
}

/** Die vergangene Zeit, dezent. Tickt für sich, damit nicht die ganze Prüfung jede Sekunde neu zeichnet. */
function Laufzeit({ seit }: { seit: number }) {
  const t = useT();
  const [jetzt, setJetzt] = useState(seit);

  useEffect(() => {
    const takt = () => setJetzt(Date.now());
    const erster = setTimeout(takt, 0);
    const id = setInterval(takt, 1000);
    return () => {
      clearTimeout(erster);
      clearInterval(id);
    };
  }, []);

  return (
    <span className="flex items-center gap-1 text-xs text-muted-foreground tabular-nums" aria-label={t("Vergangene Zeit")}>
      <Clock className="size-3.5" />
      {dauerText(jetzt - seit)}
    </span>
  );
}

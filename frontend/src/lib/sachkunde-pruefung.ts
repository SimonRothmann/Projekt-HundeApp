import type {
  QuizExam,
  QuizExamAnswer,
  QuizExamQuestion,
  QuizExamQuestionResult,
  QuizExamResult,
} from "@/lib/types";

/**
 * Die reine Logik der Sachkunde-Übungsprüfung im Browser: was als beantwortet
 * gilt, wie Antworten umgeschaltet und abgegeben werden und wie eine laufende
 * Prüfung ein Neuladen übersteht. Gewertet wird NICHT hier, sondern im
 * Backend (SachkundePruefung.cs) - die Oberfläche kennt die Lösung während der
 * Prüfung gar nicht.
 */

/** Alle Antworten einer Prüfung, nach Frage-Id. Fehlt ein Eintrag, ist die Frage offen. */
export type Antworten = Record<string, QuizExamAnswer>;

/**
 * Was eine LAUFENDE Prüfung im Browser festhält. Nach der Abgabe wird der
 * Stand gelöscht - sonst öffnete ein Neuladen die schon abgegebene Prüfung
 * wieder und lud zum zweiten Abgeben ein; das Ergebnis steht dann im Verlauf.
 */
export type PruefungsStand = {
  exam: QuizExam;
  antworten: Antworten;
  /** Welche Frage gerade offen ist (0-basiert). */
  index: number;
  /** Beginn in Millisekunden seit 1970 - die vergangene Zeit rechnet daraus. */
  gestartetAm: number;
};

export function leereAntwort(frageId: string): QuizExamAnswer {
  return { questionId: frageId, selectedOptionIds: [], assignments: {} };
}

/**
 * Ob zur Frage etwas angekreuzt bzw. zugeordnet ist.
 *
 * Bei einer Zuordnung genügt ein einziger Begriff: wer angefangen hat, hat
 * geantwortet - gewertet wird ohnehin nur "ganz oder gar nicht".
 */
export function istBeantwortet(antwort: QuizExamAnswer | undefined): boolean {
  if (!antwort) return false;
  return antwort.selectedOptionIds.length > 0 || Object.keys(antwort.assignments).length > 0;
}

export function anzahlOffen(exam: QuizExam, antworten: Antworten): number {
  return exam.questions.filter((frage) => !istBeantwortet(antworten[frage.id])).length;
}

/**
 * Schaltet eine Antwort um.
 *
 * Mit `einzeln` (Jugendkatalog: genau eine Antwort) ersetzt ein neuer Tipp die
 * bisherige Auswahl, bei Erwachsenen lässt sich wie auf dem Papierbogen
 * mehreres ankreuzen. Ein zweiter Tipp auf dieselbe Antwort nimmt das Kreuz
 * wieder weg - in beiden Fällen.
 */
export function antwortUmschalten(
  antwort: QuizExamAnswer,
  optionId: string,
  einzeln: boolean,
): QuizExamAnswer {
  const schonGewaehlt = antwort.selectedOptionIds.includes(optionId);
  const auswahl = schonGewaehlt
    ? antwort.selectedOptionIds.filter((id) => id !== optionId)
    : einzeln
      ? [optionId]
      : [...antwort.selectedOptionIds, optionId];
  return { ...antwort, selectedOptionIds: auswahl };
}

/** Ordnet einem Begriff einen Schlüssel zu; derselbe Schlüssel noch einmal nimmt die Zuordnung zurück. */
export function zuordnungUmschalten(antwort: QuizExamAnswer, begriffId: string, schluessel: string): QuizExamAnswer {
  const assignments = { ...antwort.assignments };
  if (assignments[begriffId] === schluessel) delete assignments[begriffId];
  else assignments[begriffId] = schluessel;
  return { ...antwort, assignments };
}

/**
 * Der Rumpf der Abgabe: eine Antwort je Frage in Reihenfolge der Prüfung,
 * unbeantwortete mit leerer Auswahl (der Server wertet sie mit 0 Punkten).
 */
export function alsAbgabe(exam: QuizExam, antworten: Antworten): { answers: QuizExamAnswer[] } {
  return {
    answers: exam.questions.map((frage) => {
      const antwort = antworten[frage.id] ?? leereAntwort(frage.id);
      return {
        questionId: frage.id,
        selectedOptionIds: [...antwort.selectedOptionIds],
        assignments: { ...antwort.assignments },
      };
    }),
  };
}

// ---- Vergangene Zeit ----

/** "4:07" bzw. "1:02:30". Negatives wird zu 0 (Uhr des Geräts wurde zurückgestellt). */
export function dauerText(millisekunden: number): string {
  const gesamt = Math.max(0, Math.floor(millisekunden / 1000));
  const stunden = Math.floor(gesamt / 3600);
  const minuten = Math.floor((gesamt % 3600) / 60);
  const sekunden = gesamt % 60;
  const zweistellig = (n: number) => String(n).padStart(2, "0");
  return stunden > 0
    ? `${stunden}:${zweistellig(minuten)}:${zweistellig(sekunden)}`
    : `${minuten}:${zweistellig(sekunden)}`;
}

// ---- Auswertung ----

/** Eine falsch (oder nicht voll richtig) beantwortete Frage samt eigener Antwort. */
export type FalscheFrage = {
  frage: QuizExamQuestion;
  ergebnis: QuizExamQuestionResult;
  antwort: QuizExamAnswer;
};

/** Die Fragen ohne volle Punktzahl, in der Reihenfolge der Prüfung. */
export function falscheFragen(exam: QuizExam, antworten: Antworten, ergebnis: QuizExamResult): FalscheFrage[] {
  const jeFrage = new Map(ergebnis.questions.map((q) => [q.questionId, q]));
  return exam.questions.flatMap((frage) => {
    const eintrag = jeFrage.get(frage.id);
    if (!eintrag || eintrag.correct) return [];
    return [{ frage, ergebnis: eintrag, antwort: antworten[frage.id] ?? leereAntwort(frage.id) }];
  });
}

/** Anteil der erreichten Punkte in Prozent, für die Balken je Komplex (0 bei 0 möglichen). */
export function balkenBreite(punkte: number, hoechstpunkte: number): number {
  if (hoechstpunkte <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((punkte * 100) / hoechstpunkte)));
}

// ---- Laufende Prüfung über ein Neuladen retten ----

const SCHLUESSEL_VORSATZ = "dogity.sachkunde.pruefung.";

function schluessel(katalogCode: string): string {
  return SCHLUESSEL_VORSATZ + katalogCode.toLowerCase();
}

/**
 * Prüft eine aus dem Speicher gelesene Struktur, bevor die Seite ihr traut.
 * Der Speicher kann von einer älteren App-Fassung stammen oder verändert sein;
 * lieber eine Prüfung verwerfen als mit einer kaputten abstürzen.
 */
export function istGueltigerStand(wert: unknown, katalogCode: string): wert is PruefungsStand {
  if (typeof wert !== "object" || wert === null) return false;
  const stand = wert as Partial<PruefungsStand>;
  const exam = stand.exam;
  if (!exam || typeof exam !== "object" || !Array.isArray(exam.questions) || exam.questions.length === 0) return false;
  if (typeof exam.catalogCode !== "string" || exam.catalogCode.toLowerCase() !== katalogCode.toLowerCase()) return false;
  if (typeof stand.index !== "number" || !Number.isInteger(stand.index)) return false;
  if (stand.index < 0 || stand.index >= exam.questions.length) return false;
  if (typeof stand.gestartetAm !== "number") return false;
  if (typeof stand.antworten !== "object" || stand.antworten === null) return false;
  return exam.questions.every(
    (q) =>
      q && typeof q.id === "string" && Array.isArray(q.options) && Array.isArray(q.terms) && Array.isArray(q.keys),
  );
}

/** Liest die laufende Prüfung dieses Katalogs; null, wenn es keine gibt oder der Speicher nicht geht. */
export function leseStand(katalogCode: string): PruefungsStand | null {
  try {
    const roh = window.sessionStorage.getItem(schluessel(katalogCode));
    if (!roh) return null;
    const stand: unknown = JSON.parse(roh);
    return istGueltigerStand(stand, katalogCode) ? stand : null;
  } catch {
    // Gesperrter Speicher oder kaputter Inhalt: dann gibt es eben keine laufende Prüfung.
    return null;
  }
}

export function schreibeStand(katalogCode: string, stand: PruefungsStand): void {
  try {
    window.sessionStorage.setItem(schluessel(katalogCode), JSON.stringify(stand));
  } catch {
    // Ohne Speicher übersteht die Prüfung kein Neuladen - sie läuft aber weiter.
  }
}

export function loescheStand(katalogCode: string): void {
  try {
    window.sessionStorage.removeItem(schluessel(katalogCode));
  } catch {
    // Nichts zu tun.
  }
}

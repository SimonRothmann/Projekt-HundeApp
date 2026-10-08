import type {
  DogCondition,
  Exercise,
  Goal,
  GroupTrainingSession,
  Sport,
  TrainingPlanItem,
  TrainingSession,
} from "@/lib/types";
import { heuteIso, tageBisPruefung } from "@/lib/pruefung";
import { gewaehltesPlanItem, offeneWochenziele, passendesPlanItem, planItemName } from "@/lib/trainingsplan";
import type { LocationValue } from "@/components/dogs/location-time-fields";

/**
 * Die Regeln hinter dem Eintragen-Fenster (components/dogs/eintragen-sheet.tsx)
 * als reine Funktionen: Was steht zur Auswahl, was ist vorbelegt, wie wird aus
 * Chips und einer Sammelbewertung der Eintrag, der an den Server geht.
 *
 * Steht hier und nicht in der Komponente, damit gerade die Stellen prüfbar
 * bleiben, an denen etwas stillschweigend falsch werden kann - eine
 * vorbelegte Dauer, ein Ort von vor drei Monaten, eine Bewertung, die jemand
 * nie gewählt hat.
 */

// ---- Sammelbewertung ----

/**
 * "Wie lief es?" - EINE Frage für alle gewählten Übungen. Drei Stufen statt
 * fünf Sternen und einem Häkchen je Übung: Auf dem Hundeplatz mit einer Hand
 * ist das der eine Tipp, der übrig bleibt. Genauer wird es je Übung unter
 * "genauer".
 */
export type Bewertung = "maessig" | "gut" | "top";

export const BEWERTUNGEN: readonly Bewertung[] = ["maessig", "gut", "top"];

/**
 * Vorgewählt ist "Gut": ein ordentliches Training, nicht das beste. Die
 * Bewertung ist die Grundlage, auf der der Plangenerator Schwächen erkennt -
 * eine stillschweigende Fünf würde ihn in die Irre führen.
 */
export const VORGABE_BEWERTUNG: Bewertung = "gut";

export type Werte = { rating: number; success: boolean };

/** Mäßig = 2 Sterne, nicht erfolgreich; Gut = 4, erfolgreich; Top = 5, erfolgreich. */
export function sammelWerte(bewertung: Bewertung): Werte {
  switch (bewertung) {
    case "maessig":
      return { rating: 2, success: false };
    case "gut":
      return { rating: 4, success: true };
    case "top":
      return { rating: 5, success: true };
  }
}

// ---- Zeilen ----

/**
 * Eine gewählte Übung im Fenster.
 *
 * Bewertung und Erfolg stehen NICHT in der Zeile, solange niemand sie ändert:
 * Sie kommen aus der Sammelbewertung. `eigeneWerte` ist gesetzt, sobald unter
 * "genauer" etwas geändert wurde - dann gilt die Sammelbewertung für genau
 * diese Zeile nicht mehr, für die anderen schon.
 */
export type EintragZeile = {
  /** Kennung der Zeile (Schlüssel für die Anzeige) - nicht aus dem Inhalt, zwei Zeilen dürfen gleich heißen. */
  schluessel: string;
  /** Katalog-Übung; null = frei eingetragen, dann ist der Name der Freitext. */
  exerciseId: string | null;
  name: string;
  eigeneWerte: Werte | null;
  notes: string;
  /**
   * Das Plan-Ziel, das der Nutzer ausdrücklich gewählt hat (Chip "Diese Woche",
   * Antippen einer Planzeile). Schlägt die automatische Zuordnung, denn nur so
   * lässt sich auch eine Freitext-Planübung verknüpfen und eine Übung aus einer
   * anderen Planwoche.
   */
  vorgabePlan: TrainingPlanItem | null;
  /** Der Nutzer hat "nicht zählen" gewählt. */
  planGeloest: boolean;
};

let zeilenZaehler = 0;
function neueKennung(): string {
  zeilenZaehler += 1;
  return `zeile-${zeilenZaehler}`;
}

export function katalogZeile(uebung: { id: string; name: string }, vorgabePlan: TrainingPlanItem | null = null): EintragZeile {
  return {
    schluessel: neueKennung(),
    exerciseId: uebung.id,
    name: uebung.name,
    eigeneWerte: null,
    notes: "",
    vorgabePlan,
    planGeloest: false,
  };
}

export function freitextZeile(text: string, vorgabePlan: TrainingPlanItem | null = null): EintragZeile {
  return {
    schluessel: neueKennung(),
    exerciseId: null,
    name: text,
    eigeneWerte: null,
    notes: "",
    vorgabePlan,
    planGeloest: false,
  };
}

/** Die Zeile zu einer Übung des Plans - Katalog-Übung oder Freitext, je nachdem, was der Plan führt. */
export function zeileAusPlanItem(item: TrainingPlanItem): EintragZeile {
  return item.exerciseId
    ? katalogZeile({ id: item.exerciseId, name: item.exerciseName ?? "" }, item)
    : freitextZeile(item.freeTextLabel ?? "", item);
}

/**
 * Baut aus einer gespeicherten Einheit die Zeilen für eine neue ("Wie beim
 * letzten Mal").
 *
 * Übernommen wird der ÜBUNGSSATZ - das, was sich von Woche zu Woche nicht
 * ändert. Bewusst NICHT übernommen werden Bewertung, Erfolg und Notiz: Die
 * Bewertung ist der eine Wert, der jedes Mal ein anderer ist, und der
 * adaptive Plangenerator erkennt an ihr die Schwächen. Eine mitgeschleppte
 * Bewertung von letzter Woche wäre keine Zeitersparnis, sondern eine falsche
 * Angabe. Plan-Ziel: wird neu bestimmt, die damalige Zuordnung gehört zur
 * damaligen Einheit.
 */
export function zeilenAusEinheit(einheit: TrainingSession): EintragZeile[] {
  return einheit.exercises.map((uebung) =>
    // exerciseId null heißt: es war eine frei eingetragene Übung, der Name IST
    // der Freitext (siehe TrainingExercise).
    uebung.exerciseId === null
      ? freitextZeile(uebung.exerciseName)
      : katalogZeile({ id: uebung.exerciseId, name: uebung.exerciseName }),
  );
}

/** Ob die Zeile etwas Speicherbares enthält: eine Katalog-Übung oder einen Freitext, der nicht leer ist. */
export function istGueltig(zeile: EintragZeile): boolean {
  return zeile.exerciseId !== null || zeile.name.trim() !== "";
}

/**
 * Woran man dieselbe Übung wiedererkennt: Kennung im Katalog, sonst der
 * Freitext ohne Großschreibung und Ränder. Gebraucht für "ist schon gewählt"
 * und um Übungen nicht doppelt als Chip anzubieten.
 */
export function inhaltsSchluessel(exerciseId: string | null | undefined, name: string): string {
  return exerciseId ? `katalog:${exerciseId}` : `frei:${name.trim().toLocaleLowerCase("de-DE")}`;
}

export function zeilenSchluessel(zeile: EintragZeile): string {
  return inhaltsSchluessel(zeile.exerciseId, zeile.name);
}

/** Die jüngste Einheit mit Übungen - Fährten-Einheiten ohne Übungen zählen nicht, sie taugen nicht als Vorlage. */
export function letzteEinheit(einheiten: readonly TrainingSession[] | null | undefined): TrainingSession | null {
  return nachDatum(einheiten).find((einheit) => einheit.exercises.length > 0) ?? null;
}

/**
 * Die Dauer der letzten Einheit, in der wirklich geübt wurde. Einheiten ohne
 * Übungen (die Einheit, die eine gelegte Fährte anlegt) bleiben außen vor,
 * ihre Dauer ist keine Übungsdauer.
 */
export function letzteUebungsdauer(einheiten: readonly TrainingSession[] | null | undefined): number | null {
  return letzteEinheit(einheiten)?.durationMinutes ?? null;
}

/** Neueste zuerst; der Server liefert so, aber ein Zwischenstand aus dem Lesecache muss es nicht. */
function nachDatum(einheiten: readonly TrainingSession[] | null | undefined): TrainingSession[] {
  return [...(einheiten ?? [])].sort((a, b) => b.date.slice(0, 10).localeCompare(a.date.slice(0, 10)));
}

// ---- Chips ----

export type WochenChip = {
  item: TrainingPlanItem;
  name: string;
  /** Wie oft schon erledigt / Ziel dieser Woche - "1/3". */
  erledigt: number;
  ziel: number;
};

/**
 * "Diese Woche": die noch offenen Übungen der laufenden Woche aller aktiven
 * Ziele des Hundes. Das Ziel mit dem früheren Prüfungstermin zuerst, wie bei
 * der automatischen Zuordnung. Leer, wenn der Hund kein laufendes Ziel hat
 * oder alles erledigt ist - dann fehlt der Abschnitt ganz.
 */
export function wochenChips(goals: readonly Goal[] | null | undefined, jetzt: number = Date.now()): WochenChip[] {
  const chips: WochenChip[] = [];
  const gesehen = new Set<string>();
  for (const goal of [...(goals ?? [])].sort((a, b) => a.targetDate.localeCompare(b.targetDate))) {
    for (const item of offeneWochenziele(goal, jetzt)?.items ?? []) {
      const name = planItemName(item);
      if (!name || gesehen.has(item.id)) continue;
      gesehen.add(item.id);
      chips.push({ item, name, erledigt: item.completedCount, ziel: item.repetitionsTarget });
    }
  }
  return chips;
}

export type ZuletztChip = { exerciseId: string | null; name: string };

/** So viele weitere Übungen zeigt "Zuletzt geübt" neben "Wie beim letzten Mal". */
export const MAX_ZULETZT = 6;

/**
 * "Zuletzt geübt": verschiedene Übungen aus den letzten Einheiten, neueste
 * zuerst. `ausser` sind die Übungen, die schon unter "Diese Woche" stehen
 * (Schlüssel nach {@link inhaltsSchluessel}) - dieselbe Übung zweimal anzubieten
 * hieße, dass man zwei Chips für einen Handgriff abwägen muss.
 */
export function zuletztChips(
  einheiten: readonly TrainingSession[] | null | undefined,
  ausser: ReadonlySet<string> = new Set(),
  max: number = MAX_ZULETZT,
): ZuletztChip[] {
  const chips: ZuletztChip[] = [];
  const gesehen = new Set(ausser);
  for (const einheit of nachDatum(einheiten)) {
    for (const uebung of einheit.exercises) {
      const schluessel = inhaltsSchluessel(uebung.exerciseId, uebung.exerciseName);
      if (gesehen.has(schluessel) || !uebung.exerciseName.trim()) continue;
      gesehen.add(schluessel);
      chips.push({ exerciseId: uebung.exerciseId, name: uebung.exerciseName });
      if (chips.length >= max) return chips;
    }
  }
  return chips;
}

// ---- Übungssuche ----

export type KatalogGruppe = { sport: Sport; uebungen: Exercise[] };

/**
 * Text für den Vergleich in Suche und Treffer: ohne Groß-/Kleinschreibung und
 * ohne Umlaute. Zwei Schreibweisen, weil man Umlaute auf der Handytastatur
 * entweder tippt ("Fuß") oder umgeht ("Fuss", "Fuess"): "ä" wird einmal zu
 * "ae" und einmal zu "a". Ein Treffer in einer von beiden zählt.
 */
export function suchformen(text: string): [string, string] {
  const ohneAkzente = (t: string) => t.normalize("NFD").replace(/[\u0300-\u036f]/g, "");
  const klein = text.normalize("NFC").toLocaleLowerCase("de-DE").replace(/ß/g, "ss");
  const ausgeschrieben = ohneAkzente(klein.replace(/ä/g, "ae").replace(/ö/g, "oe").replace(/ü/g, "ue"));
  return [ausgeschrieben, ohneAkzente(klein)];
}

/** Ob alle Wörter der Suche im Text vorkommen (in beliebiger Reihenfolge). */
export function trifftSuche(text: string, suche: string): boolean {
  const woerter = suche.trim().split(/\s+/).filter(Boolean);
  if (woerter.length === 0) return true;
  const [textAus, textEinfach] = suchformen(text);
  return woerter.every((wort) => {
    const [wortAus, wortEinfach] = suchformen(wort);
    return textAus.includes(wortAus) || textEinfach.includes(wortEinfach);
  });
}

/** Die Übungen, die zur Suche passen, in ihren Sportarten; Sportarten ohne Treffer fallen weg. */
export function sucheUebungen(katalog: readonly KatalogGruppe[], suche: string): KatalogGruppe[] {
  return katalog
    .map((gruppe) => ({
      sport: gruppe.sport,
      uebungen: gruppe.uebungen.filter((uebung) => trifftSuche(`${uebung.name} ${uebung.category ?? ""}`, suche)),
    }))
    .filter((gruppe) => gruppe.uebungen.length > 0);
}

/**
 * Ob die Liste "Eigene Übung: „…“ eintragen" anbieten soll, und wo.
 *
 * Ohne Treffer steht sie ganz oben - dann ist sie das, wonach gesucht wurde.
 * Gibt es Treffer, steht sie am Ende der Liste: Wer "Spaziergang" tippt und
 * "Spaziergang mit Leine" im Katalog findet, soll trotzdem einen Weg zu seiner
 * eigenen Bezeichnung behalten (das war im alten Formular immer möglich).
 * Heißt eine Katalog-Übung genau so, wird nichts angeboten - die ist gemeint.
 */
export function eigeneUebungAngebot(
  suche: string,
  treffer: readonly KatalogGruppe[],
): { text: string; oben: boolean } | null {
  const text = suche.trim();
  if (!text) return null;
  const [gesucht] = suchformen(text);
  const gleichnamig = treffer.some((gruppe) => gruppe.uebungen.some((uebung) => suchformen(uebung.name)[0] === gesucht));
  if (gleichnamig) return null;
  return { text, oben: treffer.length === 0 };
}

/** Die Sportarten, die dem Eintragen angeboten werden: die des Hundes; leere Auswahl heißt "keine Einschränkung". */
export function angeboteneSportarten(sports: readonly Sport[], dogSportIds: readonly string[] | null | undefined): Sport[] {
  return dogSportIds && dogSportIds.length > 0 ? sports.filter((sport) => dogSportIds.includes(sport.id)) : [...sports];
}

// ---- Plan-Zuordnung und Bewertung je Zeile ----

export type PlanZuordnung = {
  item: TrainingPlanItem;
  /** Wie oft das Ziel mit diesem Eintrag erledigt wäre, ohne diese Zeile zu zählen. */
  erledigt: number;
  /** Ob die Zeile für das Ziel zählt (false nach "nicht zählen"). */
  gezaehlt: boolean;
};

export type AufgeloesteZeile = {
  zeile: EintragZeile;
  rating: number;
  success: boolean;
  plan: PlanZuordnung | null;
};

/**
 * Die Zeilen, wie sie gespeichert würden: Bewertung/Erfolg aus Sammel- oder
 * eigenen Werten und das Plan-Ziel, das sie zählen.
 *
 * Plan-Ziel: Hat der Nutzer eines ausdrücklich gewählt (Chip, Planzeile), gilt
 * dieses - mit dem Stand aus den zuletzt geladenen Zielen, nicht dem vom
 * Antippen -, aber nur, wenn das Trainingsdatum in dessen Woche liegt
 * (gewaehltesPlanItem). Sonst steht eine Katalog-Übung diese Woche offen im Plan, zählt der
 * Eintrag dafür (siehe passendesPlanItem) - ohne dass man es wählen müsste.
 * Wer es vergisst, ließ den Wochenfortschritt auf 0/3 stehen. Weil das Ziel
 * aus Übung und Datum folgt, stimmt es nach jedem Wechsel des Tages von selbst.
 * Die einzige Eingabe dazu ist "nicht zählen" (planGeloest).
 *
 * Abgeleitet und nicht gespeichert: beides ist keine Eingabe des Nutzers,
 * sondern ergibt sich aus den geladenen Daten - eine später geladene Zielliste
 * wird so noch berücksichtigt.
 */
export function loeseZeilenAuf(
  zeilen: readonly EintragZeile[],
  bewertung: Bewertung,
  goals: readonly Goal[] | null | undefined,
  datum: string,
  jetzt: number = Date.now(),
): AufgeloesteZeile[] {
  // Wie viele Zeilen davor ein Plan-Ziel schon belegen: Zwei Zeilen mit
  // derselben Übung dürfen ein nur einmal offenes Ziel nicht beide füllen.
  const vergeben = new Map<string, number>();
  return zeilen.map((zeile) => {
    const werte = zeile.eigeneWerte ?? sammelWerte(bewertung);
    const item = zeile.vorgabePlan
      ? gewaehltesPlanItem(goals, zeile.vorgabePlan, datum, jetzt)
      : zeile.exerciseId
        ? passendesPlanItem(goals, zeile.exerciseId, datum, jetzt, vergeben)
        : null;
    if (!item) return { zeile, ...werte, plan: null };
    const erledigt = item.completedCount + (vergeben.get(item.id) ?? 0);
    if (!zeile.planGeloest) vergeben.set(item.id, (vergeben.get(item.id) ?? 0) + 1);
    return { zeile, ...werte, plan: { item, erledigt, gezaehlt: !zeile.planGeloest } };
  });
}

// ---- Tag, Dauer, Zeit, Ort ----

export type Tag = "heute" | "gestern" | "anderer";

/** Der Kalendertag vor `iso`, `tage` Tage zurück, als "JJJJ-MM-TT". */
export function tagVorher(iso: string, tage: number): string {
  const [jahr, monat, tag] = iso.split("-").map(Number);
  const d = new Date(jahr, monat - 1, tag);
  d.setDate(d.getDate() - tage);
  return heuteIso(d.getTime());
}

/**
 * Das Datum des Eintrags. Der Kalendertag des Geräts, nicht der UTC-Tag: Wer
 * nach Mitternacht (Ortszeit) einträgt, trainierte noch "heute". `anderes` ist
 * der Wert des Datumsfelds bei "Anderer Tag".
 */
export function datumFuerTag(tag: Tag, anderes: string, jetzt: number = Date.now()): string {
  const heute = heuteIso(jetzt);
  if (tag === "heute") return heute;
  if (tag === "gestern") return tagVorher(heute, 1);
  return anderes;
}

/** Ob das Datum ein vollständiger Kalendertag ist und nicht in der Zukunft liegt - ein Training ist etwas Geschehenes. */
export function istGueltigesDatum(datum: string, jetzt: number = Date.now()): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(datum) && datum <= heuteIso(jetzt);
}

/** Die vorgewählte Dauer, wenn die letzte nicht bekannt ist. */
export const DAUER_VORGABE = 30;

/** Die Dauern zum Antippen; das Zahlenfeld daneben bleibt für alles andere. */
export const DAUER_VORSCHLAEGE = [15, 30, 45, 60, 90];

export const DAUER_MAX = 600;

/** Ob die Dauer ganze Minuten sind und sich speichern lässt. */
export function istGueltigeDauer(minuten: number): boolean {
  return Number.isInteger(minuten) && minuten >= 1 && minuten <= DAUER_MAX;
}

/** Vorbelegte Dauer: die der letzten Einheit des Hundes, sonst 30 Minuten. */
export function vorbelegteDauer(einheiten: readonly TrainingSession[] | null | undefined): number {
  const letzte = letzteUebungsdauer(einheiten);
  return letzte !== null && istGueltigeDauer(letzte) ? letzte : DAUER_VORGABE;
}

/** Die Uhrzeit des Geräts als "HH:mm" - das Format des Zeitfelds. */
export function uhrzeitText(jetzt: number = Date.now()): string {
  const d = new Date(jetzt);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

/** So lange gilt der Ort der letzten Einheit noch als der, an dem man heute wohl wieder ist. */
export const ORT_MAX_ALTER_TAGE = 14;

export const KEIN_ORT: LocationValue = { latitude: null, longitude: null, locationName: "" };

/**
 * Der vorbelegte Ort, oder null - dann bietet das Fenster "Standort verwenden" an.
 *
 * Zwei Quellen, und nur diese: Ein Gruppentermin des Nutzers mit Ort, der
 * HEUTE stattfindet (nur wenn auch heute eingetragen wird) und bei dem der
 * Nutzer nicht abgesagt hat, und der Ort der letzten Einheit des Hundes, wenn
 * die höchstens 14 Tage her ist. Ein älterer
 * Ort wäre geraten - und ein falscher Ort samt Wetter ist schlimmer als keiner.
 *
 * "Letzte Einheit" ist die jüngste überhaupt, nicht die jüngste mit Ort: Wer
 * zuletzt woanders war und den Ort nicht eingetragen hat, soll nicht den
 * davor bekommen. Der Gruppentermin trägt nur einen Namen; stimmt der mit dem
 * Ort der letzten Einheit überein, kommen deren Koordinaten (und damit das
 * Wetter) dazu.
 */
export function vorbelegterOrt(
  einheiten: readonly TrainingSession[] | null | undefined,
  termine: readonly GroupTrainingSession[] | null | undefined,
  datum: string,
  jetzt: number = Date.now(),
): LocationValue | null {
  const sortiert = nachDatum(einheiten);
  const juengsterTag = sortiert[0]?.date.slice(0, 10);
  const letzte =
    juengsterTag === undefined || -tageBisPruefung(juengsterTag, jetzt) > ORT_MAX_ALTER_TAGE
      ? null
      : (sortiert.filter((einheit) => einheit.date.slice(0, 10) === juengsterTag).find((einheit) => einheit.locationName?.trim()) ??
        null);
  const ortLetzte: LocationValue | null = letzte
    ? { latitude: letzte.latitude, longitude: letzte.longitude, locationName: letzte.locationName!.trim() }
    : null;

  if (datum === heuteIso(jetzt)) {
    const termin = (termine ?? [])
      .filter((t) => t.status === 0 && t.myResponse !== false && t.location?.trim() && heuteIso(new Date(t.startsAt).getTime()) === datum)
      .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime())[0];
    if (termin) {
      const name = termin.location!.trim();
      return ortLetzte && ortLetzte.locationName.toLocaleLowerCase("de-DE") === name.toLocaleLowerCase("de-DE")
        ? ortLetzte
        : { latitude: null, longitude: null, locationName: name };
    }
  }
  return ortLetzte;
}

// ---- Eintrag ----

export type TrainingsEingabe = {
  dogId: string;
  datum: string;
  dauer: number;
  notiz: string;
  /** "HH:mm" oder leer. */
  uhrzeit: string;
  ort: LocationValue;
  verfassung: DogCondition | null;
  zeilen: readonly AufgeloesteZeile[];
};

/**
 * Der Eintrag, wie ihn POST /api/trainings erwartet - dieselbe Gestalt wie
 * beim früheren Formular. Zeilen ohne Inhalt (leerer Freitext) entfallen.
 */
export function baueNutzlast(eingabe: TrainingsEingabe) {
  return {
    dogId: eingabe.dogId,
    date: eingabe.datum,
    durationMinutes: eingabe.dauer,
    notes: eingabe.notiz || null,
    // Backend erwartet TimeOnly; leer = nicht gesetzt.
    startTime: eingabe.uhrzeit ? `${eingabe.uhrzeit}:00` : null,
    latitude: eingabe.ort.latitude,
    longitude: eingabe.ort.longitude,
    locationName: eingabe.ort.locationName.trim() || null,
    condition: eingabe.verfassung,
    exercises: eingabe.zeilen
      .filter((aufgeloest) => istGueltig(aufgeloest.zeile))
      .map(({ zeile, rating, success, plan }) => ({
        exerciseId: zeile.exerciseId,
        freeTextLabel: zeile.exerciseId === null ? zeile.name.trim() : null,
        rating,
        difficulty: 0,
        success,
        notes: zeile.notes || null,
        trainingPlanItemId: plan?.gezaehlt ? plan.item.id : null,
      })),
  };
}

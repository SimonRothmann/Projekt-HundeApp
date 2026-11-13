import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { MAX_HUNDEALTER_JAHRE, TELEFON_MIN_ZEICHEN, TELEFON_MIN_ZIFFERN } from "./anmeldung";
import { MAX_IMPORT_ZEILEN } from "./anmeldung-csv";
import { MAX_MINUTEN, MAX_TAGE_VORAUS, MIN_MINUTEN } from "./arbeitseinsaetze";
import { DAUER_MAX } from "./eintragen";
import { ALLE_TAGE, ERSTE_STUNDE, GESUNDHEIT_VORLAUF_TAGE, LETZTE_STUNDE, VORGABE_STUNDE, VORGABE_TAGE } from "./erinnerungen";
import { ABLAUF_FRIST_STUNDEN, ABLAUF_FRIST_STUNDEN_TRAIL, FAEHRTEN_SPORTARTEN } from "./faehrte";
import { BALD_TAGE, MAX_FRIST_JAHRE, MAX_JAHRE_VORAUS as GESUNDHEIT_VORAUS, MAX_JAHRE_ZURUECK as GESUNDHEIT_ZURUECK } from "./gesundheit";
import { FAEHRTE_GRENZEN, MANTRAILING_GRENZEN, MAX_TRAILALTER_STUNDEN } from "./mantrailing";
import { MAX_DAUER_JAHRE, MAX_JAHRE_VORAUS as PAUSE_VORAUS, MAX_JAHRE_ZURUECK as PAUSE_ZURUECK } from "./pausen";
import { REGELN } from "./pruefungsreife";
import { PUNKTE_JE_ANTWORT } from "./sachkunde-pruefung";

/**
 * Wächter gegen das Auseinanderlaufen von Regeln, die Frontend und Backend beide
 * kennen. Das Frontend spiegelt sie, damit die Oberfläche Fehler vor dem Senden
 * zeigt oder die Regel erklärt; maßgeblich ist der Server. Der Test liest die
 * C#-Quellen (wie i18n.test.ts) und vergleicht die Zahlen - rot heißt: eine Seite
 * wurde geändert, die andere nicht. Wird im Backend eine Konstante umbenannt,
 * schlägt der Test ebenfalls fehl (gewollt): dann hier den Namen nachziehen.
 */

const BACKEND = join(process.cwd(), "..", "backend", "src", "Dogity.Application");

const quelltexte = new Map<string, string>();
function cs(datei: string): string {
  let text = quelltexte.get(datei);
  if (text === undefined) {
    text = readFileSync(join(BACKEND, datei), "utf8");
    quelltexte.set(datei, text);
  }
  return text;
}

/** Der Ausdruck rechts vom "=" einer Konstante: `NAME = <ausdruck>;`. */
function ausdruck(datei: string, name: string): string {
  const treffer = new RegExp(`\\b${name}\\s*=\\s*([^;]+);`).exec(cs(datei));
  if (!treffer) throw new Error(`${name} steht nicht in ${datei} - umbenannt? Dann diesen Test nachziehen.`);
  return treffer[1].trim();
}

/** Eine Zahl: `4`, `4.0`, `7 * 24 * 60`, `50_000`. */
function zahl(datei: string, name: string): number {
  const text = ausdruck(datei, name).replace(/_/g, "");
  if (!/^[\d.\s*]+$/.test(text)) throw new Error(`${name} in ${datei} ist keine einfache Zahl: ${text}`);
  return text.split("*").reduce((produkt, faktor) => produkt * Number(faktor), 1);
}

/** `TimeSpan.FromHours(24)` -> 24 (Stunden). */
function stunden(datei: string, name: string): number {
  const treffer = /TimeSpan\.FromHours\((\d+)\)/.exec(ausdruck(datei, name));
  if (!treffer) throw new Error(`${name} in ${datei} ist keine TimeSpan.FromHours(n)`);
  return Number(treffer[1]);
}

describe("Frontend und Backend meinen dieselben Zahlen", () => {
  it("Fristen zum Ablaufen: Fährte 24 h, Trail 48 h (Ablauffristen)", () => {
    const datei = "Tracking/Ablauffristen.cs";
    expect(ABLAUF_FRIST_STUNDEN).toBe(stunden(datei, "Faehrte"));
    expect(ABLAUF_FRIST_STUNDEN_TRAIL).toBe(stunden(datei, "Trail"));
  });

  it("Ampelgrenzen der Auswertung: Fährte 3/6 m, Mantrailing 10/25 m (GpsTrackEvaluator)", () => {
    const datei = "Tracking/GpsTrackEvaluator.cs";
    const toleranz = (name: string) => {
      const treffer = /new\(([\d.]+),\s*([\d.]+),\s*([\d.]+)\)/.exec(ausdruck(datei, name));
      if (!treffer) throw new Error(`${name} ist kein SpurToleranz(auf, grün, gelb)`);
      return treffer.slice(1).map(Number);
    };
    // auf der Spur = grün: Das Frontend kennt nur noch eine Grenze für beides.
    const [aufF, gruenF, gelbF] = toleranz("FaehrteToleranz");
    expect([aufF, gruenF, gelbF]).toEqual([FAEHRTE_GRENZEN.gruenBis, FAEHRTE_GRENZEN.gruenBis, FAEHRTE_GRENZEN.gelbBis]);
    const [aufM, gruenM, gelbM] = toleranz("MantrailingToleranz");
    expect([aufM, gruenM, gelbM]).toEqual([MANTRAILING_GRENZEN.gruenBis, MANTRAILING_GRENZEN.gruenBis, MANTRAILING_GRENZEN.gelbBis]);
  });

  it("Trailalter höchstens eine Woche (GpsTrackService)", () => {
    expect(MAX_TRAILALTER_STUNDEN * 60).toBe(zahl("Tracking/GpsTrackService.cs", "MaxTrailAlterMinuten"));
  });

  it("Prüfungsreife: Schwellen der Erklärung (Pruefungsreife)", () => {
    const datei = "Planning/Pruefungsreife.cs";
    expect(REGELN.sicherBewertung).toBe(zahl(datei, "SicherAbBewertung"));
    expect(REGELN.sicherTage).toBe(zahl(datei, "SicherMaxTage"));
    expect(REGELN.gruenProzent).toBe(zahl(datei, "GruenAbProzent"));
    expect(REGELN.rotProzent).toBe(zahl(datei, "RotUnterProzent"));
    expect(REGELN.verschiebenWochen).toBe(zahl(datei, "VerschiebenBisWochen"));
  });

  it("Sachkunde-Übungsprüfung: Punkte je Antwort (SachkundePruefung)", () => {
    expect(PUNKTE_JE_ANTWORT).toBe(zahl("Learning/SachkundePruefung.cs", "PunkteJeAntwort"));
  });

  it("Gesundheit: Bald-Schwelle und Jahresgrenzen (Gesundheitsstand, DogHealthService)", () => {
    expect(BALD_TAGE).toBe(zahl("Dogs/Gesundheitsstand.cs", "BaldTage"));
    expect(GESUNDHEIT_ZURUECK).toBe(zahl("Dogs/DogHealthService.cs", "MaxJahreZurueck"));
    expect(GESUNDHEIT_VORAUS).toBe(zahl("Dogs/DogHealthService.cs", "MaxJahreVoraus"));
    expect(MAX_FRIST_JAHRE).toBe(zahl("Dogs/DogHealthService.cs", "MaxFristJahre"));
  });

  it("Trainingspausen: Jahresgrenzen (DogPauseService)", () => {
    const datei = "Dogs/DogPauseService.cs";
    expect(PAUSE_ZURUECK).toBe(zahl(datei, "MaxJahreZurueck"));
    expect(PAUSE_VORAUS).toBe(zahl(datei, "MaxJahreVoraus"));
    expect(MAX_DAUER_JAHRE).toBe(zahl(datei, "MaxDauerJahre"));
  });

  it("Erinnerungen: Versandfenster, Vorgaben, Vorlauf (Erinnerungsregeln)", () => {
    const datei = "Reminders/Erinnerungsregeln.cs";
    expect(ERSTE_STUNDE).toBe(zahl(datei, "ErsteStunde"));
    expect(LETZTE_STUNDE).toBe(zahl(datei, "LetzteStunde"));
    expect(VORGABE_STUNDE).toBe(zahl(datei, "VorgabeStunde"));
    expect(VORGABE_TAGE).toBe(zahl(datei, "VorgabeTage"));
    expect(ALLE_TAGE).toBe(zahl(datei, "AlleTage"));
    expect(GESUNDHEIT_VORLAUF_TAGE).toBe(zahl(datei, "GesundheitVorlaufTage"));
  });

  it("Erinnerungen: Sportarten, bei denen eine Spur gelegt wird (FaehrtenSportCodes)", () => {
    const liste = /FaehrtenSportCodes\s*=\s*\[([^\]]+)\]/.exec(cs("Reminders/Erinnerungsregeln.cs"));
    expect(liste).not.toBeNull();
    const codes = [...liste![1].matchAll(/"([^"]+)"/g)].map((m) => m[1]);
    expect([...FAEHRTEN_SPORTARTEN].sort()).toEqual([...codes].sort());
  });

  it("Training: Dauer höchstens 600 Minuten (TrainingService)", () => {
    expect(DAUER_MAX).toBe(zahl("Training/TrainingService.cs", "MaxDauerMinuten"));
  });

  it("Arbeitseinsätze: Dauer 15 bis 720 Minuten, höchstens 30 Tage im Voraus (ClubWorkService)", () => {
    const datei = "Community/ClubWorkService.cs";
    expect(MIN_MINUTEN).toBe(zahl(datei, "MinMinuten"));
    expect(MAX_MINUTEN).toBe(zahl(datei, "MaxMinuten"));
    expect(MAX_TAGE_VORAUS).toBe(zahl(datei, "MaxTageVoraus"));
  });

  it("Gruppenanmeldung: Hundealter, Telefon, Import (GroupRegistrationRules)", () => {
    const datei = "Community/GroupRegistrationRules.cs";
    expect(MAX_HUNDEALTER_JAHRE).toBe(zahl(datei, "MaxHundeAlterJahre"));
    expect(TELEFON_MIN_ZEICHEN).toBe(zahl(datei, "TelefonMinZeichen"));
    expect(TELEFON_MIN_ZIFFERN).toBe(zahl(datei, "TelefonMinZiffern"));
    expect(MAX_IMPORT_ZEILEN).toBe(zahl(datei, "MaxImportZeilen"));
  });
});

"use client";

import { useEffect, useMemo, useState } from "react";
import { api } from "@/lib/api";
import { angeboteneSportarten, neuererCacheTeil, type CacheTeil } from "@/lib/eintragen";
import { heuteIso } from "@/lib/kalendertag";
import { getCachedData, getCachedEntry, setCachedData } from "@/lib/read-cache";
import type { Dog, Exercise, Goal, GroupTrainingSession, Regulation, RegulationDetail, Sport, TrainingSession } from "@/lib/types";
import { ladeSportarten } from "@/lib/sportarten";
import { einstiegsOrdnung } from "@/lib/einstieg";
import { ordnungenPfad } from "@/lib/katalog-land";
import { VORGABE_LAND } from "@/lib/i18n/laender";
import { usePreferences } from "@/lib/preferences-context";

/**
 * Daten fürs Eintragen-Fenster: Sportarten des Hundes, seine Ziele, seine
 * letzten Einheiten und die heutigen Gruppentermine.
 *
 * Die Hundeseite hat das meiste schon und reicht es mit (`vorab`) - ein zweiter
 * Abruf derselben Daten wäre Verschwendung und könnte ihr widersprechen. Von
 * der Startseite (oder nach einem Wechsel zu einem anderen Hund im Fenster)
 * fehlt es: Dann holt der Hook genau das, was nicht mitgebracht wird, über die
 * vorhandenen Endpunkte.
 *
 * Offline: erst der Lesecache - je Teil (Ziele, Einheiten) die neuere Quelle
 * aus der Hundeseite (`dog-page-<id>`) und aus diesem Fenster (`eintragen-<id>`,
 * wird nach jedem frischen Abruf hier geschrieben). Fehlt auch der, bleibt alles leer, und das Fenster funktioniert mit freier
 * Eingabe weiter (siehe `offline`).
 */

export type EintragenVorab = {
  dogId: string;
  /** Die dem Hund angebotenen Sportarten (schon nach seiner Auswahl gefiltert). */
  sports?: Sport[];
  /** null = die Seite lädt sie noch; dann wartet das Fenster darauf statt selbst zu laden. */
  goals?: Goal[] | null;
  sessions?: TrainingSession[] | null;
};

export type EintragenDaten = {
  sports: Sport[];
  goals: Goal[] | null;
  sessions: TrainingSession[] | null;
  termine: GroupTrainingSession[];
  /** Weder frisch noch aus dem Lesecache zu bekommen: nur freie Eingabe möglich. */
  offline: boolean;
  /** Ziele oder Einheiten stehen noch aus (und der Abruf ist nicht gescheitert): Vorschläge und Dauer sind noch nicht endgültig. */
  vorschlaegeLaden: boolean;
};

/** Was die Hundeseite im Lesecache hält (siehe use-dog-page.ts) - hier nur die Teile, die gebraucht werden. */
type HundeseitenCache = { dog?: Dog; sports: Sport[]; goals: Goal[]; sessions: TrainingSession[] };

/** Was dieses Fenster selbst im Lesecache ablegt (`eintragen-<id>`). */
type EintragenCache = { goals?: Goal[]; sessions?: TrainingSession[] };

type Geladen = {
  dogId: string;
  sports: Sport[] | null;
  goals: Goal[] | null;
  sessions: TrainingSession[] | null;
  termine: GroupTrainingSession[] | null;
  /** Die Sportarten aus dem Lesecache - ungefiltert und nur für den Fall, dass kein frischer Abruf gelingt. */
  cacheSports: Sport[] | null;
  sportsFehlgeschlagen: boolean;
  /** Abruf gescheitert: Dann wartet das Fenster nicht länger auf Vorschläge. */
  zieleFehlgeschlagen: boolean;
  einheitenFehlgeschlagen: boolean;
};

const leer = (dogId: string): Geladen => ({
  dogId,
  sports: null,
  goals: null,
  sessions: null,
  termine: null,
  cacheSports: null,
  sportsFehlgeschlagen: false,
  zieleFehlgeschlagen: false,
  einheitenFehlgeschlagen: false,
});

// Stabile Leerwerte: Ein frisches [] je Rendern ließe jede abgeleitete Liste
// (useMemo) bei jedem Durchlauf neu rechnen.
const KEINE_SPORTARTEN: Sport[] = [];
const KEINE_TERMINE: GroupTrainingSession[] = [];

function vorDreiMonaten(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 3);
  return heuteIso(d.getTime());
}

export function useEintragenDaten(
  dogId: string,
  vorab: EintragenVorab | null,
  /** Die Gruppentermine des Nutzers ab heute, wo die Seite sie schon hat (Startseite); sonst holt der Hook sie. */
  termineVonSeite?: GroupTrainingSession[],
  /** Die Sportarten des Hundes, wo die Seite sie kennt (Startseite): filtert die Liste aus dem Lesecache. */
  sportIds?: readonly string[],
): EintragenDaten {
  const mitgebracht = vorab !== null && vorab.dogId === dogId ? vorab : null;
  const brauchtSports = mitgebracht?.sports === undefined;
  const brauchtGoals = mitgebracht?.goals === undefined;
  const brauchtSessions = mitgebracht?.sessions === undefined;
  const brauchtTermine = termineVonSeite === undefined;

  const [geladen, setGeladen] = useState<Geladen>(() => leer(dogId));
  // Anderer Hund: von vorn beginnen, nichts vom vorigen stehen lassen. (Zustand
  // beim Rendern angleichen - so gibt es keinen Durchlauf mit den alten Daten.)
  if (geladen.dogId !== dogId) setGeladen(leer(dogId));

  useEffect(() => {
    if (!brauchtSports && !brauchtGoals && !brauchtSessions && !brauchtTermine) return;
    let abgebrochen = false;
    const frisch = { sports: false, goals: false, sessions: false };
    const setze = (teil: Partial<Geladen>) => {
      if (!abgebrochen) setGeladen((vorher) => (vorher.dogId === dogId ? { ...vorher, ...teil } : vorher));
    };

    // Nacheinander schreiben: Ziele und Einheiten kommen gleichzeitig an und teilen sich einen Eintrag.
    let schreiben: Promise<void> = Promise.resolve();
    const merke = (teil: EintragenCache) => {
      schreiben = schreiben.then(async () => {
        const alt = await getCachedData<EintragenCache>(`eintragen-${dogId}`);
        await setCachedData(`eintragen-${dogId}`, { ...alt, ...teil });
      });
    };

    async function ladeCache() {
      const [seiteEintrag, fensterEintrag] = await Promise.all([
        getCachedEntry<HundeseitenCache>(`dog-page-${dogId}`),
        getCachedEntry<EintragenCache>(`eintragen-${dogId}`),
      ]);
      if (abgebrochen) return;
      const seite = seiteEintrag?.data;
      const fenster = fensterEintrag?.data;
      const teil = <T,>(eintrag: { cachedAt: number } | null, daten: T | undefined): CacheTeil<T> =>
        eintrag && daten !== undefined ? { daten, cachedAt: eintrag.cachedAt } : null;
      const ziele = neuererCacheTeil(teil(seiteEintrag, seite?.goals), teil(fensterEintrag, fenster?.goals));
      const einheiten = neuererCacheTeil(teil(seiteEintrag, seite?.sessions), teil(fensterEintrag, fenster?.sessions));
      if (seite || ziele || einheiten) {
        // Frisches gewinnt: Der Cache antwortet meist schneller, aber nicht immer.
        // Die Sportarten des Cache sind die ganze Liste, nicht die Auswahl des
        // Hundes: Sie kommen nur zum Zug, wenn der frische Abruf ausbleibt
        // (siehe unten) - sonst liefe die Suche kurz über die ganze, nicht auf den Hund eingegrenzte Liste.
        if (brauchtSports && seite) setze({ cacheSports: seite.sports });
        if (brauchtGoals && !frisch.goals && ziele) setze({ goals: ziele });
        if (brauchtSessions && !frisch.sessions && einheiten) setze({ sessions: einheiten });
      }
      if (brauchtSports && !seite) {
        const sportListe = await getCachedData<Sport[]>("/api/sports");
        if (sportListe) setze({ cacheSports: sportListe });
      }
    }

    async function ladeSports() {
      try {
        const [alle, auswahl] = await Promise.all([
          ladeSportarten(),
          // Fällt die Auswahl aus, gilt "keine Einschränkung" - wie auf der Hundeseite.
          api.get<string[]>(`/api/preferences/dogs/${dogId}/sports`).catch(() => [] as string[]),
        ]);
        frisch.sports = true;
        setze({ sports: angeboteneSportarten(alle, auswahl), sportsFehlgeschlagen: false });
        await setCachedData("/api/sports", alle);
      } catch {
        setze({ sportsFehlgeschlagen: true });
      }
    }

    async function ladeZiele() {
      try {
        const ziele = await api.get<Goal[]>(`/api/goals?dogId=${dogId}`);
        frisch.goals = true;
        setze({ goals: ziele });
        merke({ goals: ziele });
      } catch {
        // Ohne Ziele fehlt nur "Diese Woche" und die Plan-Zuordnung.
        setze({ zieleFehlgeschlagen: true });
      }
    }

    async function ladeEinheiten() {
      try {
        let einheiten = await api.get<TrainingSession[]>(`/api/trainings?dogId=${dogId}&from=${vorDreiMonaten()}`);
        // Wie auf der Hundeseite: Wer lange nicht trainiert hat, soll trotzdem
        // seine letzte Einheit als Vorlage finden.
        if (einheiten.length === 0) einheiten = await api.get<TrainingSession[]>(`/api/trainings?dogId=${dogId}`);
        frisch.sessions = true;
        setze({ sessions: einheiten });
        merke({ sessions: einheiten });
      } catch {
        // Ohne Einheiten fehlen "Zuletzt geübt" und die Vorbelegungen.
        setze({ einheitenFehlgeschlagen: true });
      }
    }

    async function ladeTermine() {
      try {
        setze({ termine: await api.get<GroupTrainingSession[]>(`/api/group-training/schedule/mine?from=${heuteIso()}`) });
      } catch {
        setze({ termine: [] });
      }
    }

    if (brauchtSports || brauchtGoals || brauchtSessions) void ladeCache();
    if (brauchtSports) void ladeSports();
    if (brauchtGoals) void ladeZiele();
    if (brauchtSessions) void ladeEinheiten();
    if (brauchtTermine) void ladeTermine();
    return () => {
      abgebrochen = true;
    };
  }, [dogId, brauchtSports, brauchtGoals, brauchtSessions, brauchtTermine]);

  // Offline: die Liste aus dem Lesecache, nach den Sportarten des Hundes gefiltert (soweit bekannt).
  const sportsAusCache = useMemo(
    () => (geladen.cacheSports ? angeboteneSportarten(geladen.cacheSports, sportIds) : null),
    [geladen.cacheSports, sportIds],
  );
  const sports =
    mitgebracht?.sports ?? geladen.sports ?? (geladen.sportsFehlgeschlagen ? sportsAusCache : null) ?? KEINE_SPORTARTEN;
  const goals = mitgebracht?.goals !== undefined ? mitgebracht.goals : geladen.goals;
  const sessions = mitgebracht?.sessions !== undefined ? mitgebracht.sessions : geladen.sessions;
  return {
    sports,
    goals,
    sessions,
    vorschlaegeLaden:
      (goals === null && !geladen.zieleFehlgeschlagen) || (sessions === null && !geladen.einheitenFehlgeschlagen),
    termine: termineVonSeite ?? geladen.termine ?? KEINE_TERMINE,
    offline: sports.length === 0 && geladen.sportsFehlgeschlagen,
  };
}

/**
 * Die Übungskataloge der Sportarten, nach Sportart. Geladen werden alle
 * Sportarten des Hundes auf einmal (ein bis drei Abrufe): Die Suche läuft über
 * alle, und "Wie beim letzten Mal" kennt zu einer Übung nur den Namen.
 *
 * Jeder Katalog landet im Lesecache und steht beim nächsten Öffnen - auch ohne
 * Netz - sofort bereit. Das ist der "Übungs-Cache" der Suche.
 */
export function useUebungsKatalog(sports: readonly Sport[]): Record<string, Exercise[]> {
  const [katalog, setKatalog] = useState<Record<string, Exercise[]>>({});
  // Am Schlüssel statt am Array: sports wird bei jedem Rendern neu gebildet,
  // ein Abhängen daran liefe endlos.
  const schluessel = sports.map((sport) => sport.id).join(",");

  useEffect(() => {
    let abgebrochen = false;
    // Jede Liste für sich übernehmen, sobald sie da ist, statt alle gemeinsam
    // am Ende: bei drei Sportarten wäre die Suche sonst lange leer.
    for (const sportId of schluessel.split(",").filter(Boolean)) {
      const pfad = `/api/sports/${sportId}/exercises`;
      void (async () => {
        const gemerkt = await getCachedData<Exercise[]>(pfad);
        if (gemerkt && !abgebrochen) setKatalog((vorher) => (vorher[sportId] ? vorher : { ...vorher, [sportId]: gemerkt }));
        try {
          const frisch = await api.get<Exercise[]>(pfad);
          if (abgebrochen) return;
          setKatalog((vorher) => ({ ...vorher, [sportId]: frisch }));
          await setCachedData(pfad, frisch);
        } catch {
          // Offline oder Serverfehler: es bleibt beim Gemerkten, sonst bei freier Eingabe.
        }
      })();
    }
    return () => {
      abgebrochen = true;
    };
  }, [schluessel]);

  return katalog;
}

export type EinstiegsGruppe = { sportId: string; pruefung: Regulation; exerciseIds: string[] };

/** So viele Sportarten und Übungen je Prüfung bietet das erste Training an. */
const EINSTIEG_SPORTARTEN = 2;
export const EINSTIEG_UEBUNGEN = 8;

/**
 * Die Übungen der Einstiegsprüfung (lib/einstieg.ts) für die ersten zwei
 * Sportarten des Hundes - nur, wenn `aktiv` (das Fenster hat sonst keinen
 * Vorschlag) und der Hund Sportarten hat; ohne Auswahl bliebe es Raten.
 * Fällt ein Abruf aus, fehlt die Gruppe still: Die Suche bleibt der Weg.
 */
export function useEinstiegsUebungen(dogId: string, sports: readonly Sport[], aktiv: boolean): EinstiegsGruppe[] {
  const { preferences } = usePreferences();
  const land = preferences.country ?? VORGABE_LAND;
  const [ergebnis, setErgebnis] = useState<{ schluessel: string; gruppen: EinstiegsGruppe[] } | null>(null);
  const eigene = sports.filter((sport) => sport.clubId === null);
  // Am Schlüssel statt am Array (siehe useUebungsKatalog).
  const schluessel = `${dogId}|${land}|${eigene.map((sport) => `${sport.id}:${sport.code}`).join(",")}`;

  useEffect(() => {
    if (!aktiv || eigene.length === 0) return;
    let abgebrochen = false;
    void (async () => {
      try {
        const auswahl = await api.get<string[]>(`/api/preferences/dogs/${dogId}/sports`);
        const gruppen: EinstiegsGruppe[] = [];
        for (const sport of eigene.filter((s) => auswahl.includes(s.id)).slice(0, EINSTIEG_SPORTARTEN)) {
          const ordnungen = await api.get<Regulation[]>(ordnungenPfad(sport.id, land));
          const pruefung = einstiegsOrdnung(sport.code, ordnungen);
          if (!pruefung) continue;
          const detail = await api.get<RegulationDetail>(`/api/sports/regulations/${pruefung.id}`);
          gruppen.push({ sportId: sport.id, pruefung, exerciseIds: detail.exercises.map((e) => e.exerciseId) });
        }
        if (!abgebrochen) setErgebnis({ schluessel, gruppen });
      } catch {
        // Offline oder Serverfehler: keine Vorschläge, "Andere Übung" bleibt.
      }
    })();
    return () => {
      abgebrochen = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `eigene` steckt im Schlüssel
  }, [aktiv, schluessel]);

  return aktiv && ergebnis?.schluessel === schluessel ? ergebnis.gruppen : [];
}

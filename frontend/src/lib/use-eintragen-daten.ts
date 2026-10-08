"use client";

import { useEffect, useMemo, useState } from "react";
import { api } from "@/lib/api";
import { angeboteneSportarten } from "@/lib/eintragen";
import { heuteIso } from "@/lib/pruefung";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import type { Dog, Exercise, Goal, GroupTrainingSession, Sport, TrainingSession } from "@/lib/types";

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
 * Offline: erst der Lesecache der Hundeseite (`dog-page-<id>`), danach - wenn
 * auch der fehlt - bleibt alles leer, und das Fenster funktioniert mit freier
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
};

/** Was die Hundeseite im Lesecache hält (siehe use-dog-page.ts) - hier nur die Teile, die gebraucht werden. */
type HundeseitenCache = { dog?: Dog; sports: Sport[]; goals: Goal[]; sessions: TrainingSession[] };

type Geladen = {
  dogId: string;
  sports: Sport[] | null;
  goals: Goal[] | null;
  sessions: TrainingSession[] | null;
  termine: GroupTrainingSession[] | null;
  /** Die Sportarten aus dem Lesecache - ungefiltert und nur für den Fall, dass kein frischer Abruf gelingt. */
  cacheSports: Sport[] | null;
  sportsFehlgeschlagen: boolean;
};

const leer = (dogId: string): Geladen => ({
  dogId,
  sports: null,
  goals: null,
  sessions: null,
  termine: null,
  cacheSports: null,
  sportsFehlgeschlagen: false,
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

    async function ladeCache() {
      const seite = await getCachedData<HundeseitenCache>(`dog-page-${dogId}`);
      if (abgebrochen) return;
      if (seite) {
        // Frisches gewinnt: Der Cache antwortet meist schneller, aber nicht immer.
        // Die Sportarten des Cache sind die ganze Liste, nicht die Auswahl des
        // Hundes: Sie kommen nur zum Zug, wenn der frische Abruf ausbleibt
        // (siehe unten) - sonst liefe die Suche kurz über alle 17 Sportarten.
        if (brauchtSports) setze({ cacheSports: seite.sports });
        if (brauchtGoals && !frisch.goals) setze({ goals: seite.goals });
        if (brauchtSessions && !frisch.sessions) setze({ sessions: seite.sessions });
      } else if (brauchtSports) {
        const sportListe = await getCachedData<Sport[]>("/api/sports");
        if (sportListe) setze({ cacheSports: sportListe });
      }
    }

    async function ladeSports() {
      try {
        const [alle, auswahl] = await Promise.all([
          api.get<Sport[]>("/api/sports"),
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
      } catch {
        // Ohne Ziele fehlt nur "Diese Woche" und die Plan-Zuordnung.
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
      } catch {
        // Ohne Einheiten fehlen "Zuletzt geübt" und die Vorbelegungen.
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
  return {
    sports,
    goals: mitgebracht?.goals !== undefined ? mitgebracht.goals : geladen.goals,
    sessions: mitgebracht?.sessions !== undefined ? mitgebracht.sessions : geladen.sessions,
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

"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { clearCachedData, getCachedData, setCachedData } from "@/lib/read-cache";
import { useT } from "@/lib/i18n";
import type { Dog, DogOwner, Goal, Sport, TrainingSession } from "@/lib/types";

// Initial werden nur die Trainings der letzten 3 Monate geladen (die
// Historie wächst unbegrenzt) - ältere Monate holt das Tagebuch über
// "Ältere Trainings anzeigen" nach. Statistik und Druckansicht laden ihre
// Daten separat und sind davon unberührt.
function threeMonthsAgoIso(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 3);
  return d.toISOString().slice(0, 10);
}

/** Was die Hundeseite zwischen den Besuchen im Lesecache (`dog-page-<id>`) hält. */
type DogPageCache = {
  dog: Dog;
  sessions: TrainingSession[];
  sports: Sport[];
  myDogIds: string[];
  /**
   * Meine Hunde für die Chips zum Wechseln. Fehlt bei Zwischenständen, die vor
   * dieser Fassung gespeichert wurden - deshalb optional.
   */
  myDogs?: Dog[];
  goals: Goal[];
  owners: DogOwner[];
};

/**
 * Daten der Hundeseite: Hund, Trainings, Ziele, Sportarten, Mitbesitzer und
 * meine Hunde, mit Lesecache (Stale-While-Revalidate) und dem Nachladen älterer
 * Trainings. Die Seite selbst kümmert sich nur noch um das Zusammensetzen.
 *
 * `eintragId` ist der Eintrag, auf den ein Link zeigt (?eintrag=): Liegt er
 * außerhalb der geladenen drei Monate, wird die ganze Historie nachgeladen -
 * einmal. Steht er danach immer noch nicht in der Liste (fremde oder erfundene
 * Id), passiert weiter nichts.
 */
export function useDogPage(id: string, eintragId: string | null) {
  const t = useT();
  const [dog, setDog] = useState<Dog | null>(null);
  const [sessions, setSessions] = useState<TrainingSession[] | null>(null);
  const [sports, setSports] = useState<Sport[]>([]);
  const [goals, setGoals] = useState<Goal[] | null>(null);
  const [isOwner, setIsOwner] = useState(true);
  const [owners, setOwners] = useState<DogOwner[]>([]);
  const [myDogs, setMyDogs] = useState<Dog[]>([]);
  // Der Server kennt den Hund (für mich) nicht: gelöscht, Mitbesitz beendet,
  // Betreuung vorbei - oder die Seite einer anderen Person aus dem
  // Browserverlauf.
  const [nichtGefunden, setNichtGefunden] = useState(false);
  // false = nur die letzten 3 Monate geladen, true = komplette Historie.
  const [showAllHistory, setShowAllHistory] = useState(false);
  // Für diesen Hund geltende Sportarten (eigene Auswahl, sonst die des
  // Menschen). Leere Liste = keine Einschränkung, die Regel dazu steht im
  // Backend (PreferenceService.GetEffectiveDogSportsAsync) - hier wird sie
  // nur angewandt, nicht ein zweites Mal formuliert.
  const [dogSportIds, setDogSportIds] = useState<string[] | null>(null);
  // Ob die Seite ihren frischen Stand vom Server hat (nicht nur den Lesecache).
  // Der Sprung zu einem Eintrag wartet darauf: Erst dann steht fest, ob er in
  // der Liste ist.
  const [frischGeladen, setFrischGeladen] = useState(false);
  const versuchteAlleZuLaden = useRef(false);

  function applyPageData(data: DogPageCache) {
    setNichtGefunden(false);
    setDog(data.dog);
    setSessions(data.sessions);
    setSports(data.sports);
    setIsOwner(data.myDogIds.includes(id));
    setMyDogs(data.myDogs ?? []);
    setGoals(data.goals);
    setOwners(data.owners);
  }

  async function loadAll(all = showAllHistory) {
    // 1. Gecachte Daten sofort anzeigen (Stale-While-Revalidate) - ermöglicht
    //    Offline-Nutzung der letzten gesehenen Daten ohne Wartezeit.
    const cacheKey = `dog-page-${id}`;
    const cached = await getCachedData<DogPageCache>(cacheKey);
    if (cached) applyPageData(cached);

    // 2. Frische Daten im Hintergrund laden.
    try {
      const sessionsPath = all
        ? `/api/trainings?dogId=${id}`
        : `/api/trainings?dogId=${id}&from=${threeMonthsAgoIso()}`;
      const [dogData, sessionDataRaw, sportsData, myDogsData, goalData, ownersData] = await Promise.all([
        api.get<Dog>(`/api/dogs/${id}`),
        api.get<TrainingSession[]>(sessionsPath),
        api.get<Sport[]>("/api/sports"),
        api.get<Dog[]>("/api/dogs"),
        api.get<Goal[]>(`/api/goals?dogId=${id}`),
        api.get<DogOwner[]>(`/api/dogs/${id}/owners`).catch(() => [] as DogOwner[]),
      ]);
      // Getrennt vom Block oben: Fällt der Abruf aus, soll die Seite trotzdem
      // stehen - dann gilt "keine Einschränkung" wie bisher.
      api
        .get<string[]>(`/api/preferences/dogs/${id}/sports`)
        .then((ids) => setDogSportIds(ids))
        .catch(() => setDogSportIds([]));
      // Leeres 3-Monats-Fenster: automatisch auf die komplette Historie
      // zurückfallen, damit ein lange nicht trainierter Hund nicht
      // fälschlich "Noch keine Trainingseinheiten" anzeigt.
      let sessionData = sessionDataRaw;
      if (!all && sessionData.length === 0) {
        sessionData = await api.get<TrainingSession[]>(`/api/trainings?dogId=${id}`);
        setShowAllHistory(true);
      }
      const fresh: DogPageCache = {
        dog: dogData,
        sessions: sessionData,
        sports: sportsData,
        myDogIds: myDogsData.map((d) => d.id),
        myDogs: myDogsData,
        goals: goalData,
        owners: ownersData,
      };
      applyPageData(fresh);
      setFrischGeladen(true);
      await setCachedData(cacheKey, fresh);
    } catch (err) {
      // "Gibt es nicht" ist kein Netzproblem: Dann darf auch kein
      // Zwischenstand stehen bleiben. Vorher zeigte die Seite bei 404 still
      // das gespeicherte Tagebuch weiter - auch einer anderen Person, die
      // sich vorher am selben Gerät angemeldet hatte.
      if (err instanceof ApiError && (err.status === 404 || err.status === 403)) {
        await clearCachedData(cacheKey);
        setDog(null);
        setNichtGefunden(true);
        return;
      }
      // Sonst nur Fehler melden wenn kein Cache vorhanden - mit Cache sind die
      // alten Daten bereits sichtbar und ein Toast wäre verwirrend.
      const cachedAvailable = cached !== null;
      if (!cachedAvailable) toast.error(err instanceof ApiError ? err.message : t("Daten konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount/Routenwechsel (externe Quelle: REST API).
    // loadAll() wird bei jedem Render neu erzeugt, daher absichtlich nicht in
    // den Dependencies - nur "id" soll einen erneuten Abruf auslösen.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function loadOlderSessions() {
    setShowAllHistory(true);
    await loadAll(true);
  }

  useEffect(() => {
    if (!eintragId || !frischGeladen || !sessions || showAllHistory || versuchteAlleZuLaden.current) return;
    if (sessions.some((s) => s.id === eintragId)) return;
    versuchteAlleZuLaden.current = true;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void loadOlderSessions();
    // loadOlderSessions wird bei jedem Render neu erzeugt - nur die Bedingungen oben zählen.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eintragId, frischGeladen, sessions, showAllHistory]);

  return {
    dog,
    sessions,
    sports,
    goals,
    isOwner,
    owners,
    myDogs,
    nichtGefunden,
    showAllHistory,
    dogSportIds,
    frischGeladen,
    loadAll,
    loadOlderSessions,
  };
}

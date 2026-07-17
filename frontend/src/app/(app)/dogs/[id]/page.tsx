"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { Dog, DogOwner, Goal, Sport, TrainingSession } from "@/lib/types";
import { Button, buttonVariants } from "@/components/ui/button";
import { Dog as DogIcon, Plus, Printer, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { GoalsSection } from "@/components/dogs/goals-section";
import { TrainingForm } from "@/components/dogs/training-form";
import { SessionHistory } from "@/components/dogs/session-history";
import { CoOwnersSection } from "@/components/dogs/co-owners-section";
import { FahrteRecorder } from "@/components/tracking/fahrte-recorder";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import { useAuth } from "@/lib/auth-context";

// Initial werden nur die Trainings der letzten 3 Monate geladen (die
// Historie wächst unbegrenzt) - ältere Monate holt SessionHistory über
// "Ältere Trainings anzeigen" nach. Statistik und Druckansicht laden ihre
// Daten separat und sind davon unberührt.
function threeMonthsAgoIso(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 3);
  return d.toISOString().slice(0, 10);
}

/**
 * Hundeseite: orchestriert Kopfzeile, Ziele, Fährten-Recorder,
 * Trainingstagebuch (TrainingForm + SessionHistory) und Mitbesitzer.
 * Die eigentliche Logik lebt in den Sektions-Komponenten - Zerlegung nach
 * dem goals-section-Muster (siehe TODO.md Roadmap 5b), nachdem die
 * frühere 686-Zeilen-Variante derselbe Wartbarkeits-Risikofall war wie
 * goals-section.tsx vor ihrem Refactor.
 */
export default function DogDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { user } = useAuth();

  const [dog, setDog] = useState<Dog | null>(null);
  const [sessions, setSessions] = useState<TrainingSession[] | null>(null);
  const [sports, setSports] = useState<Sport[]>([]);
  const [goals, setGoals] = useState<Goal[] | null>(null);
  const [isOwner, setIsOwner] = useState(true);
  const [owners, setOwners] = useState<DogOwner[]>([]);
  const [showForm, setShowForm] = useState(false);
  // false = nur die letzten 3 Monate geladen, true = komplette Historie.
  const [showAllHistory, setShowAllHistory] = useState(false);

  type DogPageCache = {
    dog: Dog;
    sessions: TrainingSession[];
    sports: Sport[];
    myDogIds: string[];
    goals: Goal[];
    owners: DogOwner[];
  };

  function applyPageData(data: DogPageCache) {
    setDog(data.dog);
    setSessions(data.sessions);
    setSports(data.sports);
    setIsOwner(data.myDogIds.includes(id));
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
      const [dogData, sessionDataRaw, sportsData, myDogs, goalData, ownersData] = await Promise.all([
        api.get<Dog>(`/api/dogs/${id}`),
        api.get<TrainingSession[]>(sessionsPath),
        api.get<Sport[]>("/api/sports"),
        api.get<Dog[]>("/api/dogs"),
        api.get<Goal[]>(`/api/goals?dogId=${id}`),
        api.get<DogOwner[]>(`/api/dogs/${id}/owners`).catch(() => [] as DogOwner[]),
      ]);
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
        myDogIds: myDogs.map((d) => d.id),
        goals: goalData,
        owners: ownersData,
      };
      applyPageData(fresh);
      await setCachedData(cacheKey, fresh);
    } catch (err) {
      // Nur Fehler melden wenn kein Cache vorhanden - mit Cache sind die alten
      // Daten bereits sichtbar und ein Toast wäre verwirrend.
      const cachedAvailable = cached !== null;
      if (!cachedAvailable) toast.error(err instanceof ApiError ? err.message : "Daten konnten nicht geladen werden.");
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

  async function deleteDog() {
    if (!dog) return;
    // Doppelte Bestätigung: Hunde-Löschen entfernt Trainings, Fährten, Ziele
    // und Trainerzuweisungen mit - deutlich schwerwiegender als das Löschen
    // einer einzelnen Session, deshalb zusätzlich Name-Bestätigung.
    if (!confirm(`Hund „${dog.name}" wirklich löschen? Alle Trainings, Fährten, Ziele und Trainerzuweisungen werden entfernt.`)) return;
    const confirmName = prompt(`Zum Bestätigen bitte den Namen des Hundes eingeben: „${dog.name}"`);
    if (confirmName?.trim() !== dog.name) {
      if (confirmName !== null) toast.error("Name stimmt nicht - Löschen abgebrochen.");
      return;
    }
    try {
      await api.delete(`/api/dogs/${id}`);
      toast.success(`Hund „${dog.name}" gelöscht.`);
      router.push("/dogs");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  async function handleTrainingSaved(offline: boolean) {
    setShowForm(false);
    // Offline gespeicherte Trainings liegen nur in der Warteschlange - ein
    // Server-Reload würde sie nicht enthalten und nur verwirren.
    if (!offline) await loadAll();
  }

  if (!dog) return <p className="text-muted-foreground">Lädt…</p>;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="flex size-12 items-center justify-center rounded-full bg-secondary">
            <DogIcon className="size-6 text-secondary-foreground" />
          </div>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{dog.name}</h1>
            <p className="text-muted-foreground">{dog.breed ?? "Unbekannte Rasse"}</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Link href={`/dogs/${id}/print`} className={buttonVariants({ variant: "outline", size: "sm" })}>
            <Printer className="size-4" />
            Drucken / Exportieren
          </Link>
          {isOwner && (
            <Button
              size="sm"
              variant="ghost"
              className="text-destructive hover:text-destructive"
              onClick={deleteDog}
              title="Hund löschen"
            >
              <Trash2 className="size-4" />
              <span className="hidden sm:inline">Hund löschen</span>
            </Button>
          )}
        </div>
      </div>

      <GoalsSection dogId={id} sports={sports} goals={goals} onChanged={loadAll} />

      <FahrteRecorder dogId={id} onSaved={loadAll} />

      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Trainingstagebuch</h2>
        <Button size="sm" onClick={() => setShowForm((v) => !v)}>
          <Plus className="size-4" />
          Training erfassen
        </Button>
      </div>

      {showForm && <TrainingForm dogId={id} sports={sports} goals={goals} onSaved={handleTrainingSaved} />}

      <SessionHistory
        sessions={sessions}
        isOwner={isOwner}
        onChanged={loadAll}
        onLoadOlder={showAllHistory ? null : loadOlderSessions}
      />

      {isOwner && <CoOwnersSection dogId={id} owners={owners} currentUserId={user?.userId} onChanged={loadAll} />}
    </div>
  );
}

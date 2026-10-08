"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Building2, ChevronRight, GraduationCap } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/api";
import type {
  DashboardDaten,
  Dog as HundDaten,
  GroupTrainingSession,
  OffenesFeedback,
  OnboardingStatus,
  Sport,
  TrainerOpenCounts,
  TrainingPlanItem,
} from "@/lib/types";
import { OnboardingGuide, zeigtErststart } from "@/components/onboarding/onboarding-guide";
import { usePreferences } from "@/lib/preferences-context";
import { MODULE } from "@/lib/types";
import { laeuftFaehrte, nichtAbgelaufeneFaehrten } from "@/lib/faehrte";
import { merkeGelesenImErststart } from "@/lib/neuerungen-gelesen";
import { zeigeSachkundeZeile, zielAufteilung } from "@/lib/startseite";
import { ErfassenKarte } from "@/components/dashboard/erfassen-karte";
import { FeedbackKarte } from "@/components/dashboard/feedback-karte";
import { TerminKarte } from "@/components/dashboard/termin-karte";
import { ZuErledigenZeile } from "@/components/dashboard/zu-erledigen-zeile";
import { AbgelaufeneZiele, OhneZielZeilen, ZielKarten } from "@/components/dashboard/ziel-karten";
import { HeuteGelegtSection, type FaehrteMitHund } from "@/components/dashboard/heute-gelegt-section";
import { EintragenSheet } from "@/components/dogs/eintragen-sheet";
import { useT } from "@/lib/i18n";

/** Kompakte Zeile, wie sie die Startseite für Nebensächliches nutzt (Verein, Sachkunde). */
const ZEILE =
  "flex min-h-11 min-w-0 items-center gap-2 rounded-lg border border-surface-border bg-surface px-3 text-sm font-medium transition-colors hover:border-primary/40";

type Startdaten = {
  hunde: HundDaten[];
  faehrtenHundeIds: string[];
  /** Die Sportarten je Hund (leer = keine Einschränkung); fehlt bei Zwischenständen aus älteren Fassungen. */
  sportIdsJeHund?: Record<string, string[]>;
  // Aufgeteilt nach Zielen: Karten (laufend), Ergebnis fehlt (abgelaufen), ohne Ziel.
  ziele: ReturnType<typeof zielAufteilung>;
  faehrten: FaehrteMitHund[];
  onboarding: OnboardingStatus | null;
  termine: GroupTrainingSession[];
  feedback: OffenesFeedback[];
};

/**
 * Letzter Stand der Startseite, im Speicher des Tabs und an den Nutzer
 * gebunden.
 *
 * Wer über die untere Leiste zurück auf Home tippt, sieht sofort den letzten
 * Stand statt des Platzhalters; die frischen Daten ziehen im Hintergrund nach.
 * Bewusst nicht im localStorage: Neu laden beginnt sauber. Die Nutzer-Id
 * verhindert, dass nach einem Kontowechsel im selben Tab etwas vom Vorgänger
 * aufblitzt.
 */
let zwischenstand: { nutzerId: string; daten: Startdaten } | null = null;

/**
 * Alles für die Startseite in einer Welle.
 *
 * Vorher kamen erst die Hunde und danach je Hund Sportarten, Ziele, Trainings
 * und Fährten: bei zwei Hunden 13 Anfragen in zwei bis drei Wellen. Auf dem
 * Handy kostet jede Welle eine Rundreise samt Vorab-Anfrage (CORS), und die
 * Abschnitte rutschten nacheinander ins Bild. Jetzt holt /api/dashboard die
 * Daten je Hund in einem Aufruf, der Rest läuft parallel dazu.
 *
 * Fehler einzelner Teile lassen nur deren Abschnitt weg - die Startseite
 * steht auch offline oder bei einem Serverfehler.
 */
async function ladeStartdaten(): Promise<Startdaten> {
  const heute = new Date().toISOString().slice(0, 10);
  const [dashboard, sports, onboarding, termine] = await Promise.all([
    api.get<DashboardDaten>("/api/dashboard").catch(() => null),
    api.get<Sport[]>("/api/sports").catch(() => [] as Sport[]),
    api.get<OnboardingStatus>("/api/onboarding/status").catch(() => null),
    api
      .get<GroupTrainingSession[]>(`/api/group-training/schedule/mine?from=${heute}`)
      .catch(() => [] as GroupTrainingSession[]),
  ]);

  const eintraege = dashboard?.dogs ?? [];
  const jetzt = Date.now();
  const bhSportIds = new Set(sports.filter((sport) => sport.code === "BH").map((sport) => sport.id));
  return {
    hunde: eintraege.map((e) => e.dog),
    // Dieselbe Regel wie auf der Hundeseite (laeuftFaehrte): sonst führte
    // "Fährte legen" auf eine Hundeseite ohne Recorder.
    faehrtenHundeIds: eintraege.filter((e) => laeuftFaehrte(e.sportIds, sports)).map((e) => e.dog.id),
    sportIdsJeHund: Object.fromEntries(eintraege.map((e) => [e.dog.id, e.sportIds])),
    ziele: zielAufteilung(
      eintraege.map((e) => ({ dog: e.dog, activeGoals: e.activeGoals })),
      bhSportIds,
      jetzt,
    ),
    faehrten: eintraege.flatMap((e) =>
      nichtAbgelaufeneFaehrten(e.tracksToday, jetzt).map((f) => ({ ...f, hund: e.dog })),
    ),
    onboarding,
    termine,
    // Fehlt bei einem zwischengespeicherten Stand aus der Zeit vor der Feedback-Karte.
    feedback: dashboard?.openFeedback ?? [],
  };
}

export default function DashboardPage() {
  const { user, isTrainer } = useAuth();
  const { moduleEnabled } = usePreferences();
  const t = useT();
  const nutzerId = user?.userId ?? null;

  const [daten, setDaten] = useState<Startdaten | null>(() =>
    zwischenstand && zwischenstand.nutzerId === nutzerId ? zwischenstand.daten : null,
  );
  const [trainerZahlen, setTrainerZahlen] = useState<TrainerOpenCounts | null>(null);

  function uebernehmen(neu: Startdaten) {
    if (nutzerId) zwischenstand = { nutzerId, daten: neu };
    setDaten(neu);
  }

  async function neuLaden() {
    uebernehmen(await ladeStartdaten());
  }

  // Das Eintragen-Fenster: offen oder zu, getrennt von dem, wofür es aufging -
  // beim Schließen soll das Ziel stehen bleiben, sonst bräche der Inhalt schon
  // während des Wegschiebens weg.
  const [eintragenOffen, setEintragenOffen] = useState(false);
  const [eintragenZiel, setEintragenZiel] = useState<{ dogId: string; plan: TrainingPlanItem | null } | null>(null);
  function oeffneEintragen(dogId: string, plan: TrainingPlanItem | null = null) {
    setEintragenZiel({ dogId, plan });
    setEintragenOffen(true);
  }
  // Offline gespeicherte Trainings liegen nur in der Warteschlange: Ein
  // Neuladen vom Server zeigte sie nicht und ließe die Seite im Netzlosen leer.
  async function trainingGespeichert(offline: boolean) {
    if (!offline) await neuLaden();
  }

  useEffect(() => {
    let abgebrochen = false;
    void ladeStartdaten().then((frisch) => {
      if (abgebrochen) return;
      if (nutzerId) zwischenstand = { nutzerId, daten: frisch };
      setDaten(frisch);
    });
    return () => {
      abgebrochen = true;
    };
  }, [nutzerId]);

  const faehrteAn = moduleEnabled(MODULE.faehrte);
  const sachkundeAn = moduleEnabled(MODULE.sachkunde);
  const trainerZeileAn = isTrainer === true && moduleEnabled(MODULE.gruppentraining);

  // "Zu erledigen" der Trainer:innen: dieselben Zahlen wie auf der Trainer-
  // Übersicht. Eigener Abruf statt Teil der Startseiten-Daten, weil die Rolle
  // erst nach dem Anmelden feststeht und Nicht-Trainer:innen ihn nie brauchen.
  // Schlägt er fehl, fehlt nur die Zeile.
  useEffect(() => {
    if (!trainerZeileAn) return;
    let abgebrochen = false;
    api
      .get<TrainerOpenCounts>("/api/groups/open-counts")
      .then((zahlen) => {
        if (!abgebrochen) setTrainerZahlen(zahlen);
      })
      .catch(() => {});
    return () => {
      abgebrochen = true;
    };
  }, [trainerZeileAn]);

  // Vereinszugehörigkeit kommt aus dem Erststart-Status statt aus einer
  // eigenen Abfrage auf /api/clubs/my-memberships. Die kannte nur
  // Mitgliedschaften - und Vereinstrainer:innen haben keine, sie stehen in
  // einer eigenen Tabelle. Sie bekamen deshalb die Aufforderung, einem Verein
  // beizutreten, den sie leiten.
  const hasNoClub = daten?.onboarding != null && !daten.onboarding.hasClubMembership;

  const erststart = daten !== null && zeigtErststart(daten.onboarding);

  // Wer gerade erst anfängt, dem sagt "Neuerungen" nichts: still als gelesen
  // vermerken, damit der Punkt am Profil-Reiter später nicht nachträglich
  // auftaucht. Ohne den Status (noch nicht geladen) steht erststart auf false.
  useEffect(() => {
    merkeGelesenImErststart(erststart);
  }, [erststart]);

  // Wofür die Seite täglich geöffnet wird: was heute gelegt wurde und aufs
  // Ablaufen wartet, und die Karte zum Erfassen. Nur der Erststart behält die
  // alte Reihenfolge (Leitfaden vor den Terminen), er führt die Neuen ohnehin
  // zum ersten Training.
  //
  // Nur wenn relevant: eine heute gelegte, noch nicht abgelaufene Fährte steht
  // über der Karte - sie wartet auf ihr Alter.
  const heuteGelegt =
    daten !== null && faehrteAn ? (
      <HeuteGelegtSection faehrten={daten.faehrten} mehrereHunde={daten.hunde.length > 1} onChanged={neuLaden} />
    ) : null;

  const erfassen =
    daten !== null && daten.hunde.length > 0 ? (
      <ErfassenKarte hunde={daten.hunde} faehrtenHundeIds={faehrteAn ? daten.faehrtenHundeIds : []} onEintragen={oeffneEintragen} />
    ) : null;

  return (
    <div className="flex flex-col gap-4">
      {/* Unabhängig davon, ob die Seite gerade neu lädt: Ein halb ausgefülltes
          Fenster darf nicht mit einem Neuladen im Hintergrund verschwinden. */}
      {eintragenZiel && (
        <EintragenSheet
          open={eintragenOffen}
          onOpenChange={setEintragenOffen}
          dogId={eintragenZiel.dogId}
          hunde={(daten?.hunde ?? []).filter((hund) => !hund.archivedAt)}
          termine={daten?.termine}
          sportIdsJeHund={daten?.sportIdsJeHund}
          vorgabePlan={eintragenZiel.plan}
          onSaved={trainingGespeichert}
        />
      )}
      {erststart ? (
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            {t("Willkommen zurück, {name}", { name: user?.firstName ?? "" })}
          </h1>
          <p className="text-muted-foreground">{t("Hier ist dein Überblick für heute.")}</p>
        </div>
      ) : (
        <h1 className="text-2xl font-semibold tracking-tight">{t("Hallo, {name}", { name: user?.firstName ?? "" })}</h1>
      )}

      {daten === null ? (
        // Platzhalter etwa in der Größe der Karten: Die Startseite erscheint
        // danach in einem Zug, statt Abschnitt für Abschnitt nachzurutschen und
        // die Karten darunter wegzuschieben.
        <div className="flex flex-col gap-4" aria-busy="true">
          <span className="sr-only">{t("Lädt…")}</span>
          <div className="h-28 rounded-xl bg-card motion-safe:animate-pulse" />
          <div className="h-36 rounded-xl bg-card motion-safe:animate-pulse" />
        </div>
      ) : erststart ? (
        <>
          {/* Der Erststart behält seine Reihenfolge: Ziele, Leitfaden, Termine, Erfassen. */}
          <ZielKarten eintraege={daten.ziele.karten} sachkundeAn={sachkundeAn} onEintragen={oeffneEintragen} />

          <OnboardingGuide
            status={daten.onboarding}
            onDismissed={() =>
              uebernehmen({ ...daten, onboarding: daten.onboarding && { ...daten.onboarding, isDismissed: true } })
            }
          />

          <TerminKarte sessions={daten.termine} />

          {heuteGelegt}
          {erfassen}

          <FeedbackKarte eintraege={daten.feedback} onChanged={neuLaden} />
          <AbgelaufeneZiele eintraege={daten.ziele.abgelaufen} onChanged={neuLaden} />
          <ZuErledigenZeile zahlen={trainerZahlen} />
        </>
      ) : (
        <>
          {heuteGelegt}

          {/* Nur mit einem Termin in den nächsten 7 Tagen - viele Vereine regeln
              Termine anders, dann bleibt hier alles leer. */}
          <TerminKarte sessions={daten.termine} />

          <FeedbackKarte eintraege={daten.feedback} onChanged={neuLaden} />

          {erfassen}

          <ZielKarten eintraege={daten.ziele.karten} sachkundeAn={sachkundeAn} onEintragen={oeffneEintragen} />
          <AbgelaufeneZiele eintraege={daten.ziele.abgelaufen} onChanged={neuLaden} />
          <OhneZielZeilen hunde={daten.ziele.ohneZiel} />

          <ZuErledigenZeile zahlen={trainerZahlen} />

          {/* Der Vereinsbeitritt als schmale Zeile, erst nach den Hunde- und
              Zielkarten. Solange der Erststart läuft, spricht er ihn selbst an. */}
          {hasNoClub && (
            <Link href="/clubs" className={ZEILE}>
              <Building2 className="size-4 shrink-0 text-primary-text" aria-hidden />
              <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">{t("Tritt einem Verein bei")}</span>
              <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
            </Link>
          )}
        </>
      )}

      {/* Sachkunde hat in der Handy-Navigation keinen anderen Platz (Sportarten
          stehen im Profil) - deshalb diese eine kleine Zeile, außer eine
          BH-Karte trägt den Link schon. */}
      {daten !== null && zeigeSachkundeZeile(daten.ziele.karten, sachkundeAn) && (
        <Link href="/lernen" className={ZEILE}>
          <GraduationCap className="size-4 shrink-0 text-primary-text" aria-hidden />
          <span className="min-w-0 flex-1">{t("Sachkunde üben")}</span>
          <ChevronRight className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        </Link>
      )}
    </div>
  );
}

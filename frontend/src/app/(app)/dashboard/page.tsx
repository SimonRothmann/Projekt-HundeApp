"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Building2, ChevronRight, GraduationCap } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/api";
import type {
  DashboardDaten,
  DashboardGruppeneinladung,
  DashboardVereinseinladung,
  Dog as HundDaten,
  GroupTrainingSession,
  OffenesFeedback,
  OnboardingStatus,
  Sport,
  TrainerOpenCounts,
  TrainingPlanItem,
} from "@/lib/types";
import { Button } from "@/components/ui/button";
import { OnboardingGuide } from "@/components/onboarding/onboarding-guide";
import { ErsterHundKarte } from "@/components/onboarding/erster-hund-karte";
import { erststartZustand } from "@/lib/erststart";
import { usePreferences } from "@/lib/preferences-context";
import { MODULE } from "@/lib/types";
import { laeuftFaehrte, nichtAbgelaufeneFaehrten, spurArten, type SpurArten } from "@/lib/faehrte";
import { merkeGelesenImErststart } from "@/lib/neuerungen-gelesen";
import { pausenOhneKarte, zeigeSachkundeZeile, zielAufteilung } from "@/lib/startseite";
import { fuerStartseite } from "@/lib/trainer-offen";
import { ErfassenKarte } from "@/components/dashboard/erfassen-karte";
import { PushHinweisKarte } from "@/components/dashboard/push-hinweis-karte";
import { EinladungenKarte } from "@/components/dashboard/einladungen-karte";
import { FeedbackKarte } from "@/components/dashboard/feedback-karte";
import { TerminKarte } from "@/components/dashboard/termin-karte";
import { ZuErledigenZeile } from "@/components/dashboard/zu-erledigen-zeile";
import { AbgelaufeneZiele, OhneZielZeilen, PausenZeilen, ZielKarten } from "@/components/dashboard/ziel-karten";
import { HeuteGelegtSection, type FaehrteMitHund } from "@/components/dashboard/heute-gelegt-section";
import { GesundheitsHinweise } from "@/components/dashboard/gesundheit-hinweise";
import { MerkzettelHeuteKarte } from "@/components/dashboard/merkzettel-heute-karte";
import { MerkenSheet } from "@/components/dogs/merken-sheet";
import { EintragenSheet } from "@/components/dogs/eintragen-sheet";
import { DogFaehrteSheet } from "@/components/dogs/dog-faehrte-sheet";
import { useT } from "@/lib/i18n";
import { heuteIso } from "@/lib/kalendertag";

/** Kompakte Zeile, wie sie die Startseite für Nebensächliches nutzt (Verein, Sachkunde). */
const ZEILE =
  "flex min-h-11 min-w-0 items-center gap-2 rounded-lg border border-surface-border bg-surface px-3 text-sm font-medium transition-colors hover:border-primary/40";

/** Stand eines Kontos, das gerade erst den Hund angelegt hat - Grundlage, wenn der Server-Status fehlt. */
const OHNE_FORTSCHRITT: OnboardingStatus = {
  hasDog: false,
  firstDogId: null,
  hasGoal: false,
  hasTraining: false,
  hasClubMembership: false,
  hasPendingClubRequest: false,
  hasGroupMembership: false,
  hasPendingGroupRequest: false,
  isDismissed: false,
  isComplete: false,
  firstTrainingAt: null,
};

type Startdaten = {
  hunde: HundDaten[];
  faehrtenHundeIds: string[];
  /** Die Sportarten je Hund (leer = keine Einschränkung). */
  sportIdsJeHund: Record<string, string[]>;
  /** Was jeder Hund legen kann (Fährte, Trail). */
  spurArtenJeHund: Record<string, SpurArten>;
  // Aufgeteilt nach Zielen: Karten (laufend), Ergebnis fehlt (abgelaufen), ohne Ziel.
  ziele: ReturnType<typeof zielAufteilung>;
  faehrten: FaehrteMitHund[];
  onboarding: OnboardingStatus | null;
  termine: GroupTrainingSession[];
  feedback: OffenesFeedback[];
  /** Eigene offene Gruppeneinladungen. */
  einladungen: DashboardGruppeneinladung[];
  /** Eigene offene Vereinseinladungen. */
  vereinsEinladungen: DashboardVereinseinladung[];
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
 * Fehlt die Antwort von /api/dashboard oder die Sportartenliste, wirft die
 * Funktion: Leere Listen sagten dort "kein Hund", "keine Fährte" und "keine
 * Einschränkung", obwohl nur nicht geladen wurde (der Server liefert bei einem
 * Fehler einer Teilabfrage ebenfalls einen Fehler für das Ganze). Der Aufrufer
 * zeigt dann den Fehlerzustand oder behält den letzten Stand. Nur Termine und
 * Erststart-Status dürfen ausfallen: Ohne sie fehlt bloß deren Karte.
 */
async function ladeStartdaten(): Promise<Startdaten> {
  // Der Kalendertag auf dem Gerät, nicht der UTC-Tag: Nach Mitternacht läge
  // dieser sonst noch beim gestrigen und zeigte schon vorbeigegangene Termine.
  const heute = heuteIso();
  const [dashboard, sports, onboarding, termine] = await Promise.all([
    api.get<DashboardDaten>("/api/dashboard"),
    api.get<Sport[]>("/api/sports"),
    api.get<OnboardingStatus>("/api/onboarding/status").catch(() => null),
    api
      .get<GroupTrainingSession[]>(`/api/group-training/schedule/mine?from=${heute}`)
      .catch(() => [] as GroupTrainingSession[]),
  ]);

  const eintraege = dashboard.dogs;
  const jetzt = Date.now();
  const bhSportIds = new Set(sports.filter((sport) => sport.code === "BH").map((sport) => sport.id));
  return {
    hunde: eintraege.map((e) => e.dog),
    // Dieselbe Regel wie auf der Hundeseite (laeuftFaehrte): sonst führte
    // "Fährte legen" auf eine Hundeseite ohne Recorder.
    faehrtenHundeIds: eintraege.filter((e) => laeuftFaehrte(e.sportIds, sports)).map((e) => e.dog.id),
    sportIdsJeHund: Object.fromEntries(eintraege.map((e) => [e.dog.id, e.sportIds])),
    spurArtenJeHund: Object.fromEntries(eintraege.map((e) => [e.dog.id, spurArten(e.sportIds, sports)])),
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
    feedback: dashboard.openFeedback,
    einladungen: dashboard.groupInvitations,
    vereinsEinladungen: dashboard.clubInvitations,
  };
}

export default function DashboardPage() {
  const { user, isTrainer, managesMembers } = useAuth();
  const { moduleEnabled } = usePreferences();
  const t = useT();
  const nutzerId = user?.userId ?? null;

  const [daten, setDaten] = useState<Startdaten | null>(() =>
    zwischenstand && zwischenstand.nutzerId === nutzerId ? zwischenstand.daten : null,
  );
  const [trainerZahlen, setTrainerZahlen] = useState<TrainerOpenCounts | null>(null);
  // Das (Neu-)Laden ist gescheitert: ohne Daten der Fehlerzustand mit "Erneut versuchen", mit Daten
  // bleibt der letzte Stand stehen und ein Hinweis sagt, dass er nicht aktuell ist.
  const [ladefehler, setLadefehler] = useState(false);

  function uebernehmen(neu: Startdaten) {
    if (nutzerId) zwischenstand = { nutzerId, daten: neu };
    setDaten(neu);
    setLadefehler(false);
  }

  function erneutVersuchen() {
    setLadefehler(false);
    void neuLaden();
  }

  // Wirft nie: Die Aufrufer warten darauf nach einer Aktion (Training gespeichert, Ziel geändert)
  // und hätten für einen Fehler beim Neuladen keine passende Meldung.
  async function neuLaden() {
    try {
      uebernehmen(await ladeStartdaten());
    } catch {
      setLadefehler(true);
    }
  }

  // Der Hund der Karte "Wie heißt dein Hund?" ist angelegt: Startseite neu
  // laden (damit sie als normale Startseite mit Hund dasteht) und gleich das
  // Eintragen-Fenster für ihn öffnen - ohne Seitenwechsel. Schließt der
  // Nutzer es ohne Speichern, bleibt genau diese Startseite stehen.
  async function ersterHundAngelegt(hund: HundDaten) {
    const frisch = await ladeStartdaten().catch(() => null);
    // Kam der Hund im Neuladen nicht an (kurz ohne Netz), tragen wir ihn von
    // Hand ein: Die Karte bliebe sonst stehen, und ein zweiter Druck auf
    // "Weiter" legte ihn doppelt an.
    if (frisch?.hunde.some((h) => h.id === hund.id)) {
      uebernehmen(frisch);
    } else {
      const basis = frisch ?? daten;
      if (basis) {
        uebernehmen({
          ...basis,
          hunde: [...basis.hunde.filter((h) => h.id !== hund.id), hund],
          onboarding: {
            ...(basis.onboarding ?? OHNE_FORTSCHRITT),
            hasDog: true,
            firstDogId: hund.id,
          },
        });
      }
    }
    oeffneEintragen(hund.id);
  }

  function erststartWegklicken() {
    if (daten) uebernehmen({ ...daten, onboarding: daten.onboarding && { ...daten.onboarding, isDismissed: true } });
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

  // Das Merken-Fenster (Karte "Training heute"): wie beim Eintragen bleibt der Hund beim Schließen stehen.
  const [merkenOffen, setMerkenOffen] = useState(false);
  const [merkenHund, setMerkenHund] = useState<string | null>(null);
  function oeffneMerken(dogId: string) {
    setMerkenHund(dogId);
    setMerkenOffen(true);
  }

  // "Fährte legen" im Fenster statt über die Hundeseite. Wie beim Eintragen
  // bleibt der Hund beim Schließen stehen, damit der Inhalt nicht wegbricht.
  const [faehrteOffen, setFaehrteOffen] = useState(false);
  const [faehrteHund, setFaehrteHund] = useState<string | null>(null);
  function oeffneFaehrte(dogId: string) {
    setFaehrteHund(dogId);
    setFaehrteOffen(true);
  }

  useEffect(() => {
    let abgebrochen = false;
    ladeStartdaten().then(
      (frisch) => {
        if (abgebrochen) return;
        if (nutzerId) zwischenstand = { nutzerId, daten: frisch };
        setDaten(frisch);
        setLadefehler(false);
      },
      () => {
        if (!abgebrochen) setLadefehler(true);
      },
    );
    return () => {
      abgebrochen = true;
    };
  }, [nutzerId]);

  const faehrteAn = moduleEnabled(MODULE.faehrte);
  const sachkundeAn = moduleEnabled(MODULE.sachkunde);
  const gesundheitAn = moduleEnabled(MODULE.gesundheit);
  const trainerZeileAn = isTrainer === true && moduleEnabled(MODULE.gruppentraining);
  // Die Beitrittsanfragen des Vereins hängen weder an der Trainer-Rolle noch am Modul Gruppentraining.
  const zeileAn = trainerZeileAn || managesMembers === true;

  // "Zu erledigen" der Trainer:innen und der Mitgliederverwaltung: dieselben
  // Zahlen wie auf der Trainer-Übersicht. Eigener Abruf statt Teil der
  // Startseiten-Daten, weil die Rolle erst nach dem Anmelden feststeht und
  // alle anderen ihn nie brauchen. Schlägt er fehl, fehlt nur die Zeile.
  useEffect(() => {
    if (!zeileAn) return;
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
  }, [zeileAn]);

  // Vereinszugehörigkeit kommt aus dem Erststart-Status statt aus einer
  // eigenen Abfrage auf /api/clubs/my-memberships. Die kannte nur
  // Mitgliedschaften - und Vereinstrainer:innen haben keine, sie stehen in
  // einer eigenen Tabelle. Sie bekamen deshalb die Aufforderung, einem Verein
  // beizutreten, den sie leiten.
  const hasNoClub = daten?.onboarding != null && !daten.onboarding.hasClubMembership;

  const erststartKarte = daten === null ? null : erststartZustand(daten.onboarding);
  const erststart = erststartKarte !== null;

  // Wer gerade erst anfängt, dem sagt "Neuerungen" nichts: still als gelesen
  // vermerken, damit der Punkt am Profil-Reiter später nicht nachträglich
  // auftaucht. Nur solange der Hund fehlt: In der Gabelung steht auch, wer
  // schon länger dabei ist und sein erstes Training erst vor wenigen Tagen
  // eingetragen hat - der soll "Neuerungen" nicht still verpassen. Ohne den
  // Status (noch nicht geladen) steht ohneHund auf false.
  const ohneHund = erststartKarte === "hund";
  useEffect(() => {
    merkeGelesenImErststart(ohneHund);
  }, [ohneHund]);

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

  // Direkt unter dem Kopf, vor allem anderen: Wer eingeladen ist, wartet auf
  // diesen einen Tipp - und danach erscheinen ggf. die Termine der Gruppe.
  const einladungen =
    daten !== null ? <EinladungenKarte gruppen={daten.einladungen} vereine={daten.vereinsEinladungen} onChanged={neuLaden} /> : null;

  const erfassen =
    daten !== null && daten.hunde.length > 0 ? (
      <ErfassenKarte
        hunde={daten.hunde}
        faehrtenHundeIds={faehrteAn ? daten.faehrtenHundeIds : []}
        nurTrailHundeIds={Object.entries(daten.spurArtenJeHund)
          .filter(([, a]) => a.mantrailing && !a.faehrte)
          .map(([id]) => id)}
        onEintragen={oeffneEintragen} onFaehrte={oeffneFaehrte} />
    ) : null;

  return (
    <div className="flex flex-col gap-4">
      {/* Unabhängig davon, ob die Seite gerade neu lädt: Ein halb ausgefülltes
          Fenster darf nicht mit einem Neuladen im Hintergrund verschwinden. */}
      {faehrteHund && (
        // key: Ein anderer Hund bekommt einen frischen Recorder statt des Zustands des vorigen.
        <DogFaehrteSheet key={faehrteHund} dogId={faehrteHund} arten={daten?.spurArtenJeHund[faehrteHund]} open={faehrteOffen} onOpenChange={setFaehrteOffen} onSaved={neuLaden} />
      )}
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
      {merkenHund && (
        <MerkenSheet
          open={merkenOffen}
          onOpenChange={setMerkenOffen}
          dogId={merkenHund}
          hunde={(daten?.hunde ?? []).filter((hund) => !hund.archivedAt)}
          // Offline liegt der Eintrag nur in der Warteschlange - ein Neuladen würde ihn nicht zeigen.
          onSaved={async (offline) => {
            if (!offline) await neuLaden();
          }}
        />
      )}
      {erststart ? (
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            {t("Willkommen bei Dogity, {name}", { name: user?.firstName ?? "" })}
          </h1>
        </div>
      ) : (
        <h1 className="text-2xl font-semibold tracking-tight">{t("Hallo, {name}", { name: user?.firstName ?? "" })}</h1>
      )}

      {ladefehler && (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-surface-border bg-surface px-3 py-2">
          <p className="min-w-0 text-sm text-muted-foreground [overflow-wrap:anywhere]">
            {daten === null ? t("Die Startseite konnte nicht geladen werden.") : t("Die Startseite konnte nicht aktualisiert werden.")}
          </p>
          <Button type="button" size="sm" variant="outline" className="coarse:min-h-11" onClick={erneutVersuchen}>
            {t("Erneut versuchen")}
          </Button>
        </div>
      )}

      {daten === null ? (
        // Platzhalter etwa in der Größe der Karten: Die Startseite erscheint
        // danach in einem Zug, statt Abschnitt für Abschnitt nachzurutschen und
        // die Karten darunter wegzuschieben. Nach einem Ladefehler steht oben der Hinweis.
        !ladefehler && (
          <div className="flex flex-col gap-4" aria-busy="true">
            <span className="sr-only">{t("Lädt…")}</span>
            <div className="h-28 rounded-xl bg-card motion-safe:animate-pulse" />
            <div className="h-36 rounded-xl bg-card motion-safe:animate-pulse" />
          </div>
        )
      ) : erststart ? (
        <>
          {einladungen}

          {/* Der Erststart behält seine Reihenfolge: Ziele, Karte, Termine, Erfassen. */}
          <ZielKarten eintraege={daten.ziele.karten} sachkundeAn={sachkundeAn} onEintragen={oeffneEintragen} onChanged={neuLaden} />

          {erststartKarte === "hund" ? (
            <ErsterHundKarte status={daten.onboarding} onAngelegt={ersterHundAngelegt} onDismissed={erststartWegklicken} />
          ) : (
            daten.onboarding && <OnboardingGuide status={daten.onboarding} onDismissed={erststartWegklicken} />
          )}

          <TerminKarte sessions={daten.termine} />

          {heuteGelegt}
          {erfassen}

          <FeedbackKarte eintraege={daten.feedback} onChanged={neuLaden} />
          <AbgelaufeneZiele eintraege={daten.ziele.abgelaufen} onChanged={neuLaden} />
          <ZuErledigenZeile zahlen={fuerStartseite(trainerZahlen, trainerZeileAn)} />
        </>
      ) : (
        <>
          {einladungen}

          {heuteGelegt}

          {/* Nur mit einem Termin in den nächsten 7 Tagen - viele Vereine regeln
              Termine anders, dann bleibt hier alles leer. */}
          <TerminKarte sessions={daten.termine} />

          <FeedbackKarte eintraege={daten.feedback} onChanged={neuLaden} />

          {/* Nur am Trainingstag mit etwas Gemerktem - sonst nichts Neues auf der Startseite. */}
          <MerkzettelHeuteKarte hunde={daten.hunde} stand={daten} onEintragen={oeffneEintragen} onMerken={oeffneMerken} />

          {erfassen}

          {/* Unter "Erfassen", damit der Hauptweg oben bleibt - aber vor den
              Zielen, damit ihn alle sehen, ohne zu scrollen. Nicht im Erststart:
              Dort geht es erst einmal um den ersten Hund. */}
          <PushHinweisKarte />

          <ZielKarten eintraege={daten.ziele.karten} sachkundeAn={sachkundeAn} onEintragen={oeffneEintragen} onChanged={neuLaden} />
          <PausenZeilen eintraege={pausenOhneKarte(daten.hunde, daten.ziele.karten)} />
          {gesundheitAn && <GesundheitsHinweise hunde={daten.hunde} />}
          <AbgelaufeneZiele eintraege={daten.ziele.abgelaufen} onChanged={neuLaden} />
          <OhneZielZeilen hunde={daten.ziele.ohneZiel} />

          <ZuErledigenZeile zahlen={fuerStartseite(trainerZahlen, trainerZeileAn)} />

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

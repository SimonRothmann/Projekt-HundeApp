"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/api";
import type { DashboardDaten, Dog as HundDaten, GroupTrainingSession, OnboardingStatus, Sport } from "@/lib/types";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dog, Trophy, Building2, GraduationCap } from "lucide-react";
import Link from "next/link";
import { UpcomingTrainingsSection } from "@/components/schedule/upcoming-trainings-section";
import { OnboardingGuide, zeigtErststart } from "@/components/onboarding/onboarding-guide";
import { NeuerungenHinweis } from "@/components/neuerungen-hinweis";
import { usePreferences } from "@/lib/preferences-context";
import { MODULE } from "@/lib/types";
import { laeuftFaehrte, nichtAbgelaufeneFaehrten } from "@/lib/faehrte";
import { offeneWochenziele } from "@/lib/trainingsplan";
import { ErfassenKacheln } from "@/components/dashboard/erfassen-kacheln";
import { DieseWocheSection, type WochenzielEintrag } from "@/components/dashboard/diese-woche-section";
import { HeuteGelegtSection, type FaehrteMitHund } from "@/components/dashboard/heute-gelegt-section";
import { useT } from "@/lib/i18n";

type Startdaten = {
  hunde: HundDaten[];
  faehrtenHundeIds: string[];
  wochenziele: WochenzielEintrag[];
  faehrten: FaehrteMitHund[];
  onboarding: OnboardingStatus | null;
  termine: GroupTrainingSession[];
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
  return {
    hunde: eintraege.map((e) => e.dog),
    // Dieselbe Regel wie auf der Hundeseite (laeuftFaehrte): sonst führte
    // "Fährte legen" auf eine Hundeseite ohne Recorder.
    faehrtenHundeIds: eintraege.filter((e) => laeuftFaehrte(e.sportIds, sports)).map((e) => e.dog.id),
    wochenziele: eintraege.flatMap((e) =>
      e.activeGoals.flatMap((goal) => {
        const offen = offeneWochenziele(goal, jetzt);
        return offen ? [{ hund: e.dog, goal, ...offen }] : [];
      }),
    ),
    faehrten: eintraege.flatMap((e) =>
      nichtAbgelaufeneFaehrten(e.tracksToday, jetzt).map((f) => ({ ...f, hund: e.dog })),
    ),
    onboarding,
    termine,
  };
}

export default function DashboardPage() {
  const { user } = useAuth();
  const { moduleEnabled } = usePreferences();
  const t = useT();
  const nutzerId = user?.userId ?? null;

  const [daten, setDaten] = useState<Startdaten | null>(() =>
    zwischenstand && zwischenstand.nutzerId === nutzerId ? zwischenstand.daten : null,
  );

  function uebernehmen(neu: Startdaten) {
    if (nutzerId) zwischenstand = { nutzerId, daten: neu };
    setDaten(neu);
  }

  async function neuLaden() {
    uebernehmen(await ladeStartdaten());
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

  // Vereinszugehörigkeit kommt aus dem Erststart-Status statt aus einer
  // eigenen Abfrage auf /api/clubs/my-memberships. Die kannte nur
  // Mitgliedschaften - und Vereinstrainer:innen haben keine, sie stehen in
  // einer eigenen Tabelle. Sie bekamen deshalb die Aufforderung, einem Verein
  // beizutreten, den sie leiten.
  const hasNoClub = daten?.onboarding != null && !daten.onboarding.hasClubMembership;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {t("Willkommen zurück, {name}", { name: user?.firstName ?? "" })}
        </h1>
        <p className="text-muted-foreground">{t("Hier ist dein Überblick für heute.")}</p>
      </div>

      {daten === null ? (
        // Platzhalter etwa in der Größe der Erfassen-Kacheln. Die Startseite
        // erscheint danach in einem Zug, statt Abschnitt für Abschnitt
        // nachzurutschen und die Karten darunter wegzuschieben.
        <div className="grid gap-4 sm:grid-cols-2" aria-busy="true">
          <span className="sr-only">{t("Lädt…")}</span>
          <div className="h-36 rounded-xl bg-card motion-safe:animate-pulse" />
          <div className="h-36 rounded-xl bg-card motion-safe:animate-pulse" />
        </div>
      ) : (
        <>
          <OnboardingGuide
            status={daten.onboarding}
            onDismissed={() =>
              uebernehmen({ ...daten, onboarding: daten.onboarding && { ...daten.onboarding, isDismissed: true } })
            }
          />

          {/* Solange der Erststart läuft, spricht er den Vereinsbeitritt schon an -
              eine zweite Kachel mit derselben Botschaft wäre Lärm. */}
          {hasNoClub && !zeigtErststart(daten.onboarding) && (
            <Link href="/clubs" className="group block">
              <Card className="border-primary/40 bg-primary/5 transition-all duration-150 hover:-translate-y-0.5 hover:bg-primary/10 hover:shadow-[var(--shadow-glow)]">
                <CardHeader className="flex-row items-center gap-4 space-y-0">
                  <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary-text ring-1 ring-primary/25">
                    <Building2 className="size-6" />
                  </span>
                  <div>
                    <CardTitle>{t("Tritt einem Verein bei")}</CardTitle>
                    <CardDescription>
                      {t("Du bist noch keinem Verein zugeordnet - finde einen Verein und stelle eine Beitrittsanfrage.")}
                    </CardDescription>
                  </div>
                </CardHeader>
              </Card>
            </Link>
          )}

          {/* Unter dem Erststart und über den Trainings: sichtbar, ohne das zu
              verdrängen, wofür die Seite täglich geöffnet wird. */}
          <NeuerungenHinweis erststartLaeuft={zeigtErststart(daten.onboarding)} />

          <UpcomingTrainingsSection sessions={daten.termine} />

          {/* Nur wenn relevant: eine heute gelegte, noch nicht abgelaufene Fährte
              steht über allem Übrigen - sie wartet auf ihr Alter. */}
          {faehrteAn && (
            <HeuteGelegtSection faehrten={daten.faehrten} mehrereHunde={daten.hunde.length > 1} onChanged={neuLaden} />
          )}

          {daten.hunde.length > 0 && (
            <div className="grid gap-4 sm:grid-cols-2">
              <ErfassenKacheln hunde={daten.hunde} faehrtenHundeIds={faehrteAn ? daten.faehrtenHundeIds : []} />
            </div>
          )}

          <DieseWocheSection eintraege={daten.wochenziele} mehrereHunde={daten.hunde.length > 1} onChanged={neuLaden} />

          <div className="grid gap-4 sm:grid-cols-2">
            <Link href="/dogs" className="group block">
              <Card className="h-full transition-all duration-150 hover:-translate-y-0.5 hover:shadow-[var(--shadow-glow)]">
                <CardHeader className="flex-row items-center gap-4 space-y-0">
                  <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary-text ring-1 ring-primary/20 transition-colors group-hover:bg-primary/15">
                    <Dog className="size-6" />
                  </span>
                  <div>
                    <CardTitle>{t("Meine Hunde")}</CardTitle>
                    <CardDescription>{t("Hunde verwalten und Profile pflegen")}</CardDescription>
                  </div>
                </CardHeader>
              </Card>
            </Link>

            <Link href="/sports" className="group block">
              <Card className="h-full transition-all duration-150 hover:-translate-y-0.5 hover:shadow-[var(--shadow-glow)]">
                <CardHeader className="flex-row items-center gap-4 space-y-0">
                  <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-accent/15 text-accent ring-1 ring-accent/25 transition-colors group-hover:bg-accent/20">
                    <Trophy className="size-6" />
                  </span>
                  <div>
                    <CardTitle>{t("Sportarten")}</CardTitle>
                    <CardDescription>{t("Prüfungsordnungen & Übungen entdecken")}</CardDescription>
                  </div>
                </CardHeader>
              </Card>
            </Link>

            {moduleEnabled(MODULE.sachkunde) && (
              <Link href="/sachkunde" className="group block sm:col-span-2">
                <Card className="h-full transition-all duration-150 hover:-translate-y-0.5 hover:shadow-[var(--shadow-glow)]">
                  <CardHeader className="flex-row items-center gap-4 space-y-0">
                    <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary-text ring-1 ring-primary/20 transition-colors group-hover:bg-primary/15">
                      <GraduationCap className="size-6" />
                    </span>
                    <div>
                      <CardTitle>{t("Sachkunde üben")}</CardTitle>
                      <CardDescription>{t("Die Theoriefragen zur Begleithundeprüfung, mit Wiedervorlage")}</CardDescription>
                    </div>
                  </CardHeader>
                </Card>
              </Link>
            )}
          </div>
        </>
      )}
    </div>
  );
}

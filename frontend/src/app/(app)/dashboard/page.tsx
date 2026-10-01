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
import { offeneWochenziele, wochenFortschritt } from "@/lib/trainingsplan";
import { naechstesZiel } from "@/lib/pruefung";
import { ErfassenKacheln } from "@/components/dashboard/erfassen-kacheln";
import { DieseWocheSection, type WochenzielEintrag } from "@/components/dashboard/diese-woche-section";
import { KeinZielKarte, ZielKartenSection, type ZielKarteEintrag } from "@/components/dashboard/ziel-karten";
import { HeuteGelegtSection, type FaehrteMitHund } from "@/components/dashboard/heute-gelegt-section";
import { useT } from "@/lib/i18n";

const LINK_ZEILE =
  "inline-flex min-h-11 items-center gap-2 rounded-lg border border-surface-border bg-surface px-3 text-sm font-medium transition-colors hover:border-primary/40";

type Startdaten = {
  hunde: HundDaten[];
  faehrtenHundeIds: string[];
  wochenziele: WochenzielEintrag[];
  zielkarten: ZielKarteEintrag[];
  hundeOhneZiel: HundDaten[];
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
  // Je Hund das Ziel mit dem nächsten Datum; ohne Ziel kommt er in die eine
  // gemeinsame "Noch kein Prüfungsziel"-Karte. Archivierte Hunde zählen nicht
  // (das Backend liefert sie ohnehin nicht, die Regel gilt hier trotzdem).
  const naechste = eintraege
    .filter((e) => !e.dog.archivedAt)
    .map((e) => ({ hund: e.dog, goal: naechstesZiel(e.activeGoals) }));
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
    zielkarten: naechste.flatMap(({ hund, goal }) =>
      goal ? [{ hund, goal, fortschritt: wochenFortschritt(goal, jetzt) }] : [],
    ),
    hundeOhneZiel: naechste.filter(({ goal }) => goal === null).map(({ hund }) => hund),
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
          {/* Direkt unter der Begrüßung: Wann ist die Prüfung, wie weit ist die
              Woche? Ohne Ziel eine einzige Aufforderung - nicht, solange der
              Erststart mit seinem "Ziel setzen" noch da ist. */}
          <ZielKartenSection eintraege={daten.zielkarten} />
          {!zeigtErststart(daten.onboarding) && <KeinZielKarte hunde={daten.hundeOhneZiel} />}

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
                <CardHeader className="flex flex-row items-center gap-4 space-y-0">
                  <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary-text ring-1 ring-primary/25">
                    <Building2 className="size-6" />
                  </span>
                  <div className="min-w-0">
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

          {/* Früher drei große Karten; am Telefon sind es aber der Zugang zu
              Sportarten (siehe nav-items.ts), deshalb bleiben die Ziele - nur
              schmaler. */}
          <nav aria-label={t("Weitere Bereiche")} className="flex flex-wrap gap-2">
            <Link href="/dogs" className={LINK_ZEILE}>
              <Dog className="size-4" />
              {t("Meine Hunde")}
            </Link>
            <Link href="/sports" className={LINK_ZEILE}>
              <Trophy className="size-4" />
              {t("Sportarten")}
            </Link>
            {moduleEnabled(MODULE.sachkunde) && (
              <Link href="/sachkunde" className={LINK_ZEILE}>
                <GraduationCap className="size-4" />
                {t("Sachkunde üben")}
              </Link>
            )}
          </nav>
        </>
      )}
    </div>
  );
}

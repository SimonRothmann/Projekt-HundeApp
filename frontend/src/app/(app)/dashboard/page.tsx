"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/api";
import type { OnboardingStatus } from "@/lib/types";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dog, Trophy, Building2, GraduationCap } from "lucide-react";
import Link from "next/link";
import { UpcomingTrainingsSection } from "@/components/schedule/upcoming-trainings-section";
import { OnboardingGuide, zeigtErststart } from "@/components/onboarding/onboarding-guide";
import { NeuerungenHinweis } from "@/components/neuerungen-hinweis";
import { usePreferences } from "@/lib/preferences-context";
import { MODULE } from "@/lib/types";

export default function DashboardPage() {
  const { user } = useAuth();
  const [onboarding, setOnboarding] = useState<OnboardingStatus | null>(null);
  const { moduleEnabled } = usePreferences();

  // Vereinszugehörigkeit kommt aus dem Erststart-Status statt aus einer
  // eigenen Abfrage auf /api/clubs/my-memberships. Die kannte nur
  // Mitgliedschaften - und Vereinstrainer:innen haben keine, sie stehen in
  // einer eigenen Tabelle. Sie bekamen deshalb die Aufforderung, einem Verein
  // beizutreten, den sie leiten.
  const hasNoClub = onboarding !== null && !onboarding.hasClubMembership;

  useEffect(() => {
    let cancelled = false;
    api
      .get<OnboardingStatus>("/api/onboarding/status")
      .then((status) => {
        if (!cancelled) setOnboarding(status);
      })
      .catch(() => {
        // Wie oben: der Erststart ist eine Hilfe, kein Kernstück. Fällt er
        // aus, steht das Dashboard trotzdem.
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Willkommen zurück, {user?.firstName}
        </h1>
        <p className="text-muted-foreground">Hier ist dein Überblick für heute.</p>
      </div>

      <OnboardingGuide
        status={onboarding}
        onDismissed={() => setOnboarding((s) => (s ? { ...s, isDismissed: true } : s))}
      />

      {/* Solange der Erststart läuft, spricht er den Vereinsbeitritt schon an -
          eine zweite Kachel mit derselben Botschaft wäre Lärm. */}
      {hasNoClub && !zeigtErststart(onboarding) && (
        <Link href="/clubs" className="group block">
          <Card className="border-primary/40 bg-primary/5 transition-all duration-150 hover:-translate-y-0.5 hover:bg-primary/10 hover:shadow-[var(--shadow-glow)]">
            <CardHeader className="flex-row items-center gap-4 space-y-0">
              <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary ring-1 ring-primary/25">
                <Building2 className="size-6" />
              </span>
              <div>
                <CardTitle>Tritt einem Verein bei</CardTitle>
                <CardDescription>
                  Du bist noch keinem Verein zugeordnet - finde einen Verein und stelle eine Beitrittsanfrage.
                </CardDescription>
              </div>
            </CardHeader>
          </Card>
        </Link>
      )}

      {/* Unter dem Erststart und über den Trainings: sichtbar, ohne das zu
          verdrängen, wofür die Seite täglich geöffnet wird. */}
      <NeuerungenHinweis erststartLaeuft={zeigtErststart(onboarding)} />

      <UpcomingTrainingsSection />

      <div className="grid gap-4 sm:grid-cols-2">
        <Link href="/dogs" className="group block">
          <Card className="h-full transition-all duration-150 hover:-translate-y-0.5 hover:shadow-[var(--shadow-glow)]">
            <CardHeader className="flex-row items-center gap-4 space-y-0">
              <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary ring-1 ring-primary/20 transition-colors group-hover:bg-primary/15">
                <Dog className="size-6" />
              </span>
              <div>
                <CardTitle>Meine Hunde</CardTitle>
                <CardDescription>Hunde verwalten und Profile pflegen</CardDescription>
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
                <CardTitle>Sportarten</CardTitle>
                <CardDescription>Prüfungsordnungen & Übungen entdecken</CardDescription>
              </div>
            </CardHeader>
          </Card>
        </Link>

{moduleEnabled(MODULE.sachkunde) && (
                <Link href="/sachkunde" className="group block sm:col-span-2">
          <Card className="h-full transition-all duration-150 hover:-translate-y-0.5 hover:shadow-[var(--shadow-glow)]">
            <CardHeader className="flex-row items-center gap-4 space-y-0">
              <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary ring-1 ring-primary/20 transition-colors group-hover:bg-primary/15">
                <GraduationCap className="size-6" />
              </span>
              <div>
                <CardTitle>Sachkunde üben</CardTitle>
                <CardDescription>Die Theoriefragen zur Begleithundeprüfung, mit Wiedervorlage</CardDescription>
              </div>
            </CardHeader>
          </Card>
        </Link>
        )}
      </div>
    </div>
  );
}

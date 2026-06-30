"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/api";
import type { ClubMembership } from "@/lib/types";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dog, Trophy, Building2 } from "lucide-react";
import Link from "next/link";

export default function DashboardPage() {
  const { user } = useAuth();
  const [hasNoClub, setHasNoClub] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api
      .get<ClubMembership[]>("/api/clubs/my-memberships")
      .then((memberships) => {
        if (!cancelled) setHasNoClub(!memberships.some((m) => m.status === 1));
      })
      .catch(() => {
        // Stiller Fehlschlag - der Hinweis ist nur eine Komforterinnerung,
        // kein kritischer Dashboard-Bestandteil.
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

      {hasNoClub && (
        <Link href="/clubs">
          <Card className="border-primary/40 bg-primary/5 transition-colors hover:bg-primary/10">
            <CardHeader className="flex-row items-center gap-3 space-y-0">
              <Building2 className="size-8 text-primary" />
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

      <div className="grid gap-4 sm:grid-cols-2">
        <Link href="/dogs">
          <Card className="transition-colors hover:bg-accent/10">
            <CardHeader className="flex-row items-center gap-3 space-y-0">
              <Dog className="size-8 text-primary" />
              <div>
                <CardTitle>Meine Hunde</CardTitle>
                <CardDescription>Hunde verwalten und Profile pflegen</CardDescription>
              </div>
            </CardHeader>
          </Card>
        </Link>

        <Link href="/sports">
          <Card className="transition-colors hover:bg-accent/10">
            <CardHeader className="flex-row items-center gap-3 space-y-0">
              <Trophy className="size-8 text-primary" />
              <div>
                <CardTitle>Sportarten</CardTitle>
                <CardDescription>Prüfungsordnungen & Übungen entdecken</CardDescription>
              </div>
            </CardHeader>
          </Card>
        </Link>
      </div>
    </div>
  );
}

"use client";

import { useAuth } from "@/lib/auth-context";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dog, Trophy } from "lucide-react";
import Link from "next/link";

export default function DashboardPage() {
  const { user } = useAuth();

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Willkommen zurück, {user?.firstName}
        </h1>
        <p className="text-muted-foreground">Hier ist dein Überblick für heute.</p>
      </div>

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

      <Card>
        <CardContent className="py-6">
          <p className="text-sm text-muted-foreground">
            Trainingstagebuch, Zielplanung und Fährtenaufzeichnung folgen in den nächsten
            Entwicklungsschritten (siehe ROADMAP.md Phase 1).
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

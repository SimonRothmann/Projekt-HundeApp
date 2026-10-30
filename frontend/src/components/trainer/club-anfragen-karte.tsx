"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { UserCheck } from "lucide-react";
import { api } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Club, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * Einstieg zu den Beitrittsanfragen dieses Vereins auf der Vereinsseite - für
 * alle, die Mitglieder verwalten (Mitgliederverwaltung, Verwaltung), auch ohne
 * Trainer-Rolle: Für sie gibt es sonst keine Trainer-Übersicht, über die sie
 * zu /trainer/anfragen fänden. Die Anfragen selbst entscheidet jene Seite.
 *
 * Ohne offene Anfragen steht ein Hinweis statt nichts, damit klar ist, wo sie
 * erscheinen. Scheitert das Zählen, bleibt der Link: Die Seite dahinter lädt
 * selbst und meldet ihren Fehler.
 */
export function ClubAnfragenKarte({ club }: { club: Club }) {
  const t = useT();
  // undefined = lädt, null = Zählen gescheitert.
  const [anzahl, setAnzahl] = useState<number | null | undefined>(undefined);

  useEffect(() => {
    let abgebrochen = false;
    api
      .get<ClubMemberRequest[]>(`/api/clubs/${club.id}/join-requests`)
      .then((liste) => {
        if (!abgebrochen) setAnzahl(liste.length);
      })
      .catch(() => {
        if (!abgebrochen) setAnzahl(null);
      });
    return () => {
      abgebrochen = true;
    };
  }, [club.id]);

  // Zurück von der Anfragen-Seite führt hierher, nicht auf die Trainer-Übersicht.
  const href = `/trainer/anfragen?from=${encodeURIComponent(`/trainer/verein?verein=${club.id}`)}`;

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserCheck className="size-5 shrink-0" />
          {t("Beitrittsanfragen")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {anzahl === undefined && <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>}
        {anzahl === 0 && (
          <p className="text-sm text-muted-foreground">
            {t("Keine offenen Beitrittsanfragen. Neue erscheinen hier und auf der Startseite unter „Zu erledigen“.")}
          </p>
        )}
        {(anzahl === null || (anzahl !== undefined && anzahl > 0)) && (
          <Link href={href} className={cn(buttonVariants({ variant: "outline" }), "h-11 self-start")}>
            {anzahl === null
              ? t("Beitrittsanfragen öffnen")
              : anzahl === 1
                ? t("1 offene Beitrittsanfrage ansehen")
                : t("{n} offene Beitrittsanfragen ansehen", { n: anzahl })}
          </Link>
        )}
      </CardContent>
    </Card>
  );
}

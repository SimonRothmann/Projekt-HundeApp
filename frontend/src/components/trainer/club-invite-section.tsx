"use client";

import { UserPlus } from "lucide-react";
import { einladungsUrl } from "@/lib/einladung";
import { useT } from "@/lib/i18n";
import type { Club } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { LinkVerwaltung } from "@/components/trainer/link-verwaltung";

/**
 * Einladungslink und QR-Code des Vereins, den man als Trainer:in betreut.
 *
 * Die Karte steht unabhängig davon, ob Beitrittsanfragen offen sind
 * (anders als ClubJoinRequestsSection, die sich dann versteckt): Den Link
 * braucht man gerade, wenn noch niemand angefragt hat.
 */
export function ClubInviteSection({ club }: { club: Club }) {
  const t = useT();

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserPlus className="size-5 shrink-0" />
          {t("Neue Mitglieder einladen")}
        </CardTitle>
        <p className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">{club.name}</p>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          {t("Wer den Code scannt oder den Link öffnet, landet direkt bei der Beitrittsanfrage. Du gibst weiterhin jede Anfrage frei.")}
        </p>

        <LinkVerwaltung
          pfad={`/api/clubs/${club.id}/invite-link`}
          linkBauen={einladungsUrl}
          aushangPfad={`/trainer/aushang/${club.id}`}
          texte={{
            erstellen: t("Einladungslink erstellen"),
            ladenFehler: t("Einladungslink konnte nicht geladen werden."),
            erstellenFehler: t("Einladungslink konnte nicht erstellt werden."),
            abschaltenFrage: t("Link abschalten? Der QR-Code und der Link funktionieren danach nicht mehr."),
            abschalten: t("Link abschalten"),
            abschaltenErfolg: t("Einladungslink abgeschaltet."),
            abschaltenFehler: t("Einladungslink konnte nicht abgeschaltet werden."),
            qrBeschreibung: t("QR-Code mit dem Einladungslink zu {verein}", { verein: club.name }),
            teilenTitel: t("Einladung zu {verein}", { verein: club.name }),
            teilenText: t("Komm zu {verein} in Dogity.", { verein: club.name }),
          }}
        />
      </CardContent>
    </Card>
  );
}

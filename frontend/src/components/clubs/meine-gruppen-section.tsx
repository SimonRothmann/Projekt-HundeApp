"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { MyGroupMembership } from "@/lib/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Mail, Users } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";

/**
 * Die eigenen Trainingsgruppen - mit offenen Einladungen obenauf.
 *
 * Mitglied einer Gruppe wird nur, wer selbst zustimmt: Eine Trainer:in, die
 * jemanden per E-Mail hinzufügt, lädt ein; erst das Annehmen hier macht
 * daraus eine Mitgliedschaft. Das ist mehr als Höflichkeit - wer Mitglied
 * ist, dessen Hunde kann die Trainer:in betreuen und sieht dann Tagebuch,
 * Ziele und Fährten mit Standorten.
 *
 * Aus demselben Grund steht hier "Verlassen": Vorher gab es keinen Weg aus
 * einer vereinsfreien Gruppe heraus, und eine Betreuung lief weiter, solange
 * die Trainer:in sie nicht selbst beendete.
 */
export function MeineGruppenSection({ onChanged }: { onChanged?: () => void }) {
  const t = useT();
  const [eintraege, setEintraege] = useState<MyGroupMembership[] | null>(null);
  const [laeuft, setLaeuft] = useState<string | null>(null);

  async function laden() {
    try {
      setEintraege(await api.get<MyGroupMembership[]>("/api/groups/memberships"));
    } catch {
      // Ohne die Liste bleibt die Seite nutzbar; Einladungen kommen zusätzlich
      // als Benachrichtigung.
      setEintraege([]);
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
  }, []);

  async function ausfuehren(groupId: string, aufruf: () => Promise<unknown>, erfolg: string) {
    setLaeuft(groupId);
    try {
      await aufruf();
      toast.success(erfolg);
      await laden();
      onChanged?.();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setLaeuft(null);
    }
  }

  function annehmen(g: MyGroupMembership) {
    return ausfuehren(
      g.groupId,
      () => api.post(`/api/groups/${g.groupId}/invitation/accept`),
      t("Du bist jetzt Mitglied von {gruppe}.", { gruppe: g.groupName }),
    );
  }

  function ablehnen(g: MyGroupMembership) {
    return ausfuehren(g.groupId, () => api.post(`/api/groups/${g.groupId}/invitation/decline`), t("Einladung abgelehnt."));
  }

  function verlassen(g: MyGroupMembership) {
    if (
      !window.confirm(
        t("{gruppe} verlassen? Die Trainer:innen dieser Gruppe betreuen deine Hunde danach nicht mehr.", {
          gruppe: g.groupName,
        }),
      )
    )
      return;
    return ausfuehren(g.groupId, () => api.delete(`/api/groups/${g.groupId}/membership`), t("Gruppe verlassen."));
  }

  if (!eintraege || eintraege.length === 0) return null;

  const einladungen = eintraege.filter((e) => e.isInvitation);
  const gruppen = eintraege.filter((e) => !e.isInvitation);

  function herkunft(g: MyGroupMembership) {
    const teile = [g.clubName, g.trainerName ? t("bei {name}", { name: g.trainerName }) : null].filter(Boolean);
    return teile.length > 0 ? (
      <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{teile.join(" · ")}</p>
    ) : null;
  }

  return (
    <div className="flex flex-col gap-4">
      {einladungen.length > 0 && (
        <Card className="border-primary/40">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Mail className="size-5 shrink-0 text-primary-text" />
              {einladungen.length === 1 ? t("Einladung in eine Gruppe") : t("Einladungen in Gruppen")}
            </CardTitle>
            <CardDescription>
              {t(
                "Nimmst du an, können die Trainer:innen der Gruppe deine Hunde betreuen - sie sehen dann Tagebuch, Ziele und Fährten.",
              )}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {einladungen.map((g) => (
              <div key={g.groupId} className="flex flex-col gap-2 border-t pt-3 first:border-t-0 first:pt-0">
                <div className="min-w-0">
                  <p className="font-medium [overflow-wrap:anywhere]">{g.groupName}</p>
                  {herkunft(g)}
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" disabled={laeuft === g.groupId} onClick={() => annehmen(g)}>
                    {t("Annehmen")}
                  </Button>
                  <Button size="sm" variant="outline" disabled={laeuft === g.groupId} onClick={() => ablehnen(g)}>
                    {t("Ablehnen")}
                  </Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {gruppen.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Users className="size-5 shrink-0 text-primary-text" />
              {t("Deine Gruppen")}
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {gruppen.map((g) => (
              <div
                key={g.groupId}
                className="flex flex-wrap items-center justify-between gap-2 border-t pt-3 first:border-t-0 first:pt-0"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-medium [overflow-wrap:anywhere]">{g.groupName}</p>
                    <Badge variant="secondary" className="shrink-0">
                      {t("Mitglied")}
                    </Badge>
                  </div>
                  {herkunft(g)}
                </div>
                <Button size="sm" variant="ghost" disabled={laeuft === g.groupId} onClick={() => verlassen(g)}>
                  {laeuft === g.groupId ? t("Wird verlassen…") : t("Verlassen")}
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

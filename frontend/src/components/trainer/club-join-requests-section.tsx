"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { UserCheck, Check, X } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";

/**
 * Offene Beitrittsanfragen an die eigenen Vereine.
 *
 * Steht oben auf der Trainerseite und zeigt sich deshalb nur, solange es
 * offene Anfragen gibt - ein Kasten "Keine offenen Anfragen" ganz oben wäre
 * Lärm. Dafür werden die Anfragen ALLER Vereine geladen, nicht nur die des
 * gewählten: Sonst verschwände der Kasten samt Vereinsauswahl, sobald der
 * erste Verein keine Anfragen hat, und die eines zweiten Vereins wären
 * unerreichbar.
 */
export function ClubJoinRequestsSection({ clubs }: { clubs: Club[] }) {
  const t = useT();
  const [anfragen, setAnfragen] = useState<Record<string, ClubMemberRequest[]> | null>(null);
  const [selectedClubId, setSelectedClubId] = useState("");
  const vereineSchluessel = clubs.map((c) => c.id).join(",");

  async function laden() {
    const ids = vereineSchluessel.split(",").filter(Boolean);
    const ergebnisse = await Promise.all(
      ids.map(async (id) => {
        try {
          return [id, await api.get<ClubMemberRequest[]>(`/api/clubs/${id}/join-requests`)] as const;
        } catch (err) {
          toast.error(err instanceof ApiError ? err.message : t("Beitrittsanfragen konnten nicht geladen werden."));
          return [id, [] as ClubMemberRequest[]] as const;
        }
      }),
    );
    const nachVerein = Object.fromEntries(ergebnisse);
    setAnfragen(nachVerein);
    // Beim gewählten Verein bleiben, solange er noch Anfragen hat; sonst zum
    // nächsten mit offenen Anfragen wechseln.
    setSelectedClubId((aktuell) =>
      aktuell && nachVerein[aktuell]?.length ? aktuell : (ids.find((id) => nachVerein[id].length > 0) ?? ids[0] ?? ""),
    );
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vereineSchluessel]);

  async function handleDecide(membershipId: string, approve: boolean) {
    try {
      await api.post(`/api/clubs/${selectedClubId}/join-requests/${membershipId}/${approve ? "approve" : "reject"}`);
      toast.success(approve ? "Beitritt angenommen." : "Beitritt abgelehnt.");
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Aktion fehlgeschlagen.");
    }
  }

  if (!anfragen) return null;
  const offen = Object.values(anfragen).reduce((summe, liste) => summe + liste.length, 0);
  if (offen === 0) return null;
  const requests = anfragen[selectedClubId] ?? [];

  return (
    <Card>
      <CardHeader className="flex-row flex-wrap items-center justify-between gap-2 space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserCheck className="size-5" />
          Beitrittsanfragen
          <Badge variant="secondary">{t("{anzahl} offen", { anzahl: offen })}</Badge>
        </CardTitle>
        {clubs.length > 1 && (
          <Select value={selectedClubId} onValueChange={(value) => setSelectedClubId(value ?? "")}>
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {clubs.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </CardHeader>
      <CardContent>
        {requests.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Keine offenen Anfragen.")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {requests.map((r) => (
              <li key={r.membershipId} className="flex items-center justify-between gap-2 rounded-md border px-3 py-2">
                <span className="min-w-0 text-sm [overflow-wrap:anywhere]">
                  {r.firstName} {r.lastName} ({r.email})
                </span>
                <div className="flex shrink-0 gap-2">
                  <Button size="icon-sm" variant="outline" onClick={() => handleDecide(r.membershipId, true)} title="Annehmen">
                    <Check className="size-4" />
                  </Button>
                  <Button size="icon-sm" variant="ghost" onClick={() => handleDecide(r.membershipId, false)} title="Ablehnen">
                    <X className="size-4" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

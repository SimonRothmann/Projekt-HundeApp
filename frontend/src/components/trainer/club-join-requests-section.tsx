"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { UserCheck, Check, X } from "lucide-react";
import { toast } from "sonner";

export function ClubJoinRequestsSection({ clubs }: { clubs: Club[] }) {
  const [selectedClubId, setSelectedClubId] = useState(clubs[0]?.id ?? "");
  const [requests, setRequests] = useState<ClubMemberRequest[] | null>(null);

  async function loadRequests(clubId: string) {
    if (!clubId) {
      setRequests([]);
      return;
    }
    try {
      const data = await api.get<ClubMemberRequest[]>(`/api/clubs/${clubId}/join-requests`);
      setRequests(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Beitrittsanfragen konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadRequests(selectedClubId);
  }, [selectedClubId]);

  async function handleDecide(membershipId: string, approve: boolean) {
    try {
      await api.post(`/api/clubs/${selectedClubId}/join-requests/${membershipId}/${approve ? "approve" : "reject"}`);
      toast.success(approve ? "Beitritt angenommen." : "Beitritt abgelehnt.");
      await loadRequests(selectedClubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Aktion fehlgeschlagen.");
    }
  }

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserCheck className="size-5" />
          Beitrittsanfragen
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
        {requests === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : requests.length === 0 ? (
          <p className="text-sm text-muted-foreground">Keine offenen Anfragen.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {requests.map((r) => (
              <li key={r.membershipId} className="flex items-center justify-between rounded-md border px-3 py-2">
                <span className="text-sm">
                  {r.firstName} {r.lastName} ({r.email})
                </span>
                <div className="flex gap-2">
                  <Button size="icon-sm" variant="outline" onClick={() => handleDecide(r.membershipId, true)}>
                    <Check className="size-4" />
                  </Button>
                  <Button size="icon-sm" variant="ghost" onClick={() => handleDecide(r.membershipId, false)}>
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

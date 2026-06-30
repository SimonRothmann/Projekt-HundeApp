"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Group, GroupJoinRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { UserCheck } from "lucide-react";
import { toast } from "sonner";

type Props = {
  groups: Group[];
};

type RequestWithGroup = GroupJoinRequest & { groupId: string; groupName: string };

export function GroupJoinRequestsSection({ groups }: Props) {
  const [requests, setRequests] = useState<RequestWithGroup[] | null>(null);
  const [decidingId, setDecidingId] = useState<string | null>(null);

  async function loadAll() {
    const all = await Promise.all(
      groups.map((g) =>
        api
          .get<GroupJoinRequest[]>(`/api/groups/${g.id}/join-requests`)
          .then((reqs) => reqs.map((r) => ({ ...r, groupId: g.id, groupName: g.name })))
          .catch(() => [] as RequestWithGroup[]),
      ),
    );
    setRequests(all.flat());
  }

  useEffect(() => {
    if (groups.length === 0) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groups]);

  async function decide(groupId: string, memberId: string, approve: boolean) {
    setDecidingId(memberId);
    try {
      const action = approve ? "approve" : "reject";
      await api.post(`/api/groups/${groupId}/join-requests/${memberId}/${action}`);
      toast.success(approve ? "Aufgenommen." : "Abgelehnt.");
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Fehler beim Entscheiden.");
    } finally {
      setDecidingId(null);
    }
  }

  if (!requests || requests.length === 0) return null;

  return (
    <Card>
      <CardHeader className="flex-row items-center gap-2 space-y-0">
        <UserCheck className="size-5 text-primary" />
        <CardTitle className="text-base">Offene Gruppenanfragen</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {requests.map((r) => (
          <div key={`${r.groupId}-${r.memberId}`} className="flex items-center justify-between gap-2 py-1 border-b last:border-0">
            <div className="text-sm">
              <p className="font-medium">
                {r.firstName} {r.lastName}
              </p>
              <p className="text-muted-foreground text-xs">
                {r.email} · Gruppe: {r.groupName}
              </p>
            </div>
            <div className="flex gap-2 shrink-0">
              <Button
                size="sm"
                disabled={decidingId === r.memberId}
                onClick={() => decide(r.groupId, r.memberId, true)}
              >
                Annehmen
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={decidingId === r.memberId}
                onClick={() => decide(r.groupId, r.memberId, false)}
              >
                Ablehnen
              </Button>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

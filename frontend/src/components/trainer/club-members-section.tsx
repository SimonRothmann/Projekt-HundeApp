"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Users, ShieldPlus } from "lucide-react";
import { toast } from "sonner";

export function ClubMembersSection({ clubs }: { clubs: Club[] }) {
  const [selectedClubId, setSelectedClubId] = useState(clubs[0]?.id ?? "");
  const [members, setMembers] = useState<ClubMemberRequest[] | null>(null);
  const [promotingUserId, setPromotingUserId] = useState<string | null>(null);

  async function loadMembers(clubId: string) {
    if (!clubId) {
      setMembers([]);
      return;
    }
    try {
      const data = await api.get<ClubMemberRequest[]>(`/api/clubs/${clubId}/members`);
      setMembers(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Mitglieder konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadMembers(selectedClubId);
  }, [selectedClubId]);

  async function handlePromote(userId: string) {
    setPromotingUserId(userId);
    try {
      await api.post(`/api/clubs/${selectedClubId}/members/${userId}/promote`);
      toast.success("Mitglied ist jetzt Trainer.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Beförderung fehlgeschlagen.");
    } finally {
      setPromotingUserId(null);
    }
  }

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <Users className="size-5" />
          Mitglieder
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
        {members === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : members.length === 0 ? (
          <p className="text-sm text-muted-foreground">Noch keine Mitglieder.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {members.map((m) => (
              <li key={m.userId} className="flex items-center justify-between rounded-md border px-3 py-2">
                <span className="text-sm">
                  {m.firstName} {m.lastName} ({m.email})
                </span>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={promotingUserId === m.userId}
                  onClick={() => handlePromote(m.userId)}
                >
                  <ShieldPlus className="size-4" />
                  Zum Trainer machen
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

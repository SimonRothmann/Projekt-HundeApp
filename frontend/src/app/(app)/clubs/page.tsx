"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { ClubSummary, ClubMembership, Group } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Building2, Users } from "lucide-react";
import { toast } from "sonner";

function membershipFor(memberships: ClubMembership[], clubId: string): ClubMembership | undefined {
  // Bei mehrfachen Anfragen (z.B. nach Ablehnung erneut angefragt) zählt
  // die jüngste Zeile.
  return memberships
    .filter((m) => m.clubId === clubId)
    .sort((a, b) => b.requestedAt.localeCompare(a.requestedAt))[0];
}

export default function ClubsPage() {
  const [clubs, setClubs] = useState<ClubSummary[] | null>(null);
  const [memberships, setMemberships] = useState<ClubMembership[]>([]);
  const [groupsByClub, setGroupsByClub] = useState<Record<string, Group[]>>({});
  const [joiningClubId, setJoiningClubId] = useState<string | null>(null);
  const [leavingClubId, setLeavingClubId] = useState<string | null>(null);
  const [joiningGroupId, setJoiningGroupId] = useState<string | null>(null);

  async function loadGroups(approvedClubIds: string[]) {
    const entries = await Promise.all(
      approvedClubIds.map((id) =>
        api
          .get<Group[]>(`/api/clubs/${id}/groups`)
          .then((gs) => [id, gs] as [string, Group[]])
          .catch(() => [id, []] as [string, Group[]]),
      ),
    );
    setGroupsByClub(Object.fromEntries(entries));
  }

  async function loadAll() {
    try {
      const [clubsData, membershipsData] = await Promise.all([
        api.get<ClubSummary[]>("/api/clubs"),
        api.get<ClubMembership[]>("/api/clubs/my-memberships"),
      ]);
      setClubs(clubsData);
      setMemberships(membershipsData);

      const approvedIds = membershipsData.filter((m) => m.status === 1).map((m) => m.clubId);
      if (approvedIds.length > 0) await loadGroups(approvedIds);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Vereine konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleJoin(clubId: string) {
    setJoiningClubId(clubId);
    try {
      await api.post(`/api/clubs/${clubId}/join-requests`);
      toast.success("Beitrittsanfrage gesendet.");
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Anfrage fehlgeschlagen.");
    } finally {
      setJoiningClubId(null);
    }
  }

  async function handleLeave(clubId: string, clubName: string) {
    if (!window.confirm(`"${clubName}" wirklich verlassen?`)) return;
    setLeavingClubId(clubId);
    try {
      await api.delete(`/api/clubs/${clubId}/membership`);
      toast.success("Verein verlassen.");
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Verlassen fehlgeschlagen.");
    } finally {
      setLeavingClubId(null);
    }
  }

  async function handleJoinGroup(groupId: string) {
    setJoiningGroupId(groupId);
    try {
      await api.post(`/api/groups/${groupId}/join-requests`);
      toast.success("Gruppenanfrage gesendet.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Gruppenanfrage fehlgeschlagen.");
    } finally {
      setJoiningGroupId(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Vereine</h1>
        <p className="text-muted-foreground">
          Tritt einem Verein bei - ein Trainer des Vereins gibt deine Anfrage frei.
        </p>
      </div>

      {clubs === null ? (
        <p className="text-sm text-muted-foreground">Lädt…</p>
      ) : clubs.length === 0 ? (
        <p className="text-sm text-muted-foreground">Noch keine Vereine vorhanden.</p>
      ) : (
        <div className="flex flex-col gap-4">
          {clubs.map((club) => {
            const membership = membershipFor(memberships, club.id);
            const isApproved = membership?.status === 1;
            const groups = groupsByClub[club.id] ?? [];
            return (
              <Card key={club.id}>
                <CardHeader className="flex-row items-center gap-3 space-y-0">
                  <Building2 className="size-8 text-primary" />
                  <div>
                    <CardTitle>{club.name}</CardTitle>
                    {club.description && <p className="text-sm text-muted-foreground">{club.description}</p>}
                  </div>
                </CardHeader>
                <CardContent className="flex flex-col gap-4">
                  {isApproved ? (
                    <div className="flex items-center gap-2">
                      <Badge>Mitglied</Badge>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={leavingClubId === club.id}
                        onClick={() => handleLeave(club.id, club.name)}
                      >
                        {leavingClubId === club.id ? "Wird verlassen…" : "Verein verlassen"}
                      </Button>
                    </div>
                  ) : membership?.status === 0 ? (
                    <Badge variant="secondary">Anfrage ausstehend</Badge>
                  ) : (
                    <Button size="sm" disabled={joiningClubId === club.id} onClick={() => handleJoin(club.id)}>
                      {joiningClubId === club.id ? "Wird gesendet…" : "Beitreten"}
                    </Button>
                  )}

                  {isApproved && groups.length > 0 && (
                    <div className="flex flex-col gap-2 border-t pt-3">
                      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Gruppen</p>
                      {groups.map((g) => (
                        <div key={g.id} className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2 text-sm">
                            <Users className="size-4 text-muted-foreground" />
                            <span>{g.name}</span>
                            <Badge variant="outline" className="text-xs">
                              {g.memberCount} Mitglieder
                            </Badge>
                          </div>
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={joiningGroupId === g.id}
                            onClick={() => handleJoinGroup(g.id)}
                          >
                            {joiningGroupId === g.id ? "Wird gesendet…" : "Beitreten"}
                          </Button>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

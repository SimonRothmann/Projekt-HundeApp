"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Users, ShieldPlus, UserPlus } from "lucide-react";
import { toast } from "sonner";

export function ClubMembersSection({ clubs }: { clubs: Club[] }) {
  const [selectedClubId, setSelectedClubId] = useState(clubs[0]?.id ?? "");
  const [members, setMembers] = useState<ClubMemberRequest[] | null>(null);
  const [promotingUserId, setPromotingUserId] = useState<string | null>(null);
  const [neueMail, setNeueMail] = useState("");
  const [nimmtAuf, setNimmtAuf] = useState(false);

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

  /**
   * Jemanden direkt aufnehmen, ohne dass er selbst eine Anfrage stellt.
   *
   * Der Antragsweg deckt den Normalfall ab, aber nicht den häufigsten Anlass:
   * Auf dem Platz steht jemand vor einem, der eben Mitglied geworden ist -
   * ihn erst nach Hause zu schicken, um eine Anfrage zu stellen, ist umständlich.
   */
  async function aufnehmen() {
    const mail = neueMail.trim();
    if (!mail) return;
    setNimmtAuf(true);
    try {
      await api.post(`/api/clubs/${selectedClubId}/members`, { email: mail });
      toast.success(`${mail} ist jetzt Mitglied.`);
      setNeueMail("");
      await loadMembers(selectedClubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Aufnahme fehlgeschlagen.");
    } finally {
      setNimmtAuf(false);
    }
  }

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
      <CardContent className="flex flex-col gap-3">
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            void aufnehmen();
          }}
        >
          <Input
            type="email"
            className="min-w-0 flex-1"
            placeholder="E-Mail-Adresse aufnehmen"
            value={neueMail}
            onChange={(e) => setNeueMail(e.target.value)}
            disabled={!selectedClubId || nimmtAuf}
          />
          <Button type="submit" size="sm" disabled={!neueMail.trim() || nimmtAuf}>
            <UserPlus className="size-4" />
            Aufnehmen
          </Button>
        </form>

        {members === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : members.length === 0 ? (
          <p className="text-sm text-muted-foreground">Noch keine Mitglieder.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {members.map((m) => (
              // flex-wrap + min-w-0: Name und E-Mail zusammen sind auf 375 px
              // breiter als die Karte, und weder Text noch Knopf konnten
              // schrumpfen (Flex-Kinder haben von Haus aus min-width:auto).
              // Gemessen ragte "Zum Trainer machen" 4-7 px über den Rand und
              // war angeschnitten. Jetzt rutscht der Knopf bei Platzmangel in
              // die nächste Zeile, statt aus der Karte zu laufen.
              <li
                key={m.userId}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border px-3 py-2"
              >
                <span className="min-w-0 flex-1 text-sm [overflow-wrap:anywhere]">
                  {m.firstName} {m.lastName} ({m.email})
                </span>
                <Button
                  className="shrink-0"
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

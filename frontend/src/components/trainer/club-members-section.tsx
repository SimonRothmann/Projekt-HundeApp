"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Users, ShieldPlus, ShieldCheck, UserPlus } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
export function ClubMembersSection({ clubs }: { clubs: Club[] }) {
  const t = useT();
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
      toast.error(err instanceof ApiError ? err.message : t("Mitglieder konnten nicht geladen werden."));
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
      toast.success(t("Mitglied ist jetzt Trainer."));
      // Neu laden, sonst bietet die Zeile weiter "Zum Trainer machen" an,
      // obwohl die Person es gerade geworden ist.
      await loadMembers(selectedClubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Beförderung fehlgeschlagen."));
    } finally {
      setPromotingUserId(null);
    }
  }

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <Users className="size-5" />
{t("Mitglieder")}
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
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : members.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Noch keine Mitglieder.")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {members.map((m) => (
              // Der Knopf rutscht auf schmalen Geräten unter den Namen.
              //
              // min-w-[12rem] ist der Kern: Mit "flex-1" allein (Basis 0)
              // gibt der Text IMMER nach, der Knopf bleibt daneben stehen -
              // eine lange Adresse wurde dann in eine 114 px schmale Spalte
              // gequetscht und mitten im Wort über sechs Zeilen umgebrochen,
              // die Zeile 158 px hoch. Unterschreitet der Text diese
              // Mindestbreite, bricht die Zeile stattdessen um.
              <li
                key={m.userId}
                className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1 rounded-md border px-3 py-2"
              >
                <span className="min-w-[12rem] flex-1 text-sm [overflow-wrap:anywhere]">
                  {m.firstName} {m.lastName} ({m.email})
                </span>
                {m.isTrainer ? (
                  // Wer schon Trainer:in ist, braucht keinen Knopf dafür.
                  // Statt ihn zu verstecken, wird der Zustand benannt - sonst
                  // sieht die Zeile bloß unfertig aus.
                  <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground">
                    <ShieldCheck className="size-4" />
{t("Trainer:in")}
                  </span>
                ) : (
                  <Button
                    className="shrink-0"
                    size="sm"
                    variant="outline"
                    disabled={promotingUserId === m.userId}
                    onClick={() => handlePromote(m.userId)}
                  >
                    <ShieldPlus className="size-4" />
{t("Zum Trainer machen")}
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

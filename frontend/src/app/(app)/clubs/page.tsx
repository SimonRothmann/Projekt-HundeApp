"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { rollenVon } from "@/lib/vereinsrollen";
import { VereinsantragSection } from "@/components/clubs/vereinsantrag-section";
import { MeineGruppenSection } from "@/components/clubs/meine-gruppen-section";
import type { Club, ClubSummary, ClubMembership, Group } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Building2, Users } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
function membershipFor(memberships: ClubMembership[], clubId: string): ClubMembership | undefined {
  // Bei mehrfachen Anfragen (z.B. nach Ablehnung erneut angefragt) zählt
  // die jüngste Zeile.
  return memberships
    .filter((m) => m.clubId === clubId)
    .sort((a, b) => b.requestedAt.localeCompare(a.requestedAt))[0];
}

export default function ClubsPage() {
  const t = useT();
  const [clubs, setClubs] = useState<ClubSummary[] | null>(null);
  const [memberships, setMemberships] = useState<ClubMembership[]>([]);
  const [groupsByClub, setGroupsByClub] = useState<Record<string, Group[]>>({});
  // Die Vereine, in denen ich eine Rolle habe - samt Rechten (für den Link "Verein verwalten").
  const [meineVereine, setMeineVereine] = useState<Club[]>([]);
  const [joiningClubId, setJoiningClubId] = useState<string | null>(null);
  const [leavingClubId, setLeavingClubId] = useState<string | null>(null);
  const [antwortClubId, setAntwortClubId] = useState<string | null>(null);
  const [joiningGroupId, setJoiningGroupId] = useState<string | null>(null);
  // Zählt hoch, wenn hier unten eine Einladung angenommen wird - die Liste
  // "Deine Gruppen" oben lädt dann neu.
  const [gruppenStand, setGruppenStand] = useState(0);

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

      // Gruppen auch für Vereine laden, in denen man eine Rolle hat, ohne
      // Mitglied zu sein - die sahen ihre eigenen Gruppen hier sonst gar nicht.
      const meine = await api.get<Club[]>("/api/clubs/mine").catch(() => [] as Club[]);
      setMeineVereine(meine);
      const visibleIds = Array.from(
        new Set([...membershipsData.filter((m) => m.status === 1).map((m) => m.clubId), ...meine.map((c) => c.id)]),
      );
      if (visibleIds.length > 0) await loadGroups(visibleIds);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
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
      toast.success(t("Beitrittsanfrage gesendet."));
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Anfrage fehlgeschlagen."));
    } finally {
      setJoiningClubId(null);
    }
  }

  // Eingeladen ist noch kein Mitglied: Erst "Annehmen" macht daraus eine
  // Mitgliedschaft, und bis dahin kennt der Verein nur die Adresse, die er
  // selbst eingetippt hat.
  async function handleInvitation(clubId: string, annehmen: boolean) {
    setAntwortClubId(clubId);
    try {
      await api.post(`/api/clubs/${clubId}/invitation/${annehmen ? "accept" : "decline"}`);
      toast.success(annehmen ? t("Einladung angenommen.") : t("Einladung abgelehnt."));
      // Neu laden: Nach dem Annehmen erscheinen die Gruppen des Vereins.
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setAntwortClubId(null);
    }
  }

  async function handleLeave(clubId: string, clubName: string) {
    if (!window.confirm(t("„{name}“ wirklich verlassen?", { name: clubName }))) return;
    setLeavingClubId(clubId);
    try {
      await api.delete(`/api/clubs/${clubId}/membership`);
      toast.success(t("Verein verlassen."));
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Verlassen fehlgeschlagen."));
    } finally {
      setLeavingClubId(null);
    }
  }

  async function handleJoinGroup(groupId: string, eingeladen = false) {
    setJoiningGroupId(groupId);
    try {
      await api.post(`/api/groups/${groupId}/join-requests`);
      toast.success(eingeladen ? t("Einladung angenommen.") : t("Gruppenanfrage gesendet."));
      if (eingeladen) setGruppenStand((n) => n + 1);
      // Ohne Neuladen stünde weiter "Beitreten" da und man tippt ein zweites Mal.
      await loadAll();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppenanfrage fehlgeschlagen."));
    } finally {
      setJoiningGroupId(null);
    }
  }

  // Wer eingeladen ist, wartet auf eine Antwort - diese Vereine zuerst.
  const eingeladen = (clubId: string) => membershipFor(memberships, clubId)?.status === 3;
  const geordnet = [...(clubs ?? [])].sort((a, b) => Number(eingeladen(b.id)) - Number(eingeladen(a.id)));

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("Vereine")}</h1>
        <p className="text-muted-foreground">
{t("Tritt einem Verein bei - der Verein gibt deine Anfrage frei.")}
        </p>
      </div>

      <MeineGruppenSection key={gruppenStand} onChanged={loadAll} />

      {clubs === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : clubs.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("Noch keine Vereine vorhanden.")}</p>
      ) : (
        <div className="flex flex-col gap-4">
          {geordnet.map((club) => {
            const membership = membershipFor(memberships, club.id);
            const isApproved = membership?.status === 1;
            const groups = groupsByClub[club.id] ?? [];
            const rollen = rollenVon(club.roles ?? 0);
            // Gibt es in diesem Verein für meine Rollen etwas zu tun (Vorstand allein hat noch nichts)?
            const meiner = meineVereine.find((m) => m.id === club.id);
            const kannVerwalten = !!meiner && (meiner.canManage || meiner.canManageMembers || meiner.canAppointTrainers || meiner.isTrainer);
            return (
              <Card key={club.id}>
                <CardHeader className="flex flex-row items-center gap-3 space-y-0">
                  <Building2 className="size-8 shrink-0 text-primary-text" />
                  <div className="min-w-0">
                    <CardTitle className="[overflow-wrap:anywhere]">{club.name}</CardTitle>
                    {club.description && <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{club.description}</p>}
                  </div>
                </CardHeader>
                <CardContent className="flex flex-col gap-4">
                  {rollen.length > 0 && (
                    <div className="flex flex-wrap items-center gap-2">
                      {rollen.map((r) => (
                        <Badge key={r.bit} variant="secondary">
                          {t(r.name)}
                        </Badge>
                      ))}
                      {kannVerwalten && (
                        <Link
                          href={`/trainer/verein?verein=${club.id}&from=${encodeURIComponent("/clubs")}`}
                          className={buttonVariants({ size: "sm", variant: "outline" })}
                        >
                          {t("Verein verwalten")}
                        </Link>
                      )}
                    </div>
                  )}
                  {isApproved ? (
                    <div className="flex items-center gap-2">
                      <Badge>{t("Mitglied")}</Badge>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={leavingClubId === club.id}
                        onClick={() => handleLeave(club.id, club.name)}
                      >
                        {leavingClubId === club.id ? t("Wird verlassen…") : t("Verein verlassen")}
                      </Button>
                    </div>
                  ) : club.isTrainer ? (
                    // Wer eine Rolle hat, hat keine Mitgliedschaftszeile und gehört
                    // trotzdem dazu - "Beitreten" wäre hier falsch; die Rollen stehen oben.
                    null
                  ) : membership?.status === 0 ? (
                    <Badge variant="secondary">{t("Anfrage ausstehend")}</Badge>
                  ) : membership?.status === 3 ? (
                    <div className="flex min-w-0 flex-col gap-2 rounded-md border border-l-2 border-primary/25 border-l-primary bg-primary/6 p-3 dark:border-primary/35 dark:bg-primary/12">
                      <p className="text-sm font-medium [overflow-wrap:anywhere]">
                        {t("„{verein}“ lädt dich ein", { verein: club.name })}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {t("Nimmst du an, bist du Mitglied im Verein - die Trainer:innen sehen dann deinen Namen und deine E-Mail-Adresse.")}
                      </p>
                      <div className="flex flex-wrap gap-3">
                        <Button size="sm" disabled={antwortClubId === club.id} onClick={() => handleInvitation(club.id, true)}>
                          {t("Annehmen")}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={antwortClubId === club.id}
                          onClick={() => handleInvitation(club.id, false)}
                        >
                          {t("Ablehnen")}
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <Button size="sm" disabled={joiningClubId === club.id} onClick={() => handleJoin(club.id)}>
                      {joiningClubId === club.id ? t("Wird gesendet…") : t("Beitreten")}
                    </Button>
                  )}

                  {groups.length > 0 && (
                    <div className="flex flex-col gap-2 border-t pt-3">
                      <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">{t("Gruppen")}</p>
                      {groups.map((g) => (
                        <div key={g.id} className="flex flex-wrap items-center justify-between gap-2">
                          <div className="flex min-w-0 items-center gap-2 text-sm">
                            <Users className="size-4 shrink-0 text-muted-foreground" />
                            <span className="[overflow-wrap:anywhere]">{g.name}</span>
                            <Badge variant="outline" className="shrink-0 text-xs">
                              {g.memberCount === 1
                                ? t("1 Mitglied")
                                : t("{n} Mitglieder", { n: g.memberCount })}
                            </Badge>
                          </div>
                          {/* Beitreten nur, wenn man wirklich außen vor ist -
                              Trainer:innen und Mitgliedern bot die Seite vorher
                              denselben Knopf an. */}
                          {g.myRelation === 3 ? (
                            <Badge variant="secondary" className="shrink-0">{t("Trainer:in")}</Badge>
                          ) : g.myRelation === 2 ? (
                            <Badge variant="secondary" className="shrink-0">{t("Mitglied")}</Badge>
                          ) : g.myRelation === 1 ? (
                            <Badge variant="outline" className="shrink-0">{t("Anfrage ausstehend")}</Badge>
                          ) : g.myRelation === 4 ? (
                            // Eingeladen: Beitreten heißt hier Annehmen - das
                            // Backend wertet die Anfrage als Zusage.
                            <Button
                              size="sm"
                              disabled={joiningGroupId === g.id}
                              onClick={() => handleJoinGroup(g.id, true)}
                            >
                              {joiningGroupId === g.id ? t("Wird gesendet…") : t("Einladung annehmen")}
                            </Button>
                          ) : (
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={joiningGroupId === g.id}
                              onClick={() => handleJoinGroup(g.id)}
                            >
                              {joiningGroupId === g.id ? t("Wird gesendet…") : t("Beitreten")}
                            </Button>
                          )}
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
      <VereinsantragSection />
    </div>
  );
}

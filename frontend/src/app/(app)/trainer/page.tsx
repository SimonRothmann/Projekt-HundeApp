"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { Club, Group } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Users, Plus, ClipboardList, ChevronRight, CalendarDays } from "lucide-react";
import { toast } from "sonner";
import { CatalogSection } from "@/components/sports/catalog-section";
import { ClubJoinRequestsSection } from "@/components/trainer/club-join-requests-section";
import { ClubMembersSection } from "@/components/trainer/club-members-section";
import { GroupJoinRequestsSection } from "@/components/trainer/group-join-requests-section";
import { SupervisedDogsSection } from "@/components/trainer/supervised-dogs-section";
import { TrainerReviewSection } from "@/components/trainer/trainer-review-section";

import { useT } from "@/lib/i18n";
export default function TrainerPage() {
  const t = useT();
  const [groups, setGroups] = useState<Group[] | null>(null);
  const [myClubs, setMyClubs] = useState<Club[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [clubId, setClubId] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function loadGroups() {
    try {
      const data = await api.get<Group[]>("/api/groups");
      setGroups(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppen konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadGroups();
    api
      .get<Club[]>("/api/groups/my-clubs")
      .then(setMyClubs)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden.")));
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/groups", { name, description: description || null, clubId: clubId || null });
      toast.success(t("Gruppe angelegt."));
      setName("");
      setDescription("");
      setClubId("");
      await loadGroups();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppe konnte nicht angelegt werden."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("Trainer-Übersicht")}</h1>
        <p className="text-muted-foreground">
{t("Lege Trainingsgruppen an, lade Mitglieder per E-Mail ein und betreue ihre Hunde mit individuellen Trainingsplänen.")}
        </p>
      </div>

      {/* Ganz oben, noch vor Gruppentraining und Terminplanung: der Weg zum
          einzelnen Hund wird an einem Trainingsabend am häufigsten gebraucht. */}
      <SupervisedDogsSection />

      <Link href="/trainer/group-training">
        <Card className="transition-colors hover:bg-accent/30">
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <div className="flex items-center gap-3">
              <ClipboardList className="size-6 shrink-0 text-primary" />
              <div className="min-w-0">
                <CardTitle className="text-base">Gruppentraining</CardTitle>
                <p className="text-sm text-muted-foreground">
{t("Fertige Einheiten für Welpen & Junghunde übernehmen oder eigene zusammenstellen")}
                </p>
              </div>
            </div>
            <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
          </CardHeader>
        </Card>
      </Link>

      <Link href="/trainer/schedule">
        <Card className="transition-colors hover:bg-accent/30">
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <div className="flex items-center gap-3">
              <CalendarDays className="size-6 shrink-0 text-primary" />
              <div className="min-w-0">
                <CardTitle className="text-base">Terminplanung</CardTitle>
                <p className="text-sm text-muted-foreground">
{t("Gruppentrainings planen: wann, welche Gruppe, was gemacht wird (mit Mix-Generator & Serien)")}
                </p>
              </div>
            </div>
            <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
          </CardHeader>
        </Card>
      </Link>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("Neue Gruppe")}</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleCreate} className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="flex flex-col gap-2 sm:flex-1">
              <Label htmlFor="group-name">Name</Label>
              <Input id="group-name" value={name} onChange={(e) => setName(e.target.value)} required />
            </div>
            <div className="flex flex-col gap-2 sm:flex-1">
              <Label htmlFor="group-description">Beschreibung (optional)</Label>
              <Input
                id="group-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>
            {myClubs.length > 0 && (
              <div className="flex flex-col gap-2 sm:w-48">
                <Label>{t("Verein (optional)")}</Label>
                <Select value={clubId} onValueChange={(value) => setClubId(value ?? "")}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">{t("Kein Verein")}</SelectItem>
                    {myClubs.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <Button type="submit" disabled={submitting}>
              <Plus className="size-4" />
{t("Anlegen")}
            </Button>
          </form>
        </CardContent>
      </Card>

      {groups === null ? (
        <p className="text-muted-foreground">{t("Lädt…")}</p>
      ) : groups.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
{t("Noch keine Gruppen angelegt.")}
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {groups.map((group) => (
            <Link key={group.id} href={`/trainer/${group.id}`}>
              <Card className="transition-colors hover:bg-accent/30">
                <CardHeader className="flex-row items-center justify-between gap-2 space-y-0">
                  <div className="flex min-w-0 items-center gap-3">
                    <Users className="size-6 shrink-0 text-primary" />
                    <div className="min-w-0">
                      <CardTitle className="text-base [overflow-wrap:anywhere]">{group.name}</CardTitle>
                      {group.trainerName && (
                        <p className="text-xs text-muted-foreground">Trainer:in: {group.trainerName}</p>
                      )}
                    </div>
                  </div>
                  <div className="flex shrink-0 flex-col items-end gap-1">
                    {group.clubId && (
                      <Badge variant="outline">{myClubs.find((c) => c.id === group.clubId)?.name ?? t("Verein")}</Badge>
                    )}
                    <Badge variant="secondary">{group.memberCount} Mitglieder</Badge>
                  </div>
                </CardHeader>
                {group.description && (
                  <CardContent className="pt-0 text-sm text-muted-foreground">{group.description}</CardContent>
                )}
              </Card>
            </Link>
          ))}
        </div>
      )}

      <TrainerReviewSection />

      {groups !== null && groups.length > 0 && (
        <GroupJoinRequestsSection groups={groups} />
      )}

      {myClubs.length > 0 && (
        <>
          <ClubJoinRequestsSection clubs={myClubs} />
          <ClubMembersSection clubs={myClubs} />
          {myClubs.map((club) => (
            <CatalogSection
              key={club.id}
              scope={{ kind: "club", clubId: club.id, clubName: club.name }}
              title={`Vereinseigener Katalog · ${club.name}`}
              description={t("Eigene Sportarten und Übungen dieses Vereins - nur für Mitglieder und Trainer sichtbar.")}
            />
          ))}
        </>
      )}
    </div>
  );
}

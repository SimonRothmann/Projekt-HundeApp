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
import { Users, Plus } from "lucide-react";
import { toast } from "sonner";
import { ClubExercisesSection } from "@/components/trainer/club-exercises-section";
import { ClubJoinRequestsSection } from "@/components/trainer/club-join-requests-section";
import { ClubMembersSection } from "@/components/trainer/club-members-section";
import { PendingFeedbackSection } from "@/components/trainer/pending-feedback-section";

export default function TrainerPage() {
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
      toast.error(err instanceof ApiError ? err.message : "Gruppen konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadGroups();
    api
      .get<Club[]>("/api/groups/my-clubs")
      .then(setMyClubs)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Vereine konnten nicht geladen werden."));
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/groups", { name, description: description || null, clubId: clubId || null });
      toast.success("Gruppe angelegt.");
      setName("");
      setDescription("");
      setClubId("");
      await loadGroups();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Gruppe konnte nicht angelegt werden.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Trainer-Übersicht</h1>
        <p className="text-muted-foreground">
          Lege Trainingsgruppen an, lade Mitglieder per E-Mail ein und betreue ihre Hunde mit
          individuellen Trainingsplänen.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Neue Gruppe</CardTitle>
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
                <Label>Verein (optional)</Label>
                <Select value={clubId} onValueChange={(value) => setClubId(value ?? "")}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">Kein Verein</SelectItem>
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
              Anlegen
            </Button>
          </form>
        </CardContent>
      </Card>

      {groups === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : groups.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            Noch keine Gruppen angelegt.
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {groups.map((group) => (
            <Link key={group.id} href={`/trainer/${group.id}`}>
              <Card className="transition-colors hover:bg-accent/30">
                <CardHeader className="flex-row items-center justify-between space-y-0">
                  <div className="flex items-center gap-3">
                    <Users className="size-6 text-primary" />
                    <CardTitle className="text-base">{group.name}</CardTitle>
                  </div>
                  <div className="flex items-center gap-2">
                    {group.clubId && (
                      <Badge variant="outline">{myClubs.find((c) => c.id === group.clubId)?.name ?? "Verein"}</Badge>
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

      <PendingFeedbackSection />

      {myClubs.length > 0 && (
        <>
          <ClubJoinRequestsSection clubs={myClubs} />
          <ClubMembersSection clubs={myClubs} />
          <ClubExercisesSection clubs={myClubs} />
        </>
      )}
    </div>
  );
}

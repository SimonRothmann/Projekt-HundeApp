"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubDetail } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Building2, Plus, UserPlus, Trash2, ChevronDown, ChevronRight } from "lucide-react";
import { toast } from "sonner";

export function ClubsSection() {
  const [clubs, setClubs] = useState<Club[] | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [expandedClubId, setExpandedClubId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ClubDetail | null>(null);
  const [trainerEmail, setTrainerEmail] = useState("");
  const [memberEmail, setMemberEmail] = useState("");

  async function loadClubs() {
    try {
      const data = await api.get<Club[]>("/api/admin/clubs");
      setClubs(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Vereine konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadClubs();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/admin/clubs", { name, description: description || null });
      toast.success("Verein angelegt.");
      setName("");
      setDescription("");
      await loadClubs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Verein konnte nicht angelegt werden.");
    } finally {
      setSubmitting(false);
    }
  }

  async function toggleClub(clubId: string) {
    if (expandedClubId === clubId) {
      setExpandedClubId(null);
      setDetail(null);
      return;
    }
    setExpandedClubId(clubId);
    try {
      const data = await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`);
      setDetail(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Verein konnte nicht geladen werden.");
    }
  }

  async function handleAssignTrainer(clubId: string, e: React.FormEvent) {
    e.preventDefault();
    if (!trainerEmail.trim()) return;
    try {
      await api.post(`/api/admin/clubs/${clubId}/trainers`, { email: trainerEmail });
      toast.success("Trainer zugewiesen.");
      setTrainerEmail("");
      const data = await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`);
      setDetail(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Zuweisung fehlgeschlagen.");
    }
  }

  async function handleRemoveTrainer(clubId: string, userId: string) {
    try {
      await api.delete(`/api/admin/clubs/${clubId}/trainers/${userId}`);
      toast.success("Trainer entfernt.");
      const data = await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`);
      setDetail(data);
      await loadClubs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Entfernen fehlgeschlagen.");
    }
  }

  async function handleAddMember(clubId: string, e: React.FormEvent) {
    e.preventDefault();
    if (!memberEmail.trim()) return;
    try {
      await api.post(`/api/admin/clubs/${clubId}/members`, { email: memberEmail });
      toast.success("Mitglied hinzugefügt.");
      setMemberEmail("");
      const data = await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`);
      setDetail(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Zuweisung fehlgeschlagen.");
    }
  }

  async function handleRemoveMember(clubId: string, userId: string) {
    try {
      await api.delete(`/api/admin/clubs/${clubId}/members/${userId}`);
      toast.success("Mitglied entfernt.");
      const data = await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`);
      setDetail(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Entfernen fehlgeschlagen.");
    }
  }

  async function handlePromoteMember(clubId: string, userEmail: string) {
    // Beförderung zum Trainer: wir nutzen den bestehenden Trainer-Assign-
    // Endpoint, der eine E-Mail entgegennimmt.
    try {
      await api.post(`/api/admin/clubs/${clubId}/trainers`, { email: userEmail });
      toast.success("Mitglied zum Trainer befördert.");
      const data = await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`);
      setDetail(data);
      await loadClubs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Beförderung fehlgeschlagen.");
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Building2 className="size-5" />
          Vereine
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <form onSubmit={handleCreate} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex flex-col gap-2 sm:flex-1">
            <Label htmlFor="club-name">Name</Label>
            <Input id="club-name" value={name} onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="flex flex-col gap-2 sm:flex-1">
            <Label htmlFor="club-description">Beschreibung (optional)</Label>
            <Input id="club-description" value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <Button type="submit" disabled={submitting}>
            <Plus className="size-4" />
            Anlegen
          </Button>
        </form>

        {clubs === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : clubs.length === 0 ? (
          <p className="text-sm text-muted-foreground">Noch keine Vereine angelegt.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {clubs.map((club) => {
              const isOpen = expandedClubId === club.id;
              return (
                <li key={club.id} className="rounded-md border">
                  <button
                    type="button"
                    className="flex w-full items-center justify-between px-3 py-2 text-left"
                    onClick={() => toggleClub(club.id)}
                  >
                    <div>
                      <span className="font-medium">{club.name}</span>
                      {club.description && (
                        <span className="ml-2 text-sm text-muted-foreground">{club.description}</span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant="secondary">{club.trainerCount} Trainer</Badge>
                      <Badge variant="secondary">{club.groupCount} Gruppen</Badge>
                      {isOpen ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                    </div>
                  </button>
                  {isOpen && detail && (
                    <div className="flex flex-col gap-3 border-t px-3 py-3">
                      <form onSubmit={(e) => handleAssignTrainer(club.id, e)} className="flex gap-2">
                        <Input
                          type="email"
                          placeholder="trainer@example.com"
                          value={trainerEmail}
                          onChange={(e) => setTrainerEmail(e.target.value)}
                          required
                        />
                        <Button type="submit" size="sm" variant="outline">
                          <UserPlus className="size-4" />
                          Trainer zuweisen
                        </Button>
                      </form>
                      {detail.trainers.length === 0 ? (
                        <p className="text-sm text-muted-foreground">Noch keine Trainer zugewiesen.</p>
                      ) : (
                        <ul className="flex flex-col gap-1">
                          {detail.trainers.map((t) => (
                            <li key={t.userId} className="flex items-center justify-between text-sm">
                              <span>
                                <Badge variant="secondary" className="mr-2">Trainer</Badge>
                                {t.firstName} {t.lastName} ({t.email})
                              </span>
                              <Button
                                type="button"
                                size="icon-sm"
                                variant="ghost"
                                onClick={() => handleRemoveTrainer(club.id, t.userId)}
                                title="Trainer-Rolle entfernen"
                              >
                                <Trash2 className="size-3.5" />
                              </Button>
                            </li>
                          ))}
                        </ul>
                      )}

                      <div className="border-t pt-3">
                        <form onSubmit={(e) => handleAddMember(club.id, e)} className="flex gap-2">
                          <Input
                            type="email"
                            placeholder="mitglied@example.com"
                            value={memberEmail}
                            onChange={(e) => setMemberEmail(e.target.value)}
                            required
                          />
                          <Button type="submit" size="sm" variant="outline">
                            <UserPlus className="size-4" />
                            Mitglied hinzufügen
                          </Button>
                        </form>
                        {detail.members.length === 0 ? (
                          <p className="mt-2 text-sm text-muted-foreground">Noch keine Mitglieder.</p>
                        ) : (
                          <ul className="mt-2 flex flex-col gap-1">
                            {detail.members.map((m) => (
                              <li key={m.userId} className="flex items-center justify-between text-sm">
                                <span>
                                  {m.firstName} {m.lastName} ({m.email})
                                </span>
                                <div className="flex items-center gap-1">
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="ghost"
                                    onClick={() => handlePromoteMember(club.id, m.email)}
                                    title="Zum Trainer befördern"
                                  >
                                    Zum Trainer
                                  </Button>
                                  <Button
                                    type="button"
                                    size="icon-sm"
                                    variant="ghost"
                                    onClick={() => handleRemoveMember(club.id, m.userId)}
                                    title="Aus Verein entfernen"
                                  >
                                    <Trash2 className="size-3.5" />
                                  </Button>
                                </div>
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

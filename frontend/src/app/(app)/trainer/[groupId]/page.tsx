"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { GroupDetail, MemberDog } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { UserPlus, Dog as DogIcon, ChevronDown, ChevronRight, Trash2 } from "lucide-react";
import { toast } from "sonner";

export default function TrainerGroupPage() {
  const params = useParams<{ groupId: string }>();
  const groupId = params.groupId;

  const [detail, setDetail] = useState<GroupDetail | null>(null);
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [expandedMember, setExpandedMember] = useState<string | null>(null);
  const [memberDogs, setMemberDogs] = useState<Record<string, MemberDog[]>>({});

  async function loadDetail() {
    try {
      const data = await api.get<GroupDetail>(`/api/groups/${groupId}`);
      setDetail(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Gruppe konnte nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDetail();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  async function handleAddMember(e: React.FormEvent) {
    e.preventDefault();
    if (!email.trim()) return;
    setSubmitting(true);
    try {
      await api.post(`/api/groups/${groupId}/members`, { email });
      toast.success("Mitglied hinzugefügt.");
      setEmail("");
      await loadDetail();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Mitglied konnte nicht hinzugefügt werden.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleRemoveMember(memberId: string) {
    try {
      await api.delete(`/api/groups/${groupId}/members/${memberId}`);
      toast.success("Mitglied entfernt.");
      await loadDetail();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Mitglied konnte nicht entfernt werden.");
    }
  }

  async function toggleMember(memberId: string) {
    if (expandedMember === memberId) {
      setExpandedMember(null);
      return;
    }
    setExpandedMember(memberId);
    if (!memberDogs[memberId]) {
      try {
        const dogs = await api.get<MemberDog[]>(`/api/groups/${groupId}/members/${memberId}/dogs`);
        setMemberDogs((prev) => ({ ...prev, [memberId]: dogs }));
      } catch (err) {
        toast.error(err instanceof ApiError ? err.message : "Hunde konnten nicht geladen werden.");
      }
    }
  }

  async function handleAssign(memberId: string, dogId: string) {
    try {
      await api.post(`/api/groups/${groupId}/trainer-assignments`, { memberId, dogId });
      toast.success("Du betreust diesen Hund jetzt.");
      const dogs = await api.get<MemberDog[]>(`/api/groups/${groupId}/members/${memberId}/dogs`);
      setMemberDogs((prev) => ({ ...prev, [memberId]: dogs }));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Zuordnung fehlgeschlagen.");
    }
  }

  if (detail === null) {
    return <p className="text-muted-foreground">Lädt…</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{detail.group.name}</h1>
        {detail.group.description && <p className="text-muted-foreground">{detail.group.description}</p>}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Mitglied einladen</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleAddMember} className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="flex flex-col gap-2 sm:flex-1">
              <Label htmlFor="member-email">E-Mail-Adresse</Label>
              <Input
                id="member-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="mitglied@example.com"
                required
              />
            </div>
            <Button type="submit" disabled={submitting}>
              <UserPlus className="size-4" />
              Hinzufügen
            </Button>
          </form>
        </CardContent>
      </Card>

      <div className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold">Mitglieder</h2>
        {detail.members.length === 0 ? (
          <Card>
            <CardContent className="py-10 text-center text-muted-foreground">
              Noch keine Mitglieder in dieser Gruppe.
            </CardContent>
          </Card>
        ) : (
          detail.members.map((member) => {
            const isOpen = expandedMember === member.userId;
            const dogs = memberDogs[member.userId];
            return (
              <Card key={member.userId}>
                <CardHeader
                  className="flex-row cursor-pointer items-center justify-between space-y-0"
                  onClick={() => toggleMember(member.userId)}
                >
                  <div>
                    <CardTitle className="text-base">
                      {member.firstName} {member.lastName}
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">{member.email}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRemoveMember(member.userId);
                      }}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                    {isOpen ? <ChevronDown className="size-5" /> : <ChevronRight className="size-5" />}
                  </div>
                </CardHeader>
                {isOpen && (
                  <CardContent>
                    {!dogs ? (
                      <p className="text-sm text-muted-foreground">Lädt Hunde…</p>
                    ) : dogs.length === 0 ? (
                      <p className="text-sm text-muted-foreground">Dieses Mitglied hat noch keine Hunde angelegt.</p>
                    ) : (
                      <ul className="flex flex-col gap-2">
                        {dogs.map((dog) => (
                          <li key={dog.id} className="flex items-center justify-between rounded-md border px-3 py-2">
                            <div className="flex items-center gap-2">
                              <DogIcon className="size-4 text-primary" />
                              <span className="font-medium">{dog.name}</span>
                              {dog.breed && <span className="text-sm text-muted-foreground">{dog.breed}</span>}
                            </div>
                            {dog.isTrainerAssigned ? (
                              <div className="flex items-center gap-2">
                                <Badge variant="secondary">Betreut</Badge>
                                <Link href={`/dogs/${dog.id}`} className="text-sm text-primary underline">
                                  Zum Hund
                                </Link>
                              </div>
                            ) : (
                              <Button size="sm" variant="outline" onClick={() => handleAssign(member.userId, dog.id)}>
                                Als Trainer betreuen
                              </Button>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                  </CardContent>
                )}
              </Card>
            );
          })
        )}
      </div>
    </div>
  );
}

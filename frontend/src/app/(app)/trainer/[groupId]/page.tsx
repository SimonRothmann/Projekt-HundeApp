"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { GroupDetail, MemberDog, GroupTrainerOption } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { UserPlus, Dog as DogIcon, ChevronDown, ChevronRight, Trash2, Pencil, UserCog } from "lucide-react";
import { toast } from "sonner";

export default function TrainerGroupPage() {
  const params = useParams<{ groupId: string }>();
  const groupId = params.groupId;

  const [detail, setDetail] = useState<GroupDetail | null>(null);
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [expandedMember, setExpandedMember] = useState<string | null>(null);
  const [memberDogs, setMemberDogs] = useState<Record<string, MemberDog[]>>({});

  // Bearbeiten (Name/Beschreibung)
  const [editing, setEditing] = useState(false);
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  // Trainer:in zuweisen
  const [trainers, setTrainers] = useState<GroupTrainerOption[]>([]);
  const [selectedTrainerId, setSelectedTrainerId] = useState("");
  const [assigning, setAssigning] = useState(false);

  // Weitere Trainer:innen (gleichberechtigt neben der/dem Hauptverantwortlichen)
  const [coTrainerEmail, setCoTrainerEmail] = useState("");
  const [addingCoTrainer, setAddingCoTrainer] = useState(false);

  async function loadDetail() {
    try {
      const data = await api.get<GroupDetail>(`/api/groups/${groupId}`);
      setDetail(data);
      // Zuweisbare Trainer:innen (nur bei Vereinsgruppen) für die Auswahl laden.
      if (data.group.clubId) {
        try {
          setTrainers(await api.get<GroupTrainerOption[]>(`/api/groups/${groupId}/trainers`));
        } catch {
          // Trainerliste ist optional - Fehler still schlucken.
        }
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Gruppe konnte nicht geladen werden.");
    }
  }

  function startEdit() {
    if (!detail) return;
    setEditName(detail.group.name);
    setEditDescription(detail.group.description ?? "");
    setEditing(true);
  }

  async function saveEdit() {
    if (!editName.trim()) {
      toast.error("Name ist erforderlich.");
      return;
    }
    setSavingEdit(true);
    try {
      await api.put(`/api/groups/${groupId}`, { name: editName.trim(), description: editDescription.trim() || null });
      toast.success("Gruppe aktualisiert.");
      setEditing(false);
      await loadDetail();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Gruppe konnte nicht gespeichert werden.");
    } finally {
      setSavingEdit(false);
    }
  }

  async function assignTrainer() {
    if (!selectedTrainerId) return;
    setAssigning(true);
    try {
      await api.put(`/api/groups/${groupId}/trainer`, { trainerId: selectedTrainerId });
      toast.success("Trainer:in zugewiesen.");
      setSelectedTrainerId("");
      await loadDetail();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Trainer:in konnte nicht zugewiesen werden.");
    } finally {
      setAssigning(false);
    }
  }

  async function addCoTrainer(e: React.FormEvent) {
    e.preventDefault();
    if (!coTrainerEmail.trim()) return;
    setAddingCoTrainer(true);
    try {
      await api.post(`/api/groups/${groupId}/co-trainers`, { email: coTrainerEmail.trim() });
      toast.success("Trainer:in hinzugefügt.");
      setCoTrainerEmail("");
      await loadDetail();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Trainer:in konnte nicht hinzugefügt werden.");
    } finally {
      setAddingCoTrainer(false);
    }
  }

  async function removeCoTrainer(userId: string) {
    try {
      await api.delete(`/api/groups/${groupId}/co-trainers/${userId}`);
      toast.success("Trainer:in entfernt.");
      await loadDetail();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Trainer:in konnte nicht entfernt werden.");
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
        <h1 className="text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{detail.group.name}</h1>
        <p className="text-sm text-muted-foreground">
          {detail.members.length} Mitglied{detail.members.length === 1 ? "" : "er"}
          {detail.group.trainerName ? ` · Trainer:in: ${detail.group.trainerName}` : ""}
        </p>
      </div>

      {/* Gruppe verwalten: Name/Beschreibung bearbeiten + Trainer:in zuweisen.
          Für jede:n Trainer:in des Vereins möglich (Backend prüft die Rechte). */}
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">Gruppe</CardTitle>
          {!editing && (
            <Button type="button" size="sm" variant="outline" onClick={startEdit}>
              <Pencil className="size-3.5" />
              Bearbeiten
            </Button>
          )}
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {editing ? (
            <div className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="edit-group-name">Name</Label>
                <Input id="edit-group-name" value={editName} onChange={(e) => setEditName(e.target.value)} maxLength={200} />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="edit-group-desc">Beschreibung (optional)</Label>
                <Input id="edit-group-desc" value={editDescription} onChange={(e) => setEditDescription(e.target.value)} maxLength={500} />
              </div>
              <div className="flex gap-2">
                <Button type="button" size="sm" disabled={savingEdit} onClick={saveEdit}>
                  {savingEdit ? "Speichert…" : "Speichern"}
                </Button>
                <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
                  Abbrechen
                </Button>
              </div>
            </div>
          ) : (
            detail.group.description && (
              <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{detail.group.description}</p>
            )
          )}

          {/* Trainer:innen der Gruppe. Mehrere sind ausdrücklich erlaubt - wer
              hier steht, darf die Gruppe genauso verwalten und kann daneben in
              beliebig vielen anderen Gruppen eingetragen sein. */}
          <div className="flex flex-col gap-3 border-t pt-4">
            <div className="flex items-center gap-2 text-sm font-medium">
              <UserCog className="size-4 shrink-0 text-muted-foreground" />
              Trainer:innen
            </div>

            <ul className="flex flex-col gap-1.5">
              {detail.trainers.map((trainer) => (
                <li
                  key={trainer.userId}
                  className="flex items-center justify-between gap-2 rounded-md bg-muted/40 px-3 py-2"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">
                      {`${trainer.firstName} ${trainer.lastName}`.trim() || trainer.email}
                    </p>
                    <p className="truncate text-xs text-muted-foreground">{trainer.email}</p>
                  </div>
                  {trainer.isLead ? (
                    <Badge variant="secondary" className="shrink-0">
                      Hauptverantwortlich
                    </Badge>
                  ) : (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="shrink-0"
                      aria-label={`${trainer.firstName} ${trainer.lastName}`.trim() || trainer.email}
                      title="Trainer:in entfernen"
                      onClick={() => removeCoTrainer(trainer.userId)}
                    >
                      <Trash2 className="size-4 text-muted-foreground" />
                    </Button>
                  )}
                </li>
              ))}
            </ul>

            <form onSubmit={addCoTrainer} className="flex flex-col gap-2 sm:flex-row sm:items-end">
              <div className="flex flex-col gap-1.5 sm:flex-1">
                <Label htmlFor="cotrainer-email">Weitere:n Trainer:in hinzufügen</Label>
                <Input
                  id="cotrainer-email"
                  type="email"
                  value={coTrainerEmail}
                  onChange={(e) => setCoTrainerEmail(e.target.value)}
                  placeholder="trainer@example.com"
                />
              </div>
              <Button type="submit" size="sm" disabled={addingCoTrainer || !coTrainerEmail.trim()}>
                <UserPlus className="size-4" />
                {addingCoTrainer ? "Fügt hinzu…" : "Hinzufügen"}
              </Button>
            </form>

            {detail.group.clubId ? (
              <div className="flex flex-col gap-2 border-t pt-3 sm:flex-row sm:items-end">
                <div className="flex flex-col gap-1.5 sm:flex-1">
                  <Label>Hauptverantwortliche:n wechseln</Label>
                  <Select value={selectedTrainerId} onValueChange={(v) => setSelectedTrainerId(v ?? "")}>
                    <SelectTrigger>
                      <SelectValue placeholder="Trainer:in wählen…" />
                    </SelectTrigger>
                    <SelectContent>
                      {trainers.map((t) => (
                        <SelectItem key={t.userId} value={t.userId}>
                          {`${t.firstName} ${t.lastName}`.trim() || t.email}
                          {t.userId === detail.group.trainerId ? " (aktuell)" : ""}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <Button
                  type="button"
                  size="sm"
                  disabled={assigning || !selectedTrainerId || selectedTrainerId === detail.group.trainerId}
                  onClick={assignTrainer}
                >
                  {assigning ? "Weise zu…" : "Wechseln"}
                </Button>
              </div>
            ) : (
              <p className="text-xs text-muted-foreground">
                Die/den Hauptverantwortliche:n zu wechseln geht nur bei Vereinsgruppen. Weitere Trainer:innen lassen sich
                überall hinzufügen.
              </p>
            )}
          </div>
        </CardContent>
      </Card>

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

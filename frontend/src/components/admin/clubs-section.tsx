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

import { useT } from "@/lib/i18n";
export function ClubsSection() {
  const t = useT();
  const [clubs, setClubs] = useState<Club[] | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [expandedClubId, setExpandedClubId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ClubDetail | null>(null);
  const [trainerEmail, setTrainerEmail] = useState("");
  const [memberEmail, setMemberEmail] = useState("");

  async function ladeDetail(clubId: string) {
    setDetail(await api.get<ClubDetail>(`/api/admin/clubs/${clubId}`));
  }

  /**
   * Aktion ausführen, Erfolg melden, Detailansicht nachladen - und im
   * Fehlerfall die Meldung des Servers zeigen.
   *
   * Dieser Ablauf stand sechsmal wortgleich im Modul (Trainer zuweisen und
   * entfernen, Mitglied hinzufügen und entfernen, befördern, aufklappen).
   * Sechs Kopien heißt: Wer den Ablauf ändert, ändert ihn an fünf Stellen
   * nicht.
   */
  async function mitDetailAktualisierung(
    aktion: () => Promise<void>,
    erfolg: string,
    fehler: string,
    clubId: string,
    auchListe = false,
  ) {
    try {
      await aktion();
      toast.success(erfolg);
      await ladeDetail(clubId);
      if (auchListe) await loadClubs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : fehler);
    }
  }

  async function loadClubs() {
    try {
      const data = await api.get<Club[]>("/api/admin/clubs");
      setClubs(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Vereine konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    loadClubs();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/admin/clubs", { name, description: description || null });
      toast.success(t("Verein angelegt."));
      setName("");
      setDescription("");
      await loadClubs();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Verein konnte nicht angelegt werden."));
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
      await ladeDetail(clubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Verein konnte nicht geladen werden."));
    }
  }

  async function handleAssignTrainer(clubId: string, e: React.FormEvent) {
    e.preventDefault();
    if (!trainerEmail.trim()) return;
    await mitDetailAktualisierung(
      async () => {
        await api.post(`/api/admin/clubs/${clubId}/trainers`, { email: trainerEmail });
        setTrainerEmail("");
      },
      t("Trainer zugewiesen."),
      "Zuweisung fehlgeschlagen.",
      clubId,
    );
  }

  async function handleRemoveTrainer(clubId: string, userId: string) {
    await mitDetailAktualisierung(
      () => api.delete(`/api/admin/clubs/${clubId}/trainers/${userId}`),
      t("Trainer entfernt."),
      t("Entfernen fehlgeschlagen."),
      clubId,
      true,
    );
  }

  async function handleAddMember(clubId: string, e: React.FormEvent) {
    e.preventDefault();
    if (!memberEmail.trim()) return;
    await mitDetailAktualisierung(
      async () => {
        await api.post(`/api/admin/clubs/${clubId}/members`, { email: memberEmail });
        setMemberEmail("");
      },
      t("Mitglied hinzugefügt."),
      "Zuweisung fehlgeschlagen.",
      clubId,
    );
  }

  async function handleRemoveMember(clubId: string, userId: string) {
    await mitDetailAktualisierung(
      () => api.delete(`/api/admin/clubs/${clubId}/members/${userId}`),
      t("Mitglied entfernt."),
      t("Entfernen fehlgeschlagen."),
      clubId,
    );
  }

  async function handlePromoteMember(clubId: string, userEmail: string) {
    // Beförderung zum Trainer: wir nutzen den bestehenden Trainer-Assign-
    // Endpoint, der eine E-Mail entgegennimmt.
    await mitDetailAktualisierung(
      () => api.post(`/api/admin/clubs/${clubId}/trainers`, { email: userEmail }),
      t("Mitglied zum Trainer befördert."),
      t("Beförderung fehlgeschlagen."),
      clubId,
      true,
    );
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
{t("Anlegen")}
          </Button>
        </form>

        {clubs === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : clubs.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Noch keine Vereine angelegt.")}</p>
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
{t("Trainer zuweisen")}
                        </Button>
                      </form>
                      {detail.trainers.length === 0 ? (
                        <p className="text-sm text-muted-foreground">{t("Noch keine Trainer zugewiesen.")}</p>
                      ) : (
                        <ul className="flex flex-col gap-1">
                          {/* Der Laufparameter hiess t und verdeckte damit den
                              Uebersetzer - umbenannt statt umgangen. */}
                          {detail.trainers.map((trainer) => (
                            <li key={trainer.userId} className="flex items-center justify-between text-sm">
                              <span>
                                <Badge variant="secondary" className="mr-2">{t("Trainer")}</Badge>
                                {trainer.firstName} {trainer.lastName} ({trainer.email})
                              </span>
                              <Button
                                type="button"
                                size="icon-sm"
                                variant="ghost"
                                onClick={() => handleRemoveTrainer(club.id, trainer.userId)}
                                title={t("Trainer-Rolle entfernen")}
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
{t("Mitglied hinzufügen")}
                          </Button>
                        </form>
                        {detail.members.length === 0 ? (
                          <p className="mt-2 text-sm text-muted-foreground">{t("Noch keine Mitglieder.")}</p>
                        ) : (
                          <ul className="mt-2 flex flex-col gap-1">
                            {detail.members.map((m) => (
                              // gap-2 + min-w-0 + flex-wrap: Eine lange
                              // E-Mail-Adresse schob die Knöpfe sonst aus der
                              // Zeile - Flex-Kinder haben von Haus aus
                              // min-width:auto und schrumpfen nicht.
                              <li
                                key={m.userId}
                                className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1 text-sm"
                              >
                                <span className="min-w-[10rem] flex-1 [overflow-wrap:anywhere]">
                                  {m.firstName} {m.lastName} ({m.email})
                                </span>
                                <div className="flex shrink-0 items-center gap-1">
                                  {/* Wer schon Trainer:in ist, braucht die
                                      Beförderung nicht angeboten zu bekommen. */}
                                  {m.isTrainer ? (
                                    <Badge variant="secondary">{t("Trainer")}</Badge>
                                  ) : (
                                    <Button
                                      type="button"
                                      size="sm"
                                      variant="ghost"
                                      onClick={() => handlePromoteMember(club.id, m.email)}
                                      title={t("Zum Trainer befördern")}
                                    >
{t("Zum Trainer")}
                                    </Button>
                                  )}
                                  <Button
                                    type="button"
                                    size="icon-sm"
                                    variant="ghost"
                                    onClick={() => handleRemoveMember(club.id, m.userId)}
                                    title={t("Aus Verein entfernen")}
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

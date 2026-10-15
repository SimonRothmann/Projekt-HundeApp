"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubDetail } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Plus, UserPlus, Trash2, ChevronDown } from "lucide-react";
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
    // Ohne eigene Titelzeile: Die Seite /admin/vereine trägt den Titel schon.
    // Die Liste steht oben - angelegt wird selten (meist über einen Antrag),
    // nachgesehen oft.
    <div className="flex flex-col gap-6">
      {clubs === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : clubs.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("Noch keine Vereine angelegt.")}</p>
      ) : (
        <ul className="flex flex-col divide-y divide-border overflow-hidden rounded-xl bg-card text-sm ring-1 ring-foreground/10">
          {clubs.map((club) => {
            const isOpen = expandedClubId === club.id;
            return (
              <li key={club.id}>
                <button
                  type="button"
                  aria-expanded={isOpen}
                  className="flex min-h-[3.25rem] w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-muted/50 active:bg-muted"
                  onClick={() => toggleClub(club.id)}
                >
                  {/* Name und Beschreibung über die ganze Breite, die Zahlen
                      darunter: Nebeneinander drückten die Abzeichen den Namen
                      bei 375 px auf eine Wortbreite zusammen. */}
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium [overflow-wrap:anywhere]">{club.name}</span>
                    {club.description && (
                      <span className="block text-muted-foreground [overflow-wrap:anywhere]">{club.description}</span>
                    )}
                    <span className="mt-1 flex flex-wrap gap-1">
                      <Badge variant="secondary">
                        {club.trainerCount === 1 ? t("1 Trainer") : t("{n} Trainer", { n: club.trainerCount })}
                      </Badge>
                      <Badge variant="secondary">
                        {club.groupCount === 1 ? t("1 Gruppe") : t("{n} Gruppen", { n: club.groupCount })}
                      </Badge>
                    </span>
                  </span>
                  <ChevronDown
                    className={`size-5 shrink-0 text-muted-foreground transition-transform ${isOpen ? "rotate-180" : ""}`}
                    aria-hidden
                  />
                </button>
                {isOpen && detail && (
                  <div className="flex flex-col gap-4 border-t px-4 py-3">
                    <div className="flex flex-col gap-2">
                      <p className="font-medium">{t("Trainer:innen")}</p>
                      {detail.trainers.length === 0 ? (
                        <p className="text-muted-foreground">{t("Noch keine Trainer zugewiesen.")}</p>
                      ) : (
                        <ul className="flex flex-col gap-1">
                          {/* Der Laufparameter hiess t und verdeckte damit den
                              Uebersetzer - umbenannt statt umgangen. */}
                          {detail.trainers.map((trainer) => (
                            <li key={trainer.userId} className="flex items-center gap-2">
                              <span className="min-w-0 flex-1 [overflow-wrap:anywhere]">
                                {trainer.firstName} {trainer.lastName}{" "}
                                <span className="text-muted-foreground">({trainer.email})</span>
                              </span>
                              <Button
                                type="button"
                                size="icon"
                                variant="ghost"
                                className="shrink-0"
                                onClick={() => handleRemoveTrainer(club.id, trainer.userId)}
                                title={t("Trainer-Rolle entfernen")}
                                aria-label={t("Trainer-Rolle entfernen")}
                              >
                                <Trash2 className="size-4" />
                              </Button>
                            </li>
                          ))}
                        </ul>
                      )}
                      <form onSubmit={(e) => handleAssignTrainer(club.id, e)} className="flex flex-col gap-2 sm:flex-row">
                        <Input
                          type="email"
                          placeholder="trainer@example.com"
                          aria-label={t("E-Mail der Trainer:in")}
                          className="h-11 sm:flex-1"
                          value={trainerEmail}
                          onChange={(e) => setTrainerEmail(e.target.value)}
                          required
                        />
                        <Button type="submit" variant="outline" className="h-11">
                          <UserPlus className="size-4" />
                          {t("Trainer zuweisen")}
                        </Button>
                      </form>
                    </div>

                    <div className="flex flex-col gap-2 border-t pt-3">
                      <p className="font-medium">{t("Mitglieder")}</p>
                      {detail.members.length === 0 ? (
                        <p className="text-muted-foreground">{t("Noch keine Mitglieder.")}</p>
                      ) : (
                        <ul className="flex flex-col gap-1">
                          {detail.members.map((m) => (
                            // gap-2 + min-w-0 + flex-wrap: Eine lange
                            // E-Mail-Adresse schob die Knöpfe sonst aus der
                            // Zeile - Flex-Kinder haben von Haus aus
                            // min-width:auto und schrumpfen nicht.
                            <li key={m.userId} className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1">
                              <span className="min-w-[10rem] flex-1 [overflow-wrap:anywhere]">
                                {m.firstName} {m.lastName} <span className="text-muted-foreground">({m.email})</span>
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
                                  size="icon"
                                  variant="ghost"
                                  onClick={() => handleRemoveMember(club.id, m.userId)}
                                  title={t("Aus Verein entfernen")}
                                  aria-label={t("Aus Verein entfernen")}
                                >
                                  <Trash2 className="size-4" />
                                </Button>
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}
                      <form onSubmit={(e) => handleAddMember(club.id, e)} className="flex flex-col gap-2 sm:flex-row">
                        <Input
                          type="email"
                          placeholder="mitglied@example.com"
                          aria-label={t("E-Mail des Mitglieds")}
                          className="h-11 sm:flex-1"
                          value={memberEmail}
                          onChange={(e) => setMemberEmail(e.target.value)}
                          required
                        />
                        <Button type="submit" variant="outline" className="h-11">
                          <UserPlus className="size-4" />
                          {t("Mitglied hinzufügen")}
                        </Button>
                      </form>
                    </div>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Plus className="size-5 shrink-0" />
            {t("Verein anlegen")}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleCreate} className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="flex flex-col gap-2 sm:flex-1">
              <Label htmlFor="club-name">{t("Name")}</Label>
              <Input id="club-name" className="h-11" value={name} onChange={(e) => setName(e.target.value)} required />
            </div>
            <div className="flex flex-col gap-2 sm:flex-1">
              <Label htmlFor="club-description">{t("Beschreibung (optional)")}</Label>
              <Input
                id="club-description"
                className="h-11"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>
            <Button type="submit" className="h-11" disabled={submitting}>
              <Plus className="size-4" />
              {t("Anlegen")}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

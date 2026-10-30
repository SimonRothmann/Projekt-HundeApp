"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubInvitation, ClubMemberRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Users, ShieldPlus, ShieldCheck, UserMinus, UserPlus } from "lucide-react";
import { toast } from "sonner";

import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";

export function ClubMembersSection({ clubs, onBefoerdert }: { clubs: Club[]; onBefoerdert?: () => void }) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const [selectedClubId, setSelectedClubId] = useState(clubs[0]?.id ?? "");
  const [members, setMembers] = useState<ClubMemberRequest[] | null>(null);
  const [promotingUserId, setPromotingUserId] = useState<string | null>(null);
  const [entferneUserId, setEntferneUserId] = useState<string | null>(null);
  const [neueMail, setNeueMail] = useState("");
  const [ladtEin, setLadtEin] = useState(false);
  // Ein Fehlschlag beim Laden lässt sie leer - die Mitgliederliste darunter ist
  // wichtiger als diese Nebenliste.
  const [einladungen, setEinladungen] = useState<ClubInvitation[]>([]);
  const [zieheZurueck, setZieheZurueck] = useState<string | null>(null);

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

  async function loadInvitations(clubId: string) {
    if (!clubId) {
      setEinladungen([]);
      return;
    }
    try {
      setEinladungen(await api.get<ClubInvitation[]>(`/api/clubs/${clubId}/invitations`));
    } catch {
      setEinladungen([]);
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadMembers(selectedClubId);
    loadInvitations(selectedClubId);
  }, [selectedClubId]);

  /**
   * Jemanden per E-Mail-Adresse EINLADEN - nicht aufnehmen.
   *
   * Der häufigste Anlass bleibt: Auf dem Platz steht jemand, der eben
   * Mitglied geworden ist. Aber Mitglied wird die Person erst mit ihrer
   * Zusage. Sonst genügte die Adresse irgendeiner registrierten Person, um sie
   * ohne ihr Wissen in den Verein zu ziehen und danach ihren Namen zu sehen.
   * Der Verein sieht bis dahin nur die Adresse, die er selbst eingetippt hat.
   */
  async function einladen() {
    const mail = neueMail.trim();
    if (!mail) return;
    setLadtEin(true);
    try {
      const ergebnis = await api.post<{ joined: boolean }>(`/api/clubs/${selectedClubId}/members`, { email: mail });
      // Hatte die Person selbst schon angefragt, nimmt der Server sie gleich auf -
      // dann wäre "eingeladen" falsch. Deshalb meldet er, was passiert ist.
      toast.success(
        ergebnis?.joined
          ? t("{mail} hatte schon angefragt und ist jetzt Mitglied.", { mail })
          : t("{mail} ist eingeladen. Sobald die Person annimmt, ist sie Mitglied.", { mail }),
      );
      setNeueMail("");
      // Beide Listen neu laden: Mitglied oder offene Einladung.
      await Promise.all([loadMembers(selectedClubId), loadInvitations(selectedClubId)]);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einladung fehlgeschlagen."));
    } finally {
      setLadtEin(false);
    }
  }

  async function zurueckziehen(einladung: ClubInvitation) {
    if (!window.confirm(t("Einladung an {mail} zurückziehen?", { mail: einladung.invitedEmail }))) return;
    setZieheZurueck(einladung.membershipId);
    try {
      await api.delete(`/api/clubs/${selectedClubId}/invitations/${einladung.membershipId}`);
      toast.success(t("Einladung zurückgezogen."));
      await loadInvitations(selectedClubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setZieheZurueck(null);
    }
  }

  // Der Server entscheidet (Ausbildungswart:in, Verwaltung) und liefert es mit dem Verein mit; wer
  // nur Mitglieder verwaltet, bekäme beim Antippen ohnehin "Verein nicht gefunden". Fehlt die Angabe, gilt: nein.
  const darfBefoerdern = clubs.find((c) => c.id === selectedClubId)?.canAppointTrainers === true;

  async function handlePromote(m: ClubMemberRequest) {
    // Die Rolle lässt sich hier nicht wieder entziehen - deshalb vorher fragen.
    const name = `${m.firstName} ${m.lastName}`.trim() || m.email;
    if (!window.confirm(t("{name} zum Trainer machen?", { name }))) return;
    const userId = m.userId;
    setPromotingUserId(userId);
    try {
      await api.post(`/api/clubs/${selectedClubId}/members/${userId}/promote`);
      toast.success(t("Mitglied ist jetzt Trainer."));
      // Neu laden, sonst bietet die Zeile weiter "Zum Trainer machen" an,
      // obwohl die Person es gerade geworden ist.
      await loadMembers(selectedClubId);
      onBefoerdert?.();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Beförderung fehlgeschlagen."));
    } finally {
      setPromotingUserId(null);
    }
  }

  async function entfernen(m: ClubMemberRequest) {
    const name = `${m.firstName} ${m.lastName}`.trim() || m.email;
    if (!window.confirm(t("{name} aus dem Verein entfernen?", { name }))) return;
    setEntferneUserId(m.userId);
    try {
      await api.delete(`/api/clubs/${selectedClubId}/members/${m.userId}`);
      toast.success(t("Mitglied entfernt."));
      await loadMembers(selectedClubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Entfernen fehlgeschlagen."));
    } finally {
      setEntferneUserId(null);
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2 space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <Users className="size-5" />
{t("Mitglieder")}
        </CardTitle>
        {clubs.length > 1 && (
          <Select value={selectedClubId} onValueChange={(value) => setSelectedClubId(value ?? "")}>
            <SelectTrigger className="w-48 max-w-full">
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
            void einladen();
          }}
        >
          <Input
            type="email"
            className="min-w-0 flex-1"
            placeholder={t("E-Mail-Adresse")}
            value={neueMail}
            onChange={(e) => setNeueMail(e.target.value)}
            disabled={!selectedClubId || ladtEin}
          />
          <Button type="submit" size="sm" disabled={!neueMail.trim() || ladtEin}>
            <UserPlus className="size-4" />
            {t("Einladen")}
          </Button>
        </form>

        {einladungen.length > 0 && (
          <div className="flex flex-col gap-2">
            <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">{t("Offene Einladungen")}</p>
            <ul className="flex flex-col gap-2">
              {einladungen.map((e) => (
                // Vor der Zusage kennt der Verein nur die eingegebene Adresse.
                <li
                  key={e.membershipId}
                  className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1 rounded-md border px-3 py-2"
                >
                  <div className="flex min-w-[12rem] flex-1 flex-col gap-0.5">
                    <span className="min-w-0 text-sm [overflow-wrap:anywhere]">{e.invitedEmail}</span>
                    <span className="text-xs text-muted-foreground">
                      {t("eingeladen am {datum}", { datum: new Date(e.requestedAt).toLocaleDateString(ort) })}
                    </span>
                  </div>
                  <Button
                    className="shrink-0"
                    size="sm"
                    variant="outline"
                    disabled={zieheZurueck === e.membershipId}
                    onClick={() => zurueckziehen(e)}
                  >
                    {t("Zurückziehen")}
                  </Button>
                </li>
              ))}
            </ul>
          </div>
        )}

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
                  darfBefoerdern && (
                    <Button
                      className="shrink-0"
                      size="sm"
                      variant="outline"
                      disabled={promotingUserId === m.userId}
                      onClick={() => handlePromote(m)}
                    >
                      <ShieldPlus className="size-4" />
{t("Zum Trainer machen")}
                    </Button>
                  )
                )}
                {/* Wer eine Rolle hat, bleibt, bis ihm die Rollen genommen sind (der Server weist anderes ab). */}
                {!m.hatRolle && (
                  <Button
                    className="shrink-0"
                    size="sm"
                    variant="ghost"
                    disabled={entferneUserId === m.userId}
                    onClick={() => entfernen(m)}
                  >
                    <UserMinus className="size-4" />
                    {t("Entfernen")}
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

"use client";

import { useState } from "react";
import { UserPlus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupDetail, GroupMember } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { GroupMemberCard } from "@/components/trainer/group-member-card";

/**
 * Ansicht "Mitglieder" einer Gruppe: Knopf zum Einladen (das Formular klappt
 * auf), offene Einladungen als schmale Zeilen, darunter die Mitglieder.
 *
 * Das Einladungsformular war vorher immer offen und stand vor den Mitgliedern -
 * dabei lädt man selten ein, schaut aber oft nach. Es ist nur dann von Anfang
 * an offen, wenn es noch nichts anderes zu sehen gibt.
 */
export function GroupMembersView({
  groupId,
  detail,
  onGeaendert,
}: {
  groupId: string;
  detail: GroupDetail;
  /** Nach jeder Änderung die Gruppe neu laden. */
  onGeaendert: () => Promise<void>;
}) {
  const t = useT();
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  // null = noch nicht angefasst: dann entscheidet, ob es sonst etwas zu sehen gibt.
  const [einladenOffen, setEinladenOffen] = useState<boolean | null>(null);
  const nochLeer = detail.members.length === 0 && detail.invitations.length === 0;
  const formularOffen = einladenOffen ?? nochLeer;

  async function handleAddMember(e: React.FormEvent) {
    e.preventDefault();
    if (!email.trim()) return;
    setSubmitting(true);
    try {
      await api.post(`/api/groups/${groupId}/members`, { email });
      // Eine Einladung, keine Aufnahme: Mitglied wird erst, wer annimmt -
      // sonst genügte eine fremde E-Mail-Adresse, um deren Hunde zu betreuen.
      toast.success(t("Einladung verschickt - die Person muss sie noch annehmen."));
      setEmail("");
      setEinladenOffen(false);
      await onGeaendert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Mitglied konnte nicht hinzugefügt werden."));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleRemoveMember(member: GroupMember) {
    const name = `${member.firstName} ${member.lastName}`.trim() || member.email;
    if (!window.confirm(t("{name} wirklich aus der Gruppe entfernen?", { name }))) return;
    try {
      await api.delete(`/api/groups/${groupId}/members/${member.userId}`);
      toast.success(t("Mitglied entfernt."));
      await onGeaendert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Mitglied konnte nicht entfernt werden."));
    }
  }

  async function handleWithdrawInvitation(memberId: string) {
    try {
      await api.delete(`/api/groups/${groupId}/members/${memberId}`);
      toast.success(t("Einladung zurückgezogen."));
      await onGeaendert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Button
        type="button"
        variant="outline"
        className="h-11 self-start"
        aria-expanded={formularOffen}
        aria-controls="mitglied-einladen"
        onClick={() => setEinladenOffen(!formularOffen)}
      >
        <UserPlus className="size-4" />
        {t("Mitglied einladen")}
      </Button>

      {formularOffen && (
        <Card id="mitglied-einladen">
          <CardContent>
            <form onSubmit={handleAddMember} className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex flex-col gap-2 sm:flex-1">
                <Label htmlFor="member-email">{t("E-Mail-Adresse")}</Label>
                <Input
                  id="member-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@example.com"
                  required
                />
              </div>
              <Button type="submit" className="h-11" disabled={submitting}>
                <UserPlus className="size-4" />
                {t("Einladen")}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {detail.invitations.length > 0 && (
        <Card className="gap-0 py-0">
          <ul className="divide-y">
            {detail.invitations.map((invite) => (
              <li key={invite.userId} className="flex min-w-0 items-center justify-between gap-2 px-4 py-2">
                {/* Vor der Zusage kennt die Gruppe nur die eingegebene Adresse. */}
                <div className="flex min-w-0 flex-col items-start gap-1">
                  <p className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">{invite.email}</p>
                  <Badge variant="secondary">{t("eingeladen")}</Badge>
                </div>
                <Button size="sm" variant="ghost" className="shrink-0 coarse:min-h-11" onClick={() => handleWithdrawInvitation(invite.userId)}>
                  {t("Zurückziehen")}
                </Button>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {detail.members.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            {t("Noch keine Mitglieder in dieser Gruppe.")}
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {detail.members.map((member) => (
            <GroupMemberCard key={member.userId} groupId={groupId} member={member} onRemove={handleRemoveMember} />
          ))}
        </div>
      )}
    </div>
  );
}

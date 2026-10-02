"use client";

import { useEffect, useState } from "react";
import { Trash2, UserPlus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { GroupDetail, GroupTrainerOption } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/**
 * Trainer:innen der Gruppe. Mehrere sind ausdrücklich erlaubt - wer hier
 * steht, darf die Gruppe genauso verwalten und kann daneben in beliebig vielen
 * anderen Gruppen eingetragen sein.
 *
 * Eingeladen, nicht eingetragen: Trainer:in wird, wer annimmt (unter "Vereine").
 * Die/den Hauptverantwortliche:n wechselt man nur bei Vereinsgruppen - nur dort
 * gibt es eine Liste, aus der man wählen kann.
 */
export function GroupTrainersSection({ groupId, detail, onGeaendert }: { groupId: string; detail: GroupDetail; onGeaendert: () => Promise<void> }) {
  const t = useT();
  const clubId = detail.group.clubId;
  const [trainers, setTrainers] = useState<GroupTrainerOption[]>([]);
  const [selectedTrainerId, setSelectedTrainerId] = useState("");
  const [assigning, setAssigning] = useState(false);
  const [coTrainerEmail, setCoTrainerEmail] = useState("");
  const [addingCoTrainer, setAddingCoTrainer] = useState(false);

  useEffect(() => {
    // Zuweisbare Trainer:innen (nur bei Vereinsgruppen) für die Auswahl laden.
    if (!clubId) return;
    api
      .get<GroupTrainerOption[]>(`/api/groups/${groupId}/trainers`)
      .then(setTrainers)
      .catch(() => {
        // Trainerliste ist optional - Fehler still schlucken.
      });
  }, [groupId, clubId]);

  async function assignTrainer() {
    if (!selectedTrainerId) return;
    setAssigning(true);
    try {
      await api.put(`/api/groups/${groupId}/trainer`, { trainerId: selectedTrainerId });
      toast.success(t("Trainer:in zugewiesen."));
      setSelectedTrainerId("");
      await onGeaendert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Trainer:in konnte nicht zugewiesen werden."));
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
      // Eine Einladung: Trainer:in wird, wer annimmt (unter "Vereine").
      toast.success(t("Einladung verschickt - die Person muss sie noch annehmen."));
      setCoTrainerEmail("");
      await onGeaendert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Trainer:in konnte nicht hinzugefügt werden."));
    } finally {
      setAddingCoTrainer(false);
    }
  }

  async function removeCoTrainer(userId: string, eingeladen: boolean) {
    try {
      await api.delete(`/api/groups/${groupId}/co-trainers/${userId}`);
      toast.success(eingeladen ? t("Einladung zurückgezogen.") : t("Trainer:in entfernt."));
      await onGeaendert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Trainer:in konnte nicht entfernt werden."));
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <ul className="flex flex-col gap-1.5">
        {detail.trainers.map((trainer) => (
          <li key={trainer.userId} className="flex items-center justify-between gap-2 rounded-md bg-muted/40 px-3 py-2">
            <div className="min-w-0">
              <p className="text-sm font-medium [overflow-wrap:anywhere]">
                {`${trainer.firstName} ${trainer.lastName}`.trim() || trainer.email}
              </p>
              {trainer.isInvited ? (
                <Badge variant="secondary" className="mt-0.5">
                  {t("eingeladen")}
                </Badge>
              ) : (
                <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{trainer.email}</p>
              )}
            </div>
            {trainer.isLead ? (
              <Badge variant="secondary" className="shrink-0">
                {t("Hauptverantwortlich")}
              </Badge>
            ) : (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="shrink-0"
                aria-label={
                  trainer.isInvited
                    ? t("Einladung an {email} zurückziehen", { email: trainer.email })
                    : t("{name} entfernen", { name: `${trainer.firstName} ${trainer.lastName}`.trim() || trainer.email })
                }
                title={trainer.isInvited ? t("Einladung zurückziehen") : t("Trainer:in entfernen")}
                onClick={() => removeCoTrainer(trainer.userId, trainer.isInvited)}
              >
                <Trash2 className="size-4 text-muted-foreground" />
              </Button>
            )}
          </li>
        ))}
      </ul>

      <form onSubmit={addCoTrainer} className="flex flex-col gap-2 sm:flex-row sm:items-end">
        <div className="flex flex-col gap-1.5 sm:flex-1">
          <Label htmlFor="cotrainer-email">{t("Weitere:n Trainer:in einladen")}</Label>
          <Input
            id="cotrainer-email"
            type="email"
            value={coTrainerEmail}
            onChange={(e) => setCoTrainerEmail(e.target.value)}
            placeholder="trainer@example.com"
          />
        </div>
        <Button type="submit" className="h-11" disabled={addingCoTrainer || !coTrainerEmail.trim()}>
          <UserPlus className="size-4" />
          {addingCoTrainer ? t("Lädt ein…") : t("Einladen")}
        </Button>
      </form>

      {clubId ? (
        <div className="flex flex-col gap-2 border-t pt-4 sm:flex-row sm:items-end">
          <div className="flex min-w-0 flex-col gap-1.5 sm:flex-1">
            <Label>{t("Hauptverantwortliche:n wechseln")}</Label>
            <Select value={selectedTrainerId} onValueChange={(v) => setSelectedTrainerId(v ?? "")}>
              <SelectTrigger>
                <SelectValue placeholder={t("Trainer:in wählen…")} />
              </SelectTrigger>
              <SelectContent>
                {trainers.map((option) => (
                  <SelectItem key={option.userId} value={option.userId}>
                    {`${option.firstName} ${option.lastName}`.trim() || option.email}
                    {option.userId === detail.group.trainerId ? ` ${t("(aktuell)")}` : ""}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button
            type="button"
            className="h-11"
            disabled={assigning || !selectedTrainerId || selectedTrainerId === detail.group.trainerId}
            onClick={assignTrainer}
          >
            {assigning ? t("Weise zu…") : t("Wechseln")}
          </Button>
        </div>
      ) : (
        <p className="text-xs text-muted-foreground">
          {t("Die/den Hauptverantwortliche:n zu wechseln geht nur bei Vereinsgruppen. Weitere Trainer:innen lassen sich überall einladen.")}
        </p>
      )}
    </div>
  );
}

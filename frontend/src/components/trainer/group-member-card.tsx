"use client";

import { useState } from "react";
import Link from "next/link";
import { ChevronDown, ChevronRight, Dog as DogIcon, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import type { GroupMember, MemberDog } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

/**
 * Ein Mitglied der Gruppe: Name und Adresse, aufgeklappt die Hunde mit
 * "betreuen" / "Betreuung beenden" und dem Sprung zum Hund.
 *
 * Die Hunde werden erst beim Aufklappen geladen und danach gemerkt - bei einer
 * großen Gruppe wären es sonst so viele Abrufe wie Mitglieder.
 */
export function GroupMemberCard({
  groupId,
  member,
  onRemove,
}: {
  groupId: string;
  member: GroupMember;
  /** Entfernen samt Rückfrage übernimmt die Liste. */
  onRemove: (member: GroupMember) => void;
}) {
  const t = useT();
  const { user } = useAuth();
  const [offen, setOffen] = useState(false);
  const [dogs, setDogs] = useState<MemberDog[] | null>(null);
  const [ladefehler, setLadefehler] = useState(false);
  const name = `${member.firstName} ${member.lastName}`.trim() || member.email;

  async function ladeHunde() {
    setDogs(await api.get<MemberDog[]>(`/api/groups/${groupId}/members/${member.userId}/dogs`));
  }

  async function umschalten() {
    setOffen(!offen);
    if (offen || dogs) return;
    await ersterAbruf();
  }

  async function ersterAbruf() {
    setLadefehler(false);
    try {
      await ladeHunde();
    } catch (err) {
      setLadefehler(true);
      toast.error(err instanceof ApiError ? err.message : t("Hunde konnten nicht geladen werden."));
    }
  }

  async function betreuungBeenden(dogId: string, dogName: string) {
    if (!window.confirm(t("Betreuung von {name} beenden? Du verlierst damit den Zugriff auf Tagebuch, Ziele und Trainingsplan.", { name: dogName }))) return;
    if (!user) return;
    try {
      await api.delete(`/api/groups/${groupId}/trainer-assignments/${user.userId}/${dogId}`);
      toast.success(t("Betreuung beendet."));
      await ladeHunde();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Betreuung konnte nicht beendet werden."));
    }
  }

  async function betreuen(dogId: string) {
    try {
      await api.post(`/api/groups/${groupId}/trainer-assignments`, { memberId: member.userId, dogId });
      toast.success(t("Du betreust diesen Hund jetzt."));
      await ladeHunde();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Zuordnung fehlgeschlagen."));
    }
  }

  return (
    <Card className="gap-0 py-0">
      <div className="flex items-center gap-1 pr-2">
        <button
          type="button"
          aria-expanded={offen}
          onClick={umschalten}
          className="flex min-h-14 min-w-0 flex-1 items-center justify-between gap-2 px-4 py-3 text-left"
        >
          <span className="min-w-0">
            <span className="block text-base font-medium [overflow-wrap:anywhere]">{`${member.firstName} ${member.lastName}`}</span>
            <span className="block text-sm text-muted-foreground [overflow-wrap:anywhere]">{member.email}</span>
          </span>
          {offen ? <ChevronDown className="size-5 shrink-0" /> : <ChevronRight className="size-5 shrink-0" />}
        </button>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="shrink-0"
          aria-label={t("{name} entfernen", { name })}
          title={t("Mitglied entfernen")}
          onClick={() => onRemove(member)}
        >
          <Trash2 className="size-4" />
        </Button>
      </div>
      {offen && (
        <CardContent className="pb-4">
          {!dogs && ladefehler ? (
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm text-muted-foreground">{t("Hunde konnten nicht geladen werden.")}</p>
              <Button type="button" size="sm" variant="outline" onClick={() => void ersterAbruf()}>
                {t("Erneut versuchen")}
              </Button>
            </div>
          ) : !dogs ? (
            <p className="text-sm text-muted-foreground">{t("Lädt Hunde…")}</p>
          ) : dogs.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("Dieses Mitglied hat noch keine Hunde angelegt.")}</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {dogs.map((dog) => (
                <li key={dog.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border px-3 py-2">
                  <div className="flex min-w-0 items-center gap-2">
                    <DogIcon className="size-4 shrink-0 text-primary-text" />
                    <span className="font-medium [overflow-wrap:anywhere]">{dog.name}</span>
                    {dog.breed && <span className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{dog.breed}</span>}
                  </div>
                  {dog.isTrainerAssigned ? (
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="secondary">{t("Betreut")}</Badge>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-xs text-muted-foreground coarse:min-h-11"
                        onClick={() => betreuungBeenden(dog.id, dog.name)}
                      >
                        {t("Betreuung beenden")}
                      </Button>
                      <Link
                        // ?from=: sonst führt der Zurück-Button auf der Hundeseite
                        // zu den EIGENEN Hunden statt hierher in die Gruppe zurück.
                        href={`/dogs/${dog.id}?from=${encodeURIComponent(`/trainer/${groupId}?ansicht=mitglieder`)}`}
                        className="inline-flex min-h-9 items-center text-sm text-primary-text underline coarse:min-h-11"
                      >
                        {t("Zum Hund")}
                      </Link>
                    </div>
                  ) : (
                    <Button size="sm" variant="outline" className="coarse:min-h-11" onClick={() => betreuen(dog.id)}>
                      {t("Als Trainer betreuen")}
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
}

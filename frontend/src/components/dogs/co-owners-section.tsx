"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { clearCachedData } from "@/lib/read-cache";
import type { DogOwner } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Plus, UserPlus, X } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";

/**
 * Mitbesitzer-Verwaltung eines Hundes (nur für Besitzer sichtbar, siehe
 * DogOwner). Aus der Hundeseite herausgelöst (TODO.md Roadmap 5b).
 *
 * Seit 2026-09-28 lädt "Hinzufügen" nur noch ein: Mitbesitzer:in wird, wer
 * annimmt (siehe HundEinladungen). Bis dahin steht die Person hier nur mit
 * der eingegebenen Adresse - ihren Namen erfährt man erst mit der Zusage.
 */
export function CoOwnersSection({
  dogId,
  dogName,
  owners,
  currentUserId,
  onChanged,
}: {
  dogId: string;
  dogName: string;
  owners: DogOwner[];
  currentUserId: string | undefined;
  onChanged: () => Promise<void>;
}) {
  const t = useT();
  const router = useRouter();
  const [ownerEmail, setOwnerEmail] = useState("");
  const [addingOwner, setAddingOwner] = useState(false);
  const [laeuft, setLaeuft] = useState<string | null>(null);

  const aktive = owners.filter((o) => !o.isInvited);
  const eingeladen = owners.filter((o) => o.isInvited);
  // Austreten nur, wenn danach noch jemand den Hund führt - das Backend
  // lehnt es sonst ohnehin ab.
  const kannAustreten = aktive.length > 1;

  async function handleAddOwner(e: FormEvent) {
    e.preventDefault();
    if (!ownerEmail.trim()) return;
    setAddingOwner(true);
    try {
      await api.post(`/api/dogs/${dogId}/owners`, { email: ownerEmail.trim() });
      toast.success(t("Einladung verschickt. Mitbesitzer:in wird die Person, sobald sie annimmt."));
      setOwnerEmail("");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Fehler beim Hinzufügen."));
    } finally {
      setAddingOwner(false);
    }
  }

  async function entfernen(o: DogOwner) {
    const frage = o.isInvited
      ? t("Einladung an {email} zurückziehen?", { email: o.email })
      : t("{name} als Mitbesitzer:in entfernen? {hund} verschwindet dann aus der Liste dieser Person.", {
          name: `${o.firstName} ${o.lastName}`.trim() || o.email,
          hund: dogName,
        });
    if (!window.confirm(frage)) return;
    setLaeuft(o.userId);
    try {
      await api.delete(`/api/dogs/${dogId}/owners/${o.userId}`);
      toast.success(o.isInvited ? t("Einladung zurückgezogen.") : t("Mitbesitzer entfernt."));
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Fehler beim Entfernen."));
    } finally {
      setLaeuft(null);
    }
  }

  async function austreten() {
    if (!currentUserId) return;
    if (
      !window.confirm(
        t("Mitbesitz an {hund} beenden? Du siehst den Hund danach nicht mehr - die anderen Besitzer:innen behalten ihn.", {
          hund: dogName,
        }),
      )
    )
      return;
    setLaeuft(currentUserId);
    try {
      await api.delete(`/api/dogs/${dogId}/owners/${currentUserId}`);
      // Die Seite dieses Hundes gibt es für mich danach nicht mehr - auch
      // nicht im Zwischenspeicher.
      await clearCachedData(`dog-page-${dogId}`);
      toast.success(t("Mitbesitz beendet."));
      router.push("/dogs");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
      setLaeuft(null);
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2 space-y-0">
        <UserPlus className="size-5 shrink-0 text-primary-text" />
        <div className="flex min-w-0 flex-col gap-1">
          <CardTitle className="text-base">{t("Mitbesitzer")}</CardTitle>
          <CardDescription>
            {t("Eingeladene werden Mitbesitzer:innen, sobald sie annehmen - dann mit denselben Rechten wie du.")}
          </CardDescription>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {(aktive.length > 0 || eingeladen.length > 0) && (
          <ul className="flex flex-col gap-2">
            {[...aktive, ...eingeladen].map((o) => (
              <li key={o.userId} className="flex items-center justify-between gap-2 text-sm">
                <span className="min-w-0 [overflow-wrap:anywhere]">
                  {o.isInvited ? (
                    <>
                      {o.email}{" "}
                      <Badge variant="secondary" className="ml-1 align-middle">
                        {t("eingeladen")}
                      </Badge>
                    </>
                  ) : (
                    <>
                      {o.firstName} {o.lastName}{" "}
                      <span className="text-xs text-muted-foreground">{o.email}</span>
                    </>
                  )}
                </span>
                {o.userId !== currentUserId && (
                  <Button
                    size="icon"
                    variant="ghost"
                    className="size-7 shrink-0"
                    disabled={laeuft === o.userId}
                    onClick={() => entfernen(o)}
                    aria-label={
                      o.isInvited
                        ? t("Einladung an {email} zurückziehen", { email: o.email })
                        : t("{name} entfernen", { name: `${o.firstName} ${o.lastName}`.trim() || o.email })
                    }
                  >
                    <X className="size-3.5" />
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={handleAddOwner} className="flex gap-2">
          <Input
            type="email"
            placeholder={t("E-Mail der Person, die du einlädst")}
            aria-label={t("E-Mail der Person, die du einlädst")}
            value={ownerEmail}
            onChange={(e) => setOwnerEmail(e.target.value)}
          />
          <Button type="submit" size="sm" disabled={addingOwner} className="shrink-0">
            <Plus className="size-4" />
            {t("Einladen")}
          </Button>
        </form>
        {kannAustreten && (
          <Button
            variant="ghost"
            size="sm"
            className="self-start text-destructive hover:text-destructive"
            disabled={laeuft === currentUserId}
            onClick={austreten}
          >
            {t("Mitbesitz beenden")}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}

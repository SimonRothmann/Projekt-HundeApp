"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { DogInvitation } from "@/lib/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Mail } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";

/**
 * Offene Einladungen, einen Hund mitzuverwalten.
 *
 * Mitbesitzer:in wird nur, wer hier annimmt - wie bei den Gruppen (siehe
 * MeineGruppenSection). Vorher genügte der einladenden Person die
 * E-Mail-Adresse: Der fremde Hund stand ungefragt in der eigenen Liste, und
 * die Besitzerliste verriet ihr den eigenen Namen.
 *
 * Die Einladung kommt zusätzlich als Benachrichtigung, die hierher führt.
 */
export function HundEinladungen({ onAngenommen }: { onAngenommen?: () => void }) {
  const t = useT();
  const [einladungen, setEinladungen] = useState<DogInvitation[]>([]);
  const [laeuft, setLaeuft] = useState<string | null>(null);

  async function laden() {
    try {
      setEinladungen(await api.get<DogInvitation[]>("/api/dogs/invitations"));
    } catch {
      // Ohne die Liste bleibt die Seite nutzbar - die Glocke zeigt die
      // Einladung ebenfalls.
      setEinladungen([]);
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
  }, []);

  async function antworten(e: DogInvitation, annehmen: boolean) {
    setLaeuft(e.dogId);
    try {
      await api.post(`/api/dogs/${e.dogId}/invitation/${annehmen ? "accept" : "decline"}`);
      toast.success(
        annehmen ? t("Du verwaltest {hund} jetzt mit.", { hund: e.dogName }) : t("Einladung abgelehnt."),
      );
      await laden();
      if (annehmen) onAngenommen?.();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setLaeuft(null);
    }
  }

  if (einladungen.length === 0) return null;

  return (
    <Card className="border-primary/40">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Mail className="size-5 shrink-0 text-primary-text" />
          {einladungen.length === 1 ? t("Einladung zu einem Hund") : t("Einladungen zu Hunden")}
        </CardTitle>
        <CardDescription>
          {t(
            "Nimmst du an, verwaltest du den Hund gleichberechtigt mit - du siehst Tagebuch, Ziele und Fährten, und die anderen sehen deinen Namen.",
          )}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {einladungen.map((e) => (
          <div key={e.dogId} className="flex flex-col gap-2 border-t pt-3 first:border-t-0 first:pt-0">
            <div className="min-w-0">
              <p className="font-medium [overflow-wrap:anywhere]">{e.dogName}</p>
              {e.invitedByName && (
                <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
                  {t("eingeladen von {name}", { name: e.invitedByName })}
                </p>
              )}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button size="sm" disabled={laeuft === e.dogId} onClick={() => antworten(e, true)}>
                {t("Annehmen")}
              </Button>
              <Button size="sm" variant="outline" disabled={laeuft === e.dogId} onClick={() => antworten(e, false)}>
                {t("Ablehnen")}
              </Button>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

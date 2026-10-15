"use client";

import { useState } from "react";
import { BellRing } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import type { PushZustand } from "@/lib/push";
import type { AdminTestpushErgebnis } from "@/lib/types";
import { usePush } from "@/lib/use-push";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * "Test-Push an mich": eine Meldung an alle eigenen Geräte mit Push - über
 * den normalen Weg einer Benachrichtigung, also genau so, wie sie sonst
 * auch ankommt. Dazu der Stand auf DIESEM Gerät, damit klar ist, ob es zu
 * den Empfängern gehört (und Push hier gleich eingeschaltet werden kann).
 */
export function TestpushSection() {
  const t = useT();
  const { refreshUnreadNotificationCount } = useAuth();
  const geraet = usePush();
  const [laeuft, setLaeuft] = useState(false);
  const [anzeige, setAnzeige] = useState<string | null>(null);

  async function senden() {
    setLaeuft(true);
    setAnzeige(null);
    try {
      const ergebnis = await api.post<AdminTestpushErgebnis>("/api/admin/testpush");
      if (!ergebnis.pushEingerichtet) {
        setAnzeige(t("Push ist auf dem Server nicht eingerichtet (VAPID-Schlüssel oder Frontend-Adresse fehlen) – es wurde nichts verschickt."));
      } else if (ergebnis.geraete === 0) {
        setAnzeige(t("Auf keinem deiner Geräte ist Push eingeschaltet – es wurde nichts verschickt."));
      } else {
        setAnzeige(
          ergebnis.geraete === 1
            ? t("An 1 Gerät geschickt. Die Mitteilung sollte in wenigen Sekunden erscheinen, dazu ein Eintrag in der Glocke.")
            : t("An {n} Geräte geschickt. Die Mitteilung sollte in wenigen Sekunden erscheinen, dazu ein Eintrag in der Glocke.", {
                n: ergebnis.geraete,
              }),
        );
        refreshUnreadNotificationCount();
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Der Test-Push konnte nicht gesendet werden."));
    } finally {
      setLaeuft(false);
    }
  }

  const geraetText = dieserGeraetText(geraet.zustand, t);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <BellRing className="size-5 shrink-0" />
          {t("Test-Push an mich")}
        </CardTitle>
        <CardDescription>
          {t("Schickt eine Meldung an alle deine Geräte, auf denen Push eingeschaltet ist – auf demselben Weg wie jede echte Benachrichtigung.")}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {geraetText && (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="min-w-0 flex-1 text-muted-foreground [overflow-wrap:anywhere]">{geraetText}</span>
            {geraet.zustand === "aus" && (
              <Button variant="outline" className="h-11" disabled={geraet.arbeitet} onClick={() => void geraet.einschalten()}>
                {t("Hier einschalten")}
              </Button>
            )}
          </div>
        )}
        <Button className="h-11 w-full sm:w-auto sm:self-start" disabled={laeuft} onClick={() => void senden()}>
          {laeuft ? t("Wird gesendet…") : t("Test-Push senden")}
        </Button>
        {anzeige && (
          <p role="status" className="text-sm break-words text-muted-foreground">
            {anzeige}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function dieserGeraetText(zustand: PushZustand | null, t: ReturnType<typeof useT>): string | null {
  switch (zustand) {
    case "an":
      return t("Dieses Gerät: Push ist eingeschaltet.");
    case "aus":
      return t("Dieses Gerät: Push ist aus.");
    case "verweigert":
      return t("Dieses Gerät: Mitteilungen sind in den Einstellungen blockiert.");
    case "ios-ohne-home-bildschirm":
      return t("Dieses Gerät: Push gibt es hier nur in Dogity vom Home-Bildschirm.");
    case "nicht-unterstuetzt":
      return t("Dieses Gerät unterstützt keine Push-Nachrichten.");
    default:
      // Noch nicht gelesen oder Push auf dem Server aus - das Ergebnis des
      // Sendens sagt dann, woran es liegt.
      return null;
  }
}

"use client";

import { useState } from "react";
import { BellRing } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { istTouchGeraet } from "@/lib/geraet";
import { useT } from "@/lib/i18n";
import { usePush } from "@/lib/use-push";

const SPEICHER_SCHLUESSEL = "dogity.push-hinweis.weggeklickt";

function warWeggeklickt(): boolean {
  try {
    return window.localStorage.getItem(SPEICHER_SCHLUESSEL) !== null;
  } catch {
    return false;
  }
}

function merkeWeggeklickt(): void {
  try {
    window.localStorage.setItem(SPEICHER_SCHLUESSEL, "1");
  } catch {
    // Ohne Speicher kommt der Hinweis beim nächsten Öffnen wieder - kein Schaden.
  }
}

/**
 * Bietet Push dort an, wo es alle sehen - nicht nur, wer die Neuerungen liest
 * oder im Profil stöbert. Mit einem Tipp eingeschaltet (die Frage nach der
 * Erlaubnis kommt direkt aus dem Tipp, wie Safari es verlangt).
 *
 * Nur wenn Push auf diesem Gerät geht und noch aus ist: Auf dem iPhone im
 * Browser übernimmt der Hinweis "Als App installieren" (pwa-install-prompt),
 * danach erscheint diese Karte in der App vom Home-Bildschirm. Einmal
 * weggetippt, kommt sie nicht wieder; die Glocke und das Profil bieten Push
 * weiter an. Nur auf Handy und Tablet ("aufs Handy") - am Rechner bleibt Push
 * über das Profil erreichbar.
 */
export function PushHinweisKarte() {
  const t = useT();
  const [mobil] = useState(() => typeof window !== "undefined" && istTouchGeraet());
  const { zustand, arbeitet, einschalten } = usePush(mobil);
  const [weggeklickt, setWeggeklickt] = useState(warWeggeklickt);

  if (!mobil || zustand !== "aus" || weggeklickt) return null;

  function nichtJetzt() {
    merkeWeggeklickt();
    setWeggeklickt(true);
  }

  return (
    <Card size="sm">
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-start gap-2">
          <BellRing className="mt-0.5 size-4 shrink-0 text-primary-text" aria-hidden />
          <div className="flex min-w-0 flex-1 flex-col gap-1">
            <p className="text-sm font-medium">{t("Mitteilungen aufs Handy")}</p>
            <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
              {t("Erfahre sofort von Einladungen, Feedback und Änderungen an Terminen – auch wenn Dogity geschlossen ist.")}
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button className="h-11 flex-1 sm:flex-none" disabled={arbeitet} onClick={() => void einschalten()}>
            {t("Einschalten")}
          </Button>
          <Button variant="ghost" className="h-11 flex-1 sm:flex-none" disabled={arbeitet} onClick={nichtJetzt}>
            {t("Nicht jetzt")}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

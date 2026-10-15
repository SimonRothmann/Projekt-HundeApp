"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { usePreferences } from "@/lib/preferences-context";
import { useSystemFeatures } from "@/lib/system-features";
import {
  bestimmePushZustand,
  lesePushUmgebung,
  schaltePushAus,
  schaltePushEin,
  type PushZustand,
} from "@/lib/push";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { SchalterZeile } from "@/components/ui/switch";
import { ListenGruppe } from "@/components/profile/einstellungs-liste";
import { BellRing } from "lucide-react";
import { toast } from "sonner";
import { useT } from "@/lib/i18n";

/**
 * Wie Dogity dich außerhalb der App erreicht: Push-Nachrichten auf diesem Gerät
 * und - wenn der Server Mails versendet - "Wichtiges auch per E-Mail".
 *
 * Beides erscheint nur, wenn der Server es eingerichtet hat
 * (lib/system-features.ts): Eine Einstellung für etwas, das nicht läuft, wäre
 * ein Versprechen ohne Gegenwert.
 */
export function BenachrichtigungenSection() {
  const features = useSystemFeatures();
  if (!features) return null;

  return (
    <>
      {features.pushPublicKey && <PushKarte publicKey={features.pushPublicKey} />}
      {features.email && <EmailZeile />}
    </>
  );
}

function PushKarte({ publicKey }: { publicKey: string }) {
  const t = useT();
  // null = wird noch ermittelt (der Browser antwortet asynchron).
  const [zustand, setZustand] = useState<PushZustand | null>(null);
  const [arbeitet, setArbeitet] = useState(false);

  useEffect(() => {
    // Zustand des Geräts lesen (externe Quelle: Browser). Gesetzt wird erst nach
    // der asynchronen Antwort, nicht im Rumpf des Effekts.
    let abgebrochen = false;
    void lesePushUmgebung(publicKey).then((umgebung) => {
      if (!abgebrochen) setZustand(bestimmePushZustand(umgebung));
    });
    return () => {
      abgebrochen = true;
    };
  }, [publicKey]);

  async function ermitteln() {
    setZustand(bestimmePushZustand(await lesePushUmgebung(publicKey)));
  }

  // Direkt aus dem Tipp, ohne vorherige Wartepunkte: iOS zeigt die Frage nach
  // der Erlaubnis nur nach einer Nutzergeste (siehe schaltePushEin).
  async function einschalten() {
    setArbeitet(true);
    try {
      const ergebnis = await schaltePushEin(publicKey);
      if (ergebnis === "erteilt") toast.success(t("Push-Nachrichten sind auf diesem Gerät eingeschaltet."));
      else if (ergebnis === "verweigert") toast.error(t("Die Erlaubnis für Mitteilungen wurde nicht erteilt."));
      else toast.error(t("Push-Nachrichten konnten nicht eingeschaltet werden."));
      await ermitteln();
    } finally {
      setArbeitet(false);
    }
  }

  async function ausschalten() {
    setArbeitet(true);
    try {
      const serverOk = await schaltePushAus();
      if (serverOk) toast.success(t("Push-Nachrichten sind auf diesem Gerät ausgeschaltet."));
      else toast.error(t("Das Gerät konnte nicht vollständig abgemeldet werden. Auf diesem Gerät ist Push aus."));
      await ermitteln();
    } finally {
      setArbeitet(false);
    }
  }

  // Solange unklar ist, was das Gerät kann, nichts anzeigen - ein kurz
  // aufblitzendes "nicht unterstützt" wäre falsch.
  if (zustand === null || zustand === "ausgeblendet") return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <BellRing className="size-5 shrink-0" />
          <span className="min-w-0">{t("Push-Benachrichtigungen auf diesem Gerät")}</span>
        </CardTitle>
        <CardDescription>
          {t("Neue Meldungen aus der Glocke kommen dann auch als Mitteilung aufs Gerät, selbst wenn Dogity geschlossen ist.")}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex min-w-0 flex-col gap-3">
        {zustand === "aus" && (
          <Button className="h-11 self-start" disabled={arbeitet} onClick={() => void einschalten()}>
            {t("Einschalten")}
          </Button>
        )}

        {zustand === "an" && (
          <>
            <p className="text-sm">{t("Auf diesem Gerät eingeschaltet.")}</p>
            <Button variant="outline" className="h-11 self-start" disabled={arbeitet} onClick={() => void ausschalten()}>
              {t("Ausschalten")}
            </Button>
          </>
        )}

        {zustand === "verweigert" && (
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
            {t(
              "Mitteilungen sind für Dogity blockiert. Erlaube sie in den Einstellungen deines Geräts oder Browsers (iPhone: Einstellungen → Mitteilungen → Dogity) und öffne diese Seite danach neu.",
            )}
          </p>
        )}

        {zustand === "ios-ohne-home-bildschirm" && (
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
            {t(
              "Auf dem iPhone kommen Push-Nachrichten nur, wenn du Dogity zum Home-Bildschirm hinzufügst (Teilen → Zum Home-Bildschirm).",
            )}
          </p>
        )}

        {zustand === "nicht-unterstuetzt" && (
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
            {t("Dieses Gerät oder dieser Browser unterstützt keine Push-Nachrichten.")}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function EmailZeile() {
  const t = useT();
  const { preferences, reload } = usePreferences();
  const [speichert, setSpeichert] = useState(false);

  async function umschalten(an: boolean) {
    setSpeichert(true);
    try {
      await api.put("/api/preferences/email-notifications", { enabled: an });
      await reload();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einstellung konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <ListenGruppe>
      <SchalterZeile
        an={preferences.emailNotifications}
        disabled={speichert}
        onChange={(an) => void umschalten(an)}
        titel={t("Wichtiges auch per E-Mail")}
        beschreibung={t("Einladungen und Entscheidungen über Beitrittsanfragen")}
      />
    </ListenGruppe>
  );
}

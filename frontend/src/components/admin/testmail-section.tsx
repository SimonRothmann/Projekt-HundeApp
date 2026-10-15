"use client";

import { useState } from "react";
import { MailCheck } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";
import type { AdminTestmailErgebnis } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type Anzeige = { art: "angenommen" | "aus" | "abgelehnt"; text: string };

/**
 * "Testmail senden": prüft den eigenen Versandserver (deploy/mail), ohne ein
 * Konto anzulegen oder ein Passwort zurückzusetzen. Der Server sendet sofort
 * und antwortet, ob der Mailserver die Mail angenommen hat - und wenn nicht,
 * mit dessen Antwort. Vorbelegt mit der eigenen Adresse.
 */
export function TestmailSection() {
  const t = useT();
  const { user } = useAuth();
  const [an, setAn] = useState(user?.email ?? "");
  const [laeuft, setLaeuft] = useState(false);
  const [anzeige, setAnzeige] = useState<Anzeige | null>(null);

  async function senden(e: React.FormEvent) {
    e.preventDefault();
    setLaeuft(true);
    setAnzeige(null);
    try {
      const ergebnis = await api.post<AdminTestmailErgebnis>("/api/admin/testmail", { an: an.trim() });
      if (!ergebnis.versandEingerichtet) {
        setAnzeige({
          art: "aus",
          text: t("Der Mailversand ist nicht eingerichtet, es wurde nichts verschickt. Eingeschaltet wird er in der .env auf dem Server: SMTP_HOST für Prod, TEST_SMTP_HOST für Test."),
        });
      } else if (ergebnis.versendet) {
        setAnzeige({
          art: "angenommen",
          text: t("Der Mailserver hat die Mail angenommen. Ob sie im Posteingang oder im Spam landet, zeigt das Postfach."),
        });
      } else {
        setAnzeige({
          art: "abgelehnt",
          text: `${t("Der Mailserver hat die Mail nicht angenommen:")} ${ergebnis.fehler ?? ""}`.trim(),
        });
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Die Testmail konnte nicht gesendet werden."));
    } finally {
      setLaeuft(false);
    }
  }

  const farbe =
    anzeige?.art === "angenommen" ? "text-emerald-700 dark:text-emerald-400" : anzeige?.art === "abgelehnt" ? "text-destructive" : "text-muted-foreground";

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <MailCheck className="size-5 shrink-0" />
          {t("Testmail senden")}
        </CardTitle>
        <CardDescription>
          {t("Prüft den Mailversand. Für eine Bewertung von SPF, DKIM und DMARC die Adresse von mail-tester.com eintragen.")}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <form onSubmit={senden} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex min-w-0 flex-col gap-2 sm:flex-1">
            <Label htmlFor="testmail-an">{t("Empfänger")}</Label>
            <Input
              id="testmail-an"
              type="email"
              inputMode="email"
              autoComplete="off"
              required
              value={an}
              onChange={(e) => setAn(e.target.value)}
            />
          </div>
          <Button type="submit" className="w-full sm:w-auto" disabled={laeuft || an.trim() === ""}>
            {laeuft ? t("Wird gesendet…") : t("Testmail senden")}
          </Button>
        </form>
        {anzeige && (
          <p role="status" className={`text-sm break-words ${farbe}`}>
            {anzeige.text}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

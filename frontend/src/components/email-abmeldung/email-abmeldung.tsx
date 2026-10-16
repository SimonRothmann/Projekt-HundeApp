"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { PawPrint } from "lucide-react";
import { ApiError, api } from "@/lib/api";
import { abmeldeFehlerArt, abmeldeTokenAus } from "@/lib/email-abmeldung";
import { useT } from "@/lib/i18n";
import { AuthBackLink } from "@/components/auth-back-link";
import { RechtlicheLinks } from "@/components/rechtliche-links";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

type Stand = "laedt" | "abgemeldet" | "angemeldet" | "ungueltig" | "nichtErreichbar";

/**
 * Die Seite hinter dem Abmelde-Link in den Mails - ohne Anmeldung.
 *
 * Meldet beim Laden SOFORT ab (ein POST, kein GET): Wer den Link anklickt, will
 * keine Mails mehr, und ein zweiter Klick auf "Abmelden" wäre eine Hürde, die
 * nur Mails kostet. Dass sich das mit "Wieder anmelden" zurücknehmen lässt, fängt
 * den Fehlklick ab. Link-Scanner rufen nur die Adresse ab und führen kein
 * Skript aus - sie lösen die Abmeldung nicht aus.
 */
export function EmailAbmeldung() {
  const t = useT();
  const token = abmeldeTokenAus(useSearchParams().get("t"));
  const [stand, setStand] = useState<Stand>("laedt");
  const [arbeitet, setArbeitet] = useState(false);
  const [fehler, setFehler] = useState<string | null>(null);
  // Für welches Token die Abmeldung schon losgeschickt wurde: Im Entwicklungsmodus
  // läuft der Effekt doppelt, und die Abmeldung soll nur einmal rausgehen.
  const gestartetFuer = useRef<string | null>(null);

  async function setze(aktiv: boolean) {
    try {
      await api.post("/api/email-abmeldung", { token, aktiv });
      setStand(aktiv ? "angemeldet" : "abgemeldet");
      setFehler(null);
    } catch (err) {
      const art = abmeldeFehlerArt(err instanceof ApiError ? err.status : null);
      // Beim Umschalten bleibt die Seite, wie sie ist - nur die Meldung kommt dazu.
      setStand((aktuell) => (art === "ungueltig" ? "ungueltig" : aktuell === "laedt" ? "nichtErreichbar" : aktuell));
      setFehler(art === "ungueltig" ? null : t("Das hat gerade nicht geklappt. Bitte versuche es gleich noch einmal."));
    } finally {
      setArbeitet(false);
    }
  }

  useEffect(() => {
    if (!token || gestartetFuer.current === token) return;
    gestartetFuer.current = token;
    // Nicht awaiten: Der Effekt selbst setzt keinen Zustand, das tut die Antwort.
    void setze(false);
    // setze liest nur token (und t); die Abmeldung gehört einmal je Token ausgelöst.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  // Ohne brauchbares Token gibt es nichts zu fragen.
  const aktuell: Stand = token ? stand : "ungueltig";

  function umschalten(aktiv: boolean) {
    setArbeitet(true);
    setFehler(null);
    void setze(aktiv);
  }

  function nochmal() {
    setStand("laedt");
    setFehler(null);
    void setze(false);
  }

  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-sm min-w-0 flex-col gap-3">
        <AuthBackLink />
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            <CardTitle className="text-xl">
              <h1>{t("E-Mails abbestellen")}</h1>
            </CardTitle>
          </CardHeader>
          <CardContent className="flex min-w-0 flex-col gap-4">
            {aktuell === "laedt" && (
              <p role="status" className="text-center text-sm text-muted-foreground">
                {t("Einen Moment…")}
              </p>
            )}

            {aktuell === "abgemeldet" && (
              <>
                <p role="status" className="break-words text-center text-sm">
                  {t("Du bekommst keine Benachrichtigungen mehr per E-Mail.")}
                </p>
                <p className="break-words text-center text-xs text-muted-foreground">
                  {t("Willkommens- und Passwort-Mails sind davon nicht betroffen.")}
                </p>
                <Button className="h-11" disabled={arbeitet} onClick={() => umschalten(true)}>
                  {t("Wieder anmelden")}
                </Button>
              </>
            )}

            {aktuell === "angemeldet" && (
              <>
                <p role="status" className="break-words text-center text-sm">
                  {t("Du bekommst wichtige Benachrichtigungen wieder per E-Mail.")}
                </p>
                <Button variant="outline" className="h-11" disabled={arbeitet} onClick={() => umschalten(false)}>
                  {t("Doch abmelden")}
                </Button>
              </>
            )}

            {aktuell === "ungueltig" && (
              <p role="alert" className="break-words text-center text-sm text-destructive">
                {t("Der Link ist ungültig oder abgelaufen. Melde dich an und stelle es unter Profil → Funktionen ein.")}
              </p>
            )}

            {aktuell === "nichtErreichbar" && (
              <>
                <p role="alert" className="break-words text-center text-sm text-destructive">
                  {t("Das hat gerade nicht geklappt. Bitte versuche es gleich noch einmal.")}
                </p>
                <Button className="h-11" onClick={nochmal}>
                  {t("Erneut versuchen")}
                </Button>
              </>
            )}

            {fehler && (aktuell === "abgemeldet" || aktuell === "angemeldet") && (
              <p role="alert" className="break-words text-center text-sm text-destructive">
                {fehler}
              </p>
            )}

            <p className="text-center text-sm text-muted-foreground">
              <Link href="/login" className="inline-flex items-center text-primary-text underline-offset-4 hover:underline coarse:min-h-11">
                {t("Zurück zur Anmeldung")}
              </Link>
            </p>
          </CardContent>
        </Card>
        <RechtlicheLinks />
      </div>
    </main>
  );
}

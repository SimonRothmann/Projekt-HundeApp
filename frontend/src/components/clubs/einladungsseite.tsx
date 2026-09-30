"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CalendarDays, MessageSquareText, NotebookPen, PawPrint } from "lucide-react";
import { ApiError, api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { beitrittMitMeldung, istGueltigerEinladungscode } from "@/lib/einladung";
import { useT } from "@/lib/i18n";
import type { ClubInvitePreview } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { RechtlicheLinks } from "@/components/rechtliche-links";

type Stand =
  | { art: "laedt" }
  | { art: "gueltig"; vereinsname: string }
  | { art: "ungueltig" }
  // Netz weg oder Server gerade nicht erreichbar - nicht dasselbe wie ein
  // abgelaufener Link, und der Link soll dabei nicht als ungültig erscheinen.
  | { art: "nichtErreichbar" };

/**
 * Öffentliche Einladungsseite eines Vereins, erreichbar ohne Konto.
 *
 * Sie zeigt nur den Vereinsnamen - der Server gibt ohne Anmeldung nicht mehr
 * heraus. Wer angemeldet ist, bekommt statt der Wege in die Anmeldung gleich
 * den Knopf zur Beitrittsanfrage.
 */
export function Einladungsseite({ code }: { code: string }) {
  const t = useT();
  const router = useRouter();
  const { user, isLoading } = useAuth();
  const formatOk = istGueltigerEinladungscode(code);
  const [stand, setStand] = useState<Stand>({ art: "laedt" });
  const [versuch, setVersuch] = useState(0);
  const [sendet, setSendet] = useState(false);

  useEffect(() => {
    // Ein Code, der nie einer sein konnte, braucht keine Anfrage.
    if (!formatOk) return;
    let abgebrochen = false;
    api
      .get<ClubInvitePreview>(`/api/clubs/invite/${code}`)
      .then((vorschau) => {
        if (!abgebrochen) setStand({ art: "gueltig", vereinsname: vorschau.clubName });
      })
      .catch((err) => {
        if (abgebrochen) return;
        // Nur 404 heißt "gilt nicht (mehr)". Ein 429 (zu viele Anfragen) oder
        // ein Serverfehler sagt nichts über den Link.
        setStand(err instanceof ApiError && err.status === 404 ? { art: "ungueltig" } : { art: "nichtErreichbar" });
      });
    return () => {
      abgebrochen = true;
    };
  }, [code, formatOk, versuch]);

  async function handleBeitritt() {
    setSendet(true);
    if (await beitrittMitMeldung(code, t)) router.push("/clubs");
    else setSendet(false);
  }

  const aktuell: Stand = formatOk ? stand : { art: "ungueltig" };

  return (
    <main className="flex min-h-full flex-1 items-center justify-center bg-muted/40 p-4">
      <div className="flex w-full max-w-sm min-w-0 flex-col gap-3">
        <Card className="w-full">
          <CardHeader className="items-center text-center">
            <PawPrint className="size-8 text-primary-text" />
            {aktuell.art === "gueltig" ? (
              <>
                <p className="text-sm text-muted-foreground">{t("Du bist eingeladen zu")}</p>
                <CardTitle className="text-xl [overflow-wrap:anywhere]">
                  <h1>{aktuell.vereinsname}</h1>
                </CardTitle>
              </>
            ) : (
              <CardTitle className="text-xl">
                <h1>Dogity</h1>
              </CardTitle>
            )}
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {aktuell.art === "laedt" && <p className="text-center text-muted-foreground">{t("Lädt…")}</p>}

            {aktuell.art === "ungueltig" && (
              <>
                <p className="text-center">{t("Dieser Einladungslink gilt nicht mehr. Frag im Verein nach einem neuen.")}</p>
                <Link href="/" className={cn(buttonVariants({ variant: "outline" }), "h-11")}>
                  {t("Zur Startseite")}
                </Link>
              </>
            )}

            {aktuell.art === "nichtErreichbar" && (
              <>
                <p className="text-center">
                  {t("Der Einladungslink konnte gerade nicht geprüft werden. Bitte versuche es gleich noch einmal.")}
                </p>
                <Button
                  variant="outline"
                  className="h-11"
                  onClick={() => {
                    setStand({ art: "laedt" });
                    setVersuch((v) => v + 1);
                  }}
                >
                  {t("Erneut versuchen")}
                </Button>
              </>
            )}

            {aktuell.art === "gueltig" && (
              <>
                <p className="text-center font-medium">{t("Dogity ist das Trainingstagebuch deines Vereins.")}</p>
                <ul className="flex flex-col gap-3 text-sm">
                  <li className="flex items-start gap-3">
                    <NotebookPen className="mt-0.5 size-5 shrink-0 text-primary-text" />
                    <span className="min-w-0 [overflow-wrap:anywhere]">
                      {t("Training nach BH/VT, IGP oder Agility festhalten")}
                    </span>
                  </li>
                  <li className="flex items-start gap-3">
                    <CalendarDays className="mt-0.5 size-5 shrink-0 text-primary-text" />
                    <span className="min-w-0 [overflow-wrap:anywhere]">{t("Termine deiner Gruppe sehen")}</span>
                  </li>
                  <li className="flex items-start gap-3">
                    <MessageSquareText className="mt-0.5 size-5 shrink-0 text-primary-text" />
                    <span className="min-w-0 [overflow-wrap:anywhere]">
                      {t("Rückmeldung von deinen Trainer:innen bekommen")}
                    </span>
                  </li>
                </ul>

                {isLoading ? null : user ? (
                  <Button className="h-11" disabled={sendet} onClick={handleBeitritt}>
                    {sendet ? t("Wird gesendet…") : t("Beitritt anfragen")}
                  </Button>
                ) : (
                  <div className="flex flex-col gap-2">
                    <Link
                      href={`/register?einladung=${encodeURIComponent(code)}`}
                      className={cn(buttonVariants(), "h-11")}
                    >
                      {t("Konto erstellen und beitreten")}
                    </Link>
                    <Link
                      href={`/login?einladung=${encodeURIComponent(code)}`}
                      className={cn(buttonVariants({ variant: "outline" }), "h-11")}
                    >
                      {t("Ich habe schon ein Konto")}
                    </Link>
                  </div>
                )}

                <p className="text-center text-xs text-muted-foreground">
                  {t("Der Verein bestätigt deine Anfrage, bevor du Mitglied wirst.")}
                </p>
              </>
            )}
          </CardContent>
        </Card>
        <RechtlicheLinks />
      </div>
    </main>
  );
}

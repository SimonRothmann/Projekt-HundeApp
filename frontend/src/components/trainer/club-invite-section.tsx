"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Copy, Link2Off, Printer, QrCode as QrIcon, RefreshCw, Share2, UserPlus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { einladungsUrl } from "@/lib/einladung";
import { useT } from "@/lib/i18n";
import type { Club, ClubInviteLink } from "@/lib/types";
import { cn } from "@/lib/utils";
import { kopiereText, teileLink } from "@/lib/zwischenablage";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { QrCode } from "@/components/ui/qr-code";

/**
 * Einladungslink und QR-Code der Vereine, die man als Trainer:in betreut.
 *
 * Eine Karte je Verein, unabhängig davon, ob Beitrittsanfragen offen sind
 * (anders als ClubJoinRequestsSection, die sich dann versteckt): Den Link
 * braucht man gerade, wenn noch niemand angefragt hat.
 */
export function ClubInviteSection({ clubs }: { clubs: Club[] }) {
  return (
    <>
      {clubs.map((club) => (
        <ClubInviteCard key={club.id} club={club} />
      ))}
    </>
  );
}

function ClubInviteCard({ club }: { club: Club }) {
  const t = useT();
  // undefined = wird geladen, null = es gibt keinen (oder abgeschalteten) Link.
  const [code, setCode] = useState<string | null | undefined>(undefined);
  const [arbeitet, setArbeitet] = useState(false);

  useEffect(() => {
    let abgebrochen = false;
    api
      // Ohne Link antwortet der Server mit 204 - die api-Hülle macht daraus undefined.
      .get<ClubInviteLink | undefined>(`/api/clubs/${club.id}/invite-link`)
      .then((antwort) => {
        if (!abgebrochen) setCode(antwort?.code ?? null);
      })
      .catch((err) => {
        if (abgebrochen) return;
        toast.error(err instanceof ApiError ? err.message : t("Einladungslink konnte nicht geladen werden."));
        setCode(null);
      });
    return () => {
      abgebrochen = true;
    };
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [club.id]);

  const link = code ? einladungsUrl(window.location.origin, code) : null;

  async function erzeugen() {
    setArbeitet(true);
    try {
      const antwort = await api.post<ClubInviteLink>(`/api/clubs/${club.id}/invite-link`);
      setCode(antwort.code);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einladungslink konnte nicht erstellt werden."));
    } finally {
      setArbeitet(false);
    }
  }

  async function neuErzeugen() {
    // Der alte Link ist danach tot: Aushänge und verschickte Nachrichten
    // laufen ins Leere. Das soll nie aus Versehen passieren.
    if (!window.confirm(t("Neuen Link erzeugen? Der bisherige Link und sein QR-Code werden sofort ungültig."))) return;
    await erzeugen();
  }

  async function abschalten() {
    if (!window.confirm(t("Link abschalten? Der QR-Code und der Link funktionieren danach nicht mehr."))) return;
    setArbeitet(true);
    try {
      await api.delete(`/api/clubs/${club.id}/invite-link`);
      setCode(null);
      toast.success(t("Einladungslink abgeschaltet."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Einladungslink konnte nicht abgeschaltet werden."));
    } finally {
      setArbeitet(false);
    }
  }

  async function kopieren() {
    if (!link) return;
    if (await kopiereText(link)) toast.success(t("Link kopiert."));
    else toast.error(t("Link konnte nicht kopiert werden. Markiere ihn und kopiere ihn von Hand."));
  }

  async function teilen() {
    if (!link) return;
    const ausgang = await teileLink({
      title: t("Einladung zu {verein}", { verein: club.name }),
      text: t("Komm zu {verein} in Dogity.", { verein: club.name }),
      url: link,
    });
    // Ohne Teilen-Menü (Desktop) bleibt das Kopieren.
    if (ausgang === "nichtVerfuegbar") await kopieren();
  }

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserPlus className="size-5 shrink-0" />
          {t("Neue Mitglieder einladen")}
        </CardTitle>
        <p className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">{club.name}</p>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          {t("Wer den Code scannt oder den Link öffnet, landet direkt bei der Beitrittsanfrage. Du gibst weiterhin jede Anfrage frei.")}
        </p>

        {code === undefined && <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>}

        {code === null && (
          <Button className="h-11 self-start" disabled={arbeitet} onClick={erzeugen}>
            <QrIcon className="size-4" />
            {t("Einladungslink erstellen")}
          </Button>
        )}

        {link && (
          <>
            <div className="flex justify-center">
              <QrCode
                wert={link}
                beschreibung={t("QR-Code mit dem Einladungslink zu {verein}", { verein: club.name })}
                className="w-44 border"
              />
            </div>

            <div className="flex min-w-0 flex-col gap-2">
              <p className="min-w-0 rounded-md border bg-muted/40 px-3 py-2 text-xs [overflow-wrap:anywhere]">{link}</p>
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" onClick={kopieren}>
                  <Copy className="size-4" />
                  {t("Link kopieren")}
                </Button>
                <Button variant="outline" onClick={teilen}>
                  <Share2 className="size-4" />
                  {t("Link teilen")}
                </Button>
                <Link
                  href={`/trainer/aushang/${club.id}`}
                  className={cn(buttonVariants({ variant: "outline" }))}
                >
                  <Printer className="size-4" />
                  {t("Aushang drucken")}
                </Link>
              </div>
              <div className="flex flex-wrap gap-2 border-t pt-3">
                <Button variant="ghost" disabled={arbeitet} onClick={neuErzeugen}>
                  <RefreshCw className="size-4" />
                  {t("Neuen Link erzeugen")}
                </Button>
                <Button variant="ghost" disabled={arbeitet} onClick={abschalten}>
                  <Link2Off className="size-4" />
                  {t("Link abschalten")}
                </Button>
              </div>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

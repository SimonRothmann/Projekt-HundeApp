"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Copy, Link2Off, Printer, QrCode as QrIcon, RefreshCw, Share2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { anmeldungsUrl } from "@/lib/anmeldung";
import { useT } from "@/lib/i18n";
import type { GroupRegistrationLink } from "@/lib/types";
import { cn } from "@/lib/utils";
import { kopiereText, teileLink } from "@/lib/zwischenablage";
import { Button, buttonVariants } from "@/components/ui/button";
import { QrCode } from "@/components/ui/qr-code";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";

/**
 * Anmeldelink und QR-Code einer Gruppe in einem Sheet: Kursteilnehmende ohne
 * Dogity-Konto melden sich damit an (z. B. zur Welpengruppe).
 *
 * Früher eine große Karte oben auf der Gruppenseite - den QR-Code braucht man
 * aber nur zum Zeigen oder Teilen, nicht bei jedem Besuch. Der Link wird erst
 * geladen, wenn das Sheet aufgeht. Und er bleibt für jede:n erreichbar, der die
 * Anmeldungen sieht: Gebraucht wird er gerade, wenn noch niemand angemeldet ist.
 */
export function RegistrationFormSheet({
  groupId,
  groupName,
  offen,
  onOffenChange,
}: {
  groupId: string;
  groupName: string;
  offen: boolean;
  onOffenChange: (offen: boolean) => void;
}) {
  const t = useT();
  return (
    <Sheet open={offen} onOpenChange={onOffenChange}>
      <SheetContent side="bottom" className="max-h-[90vh] overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{t("Anmeldelink & QR-Code")}</SheetTitle>
          <SheetDescription>
            {t("Wer den Code scannt oder den Link öffnet, kann sich ohne Dogity-Konto zur Gruppe anmelden. Die Anmeldungen erscheinen in der Liste.")}
          </SheetDescription>
        </SheetHeader>
        <Inhalt groupId={groupId} groupName={groupName} />
      </SheetContent>
    </Sheet>
  );
}

function Inhalt({ groupId, groupName }: { groupId: string; groupName: string }) {
  const t = useT();
  // undefined = wird geladen, null = es gibt keinen (oder geschlossenen) Link.
  const [code, setCode] = useState<string | null | undefined>(undefined);
  const [arbeitet, setArbeitet] = useState(false);

  useEffect(() => {
    let abgebrochen = false;
    api
      // Ohne Link antwortet der Server mit 204 - die api-Hülle macht daraus undefined.
      .get<GroupRegistrationLink | undefined>(`/api/groups/${groupId}/registration-link`)
      .then((antwort) => {
        if (!abgebrochen) setCode(antwort?.code ?? null);
      })
      .catch((err) => {
        if (abgebrochen) return;
        toast.error(err instanceof ApiError ? err.message : t("Anmeldeformular konnte nicht geladen werden."));
        setCode(null);
      });
    return () => {
      abgebrochen = true;
    };
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  const link = code ? anmeldungsUrl(window.location.origin, code) : null;

  async function erzeugen() {
    setArbeitet(true);
    try {
      const antwort = await api.post<GroupRegistrationLink>(`/api/groups/${groupId}/registration-link`);
      setCode(antwort.code);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Anmeldeformular konnte nicht erstellt werden."));
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

  async function schliessen() {
    if (!window.confirm(t("Formular schließen? Der Link und der QR-Code funktionieren danach nicht mehr. Die bisherigen Anmeldungen bleiben."))) return;
    setArbeitet(true);
    try {
      await api.delete(`/api/groups/${groupId}/registration-link`);
      setCode(null);
      toast.success(t("Anmeldeformular geschlossen."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Anmeldeformular konnte nicht geschlossen werden."));
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
      title: t("Anmeldung zur {gruppe}", { gruppe: groupName }),
      text: t("Hier kannst du dich zur {gruppe} anmelden.", { gruppe: groupName }),
      url: link,
    });
    // Ohne Teilen-Menü (Desktop) bleibt das Kopieren.
    if (ausgang === "nichtVerfuegbar") await kopieren();
  }

  return (
    <div className="flex min-w-0 flex-col gap-4 px-4 pb-4">
      {code === undefined && <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>}

      {code === null && (
        <Button className="h-11 self-start" disabled={arbeitet} onClick={erzeugen}>
          <QrIcon className="size-4" />
          {t("Anmeldeformular erstellen")}
        </Button>
      )}

      {link && (
        <>
          <div className="flex justify-center">
            <QrCode
              wert={link}
              beschreibung={t("QR-Code mit dem Anmeldelink zu {gruppe}", { gruppe: groupName })}
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
              <Link href={`/trainer/aushang/gruppe/${groupId}`} className={cn(buttonVariants({ variant: "outline" }))}>
                <Printer className="size-4" />
                {t("Aushang drucken")}
              </Link>
            </div>
            <div className="flex flex-wrap gap-2 border-t pt-3">
              <Button variant="ghost" disabled={arbeitet} onClick={neuErzeugen}>
                <RefreshCw className="size-4" />
                {t("Neuen Link erzeugen")}
              </Button>
              <Button variant="ghost" disabled={arbeitet} onClick={schliessen}>
                <Link2Off className="size-4" />
                {t("Formular schließen")}
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

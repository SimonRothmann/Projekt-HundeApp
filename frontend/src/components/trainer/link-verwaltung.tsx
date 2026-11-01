"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Copy, Link2Off, Printer, QrCode as QrIcon, RefreshCw, Share2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { kopiereText, teileLink } from "@/lib/zwischenablage";
import { Button, buttonVariants } from "@/components/ui/button";
import { QrCode } from "@/components/ui/qr-code";

/** Die Wortlaute, in denen sich Einladungslink (Verein) und Anmeldelink (Gruppe) unterscheiden - schon übersetzt. */
export type LinkTexte = {
  /** Knopf, wenn es noch keinen Link gibt. */
  erstellen: string;
  ladenFehler: string;
  erstellenFehler: string;
  /** Rückfrage vor dem Abschalten. */
  abschaltenFrage: string;
  /** Knopf zum Abschalten. */
  abschalten: string;
  abschaltenErfolg: string;
  abschaltenFehler: string;
  qrBeschreibung: string;
  teilenTitel: string;
  teilenText: string;
};

/**
 * Die Verwaltung eines Links mit QR-Code, wie ihn Vereine (Einladung) und
 * Gruppen (Anmeldung) haben: laden, erstellen, kopieren, teilen, Aushang
 * drucken, neu erzeugen, abschalten. Beide bieten denselben Ablauf; nur
 * Endpunkt, Adressbau, Aushang und Wortlaut unterscheiden sich. Die Hülle
 * (Karte oder Fenster) liefert die Aufrufer:in.
 *
 * `pfad` ist der Endpunkt für GET (Link lesen), POST (erzeugen bzw. neu
 * erzeugen) und DELETE (abschalten). Wechselt er, lädt die Verwaltung neu.
 */
export function LinkVerwaltung({
  pfad,
  linkBauen,
  aushangPfad,
  texte,
}: {
  pfad: string;
  linkBauen: (origin: string, code: string) => string;
  aushangPfad: string;
  texte: LinkTexte;
}) {
  const t = useT();
  // undefined = wird geladen, null = es gibt keinen (oder abgeschalteten) Link.
  const [code, setCode] = useState<string | null | undefined>(undefined);
  const [arbeitet, setArbeitet] = useState(false);

  useEffect(() => {
    let abgebrochen = false;
    api
      // Ohne Link antwortet der Server mit 204 - die api-Hülle macht daraus undefined.
      .get<{ code: string } | undefined>(pfad)
      .then((antwort) => {
        if (!abgebrochen) setCode(antwort?.code ?? null);
      })
      .catch((err) => {
        if (abgebrochen) return;
        toast.error(err instanceof ApiError ? err.message : texte.ladenFehler);
        setCode(null);
      });
    return () => {
      abgebrochen = true;
    };
    // texte bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pfad]);

  const link = code ? linkBauen(window.location.origin, code) : null;

  async function erzeugen() {
    setArbeitet(true);
    try {
      const antwort = await api.post<{ code: string }>(pfad);
      setCode(antwort.code);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : texte.erstellenFehler);
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
    if (!window.confirm(texte.abschaltenFrage)) return;
    setArbeitet(true);
    try {
      await api.delete(pfad);
      setCode(null);
      toast.success(texte.abschaltenErfolg);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : texte.abschaltenFehler);
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
    const ausgang = await teileLink({ title: texte.teilenTitel, text: texte.teilenText, url: link });
    // Ohne Teilen-Menü (Desktop) bleibt das Kopieren.
    if (ausgang === "nichtVerfuegbar") await kopieren();
  }

  return (
    <>
      {code === undefined && <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>}

      {code === null && (
        <Button className="h-11 self-start" disabled={arbeitet} onClick={erzeugen}>
          <QrIcon className="size-4" />
          {texte.erstellen}
        </Button>
      )}

      {link && (
        <>
          <div className="flex justify-center">
            <QrCode wert={link} beschreibung={texte.qrBeschreibung} className="w-44 border" />
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
              <Link href={aushangPfad} className={buttonVariants({ variant: "outline" })}>
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
                {texte.abschalten}
              </Button>
            </div>
          </div>
        </>
      )}
    </>
  );
}

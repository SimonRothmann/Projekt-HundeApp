"use client";

import Link from "next/link";
import { Printer } from "lucide-react";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { QrCode } from "@/components/ui/qr-code";

/**
 * Meldung statt Aushang: kein Link, kein Zugriff, Ladefehler. Eine Stelle für
 * beide Aushänge (Vereinseinladung und Gruppenanmeldung).
 */
export function AushangKeinInhalt({ text }: { text: string }) {
  const t = useT();
  return (
    <div className="mx-auto flex max-w-md flex-col gap-4 py-10 text-center">
      <p>{text}</p>
      <Link href="/trainer" className="text-primary-text underline-offset-4 hover:underline">
        {t("Zur Trainer-Übersicht")}
      </Link>
    </div>
  );
}

/**
 * Aushang für das Vereinsheim: QR-Code, drei Schritte, Link als Text. Die Seiten
 * dazu laden ihren Link selbst und prüfen die Rechte (das entscheidet der
 * Server); das Blatt hier ist für alle Aushänge dasselbe.
 *
 * Das Blatt ist bewusst fest schwarz auf weiß (nicht themenabhängig): Im
 * dunklen Erscheinungsbild druckte sonst helle Schrift auf weißes Papier, und
 * ein QR-Code braucht ohnehin hellen Grund. Auf dem Bildschirm dient es
 * zugleich als Vorschau.
 */
export function AushangAnsicht({
  seitentitel,
  name,
  ueberschrift,
  schritte,
  link,
  qrBeschreibung,
}: {
  /** Überschrift der Bildschirmansicht (wird nicht mitgedruckt). */
  seitentitel: string;
  /** Verein bzw. Gruppe - die erste Zeile auf dem Blatt. */
  name: string;
  /** Die große Zeile auf dem Blatt. */
  ueberschrift: string;
  schritte: string[];
  link: string;
  qrBeschreibung: string;
}) {
  const t = useT();
  return (
    <div className="mx-auto flex max-w-2xl min-w-0 flex-col gap-6 print:max-w-none">
      <div className="flex flex-wrap items-center justify-between gap-2 print:hidden">
        <h1 className="min-w-0 text-2xl font-semibold tracking-tight [overflow-wrap:anywhere]">{seitentitel}</h1>
        <Button onClick={() => window.print()}>
          <Printer className="size-4" />
          {t("Drucken")}
        </Button>
      </div>

      <section className="flex min-w-0 flex-col items-center gap-6 rounded-xl border bg-white p-6 text-center text-black print:rounded-none print:border-0 print:p-0">
        <p className="text-lg font-semibold [overflow-wrap:anywhere]">{name}</p>
        <h2 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">{ueberschrift}</h2>

        {/* Gedruckt mindestens 10 cm (Scanner brauchen Reserve, wenn der Aushang
            in einigen Metern Abstand hängt); auf dem Bildschirm so groß wie
            der Platz, aber nie breiter als das Blatt - auch bei 375 px. */}
        <QrCode wert={link} ecc="Q" beschreibung={qrBeschreibung} className="w-72 max-w-full print:w-[10cm]" />

        <ol className="flex w-full max-w-md flex-col gap-3 text-left text-lg">
          {schritte.map((schritt, index) => (
            <li key={index} className="flex items-start gap-3">
              <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-black font-bold text-white">
                {index + 1}
              </span>
              <span className="min-w-0 pt-0.5 [overflow-wrap:anywhere]">{schritt}</span>
            </li>
          ))}
        </ol>

        <p className="w-full text-sm [overflow-wrap:anywhere]">{link}</p>
      </section>
    </div>
  );
}

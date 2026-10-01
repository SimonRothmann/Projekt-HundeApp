import type { Metadata } from "next";
import { Anmeldeformular } from "@/components/anmeldung/anmeldeformular";

type Params = { params: Promise<{ code: string }> };

/**
 * Die Seite hinter dem QR-Code einer Gruppe: das Anmeldeformular.
 *
 * Nicht in den Suchindex und ohne Referrer: Der Code in der Adresse ist der
 * Schlüssel zum Formular. Landete die Adresse in einer Suchmaschine, könnte
 * jede:r das Formular finden (und vollschreiben); würde sie beim Anklicken
 * eines Verweises (etwa zur Datenschutzerklärung) an eine fremde Seite
 * weitergereicht, wäre der Link nicht mehr nur für die gedacht, die ihn vom
 * Verein bekommen haben. Dasselbe setzt next.config.ts zusätzlich als
 * Kopfzeile - die Meta-Angabe hier ist der zweite Gurt (wie bei /v/{code}).
 */
export const metadata: Metadata = {
  title: "Anmeldung zur Gruppe",
  robots: { index: false, follow: false },
  referrer: "no-referrer",
};

export default async function AnmeldungPage({ params }: Params) {
  const { code } = await params;
  return <Anmeldeformular code={code} />;
}

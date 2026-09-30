import type { Metadata } from "next";
import { Einladungsseite } from "@/components/clubs/einladungsseite";

type Params = { params: Promise<{ code: string }> };

/**
 * Die Seite hinter dem QR-Code eines Vereins.
 *
 * Nicht in den Suchindex und ohne Referrer: Der Code in der Adresse ist der
 * Schlüssel zur Beitrittsanfrage. Landete die Adresse in einer Suchmaschine
 * oder würde sie beim Anklicken eines Verweises an eine fremde Seite
 * weitergereicht, wäre der Link nicht mehr nur für die gedacht, die ihn vom
 * Verein bekommen haben. Dasselbe setzt next.config.ts zusätzlich als
 * Kopfzeile - die Meta-Angabe hier ist der zweite Gurt, falls die Seite
 * einmal ohne diese Konfiguration ausgeliefert wird.
 */
export const metadata: Metadata = {
  title: "Einladung zum Verein",
  robots: { index: false, follow: false },
  referrer: "no-referrer",
};

export default async function EinladungPage({ params }: Params) {
  const { code } = await params;
  return <Einladungsseite code={code} />;
}

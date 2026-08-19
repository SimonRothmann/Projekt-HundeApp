import type { Metadata } from "next";
import { AppShell } from "./app-shell";

/**
 * Server-Hülle um die eingeloggte App - einziger Zweck: Suchmaschinen
 * fernhalten.
 *
 * Diese Seiten liefern ohne Anmeldung nur ein leeres Gerüst. Landeten sie im
 * Index, stünden dort lauter inhaltsleere Treffer; Google wertet eine Domain
 * mit vielen solchen Seiten insgesamt ab. Die Angabe hier ergänzt robots.txt:
 * jene verhindert das Abrufen, diese das Aufnehmen bereits bekannter Adressen.
 */
export const metadata: Metadata = {
  robots: { index: false, follow: false },
};

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return <AppShell>{children}</AppShell>;
}

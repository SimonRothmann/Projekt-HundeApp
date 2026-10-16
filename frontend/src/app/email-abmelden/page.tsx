import type { Metadata } from "next";
import { Suspense } from "react";
import { EmailAbmeldung } from "@/components/email-abmeldung/email-abmeldung";

/**
 * Die Seite hinter dem Abmelde-Link in den Mails (/email-abmelden?t=...).
 *
 * Nicht in den Suchindex und ohne Referrer: Das Token in der Adresse ist der
 * Schlüssel zur Einstellung eines Kontos. Dasselbe setzt next.config.ts
 * zusätzlich als Kopfzeile - die Meta-Angabe hier ist der zweite Gurt (wie bei
 * /anmeldung/{code}).
 */
export const metadata: Metadata = {
  title: "E-Mails abbestellen",
  robots: { index: false, follow: false },
  referrer: "no-referrer",
};

export default function EmailAbmeldenPage() {
  // useSearchParams braucht eine Suspense-Grenze, sonst ließe sich die Seite nicht vorab bauen.
  return (
    <Suspense>
      <EmailAbmeldung />
    </Suspense>
  );
}

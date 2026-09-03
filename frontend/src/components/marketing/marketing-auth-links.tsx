"use client";

import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useAuth } from "@/lib/auth-context";
import { useT } from "@/lib/i18n";

/**
 * Die beiden Anmelde-Verweise in der Kopfzeile der öffentlichen Seiten -
 * ausgetauscht, sobald jemand angemeldet ist.
 *
 * Nötig geworden mit der Sachkunde: die Seite ist öffentlich, wird aber auch
 * aus der App heraus aufgerufen. Wer eingeloggt ist und dort "Anmelden" liest,
 * hält die App für kaputt.
 *
 * Bewusst nur dieser kleine Ausschnitt als Client-Komponente: die Kopfzeile
 * selbst bleibt serverseitig gerendert, und Suchmaschinen sehen den
 * abgemeldeten Zustand - für sie ist er der richtige.
 */
export function MarketingAuthLinks() {
  const { user, isLoading } = useAuth();
  const t = useT();

  if (isLoading || !user) {
    return (
      <>
        <Link href="/login" className={cn(buttonVariants({ variant: "ghost", size: "sm" }))}>
          Anmelden
        </Link>
        <Link href="/register" className={cn(buttonVariants({ size: "sm" }))}>
          Kostenlos starten
        </Link>
      </>
    );
  }

  // Nur dieser Zweig wird übersetzt, die beiden darüber nicht: "Anmelden"
  // und "Kostenlos starten" stehen auf den öffentlichen Seiten, und die
  // sind deutsch. Diesen Knopf sieht dagegen nur, wer angemeldet ist - also
  // genau die Person, deren Spracheinstellung gilt. Ein Suchmaschinen-
  // Besucher ist nie angemeldet und kommt hier nie vorbei.
  return (
    <Link href="/dashboard" className={cn(buttonVariants({ size: "sm" }))}>
      {t("Zur App")}
    </Link>
  );
}

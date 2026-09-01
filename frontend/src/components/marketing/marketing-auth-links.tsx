"use client";

import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useAuth } from "@/lib/auth-context";

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

  return (
    <Link href="/dashboard" className={cn(buttonVariants({ size: "sm" }))}>
      Zur App
    </Link>
  );
}

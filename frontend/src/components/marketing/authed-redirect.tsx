"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";

/**
 * Schickt bereits angemeldete Besucher von der Startseite in die App.
 *
 * Die Startseite war früher NUR diese Weiterleitung und lieferte damit an
 * Suchmaschinen eine komplett leere Seite aus. Jetzt steht echter Inhalt im
 * HTML - der wird serverseitig gerendert, die Weiterleitung passiert erst im
 * Browser. Suchmaschinen führen sie nicht aus und sehen deshalb die Inhalte.
 */
export function AuthedRedirect() {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (isLoading || !user) return;
    router.replace("/dashboard");
  }, [isLoading, user, router]);

  return null;
}

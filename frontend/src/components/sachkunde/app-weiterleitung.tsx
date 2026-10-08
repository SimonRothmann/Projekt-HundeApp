"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { sachkundeWeiterleitung } from "@/lib/sachkunde-weiterleitung";

/**
 * Schickt Angemeldete von den öffentlichen Sachkunde-Seiten in die App
 * (/lernen), wo die untere Leiste bleibt. Gäste sehen die Seite unverändert.
 * Die Entscheidung selbst steht in lib/sachkunde-weiterleitung.ts.
 *
 * `replace` statt `push`: Der Zurück-Knopf soll nicht auf die öffentliche
 * Seite führen, die sofort wieder weiterleitet.
 */
export function SachkundeAppWeiterleitung() {
  const { user, isLoading } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  const ziel = sachkundeWeiterleitung(pathname ?? "", user !== null, isLoading);

  useEffect(() => {
    if (ziel) router.replace(ziel);
  }, [ziel, router]);

  return null;
}

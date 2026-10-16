"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";

/**
 * Impressum und Datenschutz als schmale Zeile.
 *
 * § 5 DDG verlangt, dass die Anbieterkennzeichnung "leicht erkennbar,
 * unmittelbar erreichbar und ständig verfügbar" ist - in der Praxis: von
 * jeder Seite aus in höchstens zwei Schritten. Die öffentlichen Seiten haben
 * dafür ihre Fußzeile; die Anmeldeseiten haben keine, sie bestehen nur aus
 * einer Karte in der Bildmitte. Ohne diese Zeile wäre ausgerechnet die Seite,
 * auf der jemand seine Daten eingibt, die einzige ohne Rechtsangaben.
 *
 * Nur die Beschriftung folgt der Sprache ("Legal notice" / "Privacy policy");
 * Impressum und Datenschutzerklärung selbst sind deutsche Rechtstexte.
 */
export function RechtlicheLinks({ className }: { className?: string }) {
  const t = useT();

  return (
    <p className={cn("flex flex-wrap justify-center gap-x-4 gap-y-1 text-xs text-muted-foreground", className)}>
      <Link href="/impressum" className="transition-colors hover:text-foreground">
        {t("Impressum")}
      </Link>
      <Link href="/datenschutz" className="transition-colors hover:text-foreground">
        {t("Datenschutz")}
      </Link>
    </p>
  );
}

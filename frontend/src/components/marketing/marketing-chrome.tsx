"use client";

import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SITE } from "@/lib/seo";
import { MarketingAuthLinks, MarketingFooterAuthLinks } from "@/components/marketing/marketing-auth-links";
import { VersionStand } from "@/components/version-stand";
import { SprachUmschalter } from "@/components/sprach-umschalter";
import { useT } from "@/lib/i18n";

/**
 * Kopf- und Fußzeile der öffentlichen Seiten. Die Verweise sind nicht nur
 * Navigation: Suchmaschinen finden Unterseiten über Verweise, eine Seite ohne
 * eingehenden Link wird kaum beachtet.
 *
 * Client-Komponenten, damit die Beschriftungen der gewählten Sprache folgen. Das
 * ausgelieferte HTML bleibt trotzdem deutsch (der Provider startet mit der
 * Vorgabe) - Suchmaschinen sehen also dieselbe Fassung wie vorher.
 */
export function MarketingHeader() {
  const t = useT();

  return (
    <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-3 px-4">
        <Link href="/" className="text-lg font-extrabold tracking-tight text-primary-text">
          {SITE.name}
        </Link>
        <nav className="flex items-center gap-1 text-sm">
          <Link
            href="/sachkunde"
            className="hidden text-sm font-medium text-muted-foreground transition-colors hover:text-foreground sm:inline"
          >
            {t("Sachkunde")}
          </Link>
          <Link
            href="/pruefungsordnungen"
            className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "hidden sm:inline-flex")}
          >
            {t("Prüfungsordnungen")}
          </Link>
          <SprachUmschalter />
          <MarketingAuthLinks />
        </nav>
      </div>
    </header>
  );
}

export function MarketingFooter() {
  const t = useT();

  return (
    <footer className="mt-16 border-t border-border/60 py-10">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-4 px-4 text-sm text-muted-foreground">
        <nav className="flex flex-wrap gap-x-5 gap-y-2">
          <Link href="/" className="hover:text-foreground">
            {t("Startseite")}
          </Link>
          <Link href="/sachkunde" className="hover:text-foreground">
            {t("Sachkunde")}
          </Link>
          <Link href="/pruefungsordnungen" className="hover:text-foreground">
            {t("Prüfungsordnungen")}
          </Link>
          <MarketingFooterAuthLinks />
          <Link href="/neuerungen" className="hover:text-foreground">
            {t("Neuerungen")}
          </Link>
          {/* Pflichtangaben. Sie müssen von jeder Seite aus in höchstens zwei
              Schritten erreichbar sein (§ 5 DDG: "leicht erkennbar, unmittelbar
              erreichbar und ständig verfügbar") - deshalb stehen sie hier und
              zusätzlich unter den Anmeldeformularen und im Profil. */}
          <Link href="/impressum" className="hover:text-foreground">
            {t("Impressum")}
          </Link>
          <Link href="/datenschutz" className="hover:text-foreground">
            {t("Datenschutz")}
          </Link>
        </nav>
        <p className="[overflow-wrap:anywhere]">
          {t("{name} – Trainingstagebuch und Vereinsplattform für den Hundesport im deutschsprachigen Raum.", { name: SITE.name })}
        </p>
        {/* Die Fußzeile ist der Ort, an dem man eine Versionsangabe sucht,
            ohne dass sie sich irgendwo aufdrängt. */}
        <VersionStand />
      </div>
    </footer>
  );
}

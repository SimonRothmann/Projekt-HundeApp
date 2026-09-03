import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { SITE } from "@/lib/seo";
import { MarketingAuthLinks } from "@/components/marketing/marketing-auth-links";
import { VersionStand } from "@/components/version-stand";

/**
 * Kopf- und Fußzeile der öffentlichen Seiten. Die Verweise sind nicht nur
 * Navigation: Suchmaschinen finden Unterseiten über Verweise, eine Seite ohne
 * eingehenden Link wird kaum beachtet.
 */
export function MarketingHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-3 px-4">
        <Link href="/" className="text-lg font-extrabold tracking-tight text-primary">
          {SITE.name}
        </Link>
        <nav className="flex items-center gap-1 text-sm">
          <Link
            href="/sachkunde"
            className="hidden text-sm font-medium text-muted-foreground transition-colors hover:text-foreground sm:inline"
          >
            Sachkunde
          </Link>
          <Link
            href="/pruefungsordnungen"
            className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "hidden sm:inline-flex")}
          >
            Prüfungsordnungen
          </Link>
          <MarketingAuthLinks />
        </nav>
      </div>
    </header>
  );
}

export function MarketingFooter() {
  return (
    <footer className="mt-16 border-t border-border/60 py-10">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-4 px-4 text-sm text-muted-foreground">
        <nav className="flex flex-wrap gap-x-5 gap-y-2">
          <Link href="/" className="hover:text-foreground">
            Startseite
          </Link>
          <Link href="/sachkunde" className="hover:text-foreground">
            Sachkunde
          </Link>
          <Link href="/pruefungsordnungen" className="hover:text-foreground">
            Prüfungsordnungen
          </Link>
          <Link href="/register" className="hover:text-foreground">
            Konto anlegen
          </Link>
          <Link href="/login" className="hover:text-foreground">
            Anmelden
          </Link>
          <Link href="/neuerungen" className="hover:text-foreground">
            Neuerungen
          </Link>
        </nav>
        <p className="[overflow-wrap:anywhere]">
          {SITE.name} – Trainingstagebuch und Vereinsplattform für den Hundesport im deutschsprachigen Raum.
        </p>
        {/* Die Fußzeile ist der Ort, an dem man eine Versionsangabe sucht,
            ohne dass sie sich irgendwo aufdrängt. */}
        <VersionStand />
      </div>
    </footer>
  );
}

"use client";

import { Suspense, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { SidebarNav } from "@/components/nav/sidebar-nav";
import { BottomNav } from "@/components/nav/bottom-nav";
import { SubpageBackButton } from "@/components/nav/subpage-back-button";
import { ThemeToggle } from "@/components/theme-toggle";
import { NotificationBell } from "@/components/nav/notification-bell";
import { OfflineSyncListener } from "@/components/offline-sync-listener";
import { EnvBadge } from "@/components/env-badge";
import { useT } from "@/lib/i18n";

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, isLoading } = useAuth();
  const t = useT();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !user) router.replace("/login");
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return <div className="flex flex-1 items-center justify-center text-muted-foreground">{t("Lädt…")}</div>;
  }

  return (
    <div className="flex flex-1">
      <SidebarNav />
      {/* min-w-0: verhindert, dass ein breiter Inhalt (Flex-Kind mit
          min-width:auto) die Spalte über die Viewport-Breite hinaus dehnt und
          so horizontales Scrollen erzeugt - Mobile-App-First, nie H-Scroll. */}
      <div className="relative flex min-w-0 flex-1 flex-col">
        {/* Dekorativer Aurora-Schein oben, hinter dem Inhalt (z-0), fading zu
            transparent - kein eigener Platzbedarf, kein horizontaler Scroll. */}
        <div aria-hidden className="aurora pointer-events-none absolute inset-x-0 top-0 z-0 h-72 print:hidden" />
        <header className="sticky top-0 z-30 flex items-center justify-between border-b border-border/60 bg-background/70 px-4 py-3 backdrop-blur-md md:hidden print:hidden">
          <div className="flex items-center gap-2">
            <span className="text-gradient text-lg font-bold tracking-tight">Dogity</span>
            <EnvBadge />
          </div>
          <div className="flex items-center gap-1">
            <NotificationBell />
            <ThemeToggle />
          </div>
        </header>
        <OfflineSyncListener />
        {/* pb-28 (mobil) hält den Inhalt frei von der fixierten BottomNav
            (~64px) inkl. iOS-Safe-Area; Desktop nutzt md:pb-8 (keine BottomNav). */}
        <main className="relative z-10 flex-1 px-4 py-6 pb-28 md:px-8 md:pb-8 print:p-0">
          {/* Suspense-Grenze, weil SubpageBackButton useSearchParams liest
              (?from=): ohne sie fiele der gesamte Client-Baum darüber aus dem
              Prerendering - so empfiehlt es die Next-Dokumentation. Kein
              Platzhalter: der Knopf ist Beiwerk, ein kurzes Fehlen fällt
              weniger auf als ein springender Platzhalter. */}
          <Suspense fallback={null}>
            <SubpageBackButton />
          </Suspense>
          {children}
        </main>
      </div>
      <BottomNav />
    </div>
  );
}

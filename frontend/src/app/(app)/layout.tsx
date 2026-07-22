"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { SidebarNav } from "@/components/nav/sidebar-nav";
import { BottomNav } from "@/components/nav/bottom-nav";
import { ThemeToggle } from "@/components/theme-toggle";
import { NotificationBell } from "@/components/nav/notification-bell";
import { OfflineSyncListener } from "@/components/offline-sync-listener";
import { EnvBadge } from "@/components/env-badge";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !user) router.replace("/login");
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return <div className="flex flex-1 items-center justify-center text-muted-foreground">Lädt…</div>;
  }

  return (
    <div className="flex flex-1">
      <SidebarNav />
      <div className="flex flex-1 flex-col">
        <header className="flex items-center justify-between border-b px-4 py-3 md:hidden print:hidden">
          <div className="flex items-center gap-2">
            <span className="text-lg font-semibold text-primary">Dogity</span>
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
        <main className="flex-1 px-4 py-6 pb-28 md:px-8 md:pb-8 print:p-0">{children}</main>
      </div>
      <BottomNav />
    </div>
  );
}

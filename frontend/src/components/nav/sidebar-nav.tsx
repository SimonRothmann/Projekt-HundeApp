"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { coreNavItems, profileNavItem, trainerNavItem, adminNavItem } from "@/components/nav/nav-items";
import { ThemeToggle } from "@/components/theme-toggle";
import { NotificationBell } from "@/components/nav/notification-bell";
import { useAuth } from "@/lib/auth-context";
import { PawPrint } from "lucide-react";
import { EnvBadge } from "@/components/env-badge";

export function SidebarNav() {
  const pathname = usePathname();
  const { user, isTrainer } = useAuth();
  const items = [
    ...coreNavItems,
    ...(isTrainer ? [trainerNavItem] : []),
    profileNavItem,
    ...(user?.roles.includes("ADMIN") ? [adminNavItem] : []),
  ];

  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r bg-sidebar p-4 md:flex print:hidden">
      <div className="mb-6 flex items-center justify-between gap-2 px-2">
        <div className="flex items-center gap-2">
          <PawPrint className="size-6 text-primary" />
          <span className="text-lg font-semibold">Dogity</span>
          <EnvBadge />
        </div>
        <NotificationBell />
      </div>
      <nav className="flex flex-1 flex-col gap-1">
        {items.map(({ href, label, icon: Icon }) => {
          const isActive = pathname.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              className={cn(
                "flex items-center gap-3 rounded-xl px-3 py-2 text-sm font-medium transition-colors duration-150",
                isActive
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground/70 hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground",
              )}
            >
              <Icon className="size-4" />
              {label}
            </Link>
          );
        })}
      </nav>
      <div className="flex items-center justify-between px-2">
        <span className="text-xs text-muted-foreground">Theme</span>
        <ThemeToggle />
      </div>
    </aside>
  );
}

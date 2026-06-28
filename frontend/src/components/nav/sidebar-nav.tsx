"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { navItems, adminNavItem } from "@/components/nav/nav-items";
import { ThemeToggle } from "@/components/theme-toggle";
import { useAuth } from "@/lib/auth-context";
import { PawPrint } from "lucide-react";

export function SidebarNav() {
  const pathname = usePathname();
  const { user } = useAuth();
  const items = user?.roles.includes("ADMIN") ? [...navItems, adminNavItem] : navItems;

  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r bg-sidebar p-4 md:flex">
      <div className="mb-6 flex items-center gap-2 px-2">
        <PawPrint className="size-6 text-primary" />
        <span className="text-lg font-semibold">CanisTrack</span>
      </div>
      <nav className="flex flex-1 flex-col gap-1">
        {items.map(({ href, label, icon: Icon }) => {
          const isActive = pathname.startsWith(href);
          return (
            <Link
              key={href}
              href={href}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium",
                isActive
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
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

"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { coreNavItems, profileNavItem, trainerNavItem } from "@/components/nav/nav-items";
import { useAuth } from "@/lib/auth-context";

// Tailwind muss Klassennamen als Literal im Quellcode sehen, um sie ins CSS
// aufzunehmen - eine zur Laufzeit interpolierte Klasse wie `grid-cols-${n}`
// würde ignoriert. Daher hier als feste Lookup-Tabelle für die möglichen
// Item-Anzahlen (4 ohne, 5 mit Trainer-Perspektive).
const GRID_COLS_CLASS: Record<number, string> = {
  4: "grid-cols-4",
  5: "grid-cols-5",
};

export function BottomNav() {
  const pathname = usePathname();
  const { isTrainer } = useAuth();
  const navItems = [...coreNavItems, ...(isTrainer ? [trainerNavItem] : []), profileNavItem];

  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t bg-background/95 backdrop-blur supports-backdrop-filter:bg-background/60 md:hidden">
      <ul className={cn("grid", GRID_COLS_CLASS[navItems.length])}>
        {navItems.map(({ href, label, icon: Icon }) => {
          const isActive = pathname.startsWith(href);
          return (
            <li key={href}>
              <Link
                href={href}
                className={cn(
                  "flex flex-col items-center gap-1 py-2.5 text-xs",
                  isActive ? "text-primary" : "text-muted-foreground",
                )}
              >
                <Icon className="size-6" />
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

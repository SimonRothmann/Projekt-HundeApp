"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { coreNavItems, profileNavItem, trainerNavItem } from "@/components/nav/nav-items";
import { useAuth } from "@/lib/auth-context";

// Tailwind muss Klassennamen als Literal im Quellcode sehen, um sie ins CSS
// aufzunehmen - eine zur Laufzeit interpolierte Klasse wie `grid-cols-${n}`
// würde ignoriert. Daher hier als feste Lookup-Tabelle für die möglichen
// Item-Anzahlen (6 ohne, 7 mit Trainer-Perspektive).
const GRID_COLS_CLASS: Record<number, string> = {
  4: "grid-cols-4",
  5: "grid-cols-5",
  6: "grid-cols-6",
  7: "grid-cols-7",
};

/**
 * Ab sieben Einträgen wird die Schrift kleiner.
 *
 * Die Leiste war für vier bis fünf Einträge gebaut und ist auf sieben
 * gewachsen. Auf 375 px bleiben davon 53 px je Feld - "Statistiken" braucht
 * bei 12 px aber 56 px. Gemessen überlappten sich die Beschriftungen um zwei
 * bis drei Pixel, die erste ragte links aus dem Bild, die letzte rechts:
 * "Sportarten Vereine Statistiken" las sich als ein Wort.
 *
 * Betroffen ist, wer die Trainer-Perspektive sieht (leitet eine Gruppe oder
 * ist Vereinstrainer:in) - für alle anderen sind es sechs Einträge, die
 * gerade noch passen.
 */
const LABEL_SIZE_CLASS = (anzahl: number) => (anzahl >= 7 ? "text-[10px]" : "text-xs");

export function BottomNav() {
  const pathname = usePathname();
  const { isTrainer } = useAuth();
  const navItems = [...coreNavItems, ...(isTrainer ? [trainerNavItem] : []), profileNavItem];

  return (
    <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-border/60 bg-background/80 pb-[env(safe-area-inset-bottom)] backdrop-blur-xl supports-backdrop-filter:bg-background/60 md:hidden print:hidden">
      <ul className={cn("grid", GRID_COLS_CLASS[navItems.length])}>
        {navItems.map(({ href, label, icon: Icon }) => {
          const isActive = pathname.startsWith(href);
          return (
            <li key={href} className="min-w-0">
              <Link
                href={href}
                className={cn(
                  "flex min-w-0 flex-col items-center gap-1 py-2 font-medium transition-transform active:scale-95",
                  LABEL_SIZE_CLASS(navItems.length),
                  isActive ? "text-primary" : "text-muted-foreground",
                )}
              >
                <span
                  className={cn(
                    // max-w statt fester Breite: w-14 sind 56 px und damit
                    // breiter als ein Feld bei sieben Einträgen (53 px) - die
                    // Pille ragte damit selbst über die Spalte hinaus.
                    "flex h-8 w-full max-w-14 items-center justify-center rounded-full transition-colors",
                    isActive && "bg-primary/12",
                  )}
                >
                  <Icon className="size-5" />
                </span>
                {/* truncate als Auffangnetz für sehr schmale Geräte (320 px):
                    lieber ein abgeschnittenes Wort als zwei ineinander
                    laufende. Das Symbol darüber bleibt eindeutig. */}
                <span className="w-full truncate text-center">{label}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

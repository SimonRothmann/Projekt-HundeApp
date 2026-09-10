"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { adminNavItem, bottomNavItems, profileNavItem, statsNavItem, trainerNavItem } from "@/components/nav/nav-items";
import { useAuth } from "@/lib/auth-context";
import { usePreferences } from "@/lib/preferences-context";
import { useT } from "@/lib/i18n";

// Tailwind muss Klassennamen als Literal im Quellcode sehen, um sie ins CSS
// aufzunehmen - eine zur Laufzeit interpolierte Klasse wie `grid-cols-${n}`
// würde ignoriert. Daher hier als feste Lookup-Tabelle für die möglichen
// Item-Anzahlen: seit 2026-09-10 drei bis fünf, sechs nur für Admins, die
// zugleich Trainer:in sind. 7 bleibt als Auffangnetz stehen.
const GRID_COLS_CLASS: Record<number, string> = {
  3: "grid-cols-3",
  4: "grid-cols-4",
  5: "grid-cols-5",
  6: "grid-cols-6",
  7: "grid-cols-7",
};

/**
 * Die Schriftgröße richtet sich nach der Zahl der Einträge.
 *
 * Die Leiste war für vier bis fünf Einträge gebaut und ist auf sieben
 * gewachsen. Auf 375 px bleiben bei sieben Einträgen 53 px je Feld -
 * "Statistiken" braucht bei 12 px aber 56 px. Die Beschriftungen überlappten
 * sich gemessen um zwei bis drei Pixel, die erste ragte links aus dem Bild,
 * die letzte rechts: "Sportarten Vereine Statistiken" las sich als ein Wort.
 *
 * Auch sechs Einträge sind schon zu eng, nur knapper: Dort stehen 62,50 px
 * je Feld zur Verfügung, "Sportarten" braucht bei 12 px 62,61 px. Diese
 * 0,11 px genügen, damit die Beschriftung als "Sportart…" abgeschnitten
 * wird - gemessen auf test. Ganzzahlige Werte wie scrollWidth verraten das
 * nicht, sie runden beides auf 63.
 *
 * Deshalb eine Leiter statt einer einzelnen Schwelle. Nachgemessen bei
 * 375 px: bei 11 px braucht "Sportarten" 57,4 px (Platz 62,5), bei 10 px
 * 52,2 px (Platz 53,6) - beides passt vollständig.
 *
 * Seit 2026-09-10 steht "Sportarten" nicht mehr in der Leiste; längste
 * Beschriftung ist "Statistiken" mit 56,3 px bei 11 px (Platz 62,5). Bei
 * 320 px (53,3 px je Feld) greift dafür das truncate unten.
 */
const LABEL_SIZE_CLASS = (anzahl: number) =>
  anzahl >= 7 ? "text-[10px]" : anzahl >= 6 ? "text-[11px]" : "text-xs";

/**
 * Sechs Einträge hat nur, wer Admin und Trainer:in zugleich ist - und das ist
 * allein der Betreiber. Dafür kein Umbau der Leiste (Entscheidung
 * 2026-09-10), nur kleinere Symbole und eine schmalere Pille, damit zwischen
 * den 62,5 px breiten Feldern Luft bleibt. Die Höhe der Pille bleibt: Die
 * feste Speichern-Leiste im Trainingsformular sitzt auf der Höhe der
 * Navigation.
 */
const ICON_SIZE_CLASS = (anzahl: number) => (anzahl >= 6 ? "size-[18px]" : "size-5");
const PILL_WIDTH_CLASS = (anzahl: number) => (anzahl >= 6 ? "max-w-12" : "max-w-14");

export function BottomNav() {
  const pathname = usePathname();
  const { user, isTrainer } = useAuth();
  const { moduleEnabled } = usePreferences();
  const t = useT();
  // Ausgeblendete Module verschwinden auch aus der Navigation - sonst führte
  // ein Menüpunkt auf eine Seite, die es für diesen Nutzer nicht gibt.
  //
  // Höchstens fünf Ziele für die meisten (Material: drei bis fünf; Apple:
  // weniger Tabs sind leichter). Vorher sieben bei 54 px je Feld. Keine
  // Aktion in der Leiste - Erfassen steht als Kachel auf der Startseite.
  const navItems = [
    ...bottomNavItems,
    ...(isTrainer ? [trainerNavItem] : []),
    statsNavItem,
    ...(user?.roles.includes("ADMIN") ? [adminNavItem] : []),
    profileNavItem,
  ].filter(
    (item) => !item.module || moduleEnabled(item.module),
  );

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
                  isActive ? "text-primary-text" : "text-muted-foreground",
                )}
              >
                <span
                  className={cn(
                    // max-w statt fester Breite: w-14 sind 56 px und damit
                    // breiter als ein Feld bei sieben Einträgen (53 px) - die
                    // Pille ragte damit selbst über die Spalte hinaus.
                    "flex h-8 w-full items-center justify-center rounded-full transition-colors",
                    PILL_WIDTH_CLASS(navItems.length),
                    isActive && "bg-primary/12",
                  )}
                >
                  <Icon className={ICON_SIZE_CLASS(navItems.length)} />
                </span>
                {/* truncate als Auffangnetz für sehr schmale Geräte (320 px):
                    lieber ein abgeschnittenes Wort als zwei ineinander
                    laufende. Das Symbol darüber bleibt eindeutig. */}
                <span className="w-full truncate text-center">{t(label)}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

import { LayoutDashboard, Dog, Trophy, Users, User, ShieldCheck, Building2, BarChart } from "lucide-react";
import { MODULE } from "@/lib/types";
import { uebersetzbar } from "@/lib/i18n/sprachen";

/**
 * Ein Menüpunkt. `module` benennt das Modul, an dem er hängt - fehlt es,
 * gehört der Punkt zum Kern und lässt sich nicht abschalten.
 */
export type NavItem = {
  href: string;
  label: string;
  icon: typeof Dog;
  module?: string;
};

export const homeNavItem: NavItem = { href: "/dashboard", label: uebersetzbar("Home"), icon: LayoutDashboard };
export const dogsNavItem: NavItem = { href: "/dogs", label: uebersetzbar("Hunde"), icon: Dog };
export const sportsNavItem: NavItem = { href: "/sports", label: uebersetzbar("Sportarten"), icon: Trophy };
export const clubsNavItem: NavItem = { href: "/clubs", label: uebersetzbar("Vereine"), icon: Building2 };
export const statsNavItem: NavItem = {
  href: "/stats",
  label: uebersetzbar("Statistiken"),
  icon: BarChart,
  module: MODULE.statistik,
};

/** Seitenleiste am Desktop: dort ist Platz für alle Kernbereiche. */
export const coreNavItems: NavItem[] = [homeNavItem, dogsNavItem, sportsNavItem, clubsNavItem, statsNavItem];

/**
 * Untere Leiste am Telefon: nur, was man täglich öffnet - Trainer,
 * Statistiken, Admin und Profil fügt BottomNav in der Reihenfolge an.
 * Sportarten und Verein öffnet man selten; sie stehen im Profil und auf der
 * Startseite (Entscheidung 2026-09-10: höchstens fünf Ziele, keine Aktion
 * in der Leiste).
 */
export const bottomNavItems: NavItem[] = [homeNavItem, dogsNavItem];

export const profileNavItem: NavItem = { href: "/profile", label: uebersetzbar("Profil"), icon: User };

// Nur sichtbar, wenn die jeweilige Perspektive auf die Person zutrifft
// (siehe TODO.md "Rollenswitch": rein datengetrieben, keine eigene
// Identity-Rolle nötig - useAuth().isTrainer/roles entscheidet).
export const trainerNavItem: NavItem = { href: "/trainer", label: uebersetzbar("Trainer"), icon: Users, module: MODULE.gruppentraining };
export const adminNavItem: NavItem = { href: "/admin", label: uebersetzbar("Admin"), icon: ShieldCheck };

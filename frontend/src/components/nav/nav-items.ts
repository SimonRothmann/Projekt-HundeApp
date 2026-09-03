import { LayoutDashboard, Dog, Trophy, Users, User, ShieldCheck, Building2, BarChart } from "lucide-react";
import { MODULE } from "@/lib/types";

// Für jede Person sichtbar, sofern das zugehörige Modul nicht ausgeblendet
// wurde. `module` fehlt bei allem, was zum Kern gehört und sich nicht
// abschalten lässt - ein Tagebuch ohne Hunde wäre keins.
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

export const coreNavItems: NavItem[] = [
  { href: "/dashboard", label: "Home", icon: LayoutDashboard },
  { href: "/dogs", label: "Hunde", icon: Dog },
  { href: "/sports", label: "Sportarten", icon: Trophy },
  { href: "/clubs", label: "Vereine", icon: Building2 },
  { href: "/stats", label: "Statistiken", icon: BarChart, module: MODULE.statistik },
];

export const profileNavItem: NavItem = { href: "/profile", label: "Profil", icon: User };

// Nur sichtbar, wenn die jeweilige Perspektive auf die Person zutrifft
// (siehe TODO.md "Rollenswitch": rein datengetrieben, keine eigene
// Identity-Rolle nötig - useAuth().isTrainer/roles entscheidet).
export const trainerNavItem: NavItem = { href: "/trainer", label: "Trainer", icon: Users, module: MODULE.gruppentraining };
export const adminNavItem: NavItem = { href: "/admin", label: "Admin", icon: ShieldCheck };

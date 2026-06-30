import { LayoutDashboard, Dog, Trophy, Users, User, ShieldCheck, Building2, BarChart } from "lucide-react";

// Für jede Person immer sichtbar - unabhängig von Rolle/Perspektive.
export const coreNavItems = [
  { href: "/dashboard", label: "Home", icon: LayoutDashboard },
  { href: "/dogs", label: "Hunde", icon: Dog },
  { href: "/sports", label: "Sportarten", icon: Trophy },
  { href: "/clubs", label: "Vereine", icon: Building2 },
  { href: "/stats", label: "Statistiken", icon: BarChart },
];

export const profileNavItem = { href: "/profile", label: "Profil", icon: User };

// Nur sichtbar, wenn die jeweilige Perspektive auf die Person zutrifft
// (siehe TODO.md "Rollenswitch": rein datengetrieben, keine eigene
// Identity-Rolle nötig - useAuth().isTrainer/roles entscheidet).
export const trainerNavItem = { href: "/trainer", label: "Trainer", icon: Users };
export const adminNavItem = { href: "/admin", label: "Admin", icon: ShieldCheck };

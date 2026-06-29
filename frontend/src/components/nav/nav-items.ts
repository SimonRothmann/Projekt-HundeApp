import { LayoutDashboard, Dog, Trophy, Users, User, ShieldCheck } from "lucide-react";

// Für jede Person immer sichtbar - unabhängig von Rolle/Perspektive.
export const coreNavItems = [
  { href: "/dashboard", label: "Home", icon: LayoutDashboard },
  { href: "/dogs", label: "Hunde", icon: Dog },
  { href: "/sports", label: "Sportarten", icon: Trophy },
];

export const profileNavItem = { href: "/profile", label: "Profil", icon: User };

// Nur sichtbar, wenn die jeweilige Perspektive auf die Person zutrifft
// (siehe TODO.md "Rollenswitch": rein datengetrieben, keine eigene
// Identity-Rolle nötig - useAuth().isTrainer/roles entscheidet).
export const trainerNavItem = { href: "/trainer", label: "Trainer", icon: Users };
export const adminNavItem = { href: "/admin", label: "Admin", icon: ShieldCheck };

import { LayoutDashboard, Dog, Trophy, Users, User, ShieldCheck } from "lucide-react";

export const navItems = [
  { href: "/dashboard", label: "Home", icon: LayoutDashboard },
  { href: "/dogs", label: "Hunde", icon: Dog },
  { href: "/sports", label: "Sportarten", icon: Trophy },
  { href: "/trainer", label: "Trainer", icon: Users },
  { href: "/profile", label: "Profil", icon: User },
];

export const adminNavItem = { href: "/admin", label: "Admin", icon: ShieldCheck };

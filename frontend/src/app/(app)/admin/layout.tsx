"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";

/**
 * Der Adminbereich gehört allein den Admins - /admin und alle Unterseiten.
 * Das Backend prüft die Rolle an jedem Endpunkt ohnehin; diese Hülle sorgt
 * dafür, dass auch die Seiten selbst Nicht-Admins nichts zeigen (nur der
 * Menüpunkt war bisher verborgen) und führt sie zurück zur Startseite.
 * Angemeldet ist hier sicher jemand: AppShell zeigt Gästen den Inhalt nicht.
 */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const router = useRouter();
  const istAdmin = user?.roles.includes("ADMIN") ?? false;

  useEffect(() => {
    if (user && !istAdmin) router.replace("/dashboard");
  }, [user, istAdmin, router]);

  if (!istAdmin) return null;
  return <>{children}</>;
}

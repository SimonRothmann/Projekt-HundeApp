"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";

import { useT } from "@/lib/i18n";
import { useAuth } from "@/lib/auth-context";
import { zurueckZiel } from "@/lib/zurueck";
// Top-Level-Routen (über die Navigation direkt erreichbar) - hier gibt es
// bewusst KEINEN Zurück-Button.
const ROOT_PATHS = new Set([
  "/dashboard",
  "/dogs",
  "/sports",
  "/clubs",
  "/stats",
  "/profile",
  "/trainer",
  "/admin",
]);

/**
 * Einheitlicher "Zurück"-Button für ALLE Unterseiten. Wird zentral im
 * (app)-Layout oberhalb des Seiteninhalts gerendert und blendet sich auf
 * Top-Level-Seiten (und Druckansichten) selbst aus - so gibt es überall
 * konsistent oben eine Möglichkeit zurückzukehren, ohne dass jede Seite es
 * einzeln pflegen muss. Ziel ist jeweils die übergeordnete Route
 * (z.B. /trainer/group-training -> /trainer, /dogs/123 -> /dogs); wer keine
 * Trainer:in ist, kommt von /trainer-Seiten zu den Vereinen (siehe zurueckZiel).
 *
 * Ausnahme: ein ?from=-Parameter gewinnt. Der Pfad allein weiß nämlich nicht,
 * WOHER man kam - eine Trainer:in, die aus ihrer Gruppe den Hund eines
 * Mitglieds öffnet, landete über /dogs/123 -> /dogs bei ihren EIGENEN Hunden
 * statt zurück in der Gruppe.
 */

export function SubpageBackButton() {
  const t = useT();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { isTrainer } = useAuth();
  if (!pathname) return null;

  const normalized = pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
  if (normalized === "/" || ROOT_PATHS.has(normalized) || normalized.endsWith("/print")) return null;

  // Nur seiteninterne Ziele: ein ?from= aus einem geteilten Link darf nicht von der Seite fortführen.
  const target = zurueckZiel(normalized, searchParams?.get("from"), isTrainer);

  return (
    <Link
      href={target}
      className="mb-4 inline-flex h-9 items-center gap-1.5 rounded-lg px-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground active:scale-[0.97] coarse:min-h-11 print:hidden"
    >
      <ArrowLeft className="size-4" />
{t("Zurück")}
    </Link>
  );
}

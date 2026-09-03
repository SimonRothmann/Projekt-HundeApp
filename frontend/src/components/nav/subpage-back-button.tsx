"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";

import { useT } from "@/lib/i18n";
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
 * (z.B. /trainer/group-training -> /trainer, /dogs/123 -> /dogs).
 *
 * Ausnahme: ein ?from=-Parameter gewinnt. Der Pfad allein weiß nämlich nicht,
 * WOHER man kam - eine Trainer:in, die aus ihrer Gruppe den Hund eines
 * Mitglieds öffnet, landete über /dogs/123 -> /dogs bei ihren EIGENEN Hunden
 * statt zurück in der Gruppe.
 */

/**
 * Nur echte, seiteninterne Pfade zulassen. "//host" wäre für den Browser eine
 * fremde Adresse - ein ?from= aus einem geteilten Link dürfte sonst von der
 * eigenen Seite fortführen.
 */
function safeInternalPath(value: string | null): string | null {
  if (!value || !value.startsWith("/") || value.startsWith("//")) return null;
  return value;
}

export function SubpageBackButton() {
  const t = useT();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  if (!pathname) return null;

  const normalized = pathname.length > 1 && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
  if (normalized === "/" || ROOT_PATHS.has(normalized) || normalized.endsWith("/print")) return null;

  const parent = normalized.slice(0, normalized.lastIndexOf("/")) || "/dashboard";
  const target = safeInternalPath(searchParams?.get("from") ?? null) ?? parent;

  return (
    <Link
      href={target}
      className="mb-4 inline-flex h-9 items-center gap-1.5 rounded-lg px-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground active:scale-[0.97] coarse:min-h-11"
    >
      <ArrowLeft className="size-4" />
{t("Zurück")}
    </Link>
  );
}

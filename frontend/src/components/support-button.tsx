"use client";

import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { Coffee } from "lucide-react";

import { useT } from "@/lib/i18n";
const KOFI_URL = "https://ko-fi.com/simonrothmann";

/**
 * "Auf Ko-fi unterstützen"-Button: bewusst nur ein externer Link (kein Ko-fi-
 * Skript/-iframe). Dadurch keine CSP-Freigabe nötig, kein Third-Party-Tracking
 * und kein floating Widget, das mit der Bottom-Nav/Safe-Area kollidiert. Öffnet
 * die Ko-fi-Seite in einem neuen Tab (rel=noopener gegen Tab-Nabbing).
 */
export function SupportButton({ className }: { className?: string }) {
  const t = useT();
  return (
    <a
      href={KOFI_URL}
      target="_blank"
      rel="noopener noreferrer"
      // Bewusst in Ko-fi-Korall (nicht Indigo/Primary), damit die Support-CTA
      // sich klar von den übrigen Buttons abhebt; sanfter farbiger Glow.
      className={cn(
        buttonVariants({ variant: "default" }),
        "h-10 gap-2 px-5 text-sm bg-[#ff5e5b] text-white shadow-[0_8px_24px_-8px_rgba(255,94,91,0.6)] hover:bg-[#f24b48] hover:text-white hover:shadow-[0_10px_30px_-8px_rgba(255,94,91,0.75)]",
        className,
      )}
    >
      <Coffee className="size-[18px]" />
{t("Auf Ko-fi unterstützen")}
    </a>
  );
}

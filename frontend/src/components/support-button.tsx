import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { Coffee } from "lucide-react";

const KOFI_URL = "https://ko-fi.com/simonrothmann";

/**
 * "Auf Ko-fi unterstützen"-Button: bewusst nur ein externer Link (kein Ko-fi-
 * Skript/-iframe). Dadurch keine CSP-Freigabe nötig, kein Third-Party-Tracking
 * und kein floating Widget, das mit der Bottom-Nav/Safe-Area kollidiert. Öffnet
 * die Ko-fi-Seite in einem neuen Tab (rel=noopener gegen Tab-Nabbing).
 */
export function SupportButton({ className }: { className?: string }) {
  return (
    <a
      href={KOFI_URL}
      target="_blank"
      rel="noopener noreferrer"
      className={cn(buttonVariants({ variant: "outline", size: "sm" }), className)}
    >
      <Coffee className="size-4" />
      Auf Ko-fi unterstützen
    </a>
  );
}

"use client";

import { useT } from "@/lib/i18n";
import { ThemeSection } from "@/components/preferences/theme-section";
import { SchriftgroesseSection } from "@/components/preferences/schriftgroesse-section";
import { SpracheUndLandSection } from "@/components/preferences/sprache-und-land-section";

/**
 * Wie die App aussieht und in welcher Sprache sie spricht. Der
 * Geltungsbereich der Prüfungsordnungen erscheint in der Spracheinstellung
 * nur, wenn es mehr als ein Land zu wählen gibt.
 */
export default function DarstellungPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Darstellung & Sprache")}</h1>
      <ThemeSection />
      <SchriftgroesseSection />
      <SpracheUndLandSection />
    </div>
  );
}

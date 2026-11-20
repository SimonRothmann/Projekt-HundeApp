"use client";

import { useEffect, useState } from "react";
import { useT } from "@/lib/i18n";
import { geraeteZeitzone } from "@/lib/zeitzone";
import { ThemeSection } from "@/components/preferences/theme-section";
import { SchriftgroesseSection } from "@/components/preferences/schriftgroesse-section";
import { FarbenSection } from "@/components/preferences/farben-section";
import { SpracheUndLandSection } from "@/components/preferences/sprache-und-land-section";

/**
 * Wie die App aussieht und in welcher Sprache sie spricht. Der
 * Geltungsbereich der Prüfungsordnungen erscheint in der Spracheinstellung
 * nur, wenn es mehr als ein Land zu wählen gibt.
 */
export default function DarstellungPage() {
  const t = useT();
  // Erst im Browser ermitteln: Der Server kennt die Zone des Geräts nicht (sonst Abweichung beim Hydratisieren).
  const [zone, setZone] = useState<string | null>(null);
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setZone(geraeteZeitzone());
  }, []);
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Darstellung & Sprache")}</h1>
      <ThemeSection />
      <SchriftgroesseSection />
      <FarbenSection />
      <SpracheUndLandSection />
      {zone && (
        <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
          {t("Zeitzone: {zone} (von deinem Gerät)", { zone })}
        </p>
      )}
    </div>
  );
}

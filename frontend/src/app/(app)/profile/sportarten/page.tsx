"use client";

import { useT } from "@/lib/i18n";
import { usePreferences } from "@/lib/preferences-context";
import { MODULE } from "@/lib/types";
import { SportartenSection } from "@/components/preferences/einstellungen-section";
import { SchrittlaengeSection } from "@/components/preferences/schrittlaenge-section";

export default function MeineSportartenPage() {
  const t = useT();
  const { moduleEnabled } = usePreferences();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Sportarten, die ich trainiere")}</h1>
      <SportartenSection />
      {/* Schritte gibt es nur bei der Fährte; wer das Modul abgewählt hat, braucht die Karte nicht. */}
      {moduleEnabled(MODULE.faehrte) && <SchrittlaengeSection />}
    </div>
  );
}

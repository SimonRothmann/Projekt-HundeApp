"use client";

import { useT } from "@/lib/i18n";
import { SportartenSection } from "@/components/preferences/einstellungen-section";

export default function MeineSportartenPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Sportarten, die ich trainiere")}</h1>
      <SportartenSection />
    </div>
  );
}

"use client";

import { useT } from "@/lib/i18n";
import { FunktionenSection } from "@/components/preferences/einstellungen-section";

export default function FunktionenPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Funktionen")}</h1>
      <FunktionenSection />
    </div>
  );
}

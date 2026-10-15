"use client";

import { useT } from "@/lib/i18n";
import { ClubsSection } from "@/components/admin/clubs-section";
import { VereinsantraegeSection } from "@/components/admin/vereinsantraege-section";

export default function AdminVereinePage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Vereine")}</h1>
      <VereinsantraegeSection />
      <ClubsSection />
    </div>
  );
}

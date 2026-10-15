"use client";

import { useT } from "@/lib/i18n";
import { SachkundeSection } from "@/components/admin/sachkunde-section";

export default function AdminSachkundePage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Sachkunde-Fragen")}</h1>
      <SachkundeSection />
    </div>
  );
}

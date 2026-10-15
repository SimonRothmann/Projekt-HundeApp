"use client";

import { useT } from "@/lib/i18n";
import { TestmailSection } from "@/components/admin/testmail-section";
import { TestpushSection } from "@/components/admin/testpush-section";
import { VerwaisteDatenSection } from "@/components/admin/verwaiste-daten-section";

export default function AdminSystemPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("System")}</h1>
      {/* VerwaisteDatenSection rendert ohne Altdaten nichts; die Seite hat
          dann trotzdem die Testmail, und der Abstand (gap) entsteht nur
          zwischen vorhandenen Blöcken. */}
      <VerwaisteDatenSection />
      <TestmailSection />
      <TestpushSection />
    </div>
  );
}

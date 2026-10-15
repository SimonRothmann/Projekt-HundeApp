"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { istGuid } from "@/lib/admin";
import { useT } from "@/lib/i18n";
import { NutzerSection } from "@/components/admin/nutzer-section";

export default function AdminNutzerPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Nutzer")}</h1>
      {/* Suspense, weil useSearchParams sonst die Seite aus dem Prerendering wirft. */}
      <Suspense fallback={null}>
        <NutzerMitReset />
      </Suspense>
    </div>
  );
}

/** ?resetUser=<id> (Link aus der Passwort-Reset-Benachrichtigung) - nur eine Guid zählt. */
function NutzerMitReset() {
  const resetUser = useSearchParams().get("resetUser");
  return <NutzerSection fokusId={istGuid(resetUser) ? resetUser : null} />;
}

"use client";

import { useT } from "@/lib/i18n";
import { DatenExport, KontoLoeschen } from "@/components/profile/daten-section";

/** Auskunft und Löschung. Das Löschen steht abgesetzt am Ende, weit weg vom Export. */
export default function DatenPage() {
  const t = useT();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Daten & Konto")}</h1>
      <DatenExport />
      <div className="mt-6">
        <KontoLoeschen />
      </div>
    </div>
  );
}

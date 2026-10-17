"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Sport } from "@/lib/types";
import { PruefungsordnungQuelleSection } from "@/components/admin/pruefungsordnung-quelle-section";
import { RegulationImportSection } from "@/components/admin/regulation-import-section";
import { CatalogSection } from "@/components/sports/catalog-section";

export default function AdminKatalogPage() {
  const t = useT();
  const [sports, setSports] = useState<Sport[] | null>(null);

  useEffect(() => {
    // Die Sportarten brauchen der Import und die Quellenpflege - einmal abrufen.
    api
      .get<Sport[]>("/api/sports?alle=true")
      .then(setSports)
      .catch((err) => toast.error(err instanceof ApiError ? err.message : t("Sportarten konnten nicht geladen werden.")));
    // t bewusst nicht in der Liste: nur für den Fehlerfall, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-tight">{t("Sportarten & Prüfungsordnungen")}</h1>

      <CatalogSection
        scope={{ kind: "global" }}
        title={t("Globaler Sportarten-Katalog")}
        description={t("Pflegt die für alle Nutzer sichtbaren Sportarten und Übungen nach VDH-Prüfungsordnungen.")}
      />

      <RegulationImportSection sports={sports ?? []} />

      <PruefungsordnungQuelleSection sports={sports} />
    </div>
  );
}

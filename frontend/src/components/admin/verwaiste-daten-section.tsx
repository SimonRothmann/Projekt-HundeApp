"use client";

import { useEffect, useState } from "react";
import { ArchiveX } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { AdminOrphanedData, AdminOrphanedDataPurge } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * "Daten gelöschter Konten": Altlast aus der Zeit, als das Löschen eines
 * Kontos seine Hunde, Trainings und Verweise stehen ließ.
 *
 * Nur Zählungen - der Server liefert weder Namen noch Ids. Der Knopf löscht
 * endgültig, über dieselbe Löschung wie jede Kontolöschung heute.
 *
 * Ohne Altdaten verschwindet die Karte, wie die Vereinsanträge: Eine leere
 * Karte auf der ohnehin langen Admin-Seite wäre nur Rauschen. Dasselbe, wenn
 * der Server die Konten nicht zuverlässig lesen konnte - dann gibt es nichts
 * Sicheres zu zeigen.
 */
export function VerwaisteDatenSection() {
  const t = useT();
  const [daten, setDaten] = useState<AdminOrphanedData | null>(null);
  const [laeuft, setLaeuft] = useState(false);

  async function laden() {
    try {
      setDaten(await api.get<AdminOrphanedData>("/api/admin/orphaned-data"));
    } catch {
      setDaten(null);
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, []);

  async function loeschen() {
    if (
      !window.confirm(
        t("Diese Daten werden endgültig gelöscht. Das lässt sich nicht rückgängig machen. Wirklich löschen?"),
      )
    ) {
      return;
    }
    setLaeuft(true);
    try {
      const ergebnis = await api.post<AdminOrphanedDataPurge>("/api/admin/orphaned-data/purge");
      toast.success(
        ergebnis.konten === 1
          ? t("Daten eines gelöschten Kontos bereinigt.")
          : t("Daten von {n} gelöschten Konten bereinigt.", { n: ergebnis.konten }),
      );
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Löschen fehlgeschlagen."));
    } finally {
      setLaeuft(false);
    }
  }

  if (daten === null || daten.konten === 0) return null;

  const zahlen = [
    { label: t("Konten"), wert: daten.konten },
    { label: t("Trainings"), wert: daten.trainings },
    { label: t("Hunde"), wert: daten.hunde },
    { label: t("Ziele"), wert: daten.ziele },
    { label: t("Benachrichtigungen"), wert: daten.benachrichtigungen },
    { label: t("Sonstige Einträge"), wert: daten.sonstige },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ArchiveX className="size-5 shrink-0" />
          {t("Daten gelöschter Konten")}
        </CardTitle>
        <CardDescription>
          {t("Diese Daten gehören zu Konten, die es nicht mehr gibt. Sie stammen aus der Zeit vor der vollständigen Kontolöschung und werden nicht mehr gebraucht.")}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {zahlen.map((z) => (
            <li key={z.label} className="min-w-0 rounded-md border px-3 py-2">
              <span className="block text-lg font-semibold tabular-nums">{z.wert}</span>
              <span className="block break-words text-xs text-muted-foreground">{z.label}</span>
            </li>
          ))}
        </ul>
        <Button variant="destructive" className="w-full sm:w-auto sm:self-start" disabled={laeuft} onClick={loeschen}>
          {laeuft ? t("Wird gelöscht…") : t("Endgültig löschen")}
        </Button>
      </CardContent>
    </Card>
  );
}

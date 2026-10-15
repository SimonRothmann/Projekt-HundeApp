"use client";

import { useState } from "react";
import { ScrollText } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Regulation, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/**
 * "Prüfungsordnung: Quelle pflegen": die offizielle Adresse und das zuletzt
 * bekannte Versionslabel einer Prüfungsordnung. Die Sportarten bekommt der
 * Abschnitt von der Seite (wie RegulationImportSection), damit sie nur
 * einmal abgerufen werden.
 */
export function PruefungsordnungQuelleSection({ sports }: { sports: Sport[] | null }) {
  const t = useT();
  const [selectedSportId, setSelectedSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [selectedRegulationId, setSelectedRegulationId] = useState("");
  const [sourceUrl, setSourceUrl] = useState("");
  const [versionLabel, setVersionLabel] = useState("");
  const [saving, setSaving] = useState(false);

  async function handleSportChange(sportId: string) {
    setSelectedSportId(sportId);
    setSelectedRegulationId("");
    setSourceUrl("");
    setVersionLabel("");
    if (!sportId) {
      setRegulations([]);
      return;
    }
    try {
      const data = await api.get<Regulation[]>(`/api/sports/${sportId}/regulations`);
      setRegulations(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Prüfungsordnungen konnten nicht geladen werden."));
    }
  }

  function handleRegulationChange(regulationId: string) {
    setSelectedRegulationId(regulationId);
    const regulation = regulations.find((r) => r.id === regulationId);
    setSourceUrl(regulation?.sourceUrl ?? "");
    setVersionLabel(regulation?.latestKnownVersionLabel ?? "");
  }

  async function handleSaveSource(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedRegulationId) return;
    setSaving(true);
    try {
      await api.put(`/api/admin/regulations/${selectedRegulationId}/source`, {
        sourceUrl: sourceUrl || null,
        latestKnownVersionLabel: versionLabel || null,
      });
      toast.success(t("Quelle aktualisiert."));
      const data = await api.get<Regulation[]>(`/api/sports/${selectedSportId}/regulations`);
      setRegulations(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Speichern fehlgeschlagen."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ScrollText className="size-5" />
          {t("Prüfungsordnung: Quelle pflegen")}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSaveSource} className="flex flex-col gap-3">
          <div className="flex flex-col gap-3 sm:flex-row">
            <div className="flex min-w-0 flex-col gap-2 sm:w-56">
              <Label>{t("Sportart")}</Label>
              <Select value={selectedSportId} onValueChange={(value) => handleSportChange(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder={t("Auswählen…")} />
                </SelectTrigger>
                <SelectContent>
                  {sports?.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex min-w-0 flex-col gap-2 sm:w-56">
              <Label>{t("Prüfungsordnung")}</Label>
              <Select
                value={selectedRegulationId}
                disabled={!selectedSportId}
                onValueChange={(value) => handleRegulationChange(value ?? "")}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("Auswählen…")} />
                </SelectTrigger>
                <SelectContent>
                  {regulations.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          {selectedRegulationId && (
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex min-w-0 flex-col gap-2 sm:flex-1">
                <Label htmlFor="source-url">{t("Offizielle Quelle (URL)")}</Label>
                <Input
                  id="source-url"
                  value={sourceUrl}
                  onChange={(e) => setSourceUrl(e.target.value)}
                  placeholder="https://verband.de/pruefungsordnung"
                />
              </div>
              <div className="flex min-w-0 flex-col gap-2 sm:w-40">
                <Label htmlFor="version-label">{t("Versionslabel")}</Label>
                <Input
                  id="version-label"
                  value={versionLabel}
                  onChange={(e) => setVersionLabel(e.target.value)}
                  placeholder={t("z.B. 2026")}
                />
              </div>
              <Button type="submit" disabled={saving}>
                {t("Speichern")}
              </Button>
            </div>
          )}
        </form>
      </CardContent>
    </Card>
  );
}

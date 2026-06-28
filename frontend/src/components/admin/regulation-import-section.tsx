"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { ParsedExerciseCandidate, Regulation, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScanSearch } from "lucide-react";
import { toast } from "sonner";

type CandidateRow = ParsedExerciseCandidate & { selected: boolean };

export function RegulationImportSection({ sports }: { sports: Sport[] }) {
  const [scanning, setScanning] = useState(false);
  const [candidates, setCandidates] = useState<CandidateRow[] | null>(null);
  const [selectedSportId, setSelectedSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [selectedRegulationId, setSelectedRegulationId] = useState("");
  const [applying, setApplying] = useState(false);

  async function handleScan() {
    setScanning(true);
    try {
      const data = await api.post<ParsedExerciseCandidate[]>("/api/admin/regulation-import/scan");
      setCandidates(data.map((c) => ({ ...c, selected: false })));
      toast.success(`${data.length} Vorschläge gefunden. Bitte einzeln prüfen und auswählen.`);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Scan fehlgeschlagen.");
    } finally {
      setScanning(false);
    }
  }

  async function handleSportChange(sportId: string) {
    setSelectedSportId(sportId);
    setSelectedRegulationId("");
    if (!sportId) {
      setRegulations([]);
      return;
    }
    try {
      const data = await api.get<Regulation[]>(`/api/sports/${sportId}/regulations`);
      setRegulations(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Prüfungsordnungen konnten nicht geladen werden.");
    }
  }

  function toggleCandidate(index: number) {
    setCandidates((prev) => prev?.map((c, i) => (i === index ? { ...c, selected: !c.selected } : c)) ?? null);
  }

  function updatePoints(index: number, value: number) {
    setCandidates((prev) => prev?.map((c, i) => (i === index ? { ...c, maxPoints: value } : c)) ?? null);
  }

  async function handleApply() {
    if (!candidates || !selectedRegulationId) return;
    const selected = candidates.filter((c) => c.selected);
    if (selected.length === 0) {
      toast.error("Bitte mindestens einen Vorschlag auswählen.");
      return;
    }

    setApplying(true);
    try {
      await api.post("/api/admin/regulation-import/apply", {
        regulationId: selectedRegulationId,
        candidates: selected.map((c) => ({ name: c.name, maxPoints: c.maxPoints })),
      });
      toast.success(`${selected.length} Übung(en) übernommen.`);
      setCandidates((prev) => prev?.filter((c) => !c.selected) ?? null);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übernahme fehlgeschlagen.");
    } finally {
      setApplying(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ScanSearch className="size-5" />
          Prüfungsordnung-Import
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted-foreground">
          Scannt die lokal abgelegte Prüfungsordnungs-PDF nach Übungsname+Punktzahl-Mustern (reine Fakten,
          keine geschützte Beschreibungsprosa). Jeder Vorschlag muss einzeln bestätigt werden, bevor er in
          den Katalog übernommen wird.
        </p>
        <Button type="button" onClick={handleScan} disabled={scanning} className="self-start">
          {scanning ? "Scanne…" : "PDF scannen"}
        </Button>

        {candidates && candidates.length > 0 && (
          <>
            <div className="flex flex-col gap-3 sm:flex-row">
              <div className="flex flex-col gap-2 sm:w-56">
                <Label>Sportart</Label>
                <select
                  className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                  value={selectedSportId}
                  onChange={(e) => handleSportChange(e.target.value)}
                >
                  <option value="">Auswählen…</option>
                  {sports.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="flex flex-col gap-2 sm:w-56">
                <Label>Prüfungsordnung</Label>
                <select
                  className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                  value={selectedRegulationId}
                  disabled={!selectedSportId}
                  onChange={(e) => setSelectedRegulationId(e.target.value)}
                >
                  <option value="">Auswählen…</option>
                  {regulations.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <ul className="flex max-h-96 flex-col gap-1 overflow-y-auto">
              {candidates.map((c, i) => (
                <li key={`${c.name}-${i}`} className="flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm">
                  <input
                    type="checkbox"
                    checked={c.selected}
                    onChange={() => toggleCandidate(i)}
                    className="size-4"
                  />
                  <span className="flex-1">{c.name}</span>
                  <Input
                    type="number"
                    value={c.maxPoints}
                    onChange={(e) => updatePoints(i, Number(e.target.value))}
                    className="h-7 w-20"
                  />
                  <span className="text-muted-foreground">Punkte</span>
                </li>
              ))}
            </ul>

            <Button type="button" onClick={handleApply} disabled={applying || !selectedRegulationId} className="self-start">
              Ausgewählte übernehmen
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}

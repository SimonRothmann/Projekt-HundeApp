"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { AdminStats, AdminUser, Regulation, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Users, Dog, Users2, ClipboardList, MapPin, ScrollText } from "lucide-react";
import { toast } from "sonner";
import { ClubsSection } from "@/components/admin/clubs-section";
import { GlobalExercisesSection } from "@/components/admin/global-exercises-section";
import { RegulationImportSection } from "@/components/admin/regulation-import-section";

export default function AdminPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [selectedSportId, setSelectedSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [selectedRegulationId, setSelectedRegulationId] = useState("");
  const [sourceUrl, setSourceUrl] = useState("");
  const [versionLabel, setVersionLabel] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    Promise.all([
      api.get<AdminStats>("/api/admin/stats"),
      api.get<AdminUser[]>("/api/admin/users"),
      api.get<Sport[]>("/api/sports"),
    ])
      .then(([statsData, usersData, sportsData]) => {
        setStats(statsData);
        setUsers(usersData);
        setSports(sportsData);
      })
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Admin-Daten konnten nicht geladen werden."));
  }, []);

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
      toast.error(err instanceof ApiError ? err.message : "Prüfungsordnungen konnten nicht geladen werden.");
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
      toast.success("Quelle aktualisiert.");
      const data = await api.get<Regulation[]>(`/api/sports/${selectedSportId}/regulations`);
      setRegulations(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Speichern fehlgeschlagen.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Admin-Übersicht</h1>
        <p className="text-muted-foreground">
          Plattformweite Kennzahlen, Nutzerverwaltung, Vereinsverwaltung, globaler Übungskatalog und
          Prüfungsordnungs-Pflege.
        </p>
      </div>

      <ClubsSection />

      {stats && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
          <StatCard icon={Users} label="Nutzer" value={stats.userCount} />
          <StatCard icon={Dog} label="Hunde" value={stats.dogCount} />
          <StatCard icon={Users2} label="Gruppen" value={stats.groupCount} />
          <StatCard icon={ClipboardList} label="Trainings" value={stats.trainingSessionCount} />
          <StatCard icon={MapPin} label="Fährten" value={stats.gpsTrackCount} />
        </div>
      )}

      <GlobalExercisesSection />

      <RegulationImportSection sports={sports ?? []} />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ScrollText className="size-5" />
            Prüfungsordnung: Quelle pflegen
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSaveSource} className="flex flex-col gap-3">
            <div className="flex flex-col gap-3 sm:flex-row">
              <div className="flex flex-col gap-2 sm:w-56">
                <Label>Sportart</Label>
                <select
                  className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
                  value={selectedSportId}
                  onChange={(e) => handleSportChange(e.target.value)}
                >
                  <option value="">Auswählen…</option>
                  {sports?.map((s) => (
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
                  onChange={(e) => handleRegulationChange(e.target.value)}
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
            {selectedRegulationId && (
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                <div className="flex flex-col gap-2 sm:flex-1">
                  <Label htmlFor="source-url">Offizielle Quelle (URL)</Label>
                  <Input
                    id="source-url"
                    value={sourceUrl}
                    onChange={(e) => setSourceUrl(e.target.value)}
                    placeholder="https://verband.de/pruefungsordnung"
                  />
                </div>
                <div className="flex flex-col gap-2 sm:w-40">
                  <Label htmlFor="version-label">Versionslabel</Label>
                  <Input
                    id="version-label"
                    value={versionLabel}
                    onChange={(e) => setVersionLabel(e.target.value)}
                    placeholder="z.B. 2026"
                  />
                </div>
                <Button type="submit" disabled={saving}>
                  Speichern
                </Button>
              </div>
            )}
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Nutzer</CardTitle>
        </CardHeader>
        <CardContent>
          {users === null ? (
            <p className="text-sm text-muted-foreground">Lädt…</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {users.map((u) => (
                <li key={u.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm">
                  <span>
                    {u.firstName} {u.lastName} <span className="text-muted-foreground">({u.email})</span>
                  </span>
                  <div className="flex gap-1">
                    {u.roles.map((role) => (
                      <Badge key={role} variant="secondary">
                        {role}
                      </Badge>
                    ))}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function StatCard({ icon: Icon, label, value }: { icon: typeof Users; label: string; value: number }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-1 py-4">
        <Icon className="size-5 text-primary" />
        <span className="text-2xl font-semibold">{value}</span>
        <span className="text-xs text-muted-foreground">{label}</span>
      </CardContent>
    </Card>
  );
}

"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import type { AdminStats, AdminUser, AdminUserPage, Regulation, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Users, Dog, Users2, ClipboardList, MapPin, ScrollText, Lock, Unlock, Trash2, ChevronLeft, ChevronRight, KeyRound } from "lucide-react";
import { toast } from "sonner";
import { ClubsSection } from "@/components/admin/clubs-section";
import { CatalogSection } from "@/components/sports/catalog-section";
import { RegulationImportSection } from "@/components/admin/regulation-import-section";

export default function AdminPage() {
  const searchParams = useSearchParams();
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [userPage, setUserPage] = useState<AdminUserPage | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [sports, setSports] = useState<Sport[] | null>(null);
  const [selectedSportId, setSelectedSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [selectedRegulationId, setSelectedRegulationId] = useState("");
  const [sourceUrl, setSourceUrl] = useState("");
  const [versionLabel, setVersionLabel] = useState("");
  const [saving, setSaving] = useState(false);
  // Passwort-Setzen: welcher Nutzer hat das Inline-Formular offen + Eingabe.
  // Vorselektiert über ?resetUser=<id> (Link aus der Admin-Benachrichtigung,
  // wenn ein Nutzer einen Passwort-Reset angefordert hat).
  const [pwUserId, setPwUserId] = useState<string | null>(searchParams.get("resetUser"));
  const [newPassword, setNewPassword] = useState("");
  const [settingPassword, setSettingPassword] = useState(false);

  async function loadUsers(page: number) {
    try {
      const data = await api.get<AdminUserPage>(`/api/admin/users?page=${page}&pageSize=50`);
      setUserPage(data);
      setCurrentPage(page);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Nutzer konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    Promise.all([
      api.get<AdminStats>("/api/admin/stats"),
      api.get<AdminUserPage>("/api/admin/users?page=1&pageSize=50"),
      api.get<Sport[]>("/api/sports"),
    ])
      .then(([statsData, usersData, sportsData]) => {
        setStats(statsData);
        setUserPage(usersData);
        setSports(sportsData);
      })
      .catch((err) => toast.error(err instanceof ApiError ? err.message : "Admin-Daten konnten nicht geladen werden."));
  }, []);

  async function handleToggleLock(user: AdminUser) {
    try {
      await api.post(`/api/admin/users/${user.id}/${user.isLockedOut ? "unlock" : "lock"}`);
      toast.success(user.isLockedOut ? "Konto entsperrt." : "Konto gesperrt.");
      await loadUsers(currentPage);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Aktion fehlgeschlagen.");
    }
  }

  async function handleDeleteUser(user: AdminUser) {
    if (!window.confirm(`${user.firstName} ${user.lastName} (${user.email}) wirklich endgültig löschen?`)) return;
    try {
      await api.delete(`/api/admin/users/${user.id}`);
      toast.success("Nutzer gelöscht.");
      await loadUsers(currentPage);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  function togglePasswordForm(userId: string) {
    setPwUserId((current) => (current === userId ? null : userId));
    setNewPassword("");
  }

  async function handleSetPassword(user: AdminUser) {
    if (newPassword.length < 8) {
      toast.error("Passwort muss mindestens 8 Zeichen lang sein.");
      return;
    }
    setSettingPassword(true);
    try {
      await api.post(`/api/admin/users/${user.id}/set-password`, { newPassword });
      toast.success(`Neues Passwort für ${user.email} gesetzt. Bitte dem Nutzer sicher mitteilen.`);
      setPwUserId(null);
      setNewPassword("");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Passwort konnte nicht gesetzt werden.");
    } finally {
      setSettingPassword(false);
    }
  }

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

      <CatalogSection
        scope={{ kind: "global" }}
        title="Globaler Sportarten-Katalog"
        description="Pflegt die für alle Nutzer sichtbaren Sportarten und Übungen nach VDH-Prüfungsordnungen."
      />

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
                <Select value={selectedSportId} onValueChange={(value) => handleSportChange(value ?? "")}>
                  <SelectTrigger>
                    <SelectValue placeholder="Auswählen…" />
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
              <div className="flex flex-col gap-2 sm:w-56">
                <Label>Prüfungsordnung</Label>
                <Select
                  value={selectedRegulationId}
                  disabled={!selectedSportId}
                  onValueChange={(value) => handleRegulationChange(value ?? "")}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Auswählen…" />
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
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">Nutzer</CardTitle>
          {userPage && (
            <span className="text-xs text-muted-foreground">
              {userPage.totalCount} gesamt · Seite {userPage.page} / {userPage.totalPages}
            </span>
          )}
        </CardHeader>
        <CardContent>
          {userPage === null ? (
            <p className="text-sm text-muted-foreground">Lädt…</p>
          ) : (
            <>
              <ul className="flex flex-col gap-2">
                {userPage.users.map((u) => (
                  <li
                    key={u.id}
                    className={`flex flex-col gap-2 rounded-md border px-3 py-2 text-sm ${
                      pwUserId === u.id ? "border-primary/50 bg-primary/5" : ""
                    }`}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span>
                        {u.firstName} {u.lastName} <span className="text-muted-foreground">({u.email})</span>
                      </span>
                      <div className="flex items-center gap-1">
                        {u.roles.map((role) => (
                          <Badge key={role} variant="secondary">
                            {role}
                          </Badge>
                        ))}
                        {u.isLockedOut && <Badge variant="destructive">Gesperrt</Badge>}
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          title="Passwort setzen"
                          onClick={() => togglePasswordForm(u.id)}
                        >
                          <KeyRound className="size-3.5" />
                        </Button>
                        <Button size="icon-sm" variant="ghost" title={u.isLockedOut ? "Entsperren" : "Sperren"} onClick={() => handleToggleLock(u)}>
                          {u.isLockedOut ? <Unlock className="size-3.5" /> : <Lock className="size-3.5" />}
                        </Button>
                        <Button size="icon-sm" variant="ghost" title="Löschen" onClick={() => handleDeleteUser(u)}>
                          <Trash2 className="size-3.5" />
                        </Button>
                      </div>
                    </div>
                    {pwUserId === u.id && (
                      <div className="flex flex-col gap-2 border-t pt-2">
                        <Label htmlFor={`pw-${u.id}`} className="text-xs">
                          Neues Passwort für {u.email}
                        </Label>
                        <div className="flex flex-wrap gap-2">
                          <Input
                            id={`pw-${u.id}`}
                            type="text"
                            autoComplete="off"
                            value={newPassword}
                            onChange={(e) => setNewPassword(e.target.value)}
                            placeholder="Mindestens 8 Zeichen"
                            className="sm:w-64"
                          />
                          <Button size="sm" disabled={settingPassword} onClick={() => handleSetPassword(u)}>
                            {settingPassword ? "Wird gesetzt…" : "Passwort setzen"}
                          </Button>
                          <Button size="sm" variant="ghost" onClick={() => togglePasswordForm(u.id)}>
                            Abbrechen
                          </Button>
                        </div>
                        <p className="text-xs text-muted-foreground">
                          Das Passwort wird im Klartext angezeigt, damit du es dem Nutzer sicher mitteilen kannst.
                          Nach dem Setzen wird es nicht erneut angezeigt.
                        </p>
                      </div>
                    )}
                  </li>
                ))}
              </ul>
              {userPage.totalPages > 1 && (
                <div className="mt-4 flex items-center justify-center gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={currentPage <= 1}
                    onClick={() => loadUsers(currentPage - 1)}
                  >
                    <ChevronLeft className="size-4" />
                    Zurück
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={currentPage >= userPage.totalPages}
                    onClick={() => loadUsers(currentPage + 1)}
                  >
                    Weiter
                    <ChevronRight className="size-4" />
                  </Button>
                </div>
              )}
            </>
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

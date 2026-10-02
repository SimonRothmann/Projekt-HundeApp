"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ChevronRight, Plus, Users } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { Club, Group } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SectionHeading } from "@/components/ui/section-heading";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/**
 * "Meine Gruppen" auf der Trainer-Übersicht: eine Zeile je Gruppe, Antippen
 * öffnet die Gruppe (dort liegen Mitglieder, Welpen-Anmeldungen und
 * Anwesenheit).
 *
 * Vorher war jede Gruppe eine eigene Karte samt Beschreibung und erst ab dem
 * sechsten Bildschirm zu finden - dabei ist sie der Einstieg in fast alles,
 * was an einem Trainingsabend passiert. Hier eine kompakte Liste in EINER
 * Karte; Beschreibung und Trainer:in stehen auf der Gruppenseite.
 *
 * Offene Beitrittsanfragen kommen als Zahl von außen (aus dem Zähler-Abruf der
 * Übersicht), damit hier nicht je Gruppe nachgefragt wird.
 */
export function MyGroupsSection({
  clubs,
  anfragenJeGruppe,
}: {
  /** null = Vereine laden noch (oder sind gescheitert): dann steht bei Vereinsgruppen kein Platzhalter-Name. */
  clubs: Club[] | null;
  anfragenJeGruppe: Record<string, number>;
}) {
  const t = useT();
  const clubListe = clubs ?? [];
  const [groups, setGroups] = useState<Group[] | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [clubId, setClubId] = useState("");
  const [submitting, setSubmitting] = useState(false);
  // "Neue Gruppe" legt man selten an - das Formular bleibt zu, bis man es braucht.
  const [zeigeNeueGruppe, setZeigeNeueGruppe] = useState(false);

  async function loadGroups() {
    try {
      setGroups(await api.get<Group[]>("/api/groups"));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppen konnten nicht geladen werden."));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadGroups();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      await api.post("/api/groups", { name, description: description || null, clubId: clubId || null });
      toast.success(t("Gruppe angelegt."));
      setZeigeNeueGruppe(false);
      setName("");
      setDescription("");
      setClubId("");
      await loadGroups();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Gruppe konnte nicht angelegt werden."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="flex flex-col gap-3">
      <SectionHeading
        icon={Users}
        title={t("Meine Gruppen")}
        action={
          <Button size="sm" variant="outline" onClick={() => setZeigeNeueGruppe((v) => !v)} aria-expanded={zeigeNeueGruppe}>
            <Plus className="size-4" />
            {t("Neue Gruppe")}
          </Button>
        }
      />

      {zeigeNeueGruppe && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t("Neue Gruppe")}</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleCreate} className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex flex-col gap-2 sm:flex-1">
                <Label htmlFor="group-name">{t("Name")}</Label>
                <Input id="group-name" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
              </div>
              <div className="flex flex-col gap-2 sm:flex-1">
                <Label htmlFor="group-description">{t("Beschreibung (optional)")}</Label>
                <Input id="group-description" value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
              {clubListe.length > 0 && (
                <div className="flex flex-col gap-2 sm:w-48">
                  <Label>{t("Verein (optional)")}</Label>
                  <Select value={clubId} onValueChange={(value) => setClubId(value ?? "")}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">{t("Kein Verein")}</SelectItem>
                      {clubListe.map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          {c.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <Button type="submit" disabled={submitting}>
                <Plus className="size-4" />
                {t("Anlegen")}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {groups === null ? (
        <p className="text-muted-foreground">{t("Lädt…")}</p>
      ) : groups.length === 0 ? (
        <Card>
          <CardContent className="py-6 text-center text-muted-foreground">{t("Noch keine Gruppen angelegt.")}</CardContent>
        </Card>
      ) : (
        <Card className="gap-0 py-0">
          <ul className="divide-y">
            {groups.map((group) => {
              const offen = anfragenJeGruppe[group.id] ?? 0;
              const verein = group.clubId && clubs ? (clubs.find((c) => c.id === group.clubId)?.name ?? t("Verein")) : null;
              return (
                <li key={group.id}>
                  <Link
                    href={`/trainer/${group.id}`}
                    className="flex min-w-0 items-center gap-3 px-4 py-2.5 transition-colors hover:bg-accent/30 coarse:min-h-14"
                  >
                    <span className="flex min-w-0 flex-1 flex-col">
                      <span className="text-sm font-medium [overflow-wrap:anywhere]">{group.name}</span>
                      <span className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                        {[verein, group.memberCount === 1 ? t("1 Mitglied") : t("{n} Mitglieder", { n: group.memberCount })]
                          .filter(Boolean)
                          .join(" · ")}
                      </span>
                    </span>
                    {offen > 0 && <Badge>{offen === 1 ? t("1 Anfrage") : t("{n} Anfragen", { n: offen })}</Badge>}
                    <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
                  </Link>
                </li>
              );
            })}
          </ul>
        </Card>
      )}
    </section>
  );
}

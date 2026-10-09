"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, ClubMemberRequest, Group } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/dogs/eintragen-chip";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { UserCheck } from "lucide-react";
import { toast } from "sonner";

import { useT } from "@/lib/i18n";
import { einladbareGruppen, gewaehlteGruppe } from "@/lib/beitrittsfreigabe";

/**
 * Offene Beitrittsanfragen an die eigenen Vereine.
 *
 * Steht auf /trainer/anfragen und zeigt sich nur, solange es offene Anfragen
 * gibt - den Leerzustand ("Keine offenen Anfragen.") zeigt die Seite, die
 * über `onAnzahl` erfährt, wie viele es sind. Dafür werden die Anfragen ALLER
 * Vereine geladen, nicht nur die des gewählten: Sonst verschwände der Kasten
 * samt Vereinsauswahl, sobald der erste Verein keine Anfragen hat, und die
 * eines zweiten Vereins wären unerreichbar.
 *
 * Hat der Verein Gruppen, lädt "Annehmen" auf Wunsch gleich in eine davon ein
 * ("In Gruppe einladen:"). Eine Einladung, keine Aufnahme: Die Vereinsanfrage
 * ist keine Zustimmung zur Gruppe, Mitglied wird erst, wer annimmt. So braucht
 * ein neues Mitglied nicht mehr zwei Freigaben derselben Trainer:in.
 *
 * `onFehler` meldet, wenn eine Abfrage scheiterte: Die Seite darf dann nicht
 * "Keine offenen Anfragen." behaupten.
 */
export function ClubJoinRequestsSection({
  clubs,
  onAnzahl,
  onFehler,
}: {
  clubs: Club[];
  onAnzahl?: (anzahl: number) => void;
  onFehler?: () => void;
}) {
  const t = useT();
  const [anfragen, setAnfragen] = useState<Record<string, ClubMemberRequest[]> | null>(null);
  const [selectedClubId, setSelectedClubId] = useState("");
  // Gruppen je Verein, die die Trainer:in verwalten darf - Ziel der Einladung.
  const [gruppen, setGruppen] = useState<Record<string, Group[]>>({});
  // Die Wahl je Anfrage: fehlt = nichts angetippt (dann gilt die Vorauswahl), null = ausdrücklich "Keine".
  const [wahl, setWahl] = useState<Record<string, string | null>>({});
  const vereineSchluessel = clubs.map((c) => c.id).join(",");

  async function laden() {
    const ids = vereineSchluessel.split(",").filter(Boolean);
    const ergebnisse = await Promise.all(
      ids.map(async (id) => {
        try {
          return [id, await api.get<ClubMemberRequest[]>(`/api/clubs/${id}/join-requests`)] as const;
        } catch (err) {
          toast.error(err instanceof ApiError ? err.message : t("Beitrittsanfragen konnten nicht geladen werden."));
          onFehler?.();
          return [id, [] as ClubMemberRequest[]] as const;
        }
      }),
    );
    const nachVerein = Object.fromEntries(ergebnisse);
    setAnfragen(nachVerein);
    // Ohne Gruppen (oder wenn der Abruf scheitert) bleibt es beim Annehmen wie bisher.
    const gruppenListen = await Promise.all(
      ids.map((id) =>
        api
          .get<Group[]>(`/api/clubs/${id}/groups`)
          .then((liste) => [id, einladbareGruppen(liste)] as const)
          .catch(() => [id, [] as Group[]] as const),
      ),
    );
    setGruppen(Object.fromEntries(gruppenListen));
    onAnzahl?.(Object.values(nachVerein).reduce((summe, liste) => summe + liste.length, 0));
    // Beim gewählten Verein bleiben, solange er noch Anfragen hat; sonst zum
    // nächsten mit offenen Anfragen wechseln.
    setSelectedClubId((aktuell) =>
      aktuell && nachVerein[aktuell]?.length ? aktuell : (ids.find((id) => nachVerein[id].length > 0) ?? ids[0] ?? ""),
    );
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [vereineSchluessel]);

  async function handleDecide(r: ClubMemberRequest, approve: boolean) {
    // Ablehnen lässt sich nicht zurücknehmen und liegt nach dem Annehmen-Knopf: ein Fehltipp kostet sonst eine Anfrage.
    if (!approve && !window.confirm(t("Beitrittsanfrage von {name} ablehnen?", { name: `${r.firstName} ${r.lastName}`.trim() || r.email }))) return;
    const membershipId = r.membershipId;
    // Nur beim Annehmen; beim Ablehnen ignoriert der Server die Gruppe ohnehin.
    const verfuegbar = gruppen[selectedClubId] ?? [];
    const gruppeId = approve ? gewaehlteGruppe(wahl[membershipId], verfuegbar) : null;
    const gruppe = verfuegbar.find((g) => g.id === gruppeId);
    try {
      await api.post(
        `/api/clubs/${selectedClubId}/join-requests/${membershipId}/${approve ? "approve" : "reject"}`,
        approve && gruppeId ? { groupId: gruppeId } : undefined,
      );
      toast.success(
        !approve
          ? t("Beitritt abgelehnt.")
          : gruppe
            ? t("Angenommen und in {gruppe} eingeladen.", { gruppe: gruppe.name })
            : t("Beitritt angenommen."),
      );
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Aktion fehlgeschlagen."));
    }
  }

  if (!anfragen) return null;
  const offen = Object.values(anfragen).reduce((summe, liste) => summe + liste.length, 0);
  if (offen === 0) return null;
  const requests = anfragen[selectedClubId] ?? [];
  const einladbar = gruppen[selectedClubId] ?? [];
  const gewaehltFuer = (r: ClubMemberRequest) => gewaehlteGruppe(wahl[r.membershipId], einladbar);

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2 space-y-0">
        <CardTitle className="flex items-center gap-2 text-base">
          <UserCheck className="size-5" />
          {t("Beitrittsanfragen")}
          <Badge variant="secondary">{t("{anzahl} offen", { anzahl: offen })}</Badge>
        </CardTitle>
        {clubs.length > 1 && (
          <Select value={selectedClubId} onValueChange={(value) => setSelectedClubId(value ?? "")}>
            <SelectTrigger className="w-48 max-w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {clubs.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </CardHeader>
      <CardContent>
        {requests.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Keine offenen Anfragen.")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {requests.map((r) => (
              <li key={r.membershipId} className="flex flex-col gap-3 rounded-md border px-3 py-2">
                <div className="flex min-w-0 flex-col items-start gap-1">
                  <span className="min-w-0 text-sm [overflow-wrap:anywhere]">
                    {r.firstName} {r.lastName} ({r.email})
                  </span>
                  {/* 1 = über den Einladungslink bzw. QR-Code des Vereins */}
                  {r.source === 1 && <Badge variant="outline">{t("über Einladungslink")}</Badge>}
                </div>
                {einladbar.length > 0 && (
                  <div className="flex min-w-0 flex-col gap-1.5">
                    <p className="text-sm text-muted-foreground">{t("In Gruppe einladen:")}</p>
                    <div className="flex flex-wrap gap-2">
                      <Chip gewaehlt={gewaehltFuer(r) === null} onClick={() => setWahl((w) => ({ ...w, [r.membershipId]: null }))}>
                        {t("Keine")}
                      </Chip>
                      {einladbar.map((g) => (
                        <Chip
                          key={g.id}
                          gewaehlt={gewaehltFuer(r) === g.id}
                          onClick={() => setWahl((w) => ({ ...w, [r.membershipId]: g.id }))}
                        >
                          {g.name}
                        </Chip>
                      ))}
                    </div>
                  </div>
                )}
                {/* Beschriftete Knöpfe statt Symbolen: Haken und Kreuz nebeneinander wurden verwechselt. */}
                <div className="flex flex-wrap gap-3">
                  <Button size="sm" onClick={() => handleDecide(r, true)}>
                    {t("Annehmen")}
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => handleDecide(r, false)}>
                    {t("Ablehnen")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

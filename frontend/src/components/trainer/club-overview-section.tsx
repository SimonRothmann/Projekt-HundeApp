"use client";

import { useEffect, useMemo, useState } from "react";
import { LayoutDashboard } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import type { ClubOverview, ClubOverviewMember } from "@/lib/types";
import { filtereMitglieder, mitgliedName, monatsLabel } from "@/lib/vereinsuebersicht";
import { rollenVon, ROLLEN } from "@/lib/vereinsrollen";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Abschnittsname } from "@/components/dogs/eintragen-chip";

/**
 * Die Vereinsübersicht für Vorstand und Verwaltung: Zahlen aus den Vereinsdaten
 * (Mitglieder, Rollen, Gruppen, Termine je Monat) und die Mitgliederliste mit
 * Namen, Gruppen und Rollen - ohne E-Mail-Adressen und ohne Trainingsdaten
 * einzelner Personen. Nur lesen: Der Server entscheidet über das Recht
 * (`canViewOverview`), die Seite zeigt den Abschnitt nur dann an.
 */
export function ClubOverviewSection({ clubId }: { clubId: string }) {
  const t = useT();
  const ort = ortsformat(useSprache());
  const [uebersicht, setUebersicht] = useState<ClubOverview | null>(null);
  const [mitglieder, setMitglieder] = useState<ClubOverviewMember[] | null>(null);
  const [fehler, setFehler] = useState(false);
  const [suche, setSuche] = useState("");

  useEffect(() => {
    let abgebrochen = false;
    Promise.all([
      api.get<ClubOverview>(`/api/clubs/${clubId}/overview`),
      api.get<ClubOverviewMember[]>(`/api/clubs/${clubId}/overview/members`),
    ])
      .then(([u, m]) => {
        if (abgebrochen) return;
        setUebersicht(u);
        setMitglieder(m);
      })
      .catch((err) => {
        if (abgebrochen) return;
        setFehler(true);
        toast.error(err instanceof ApiError ? err.message : t("Die Vereinsübersicht konnte nicht geladen werden."));
      });
    return () => {
      abgebrochen = true;
    };
    // t bewusst nicht in der Liste: nur für die Fehlermeldung, ein Sprachwechsel soll nicht neu laden.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clubId]);

  const treffer = useMemo(() => filtereMitglieder(mitglieder ?? [], suche), [mitglieder, suche]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <LayoutDashboard className="size-5" />
          {t("Vereinsübersicht")}
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {t("Nur Zahlen des Vereins und die Mitgliederliste mit Namen und Gruppen - keine Trainingsdaten, keine E-Mail-Adressen.")}
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        {fehler ? (
          <p className="text-sm text-muted-foreground">{t("Die Vereinsübersicht konnte nicht geladen werden.")}</p>
        ) : uebersicht === null || mitglieder === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <>
            <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Kachel label={t("Mitglieder")} wert={uebersicht.memberCount} />
              <Kachel label={t("Neu in 30 Tagen")} wert={uebersicht.newMembersLast30Days} />
              <Kachel label={t("Offene Anfragen")} wert={uebersicht.pendingRequests} />
              <Kachel label={t("Offene Einladungen")} wert={uebersicht.openInvitations} />
            </dl>

            <div className="flex flex-col gap-2">
              <Abschnittsname>{t("Rollen")}</Abschnittsname>
              <ul className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
                {ROLLEN.map((r) => (
                  <li key={r.bit} className="min-w-0 [overflow-wrap:anywhere]">
                    {t(r.name)}: <span className="font-medium tabular-nums">{uebersicht.roles.find((x) => x.role === r.bit)?.count ?? 0}</span>
                  </li>
                ))}
              </ul>
            </div>

            <div className="flex flex-col gap-2">
              <Abschnittsname>{t("Gruppen")}</Abschnittsname>
              {uebersicht.groups.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t("Noch keine Gruppen.")}</p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {uebersicht.groups.map((g) => (
                    <li key={g.id} className="flex min-w-0 flex-col gap-0.5 rounded-md border px-3 py-2">
                      <span className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">{g.name}</span>
                      <span className="text-xs text-muted-foreground">
                        {t("Mitglieder: {mitglieder} · Trainer:innen: {trainer}", { mitglieder: g.memberCount, trainer: g.trainerCount })}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              <p className="text-sm text-muted-foreground">
                {t("Mitglieder ohne Gruppe: {anzahl}", { anzahl: uebersicht.membersWithoutGroup })}
              </p>
            </div>

            <div className="flex flex-col gap-2">
              <Abschnittsname>{t("Gruppentermine, letzte 6 Monate")}</Abschnittsname>
              <table className="w-full table-fixed text-sm">
                <thead>
                  <tr className="text-left text-xs text-muted-foreground">
                    <th scope="col" className="w-2/5 py-1 pr-2 font-medium">{t("Monat")}</th>
                    <th scope="col" className="px-1 py-1 text-right font-medium">{t("Termine")}</th>
                    <th scope="col" className="px-1 py-1 text-right font-medium">{t("Abgesagt")}</th>
                    <th scope="col" className="py-1 pl-1 text-right font-medium">{t("Zusagen")}</th>
                  </tr>
                </thead>
                <tbody>
                  {uebersicht.months.map((m) => (
                    <tr key={`${m.year}-${m.month}`} className="border-t">
                      <th scope="row" className="py-1.5 pr-2 text-left font-normal">{monatsLabel(m.year, m.month, ort)}</th>
                      <td className="px-1 py-1.5 text-right tabular-nums">{m.planned}</td>
                      <td className="px-1 py-1.5 text-right tabular-nums">{m.cancelled}</td>
                      <td className="py-1.5 pl-1 text-right tabular-nums">{m.attendances}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="text-xs text-muted-foreground">
                {t("Termine: stattgefundene und geplante, ohne abgesagte. Zusagen: Summe der Zusagen von Gruppenmitgliedern zu diesen Terminen.")}
              </p>
            </div>

            <div className="flex flex-col gap-3">
              <Abschnittsname>{t("Mitglieder")}</Abschnittsname>
              {mitglieder.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t("Noch keine Mitglieder.")}</p>
              ) : (
                <>
                  <Input
                    type="search"
                    className="min-w-0"
                    value={suche}
                    onChange={(e) => setSuche(e.target.value)}
                    placeholder={t("Name oder Gruppe suchen")}
                    aria-label={t("Name oder Gruppe suchen")}
                  />
                  {treffer.length === 0 ? (
                    <p className="text-sm text-muted-foreground">{t("Keine Treffer.")}</p>
                  ) : (
                    <ul className="flex flex-col gap-2">
                      {treffer.map((m) => (
                        <li key={m.membershipId} className="flex min-w-0 flex-col gap-1.5 rounded-md border px-3 py-2">
                          <span className="min-w-0 text-sm font-medium [overflow-wrap:anywhere]">
                            {mitgliedName(m) || t("(unbekannt)")}
                          </span>
                          <span className="text-xs text-muted-foreground">
                            {t("Mitglied seit {datum}", { datum: new Date(m.memberSince).toLocaleDateString(ort) })}
                          </span>
                          {(m.groups.length > 0 || m.roles !== 0) && (
                            <div className="flex flex-wrap gap-1">
                              {m.groups.map((g) => (
                                <span
                                  key={g}
                                  className="inline-flex max-w-full min-w-0 rounded-full border px-2 py-0.5 text-xs [overflow-wrap:anywhere]"
                                >
                                  {g}
                                </span>
                              ))}
                              {rollenVon(m.roles).map((r) => (
                                <Badge key={r.bit} variant="secondary">
                                  {t(r.name)}
                                </Badge>
                              ))}
                            </div>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              )}
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Kachel({ label, wert }: { label: string; wert: number }) {
  return (
    <div className="flex min-w-0 flex-col rounded-md border px-3 py-2">
      {/* Im Quelltext Begriff vor Wert (so liest es ein Screenreader), gezeigt wird die Zahl darüber. */}
      <dt className="order-2 text-xs text-muted-foreground [overflow-wrap:anywhere]">{label}</dt>
      <dd className="order-1 text-2xl font-semibold tabular-nums">{wert}</dd>
    </div>
  );
}

"use client";

import { useEffect, useState } from "react";
import { ShieldCheck, UserPlus } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import type { ClubMemberRequest, ClubTrainerInfo } from "@/lib/types";
import { aenderbareRollen, darfAbberufen, hatRolle, mitRolle, ROLLE, ROLLEN, rollenVon } from "@/lib/vereinsrollen";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Chip } from "@/components/dogs/eintragen-chip";

function anzeigename(p: { firstName: string; lastName: string; email?: string | null }) {
  return `${p.firstName} ${p.lastName}`.trim() || p.email || "";
}

/**
 * Wer im Verein welche Rolle hat - und wer sie vergeben und entziehen darf.
 *
 * Die Verwaltung (und der Admin) ändern alle Rollen, eine Ausbildungswart:in
 * nur Training; was sonst nicht änderbar ist, steht als Marke da. Der Server
 * entscheidet (lib/vereinsrollen.ts spiegelt nur die Regel, damit nichts
 * angeboten wird, was dort abgewiesen würde).
 *
 * Berufen wird aus den Mitgliedern des Vereins: Wer die Mitgliederliste lesen
 * darf (`mitgliederLesbar`), wählt dort; sonst - Ausbildungswart:in, Admin -
 * gibt man die E-Mail-Adresse eines Mitglieds ein. Eine Person ohne Mitglied-
 * schaft weist der Server ab (außer beim Admin).
 *
 * `onGeaendert` meldet der Seite, dass sich Rollen geändert haben: Wer sich
 * selbst Rechte nimmt, sieht danach andere Abschnitte.
 */
export function ClubRolesSection({
  clubId,
  canManage,
  canAppointTrainers,
  istAdmin = false,
  mitgliederLesbar,
  onGeaendert,
}: {
  clubId: string;
  canManage?: boolean;
  canAppointTrainers?: boolean;
  istAdmin?: boolean;
  mitgliederLesbar?: boolean;
  onGeaendert?: () => void | Promise<void>;
}) {
  const t = useT();
  const aenderbar = aenderbareRollen({ canManage, canAppointTrainers }, istAdmin);
  const [personen, setPersonen] = useState<ClubTrainerInfo[] | null>(null);
  const [mitglieder, setMitglieder] = useState<ClubMemberRequest[]>([]);
  const [bearbeitet, setBearbeitet] = useState<string | null>(null);
  const [arbeitet, setArbeitet] = useState<string | null>(null);
  // Neu berufen: eine Person aus der Liste (Id) oder per E-Mail - und ihre Rollen (Standard: Training).
  const [wahl, setWahl] = useState("");
  const [mail, setMail] = useState("");
  const [neueRollen, setNeueRollen] = useState<number>(ROLLE.training);

  async function laden() {
    try {
      setPersonen(await api.get<ClubTrainerInfo[]>(`/api/clubs/${clubId}/roles`));
    } catch (err) {
      setPersonen([]);
      toast.error(err instanceof ApiError ? err.message : t("Rollen konnten nicht geladen werden."));
    }
    if (mitgliederLesbar) {
      // Die Liste ist Beiwerk: Ohne sie bleibt die Eingabe per E-Mail-Adresse.
      setMitglieder(await api.get<ClubMemberRequest[]>(`/api/clubs/${clubId}/members`).catch(() => []));
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    laden();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clubId]);

  async function nachAenderung() {
    await laden();
    await onGeaendert?.();
  }

  async function setzen(p: ClubTrainerInfo, rollen: number) {
    // Die letzte Rolle zu nehmen heißt abberufen - und das lässt sich nicht von selbst zurücknehmen.
    if (rollen === 0) return abberufen(p);
    setArbeitet(p.userId);
    try {
      await api.put(`/api/clubs/${clubId}/trainers/${p.userId}/role`, { role: rollen });
      toast.success(t("Rollen geändert."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setArbeitet(null);
    }
  }

  async function abberufen(p: ClubTrainerInfo) {
    if (!window.confirm(t("{name} alle Rollen im Verein entziehen?", { name: anzeigename(p) }))) return;
    setArbeitet(p.userId);
    try {
      await api.delete(`/api/clubs/${clubId}/trainers/${p.userId}`);
      toast.success(t("Rollen entzogen."));
      setBearbeitet(null);
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    } finally {
      setArbeitet(null);
    }
  }

  // Wer schon eine Rolle hat, wird über "Rollen ändern" bearbeitet - nicht erneut berufen.
  const kandidaten = mitglieder.filter((m) => !(personen ?? []).some((p) => p.userId === m.userId));
  const gewaehlt = kandidaten.find((m) => m.userId === wahl);
  const adresse = mitgliederLesbar ? (gewaehlt?.email ?? "") : mail.trim();

  async function berufen(e: React.FormEvent) {
    e.preventDefault();
    if (!adresse || neueRollen === 0) return;
    setArbeitet("neu");
    try {
      await api.post(`/api/clubs/${clubId}/trainers`, { email: adresse, role: neueRollen });
      toast.success(t("Person berufen."));
      setWahl("");
      setMail("");
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Berufen fehlgeschlagen."));
    } finally {
      setArbeitet(null);
    }
  }

  if (aenderbar.length === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ShieldCheck className="size-5" />
          {t("Rollen im Verein")}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {personen === null ? (
          <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : personen.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("Noch niemand hat eine Rolle.")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {personen.map((p) => (
              <li key={p.userId} className="flex min-w-0 flex-col gap-2 rounded-md border px-3 py-2">
                <div className="flex min-w-0 flex-col gap-1.5">
                  <span className="min-w-0 text-sm [overflow-wrap:anywhere]">
                    {p.firstName} {p.lastName}{p.email ? ` (${p.email})` : ""}
                  </span>
                  <div className="flex flex-wrap gap-1">
                    {rollenVon(p.roles).map((r) => (
                      <Badge key={r.bit} variant="secondary">
                        {t(r.name)}
                      </Badge>
                    ))}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    aria-expanded={bearbeitet === p.userId}
                    onClick={() => setBearbeitet(bearbeitet === p.userId ? null : p.userId)}
                  >
                    {t("Rollen ändern")}
                  </Button>
                  {darfAbberufen(p.roles, aenderbar) && (
                    <Button size="sm" variant="outline" disabled={arbeitet === p.userId} onClick={() => abberufen(p)}>
                      {t("Abberufen")}
                    </Button>
                  )}
                </div>
                {bearbeitet === p.userId && (
                  <div className="flex flex-wrap gap-2">
                    {ROLLEN.map((r) =>
                      aenderbar.includes(r.bit) ? (
                        <Chip
                          key={r.bit}
                          gewaehlt={hatRolle(p.roles, r.bit)}
                          disabled={arbeitet === p.userId}
                          onClick={() => setzen(p, mitRolle(p.roles, r.bit, !hatRolle(p.roles, r.bit)))}
                        >
                          {t(r.name)}
                        </Chip>
                      ) : null,
                    )}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={berufen} className="flex flex-col gap-3 border-t pt-4">
          <p className="text-sm font-medium">{t("Person berufen")}</p>
          {mitgliederLesbar ? (
            kandidaten.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("Alle Mitglieder haben schon eine Rolle.")}</p>
            ) : (
              <Select value={wahl} onValueChange={(v) => setWahl(v ?? "")}>
                <SelectTrigger className="w-full max-w-full" aria-label={t("Mitglied wählen")}>
                  <SelectValue placeholder={t("Mitglied wählen")} />
                </SelectTrigger>
                <SelectContent>
                  {kandidaten.map((m) => (
                    <SelectItem key={m.userId} value={m.userId}>
                      {anzeigename(m)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )
          ) : (
            <Input
              type="email"
              className="min-w-0"
              placeholder={t("E-Mail-Adresse eines Mitglieds")}
              aria-label={t("E-Mail-Adresse eines Mitglieds")}
              value={mail}
              onChange={(e) => setMail(e.target.value)}
            />
          )}
          {/* Wer nur Training vergeben darf, hat nichts zu wählen. */}
          {aenderbar.length > 1 && (
            <div className="flex flex-wrap gap-2">
              {ROLLEN.map((r) =>
                aenderbar.includes(r.bit) ? (
                  <Chip
                    key={r.bit}
                    gewaehlt={hatRolle(neueRollen, r.bit)}
                    onClick={() => setNeueRollen(mitRolle(neueRollen, r.bit, !hatRolle(neueRollen, r.bit)))}
                  >
                    {t(r.name)}
                  </Chip>
                ) : null,
              )}
            </div>
          )}
          <Button type="submit" className="h-11 self-start" disabled={!adresse || neueRollen === 0 || arbeitet === "neu"}>
            <UserPlus className="size-4" />
            {t("Berufen")}
          </Button>
        </form>

        <div className="flex flex-col gap-2 border-t pt-4">
          <p className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{t("Was die Rollen dürfen")}</p>
          <ul className="flex flex-col gap-1.5 text-sm">
            {ROLLEN.map((r) => (
              <li key={r.bit} className="min-w-0 [overflow-wrap:anywhere]">
                <span className="font-medium">{t(r.name)}:</span> <span className="text-muted-foreground">{t(r.erklaerung)}</span>
              </li>
            ))}
          </ul>
        </div>
      </CardContent>
    </Card>
  );
}

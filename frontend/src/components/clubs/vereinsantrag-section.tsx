"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { CLUB_REGISTRATION_STATUS, type ClubRegistration } from "@/lib/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Building2, Clock } from "lucide-react";
import { toast } from "sonner";

/**
 * Einen Verein beantragen - und den Stand des eigenen Antrags sehen.
 *
 * Warum ein Antrag und kein direktes Anlegen: Vereinsnamen sind real
 * vergeben. Wer einen fremden Namen besetzt, blockiert ihn für die, die
 * wirklich dazugehören (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
 *
 * Der abgelehnte Antrag bleibt mit Begründung stehen. Ein Antrag, der
 * kommentarlos verschwindet, wirkt wie ein Fehler der App.
 */
export function VereinsantragSection() {
  const [antraege, setAntraege] = useState<ClubRegistration[] | null>(null);
  const [name, setName] = useState("");
  const [beschreibung, setBeschreibung] = useState("");
  const [sendet, setSendet] = useState(false);

  async function laden() {
    try {
      setAntraege(await api.get<ClubRegistration[]>("/api/clubs/registrations/mine"));
    } catch {
      // Beiwerk: Ohne die Liste bleibt der Antragsweg trotzdem nutzbar.
      setAntraege([]);
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, []);

  async function beantragen(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSendet(true);
    try {
      await api.post("/api/clubs/registrations", { name, description: beschreibung || null });
      toast.success("Antrag gestellt. Ein Administrator prüft ihn.");
      setName("");
      setBeschreibung("");
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Antrag konnte nicht gestellt werden.");
    } finally {
      setSendet(false);
    }
  }

  const offen = antraege?.some((a) => a.status === CLUB_REGISTRATION_STATUS.offen) ?? false;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Building2 className="size-5" />
          Verein gründen
        </CardTitle>
        <CardDescription>
          Dein Verein fehlt? Beantrage ihn – ein Administrator gibt ihn frei. Danach verwaltest du ihn selbst.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {antraege && antraege.length > 0 && (
          <ul className="flex flex-col gap-2">
            {antraege.map((a) => (
              <li
                key={a.id}
                className="flex flex-wrap items-center justify-between gap-x-2 gap-y-1 rounded-md border px-3 py-2 text-sm"
              >
                <span className="min-w-[10rem] flex-1 [overflow-wrap:anywhere]">
                  {a.name}
                  {a.decisionNote && <span className="block text-xs text-muted-foreground">{a.decisionNote}</span>}
                </span>
                {a.status === CLUB_REGISTRATION_STATUS.offen && (
                  <Badge variant="secondary" className="shrink-0">
                    <Clock className="mr-1 size-3" />
                    In Prüfung
                  </Badge>
                )}
                {a.status === CLUB_REGISTRATION_STATUS.freigegeben && <Badge className="shrink-0">Freigegeben</Badge>}
                {a.status === CLUB_REGISTRATION_STATUS.abgelehnt && (
                  <Badge variant="destructive" className="shrink-0">
                    Abgelehnt
                  </Badge>
                )}
              </li>
            ))}
          </ul>
        )}

        {offen ? (
          <p className="text-sm text-muted-foreground">
            Dein Antrag wird geprüft. Du bekommst eine Benachrichtigung, sobald entschieden ist.
          </p>
        ) : (
          <form className="flex flex-col gap-3" onSubmit={beantragen}>
            <div className="flex flex-col gap-2">
              <Label htmlFor="verein-name">Name des Vereins</Label>
              <Input
                id="verein-name"
                placeholder="z.B. Hundesportverein Beispieldorf e.V."
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="verein-beschreibung">Beschreibung (optional)</Label>
              <Input
                id="verein-beschreibung"
                placeholder="Kurz, worum es geht"
                value={beschreibung}
                onChange={(e) => setBeschreibung(e.target.value)}
              />
            </div>
            <Button type="submit" className="self-start" disabled={sendet || !name.trim()}>
              {sendet ? "Wird gesendet…" : "Verein beantragen"}
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}

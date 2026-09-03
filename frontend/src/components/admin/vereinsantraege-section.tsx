"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { ClubRegistration } from "@/lib/types";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Building2, Check, X } from "lucide-react";
import { toast } from "sonner";

/**
 * Offene Vereinsanträge freigeben oder ablehnen.
 *
 * Die Freigabe legt den Verein an UND macht den Antragsteller zu seiner
 * ersten verwaltenden Person - sonst entstünde ein Verein, den niemand
 * betreuen kann, und die Prüfung hätte nur Arbeit verschoben.
 *
 * Beim Ablehnen ist die Begründung wichtig genug für ein eigenes Feld: Sie
 * geht als Benachrichtigung an den Antragsteller. Eine kommentarlose
 * Ablehnung führt nur zum nächsten, gleichlautenden Antrag.
 */
export function VereinsantraegeSection() {
  const [antraege, setAntraege] = useState<ClubRegistration[] | null>(null);
  const [begruendung, setBegruendung] = useState<Record<string, string>>({});
  const [laeuft, setLaeuft] = useState<string | null>(null);

  async function laden() {
    try {
      setAntraege(await api.get<ClubRegistration[]>("/api/admin/club-registrations"));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Anträge konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, []);

  async function entscheiden(id: string, freigeben: boolean) {
    setLaeuft(id);
    try {
      if (freigeben) {
        await api.post(`/api/admin/club-registrations/${id}/approve`);
        toast.success("Verein freigegeben.");
      } else {
        await api.post(`/api/admin/club-registrations/${id}/reject`, { note: begruendung[id] ?? null });
        toast.success("Antrag abgelehnt.");
      }
      await laden();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Entscheidung fehlgeschlagen.");
    } finally {
      setLaeuft(null);
    }
  }

  // Der Abschnitt verschwindet, wenn nichts anliegt - eine leere Karte auf
  // der ohnehin langen Admin-Seite wäre nur Rauschen.
  if (antraege !== null && antraege.length === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Building2 className="size-5" />
          Vereinsanträge
        </CardTitle>
        <CardDescription>
          Freigeben legt den Verein an und macht den Antragsteller zu seiner ersten verwaltenden Person.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {antraege === null ? (
          <p className="text-sm text-muted-foreground">Lädt…</p>
        ) : (
          antraege.map((a) => (
            <div key={a.id} className="flex flex-col gap-2 rounded-md border px-3 py-2">
              <div className="text-sm">
                <p className="font-medium [overflow-wrap:anywhere]">{a.name}</p>
                {a.description && (
                  <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">{a.description}</p>
                )}
                <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                  {a.requestedByName} ({a.requestedByEmail})
                </p>
              </div>
              <Input
                placeholder="Begründung bei Ablehnung (optional)"
                value={begruendung[a.id] ?? ""}
                onChange={(e) => setBegruendung((v) => ({ ...v, [a.id]: e.target.value }))}
              />
              <div className="flex flex-wrap gap-2">
                <Button size="sm" disabled={laeuft === a.id} onClick={() => void entscheiden(a.id, true)}>
                  <Check className="size-4" />
                  Freigeben
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={laeuft === a.id}
                  onClick={() => void entscheiden(a.id, false)}
                >
                  <X className="size-4" />
                  Ablehnen
                </Button>
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

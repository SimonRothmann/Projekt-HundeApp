"use client";

import { useState } from "react";
import { Building2 } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";
import type { Club } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/**
 * Name und Beschreibung des Vereins - für die Verwaltung des Vereins (der
 * Server prüft das Recht; die Seite zeigt den Abschnitt nur dann).
 *
 * "Speichern" ist erst aktiv, wenn sich etwas geändert hat, wie beim
 * Bearbeiten einer Gruppe. Nach dem Speichern lädt die Seite die Vereine neu
 * (`onGespeichert`), damit der neue Name überall steht.
 */
export function ClubStammdatenSection({ club, onGespeichert }: { club: Club; onGespeichert: () => Promise<void> }) {
  const t = useT();
  const [name, setName] = useState(club.name);
  const [beschreibung, setBeschreibung] = useState(club.description ?? "");
  const [speichert, setSpeichert] = useState(false);

  const geaendert = name.trim() !== club.name || beschreibung.trim() !== (club.description ?? "");

  async function speichern(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      toast.error(t("Name ist erforderlich."));
      return;
    }
    setSpeichert(true);
    try {
      await api.put(`/api/clubs/${club.id}`, { name: name.trim(), description: beschreibung.trim() || null });
      toast.success(t("Verein aktualisiert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Verein konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Building2 className="size-5" />
          {t("Verein")}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={speichern} className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`verein-name-${club.id}`}>{t("Name")}</Label>
            <Input
              id={`verein-name-${club.id}`}
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={TEXTLAENGE.vereinsName}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor={`verein-beschreibung-${club.id}`}>{t("Beschreibung (optional)")}</Label>
            <Input
              id={`verein-beschreibung-${club.id}`}
              value={beschreibung}
              onChange={(e) => setBeschreibung(e.target.value)}
              maxLength={TEXTLAENGE.vereinsBeschreibung}
            />
          </div>
          <Button type="submit" className="h-11 self-start" disabled={speichert || !geaendert}>
            {speichert ? t("Speichert…") : t("Speichern")}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

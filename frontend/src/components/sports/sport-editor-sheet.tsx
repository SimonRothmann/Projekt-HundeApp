"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { toast } from "sonner";

// "global" ist der Sentinel-Wert für den Admin-Auswahleintrag "Für alle
// Nutzer sichtbar (global)" - Select erwartet einen non-empty String, das
// leere String-Verhalten kollidiert mit dem "Kein Wert"-Placeholder.
const GLOBAL_SENTINEL = "__global__";

/**
 * Sheet-basierter Editor zum Anlegen einer neuen Sportart.
 *
 * - Admin sieht die Auswahl "Global | Verein X | Verein Y..." und kann
 *   frei entscheiden.
 * - Vereinstrainer sieht nur die Vereine, in denen er als Trainer geführt
 *   wird - dementsprechend fällt "Global" bei ihm weg.
 *
 * Der Code muss innerhalb seines Sichtbarkeitsbereichs eindeutig sein
 * (siehe SportConfiguration.HasIndex(Code, ClubId)) - Vereine dürfen also
 * einen "GRUND"-Code haben, ohne mit dem globalen Katalog zu kollidieren.
 */
export function SportEditorSheet({
  open,
  onOpenChange,
  isAdmin,
  clubs,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  isAdmin: boolean;
  clubs: Club[];
  onCreated: (sport: Sport) => void;
}) {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  // Default: Admin startet auf "Global", Trainer auf ersten Verein.
  const [scope, setScope] = useState<string>(isAdmin ? GLOBAL_SENTINEL : clubs[0]?.id ?? "");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      // Formular auf sinnvolle Defaults zurücksetzen, wenn das Sheet erneut
      // geöffnet wird - insbesondere die Scope-Auswahl (Rolle des Nutzers
      // kann sich zwischen Öffnungen theoretisch geändert haben).
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setCode("");
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setName("");
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setDescription("");
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setScope(isAdmin ? GLOBAL_SENTINEL : clubs[0]?.id ?? "");
    }
  }, [open, isAdmin, clubs]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;
    setSubmitting(true);
    try {
      const created = await api.post<Sport>("/api/sports", {
        code: code.trim().toUpperCase(),
        name: name.trim(),
        description: description.trim() || null,
        clubId: scope === GLOBAL_SENTINEL ? null : scope,
      });
      toast.success("Sportart angelegt.");
      onCreated(created);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Sportart konnte nicht angelegt werden.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>Neue Sportart</SheetTitle>
          <SheetDescription>
            Sportarten strukturieren den Übungskatalog. Der Code dient als kurze Anzeige-Kennung
            (z.&nbsp;B. &bdquo;BH&ldquo;, &bdquo;IBGH&ldquo;).
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit} className="flex flex-1 flex-col gap-4 overflow-y-auto px-4">
          <div className="grid gap-4 sm:grid-cols-[8rem_1fr]">
            <div className="flex flex-col gap-2">
              <Label htmlFor="sport-code">Code</Label>
              <Input
                id="sport-code"
                required
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="GRUND"
                maxLength={30}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="sport-name">Name</Label>
              <Input
                id="sport-name"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Grundlagen-Training"
                maxLength={100}
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="sport-description">Beschreibung (optional)</Label>
            <Input
              id="sport-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Kurz, worum es geht"
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label>Sichtbarkeit</Label>
            <Select value={scope} onValueChange={(v) => v && setScope(v)}>
              <SelectTrigger>
                <SelectValue placeholder="Auswählen…" />
              </SelectTrigger>
              <SelectContent>
                {isAdmin && (
                  <SelectItem value={GLOBAL_SENTINEL}>Global (für alle sichtbar)</SelectItem>
                )}
                {clubs.map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    Nur für Verein &bdquo;{c.name}&ldquo;
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Vereinsspezifische Sportarten sind nur für Trainer und Mitglieder des jeweiligen Vereins
              sichtbar.
            </p>
          </div>
        </form>

        <SheetFooter className="flex-row justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Abbrechen
          </Button>
          <Button type="submit" onClick={handleSubmit} disabled={submitting || !code.trim() || !name.trim() || !scope}>
            {submitting ? "Wird angelegt…" : "Anlegen"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

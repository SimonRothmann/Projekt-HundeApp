"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { toast } from "sonner";

/**
 * Sichtbarkeit einer neu angelegten Sportart.
 * - `{ kind: "global" }` legt eine für alle sichtbare Sportart an (nur Admin).
 * - `{ kind: "club", clubId, clubName }` legt sie im Verein an - nur Mitglieder/
 *   Trainer dieses Vereins sehen sie.
 * Bewusst kein Auswahl-Dropdown mehr im Editor: der Kontext (Admin- vs.
 * Trainer-Bereich) legt den Scope eindeutig fest, was dem Nutzer die Frage
 * "wohin gehört das?" abnimmt und Falsch-Zuordnungen verhindert.
 */
export type SportScope = { kind: "global" } | { kind: "club"; clubId: string; clubName: string };

export function SportEditorSheet({
  open,
  onOpenChange,
  scope,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  scope: SportScope;
  onCreated: (sport: Sport) => void;
}) {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      /* eslint-disable react-hooks/set-state-in-effect */
      setCode("");
      setName("");
      setDescription("");
      /* eslint-enable react-hooks/set-state-in-effect */
    }
  }, [open]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;
    setSubmitting(true);
    try {
      const created = await api.post<Sport>("/api/sports", {
        code: code.trim().toUpperCase(),
        name: name.trim(),
        description: description.trim() || null,
        clubId: scope.kind === "club" ? scope.clubId : null,
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
            {scope.kind === "global"
              ? "Wird für alle Nutzer sichtbar (globaler VDH-Katalog)."
              : `Nur für Mitglieder und Trainer des Vereins „${scope.clubName}“ sichtbar.`}
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
        </form>

        <SheetFooter className="flex-row justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Abbrechen
          </Button>
          <Button type="submit" onClick={handleSubmit} disabled={submitting || !code.trim() || !name.trim()}>
            {submitting ? "Wird angelegt…" : "Anlegen"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

"use client";

import { useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { DogOwner } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Plus, UserPlus, X } from "lucide-react";
import { toast } from "sonner";

/**
 * Mitbesitzer-Verwaltung eines Hundes (nur für Besitzer sichtbar, siehe
 * DogOwner). Aus der Hundeseite herausgelöst (TODO.md Roadmap 5b).
 */
export function CoOwnersSection({
  dogId,
  owners,
  currentUserId,
  onChanged,
}: {
  dogId: string;
  owners: DogOwner[];
  currentUserId: string | undefined;
  onChanged: () => Promise<void>;
}) {
  const [ownerEmail, setOwnerEmail] = useState("");
  const [addingOwner, setAddingOwner] = useState(false);

  async function handleAddOwner(e: FormEvent) {
    e.preventDefault();
    if (!ownerEmail.trim()) return;
    setAddingOwner(true);
    try {
      await api.post(`/api/dogs/${dogId}/owners`, { email: ownerEmail.trim() });
      toast.success("Mitbesitzer hinzugefügt.");
      setOwnerEmail("");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Fehler beim Hinzufügen.");
    } finally {
      setAddingOwner(false);
    }
  }

  async function handleRemoveOwner(userId: string) {
    try {
      await api.delete(`/api/dogs/${dogId}/owners/${userId}`);
      toast.success("Mitbesitzer entfernt.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Fehler beim Entfernen.");
    }
  }

  return (
    <Card>
      <CardHeader className="flex-row items-center gap-2 space-y-0">
        <UserPlus className="size-5 text-primary" />
        <CardTitle className="text-base">Mitbesitzer</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {owners.length > 0 && (
          <ul className="flex flex-col gap-2">
            {owners.map((o) => (
              <li key={o.userId} className="flex items-center justify-between text-sm">
                <span>
                  {o.firstName} {o.lastName}{" "}
                  <span className="text-muted-foreground text-xs">{o.email}</span>
                </span>
                {o.userId !== currentUserId && (
                  <Button size="icon" variant="ghost" className="size-7" onClick={() => handleRemoveOwner(o.userId)}>
                    <X className="size-3.5" />
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={handleAddOwner} className="flex gap-2">
          <Input
            type="email"
            placeholder="E-Mail des neuen Mitbesitzers"
            value={ownerEmail}
            onChange={(e) => setOwnerEmail(e.target.value)}
          />
          <Button type="submit" size="sm" disabled={addingOwner}>
            <Plus className="size-4" />
            Hinzufügen
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

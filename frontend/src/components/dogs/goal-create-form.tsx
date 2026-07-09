"use client";

import { useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, Regulation, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "sonner";

/**
 * Formular zum Anlegen eines neuen Ziels (mit oder ohne Prüfungsbezug).
 * Eigenständig mit eigenem State - die Ziel-Anlage ist unabhängig von der
 * Ziel-/Plan-Darstellung in GoalsSection. onCreated wird nach erfolgreichem
 * Anlegen gerufen (schließt in der Regel das Formular und lädt die Ziele neu).
 */
export function GoalCreateForm({
  dogId,
  sports,
  onCreated,
}: {
  dogId: string;
  sports: Sport[];
  onCreated: () => Promise<void>;
}) {
  const [sportId, setSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [regulationId, setRegulationId] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [notes, setNotes] = useState("");
  const [isCustom, setIsCustom] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSportChange(value: string) {
    setSportId(value);
    setRegulationId("");
    setRegulations([]);
    if (!value) return;
    try {
      const data = await api.get<Regulation[]>(`/api/sports/${value}/regulations`);
      setRegulations(data);
    } catch {
      // Prüfungsauswahl ist optional - bleibt leer, Plan wird dann aus
      // allen Übungen der Sportart generiert (Fallback, siehe Backend).
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await api.post<Goal>("/api/goals", {
        dogId,
        sportId,
        regulationId: isCustom ? null : (regulationId || null),
        targetDate,
        notes: notes || null,
        isCustom,
      });
      toast.success(
        isCustom
          ? "Individueller Plan angelegt - füge jetzt Wochenübungen hinzu."
          : "Ziel angelegt - Trainingsplan wurde generiert.",
      );
      await onCreated();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ziel konnte nicht angelegt werden.");
    } finally {
      setIsSubmitting(false);
    }
  }

  const selectedRegulation = regulations.find((r) => r.id === regulationId);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Neues Ziel</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="size-4 accent-primary"
              checked={isCustom}
              onChange={(e) => setIsCustom(e.target.checked)}
            />
            <span>Individueller Plan (ohne Prüfungsziel) &ndash; leere Wochen, Übungen manuell festlegen</span>
          </label>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label>Sportart</Label>
              <Select required value={sportId} onValueChange={(value) => handleSportChange(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder="Auswählen…" />
                </SelectTrigger>
                <SelectContent>
                  {sports.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="targetDate">Zieldatum</Label>
              <Input
                id="targetDate"
                type="date"
                required
                value={targetDate}
                onChange={(e) => setTargetDate(e.target.value)}
              />
            </div>
          </div>
          {!isCustom && regulations.length > 0 && (
            <div className="flex flex-col gap-2">
              <Label>Prüfung</Label>
              <Select value={regulationId} onValueChange={(value) => setRegulationId(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder="Allgemein (alle Übungen der Sportart)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="">Allgemein (alle Übungen der Sportart)</SelectItem>
                  {regulations.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Legt fest, aus welcher Pflichtübungsliste der Plan generiert wird - mehrere Prüfungsordnungen
                derselben Sportart (z.B. Fährte A/B/C) haben unterschiedliche Anforderungen.
              </p>
              {selectedRegulation?.description && (
                <div className="rounded-md bg-primary/5 px-3 py-2.5">
                  <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-primary">Rahmenbedingungen</p>
                  <p className="whitespace-pre-line text-sm leading-relaxed">{selectedRegulation.description}</p>
                </div>
              )}
            </div>
          )}
          <div className="flex flex-col gap-2">
            <Label htmlFor="goalNotes">Notizen</Label>
            <Input id="goalNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <Button type="submit" className="self-start" disabled={isSubmitting}>
            {isSubmitting
              ? isCustom
                ? "Wird angelegt…"
                : "Wird generiert…"
              : isCustom
                ? "Individuellen Plan anlegen"
                : "Ziel anlegen & Plan generieren"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

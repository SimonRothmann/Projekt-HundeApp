"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Exercise, ExerciseDifficulty, Sport } from "@/lib/types";
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

const NO_SPORT_SENTINEL = "__none__";

/**
 * Der Übungs-Editor kennt denselben Scope wie der Sport-Editor: der aufrufende
 * Kontext (Admin- vs. Trainer-Bereich) legt fest, ob eine neu angelegte Übung
 * global oder vereinsspezifisch ist. Die Sichtbarkeitsauswahl entfällt damit
 * im Editor selbst und der Nutzer sieht sofort, wohin die Übung geht.
 */
export type ExerciseScope = { kind: "global" } | { kind: "club"; clubId: string; clubName: string };

export function ExerciseEditorSheet({
  open,
  onOpenChange,
  scope,
  sports,
  presetSportId,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  scope: ExerciseScope;
  // Auswahlbare Sportarten. Aufrufer filtert - im Trainer-Bereich reine
  // Vereinssportarten + globale, im Admin-Bereich nur globale.
  sports: Sport[];
  // Wenn gesetzt, wird die Sportart-Auswahl vorbelegt (Klick auf "+ Übung"
  // in einer bestimmten Sport-Karte).
  presetSportId?: string | null;
  onCreated: (exercise: Exercise) => void;
}) {
  const [sportId, setSportId] = useState<string>(NO_SPORT_SENTINEL);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [difficulty, setDifficulty] = useState<ExerciseDifficulty>(0);
  const [category, setCategory] = useState("");
  const [scoringCriteria, setScoringCriteria] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      /* eslint-disable react-hooks/set-state-in-effect */
      setSportId(presetSportId ?? NO_SPORT_SENTINEL);
      setName("");
      setDescription("");
      setDifficulty(0);
      setCategory("");
      setScoringCriteria("");
      /* eslint-enable react-hooks/set-state-in-effect */
    }
  }, [open, presetSportId]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setSubmitting(true);
    try {
      const created = await api.post<Exercise>("/api/exercises", {
        sportId: sportId === NO_SPORT_SENTINEL ? null : sportId,
        name: name.trim(),
        description: description.trim() || null,
        difficulty,
        category: category.trim() || null,
        scoringCriteria: scoringCriteria.trim() || null,
        clubId: scope.kind === "club" ? scope.clubId : null,
      });
      toast.success("Übung angelegt.");
      onCreated(created);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Übung konnte nicht angelegt werden.");
    } finally {
      setSubmitting(false);
    }
  }

  const difficultyOptions: { value: ExerciseDifficulty; label: string }[] = [
    { value: 0, label: "Anfänger" },
    { value: 1, label: "Fortgeschritten" },
    { value: 2, label: "Profi" },
  ];

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>Neue Übung</SheetTitle>
          <SheetDescription>
            {scope.kind === "global"
              ? "Wird für alle Nutzer sichtbar (globaler VDH-Katalog)."
              : `Nur für Mitglieder und Trainer des Vereins „${scope.clubName}“ sichtbar.`}
            {" "}Sportart ist optional – sportartübergreifende Übungen laufen ohne Zuordnung.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit} className="flex flex-1 flex-col gap-4 overflow-y-auto px-4">
          <div className="flex flex-col gap-2">
            <Label>Sportart</Label>
            <Select value={sportId} onValueChange={(v) => v && setSportId(v)}>
              <SelectTrigger>
                <SelectValue placeholder="Auswählen…" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_SPORT_SENTINEL}>Ohne Sportart</SelectItem>
                {sports.map((s) => (
                  <SelectItem key={s.id} value={s.id}>
                    {s.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="ex-name">Name</Label>
            <Input
              id="ex-name"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Fußarbeit, Sitz aus Bewegung, ..."
              maxLength={150}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label>Schwierigkeit</Label>
              <Select value={String(difficulty)} onValueChange={(v) => v && setDifficulty(Number(v) as ExerciseDifficulty)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {difficultyOptions.map((o) => (
                    <SelectItem key={o.value} value={String(o.value)}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="ex-category">Kategorie (optional)</Label>
              <Input
                id="ex-category"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                placeholder="Unterordnung, Fährte, ..."
              />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="ex-description">Beschreibung (optional)</Label>
            <Input
              id="ex-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Was wird gemacht?"
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="ex-scoring">Bewertungskriterien (optional)</Label>
            <Input
              id="ex-scoring"
              value={scoringCriteria}
              onChange={(e) => setScoringCriteria(e.target.value)}
              placeholder="Tempo, Position, Sauberkeit, ..."
            />
          </div>
        </form>

        <SheetFooter className="flex-row justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Abbrechen
          </Button>
          <Button type="submit" onClick={handleSubmit} disabled={submitting || !name.trim()}>
            {submitting ? "Wird angelegt…" : "Übung anlegen"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

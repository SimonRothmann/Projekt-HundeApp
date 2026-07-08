"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Club, Exercise, ExerciseDifficulty, Sport } from "@/lib/types";
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
const GLOBAL_SENTINEL = "__global__";

/**
 * Sheet-basierter Editor zum Anlegen einer neuen Übung.
 * Sportart ist optional (sportartlose Übungen wie „Aufmerksamkeit halten"
 * werden ohne Sport-Zuordnung geführt). Sichtbarkeit analog zum Sport-
 * Editor: Admin darf global, Trainer nur für seine Vereine.
 */
export function ExerciseEditorSheet({
  open,
  onOpenChange,
  isAdmin,
  sports,
  clubs,
  presetSportId,
  presetClubId,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  isAdmin: boolean;
  sports: Sport[];
  clubs: Club[];
  // Wenn gesetzt, wird die Sportart-Auswahl im Editor vorbelegt (z. B. wenn
  // der Nutzer auf einer Sportart-Karte "Übung hinzufügen" klickt).
  presetSportId?: string | null;
  presetClubId?: string | null;
  onCreated: (exercise: Exercise) => void;
}) {
  const [sportId, setSportId] = useState<string>(NO_SPORT_SENTINEL);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [difficulty, setDifficulty] = useState<ExerciseDifficulty>(0);
  const [category, setCategory] = useState("");
  const [scoringCriteria, setScoringCriteria] = useState("");
  const [scope, setScope] = useState<string>(isAdmin ? GLOBAL_SENTINEL : clubs[0]?.id ?? "");
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
      // Wenn eine Preset-Sportart eine ClubId hat, ist die Sichtbarkeit
      // durch die Sportart implizit fixiert - ansonsten die letzte Wahl
      // oder Admin-Default.
      setScope(presetClubId ?? (isAdmin ? GLOBAL_SENTINEL : clubs[0]?.id ?? ""));
      /* eslint-enable react-hooks/set-state-in-effect */
    }
  }, [open, isAdmin, clubs, presetSportId, presetClubId]);

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
        clubId: scope === GLOBAL_SENTINEL ? null : scope,
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
            Sportart ist optional &ndash; sportartübergreifende Übungen (z. B. Grundlagen) laufen ohne
            Zuordnung.
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

          <div className="flex flex-col gap-2">
            <Label>Sichtbarkeit</Label>
            <Select value={scope} onValueChange={(v) => v && setScope(v)} disabled={!!presetClubId}>
              <SelectTrigger>
                <SelectValue placeholder="Auswählen…" />
              </SelectTrigger>
              <SelectContent>
                {isAdmin && <SelectItem value={GLOBAL_SENTINEL}>Global (für alle sichtbar)</SelectItem>}
                {clubs.map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    Nur für Verein &bdquo;{c.name}&ldquo;
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {presetClubId && (
              <p className="text-xs text-muted-foreground">
                Übernommen aus der übergeordneten Sportart.
              </p>
            )}
          </div>
        </form>

        <SheetFooter className="flex-row justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            Abbrechen
          </Button>
          <Button type="submit" onClick={handleSubmit} disabled={submitting || !name.trim() || !scope}>
            {submitting ? "Wird angelegt…" : "Übung anlegen"}
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}

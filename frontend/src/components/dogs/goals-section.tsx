"use client";

import { useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { CheckCircle2, Circle, Plus, Target } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import type { TrainingPlanItem } from "@/lib/types";

// Eine Woche kann jetzt mehrere Plan-Ziele haben (siehe TrainingPlanGenerator
// "ItemsPerWeek") statt wie vorher genau eines - für die Anzeige nach
// Wochennummer gruppiert, in der vom Server gelieferten Reihenfolge.
function groupByWeek(items: TrainingPlanItem[]): [number, TrainingPlanItem[]][] {
  const byWeek = new Map<number, TrainingPlanItem[]>();
  for (const item of items) {
    const group = byWeek.get(item.weekNumber);
    if (group) group.push(item);
    else byWeek.set(item.weekNumber, [item]);
  }
  return [...byWeek.entries()];
}

const statusLabel: Record<Goal["status"], string> = {
  0: "Aktiv",
  1: "Erreicht",
  2: "Abgebrochen",
};

const statusVariant: Record<Goal["status"], "default" | "secondary" | "outline"> = {
  0: "default",
  1: "secondary",
  2: "outline",
};

// "goals"/"onChanged" kommen jetzt von der Eltern-Seite statt aus einem
// eigenen Fetch hier (siehe dogs/[id]/page.tsx) - die Seite braucht dieselben
// Daten ohnehin für die "Plan-Ziel"-Auswahl im Trainingstagebuch-Formular,
// und nur ein gemeinsamer State stellt sicher, dass der Fortschritt hier
// sofort sichtbar wird, sobald dort ein verknüpftes Training gespeichert wird.
export function GoalsSection({
  dogId,
  sports,
  goals,
  onChanged,
}: {
  dogId: string;
  sports: Sport[];
  goals: Goal[] | null;
  onChanged: () => Promise<void>;
}) {
  const [showForm, setShowForm] = useState(false);
  const [sportId, setSportId] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await api.post<Goal>("/api/goals", { dogId, sportId, targetDate, notes: notes || null });
      toast.success("Ziel angelegt - Trainingsplan wurde generiert.");
      setShowForm(false);
      setSportId("");
      setTargetDate("");
      setNotes("");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ziel konnte nicht angelegt werden.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function updateStatus(goalId: string, status: 1 | 2) {
    try {
      await api.put<Goal>(`/api/goals/${goalId}/status`, { status });
      toast.success(status === 1 ? "Ziel als erreicht markiert." : "Ziel abgebrochen.");
      await onChanged();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Status konnte nicht aktualisiert werden.");
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Ziele & Trainingsplan</h2>
        <Button size="sm" variant="outline" onClick={() => setShowForm((v) => !v)}>
          <Plus className="size-4" />
          Ziel setzen
        </Button>
      </div>

      {showForm && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Neues Ziel</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="flex flex-col gap-2">
                  <Label>Sportart</Label>
                  <Select required value={sportId} onValueChange={(value) => setSportId(value ?? "")}>
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
              <div className="flex flex-col gap-2">
                <Label htmlFor="goalNotes">Notizen</Label>
                <Input id="goalNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
              </div>
              <Button type="submit" className="self-start" disabled={isSubmitting}>
                {isSubmitting ? "Wird generiert…" : "Ziel anlegen & Plan generieren"}
              </Button>
            </form>
          </CardContent>
        </Card>
      )}

      {goals === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : goals.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-10 text-center text-muted-foreground">
            <Target className="size-8" />
            <p>Noch kein Ziel gesetzt.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-3">
          {goals.map((goal) => (
            <Card key={goal.id}>
              <CardHeader className="flex-row items-center justify-between space-y-0">
                <div>
                  <CardTitle className="text-base">{goal.sportName}</CardTitle>
                  <p className="text-sm text-muted-foreground">
                    Ziel: {new Date(goal.targetDate).toLocaleDateString("de-DE")}
                  </p>
                </div>
                <Badge variant={statusVariant[goal.status]}>{statusLabel[goal.status]}</Badge>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                {goal.notes && <p className="text-sm text-muted-foreground">{goal.notes}</p>}

                {goal.trainingPlan && (
                  <div className="flex flex-col gap-3">
                    {groupByWeek(goal.trainingPlan.items).map(([weekNumber, items]) => (
                      <div key={weekNumber} className="flex flex-col gap-1.5 rounded-md border p-2.5">
                        <span className="text-xs font-medium text-muted-foreground">KW {weekNumber}</span>
                        {items[0].isRestWeek ? (
                          <span className="text-sm text-muted-foreground">Pause</span>
                        ) : (
                          items.map((item) => (
                            <div key={item.id} className="flex flex-col gap-1">
                              <div className="flex items-center gap-2 text-sm">
                                {item.isComplete ? (
                                  <CheckCircle2 className="size-4 shrink-0 text-accent" />
                                ) : (
                                  <Circle className="size-4 shrink-0 text-muted-foreground" />
                                )}
                                <span className={cn(item.isComplete && "text-muted-foreground line-through")}>
                                  {item.exerciseName}
                                </span>
                                <span className="text-xs text-muted-foreground">
                                  {item.completedCount}/{item.repetitionsTarget}x erledigt
                                </span>
                              </div>
                              {item.logs.length > 0 && (
                                <ul className="ml-6 flex flex-col gap-0.5 border-l pl-2.5">
                                  {item.logs.map((log) => (
                                    <li key={`${log.trainingSessionId}-${log.date}`} className="text-xs text-muted-foreground">
                                      {new Date(log.date).toLocaleDateString("de-DE")} ·{" "}
                                      {"★".repeat(log.rating)}
                                      {"☆".repeat(5 - log.rating)} {log.success ? "✓" : "✗"}
                                      {log.notes && <span> · {log.notes}</span>}
                                    </li>
                                  ))}
                                </ul>
                              )}
                            </div>
                          ))
                        )}
                      </div>
                    ))}
                  </div>
                )}

                {goal.status === 0 && (
                  <div className="flex gap-2">
                    <Button size="sm" variant="outline" onClick={() => updateStatus(goal.id, 1)}>
                      Als erreicht markieren
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => updateStatus(goal.id, 2)}>
                      Abbrechen
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

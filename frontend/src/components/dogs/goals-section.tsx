"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Plus, Target } from "lucide-react";
import { toast } from "sonner";

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

export function GoalsSection({ dogId, sports }: { dogId: string; sports: Sport[] }) {
  const [goals, setGoals] = useState<Goal[] | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [sportId, setSportId] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function loadGoals() {
    try {
      const data = await api.get<Goal[]>(`/api/goals?dogId=${dogId}`);
      setGoals(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Ziele konnten nicht geladen werden.");
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount/Routenwechsel (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadGoals();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dogId]);

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
      await loadGoals();
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
      await loadGoals();
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
                  <ul className="flex flex-col gap-1">
                    {goal.trainingPlan.items.map((item) => (
                      <li key={item.id} className="flex items-center justify-between text-sm">
                        <span className="text-muted-foreground">KW {item.weekNumber}</span>
                        {item.isRestWeek ? (
                          <span>Pause</span>
                        ) : (
                          <span>
                            {item.repetitionsTarget}x {item.exerciseName}
                          </span>
                        )}
                      </li>
                    ))}
                  </ul>
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

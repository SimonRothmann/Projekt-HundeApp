"use client";

import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { getCachedData, setCachedData } from "@/lib/read-cache";
import { useAuth } from "@/lib/auth-context";
import type { Exercise, ExerciseDifficulty, Regulation, RegulationDetail, RegulationExerciseInfo, Sport } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  Trophy,
  ChevronDown,
  ChevronRight,
  ScrollText,
  Sparkles,
  Building2,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import { difficultyLabel } from "@/lib/constants";

const textareaClass =
  "w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/60 md:text-sm dark:bg-input/30";

const DIFFICULTIES: ExerciseDifficulty[] = [0, 1, 2];

function apiError(err: unknown, fallback: string): string {
  return err instanceof ApiError ? err.message : fallback;
}

// GET mit Offline-Fallback: frische Daten laden und cachen; schlägt das
// Netz fehl (offline auf dem Hundeplatz), die zuletzt gesehene Version aus
// dem Read-Cache liefern. Wirft nur, wenn beides fehlschlägt.
async function getWithCache<T>(path: string): Promise<T> {
  try {
    const fresh = await api.get<T>(path);
    await setCachedData(path, fresh);
    return fresh;
  } catch (err) {
    const cached = await getCachedData<T>(path);
    if (cached) return cached;
    throw err;
  }
}

export default function SportsPage() {
  const { user } = useAuth();
  const isAdmin = user?.roles.includes("ADMIN") ?? false;

  const [sports, setSports] = useState<Sport[] | null>(null);
  const [uncategorized, setUncategorized] = useState<Exercise[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [showUncategorized, setShowUncategorized] = useState(false);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});
  const [regulationsBySport, setRegulationsBySport] = useState<Record<string, Regulation[]>>({});
  const [expandedRegulation, setExpandedRegulation] = useState<string | null>(null);
  const [regulationDetails, setRegulationDetails] = useState<Record<string, RegulationDetail>>({});

  const globalSports = useMemo(() => sports?.filter((s) => !s.clubId) ?? [], [sports]);
  const clubSports = useMemo(() => sports?.filter((s) => s.clubId) ?? [], [sports]);

  useEffect(() => {
    getWithCache<Sport[]>("/api/sports")
      .then(setSports)
      .catch((err) => toast.error(apiError(err, "Sportarten konnten nicht geladen werden.")));
    getWithCache<Exercise[]>("/api/exercises/uncategorized")
      .then(setUncategorized)
      .catch(() => {
        // Kein Toast: erwartet fehlend für Nutzer ohne Zugriff.
      });
  }, []);

  // ---- Reload-Helfer (nach Admin-Bearbeitungen) ----
  async function reloadSports() {
    try {
      setSports(await api.get<Sport[]>("/api/sports"));
    } catch (err) {
      toast.error(apiError(err, "Sportarten konnten nicht neu geladen werden."));
    }
  }
  async function reloadExercises(sportId: string) {
    try {
      const list = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
      setExercisesBySport((prev) => ({ ...prev, [sportId]: list }));
    } catch (err) {
      toast.error(apiError(err, "Übungen konnten nicht neu geladen werden."));
    }
  }
  async function reloadUncategorized() {
    try {
      setUncategorized(await api.get<Exercise[]>("/api/exercises/uncategorized"));
    } catch {
      /* still */
    }
  }
  async function reloadRegulation(regulationId: string) {
    try {
      const detail = await api.get<RegulationDetail>(`/api/sports/regulations/${regulationId}`);
      setRegulationDetails((prev) => ({ ...prev, [regulationId]: detail }));
    } catch (err) {
      toast.error(apiError(err, "Prüfungsordnung konnte nicht neu geladen werden."));
    }
  }

  async function toggleExpand(sportId: string) {
    if (expanded === sportId) {
      setExpanded(null);
      return;
    }
    setExpanded(sportId);
    try {
      if (!exercisesBySport[sportId]) {
        const exercises = await getWithCache<Exercise[]>(`/api/sports/${sportId}/exercises`);
        setExercisesBySport((prev) => ({ ...prev, [sportId]: exercises }));
      }
      if (!regulationsBySport[sportId]) {
        const regulations = await getWithCache<Regulation[]>(`/api/sports/${sportId}/regulations`);
        setRegulationsBySport((prev) => ({ ...prev, [sportId]: regulations }));
      }
    } catch (err) {
      toast.error(apiError(err, "Daten konnten nicht geladen werden."));
    }
  }

  async function toggleRegulation(regulationId: string) {
    if (expandedRegulation === regulationId) {
      setExpandedRegulation(null);
      return;
    }
    setExpandedRegulation(regulationId);
    if (!regulationDetails[regulationId]) {
      try {
        const detail = await getWithCache<RegulationDetail>(`/api/sports/regulations/${regulationId}`);
        setRegulationDetails((prev) => ({ ...prev, [regulationId]: detail }));
      } catch (err) {
        toast.error(apiError(err, "Prüfungsordnung konnte nicht geladen werden."));
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Sportarten</h1>
        <p className="text-sm text-muted-foreground">
          Informativer Katalog nach VDH-Prüfungsordnungen. Übungstexte und Bewertungskriterien sind
          eigene Beschreibungen, keine Kopie offizieller Prüfungsordnungen.
          {isAdmin
            ? " Als Admin kannst du globale Sportarten und ihre Prüfungsordnungen hier direkt bearbeiten."
            : " Globale Sportarten pflegt der Admin; Vereine können eigene Übungen & Sportarten anlegen (Trainer-Bereich)."}
        </p>
      </div>

      {sports === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : (
        <div className="flex flex-col gap-3">
          {/* Sportartlose Übungen als eigene Karte - direkt oben, weil sie
              kontextlos existieren und sonst leicht übersehen werden. */}
          {uncategorized.length > 0 && (
            <Card className="border-dashed">
              <CardHeader
                className="flex-row cursor-pointer items-center justify-between gap-2 space-y-0"
                onClick={() => setShowUncategorized((prev) => !prev)}
              >
                <div className="flex min-w-0 items-center gap-3">
                  <Sparkles className="size-6 shrink-0 text-muted-foreground" />
                  <div className="min-w-0">
                    <CardTitle className="text-base">Ohne Sportart</CardTitle>
                    <p className="text-xs text-muted-foreground">Übergreifende Übungen ohne feste Sport-Zuordnung</p>
                  </div>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <Badge variant="secondary">{uncategorized.length}</Badge>
                  {showUncategorized ? <ChevronDown className="size-5" /> : <ChevronRight className="size-5" />}
                </div>
              </CardHeader>
              {showUncategorized && (
                <CardContent className="flex flex-col gap-2">
                  {uncategorized.map((exercise) => (
                    <ExerciseRow
                      key={exercise.id}
                      exercise={exercise}
                      canEdit={isAdmin && !exercise.clubId}
                      onSaved={reloadUncategorized}
                    />
                  ))}
                </CardContent>
              )}
            </Card>
          )}

          {/* Globale Sportarten */}
          {globalSports.map((sport) => (
            <SportCard
              key={sport.id}
              sport={sport}
              canEdit={isAdmin}
              isOpen={expanded === sport.id}
              exercises={exercisesBySport[sport.id]}
              regulations={regulationsBySport[sport.id]}
              regulationDetails={regulationDetails}
              expandedRegulation={expandedRegulation}
              onToggle={() => toggleExpand(sport.id)}
              onToggleRegulation={toggleRegulation}
              onSportSaved={reloadSports}
              onExercisesChanged={() => reloadExercises(sport.id)}
              onRegulationChanged={reloadRegulation}
            />
          ))}

          {/* Vereinsspezifische Sportarten, klar abgesetzt */}
          {clubSports.length > 0 && (
            <div className="flex items-center gap-2 pt-2 text-sm text-muted-foreground">
              <Building2 className="size-4" />
              Vereinsspezifische Sportarten
            </div>
          )}
          {clubSports.map((sport) => (
            <SportCard
              key={sport.id}
              sport={sport}
              canEdit={false}
              isOpen={expanded === sport.id}
              exercises={exercisesBySport[sport.id]}
              regulations={regulationsBySport[sport.id]}
              regulationDetails={regulationDetails}
              expandedRegulation={expandedRegulation}
              onToggle={() => toggleExpand(sport.id)}
              onToggleRegulation={toggleRegulation}
              onSportSaved={reloadSports}
              onExercisesChanged={() => reloadExercises(sport.id)}
              onRegulationChanged={reloadRegulation}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function SportCard({
  sport,
  canEdit,
  isOpen,
  exercises,
  regulations,
  regulationDetails,
  expandedRegulation,
  onToggle,
  onToggleRegulation,
  onSportSaved,
  onExercisesChanged,
  onRegulationChanged,
}: {
  sport: Sport;
  canEdit: boolean;
  isOpen: boolean;
  exercises: Exercise[] | undefined;
  regulations: Regulation[] | undefined;
  regulationDetails: Record<string, RegulationDetail>;
  expandedRegulation: string | null;
  onToggle: () => void;
  onToggleRegulation: (id: string) => void;
  onSportSaved: () => void;
  onExercisesChanged: () => void;
  onRegulationChanged: (regulationId: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(sport.name);
  const [description, setDescription] = useState(sport.description ?? "");
  const [saving, setSaving] = useState(false);

  async function saveSport() {
    if (!name.trim()) {
      toast.error("Name ist erforderlich.");
      return;
    }
    setSaving(true);
    try {
      await api.put(`/api/sports/${sport.id}`, { name: name.trim(), description: description.trim() || null });
      toast.success("Sportart aktualisiert.");
      setEditing(false);
      onSportSaved();
    } catch (err) {
      toast.error(apiError(err, "Sportart konnte nicht gespeichert werden."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card>
      <CardHeader
        className="flex-row cursor-pointer items-center justify-between gap-2 space-y-0"
        onClick={onToggle}
      >
        <div className="flex min-w-0 items-center gap-3">
          <Trophy className={sport.clubId ? "size-6 shrink-0 text-amber-500" : "size-6 shrink-0 text-primary"} />
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <CardTitle className="text-base [overflow-wrap:anywhere]">{sport.name}</CardTitle>
              {sport.clubId && (
                <Badge variant="outline" className="border-amber-500/40 text-amber-700">
                  Verein
                </Badge>
              )}
            </div>
            <Badge variant="secondary">{sport.code}</Badge>
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-1">
          {canEdit && (
            <Button
              type="button"
              size="icon-sm"
              variant="ghost"
              aria-label="Sportart bearbeiten"
              onClick={(e) => {
                e.stopPropagation();
                setName(sport.name);
                setDescription(sport.description ?? "");
                setEditing(true);
                if (!isOpen) onToggle();
              }}
            >
              <Pencil className="size-4" />
            </Button>
          )}
          {isOpen ? <ChevronDown className="size-5" /> : <ChevronRight className="size-5" />}
        </div>
      </CardHeader>
      {isOpen && (
        <CardContent className="flex flex-col gap-5">
          {canEdit && editing && (
            <div className="flex flex-col gap-3 rounded-md border p-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor={`sport-name-${sport.id}`}>Name</Label>
                <Input id={`sport-name-${sport.id}`} value={name} onChange={(e) => setName(e.target.value)} maxLength={120} />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor={`sport-desc-${sport.id}`}>Beschreibung (optional)</Label>
                <textarea id={`sport-desc-${sport.id}`} className={textareaClass} rows={2} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={1000} />
              </div>
              <div className="flex gap-2">
                <Button type="button" size="sm" disabled={saving} onClick={saveSport}>
                  {saving ? "Speichert…" : "Speichern"}
                </Button>
                <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
                  Abbrechen
                </Button>
              </div>
            </div>
          )}

          <div>
            <h3 className="mb-2 text-sm font-semibold text-muted-foreground">Übungen</h3>
            {!exercises ? (
              <p className="text-sm text-muted-foreground">Lädt Übungen…</p>
            ) : exercises.length === 0 ? (
              <p className="text-sm text-muted-foreground">Noch keine Übungen hinterlegt.</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {exercises.map((exercise) => (
                  <ExerciseRow
                    key={exercise.id}
                    exercise={exercise}
                    canEdit={canEdit && !exercise.clubId}
                    onSaved={() => {
                      onExercisesChanged();
                      if (expandedRegulation) onRegulationChanged(expandedRegulation);
                    }}
                  />
                ))}
              </ul>
            )}
          </div>

          <div>
            <h3 className="mb-2 text-sm font-semibold text-muted-foreground">Prüfungsordnungen</h3>
            {!regulations ? (
              <p className="text-sm text-muted-foreground">Lädt…</p>
            ) : regulations.length === 0 ? (
              <p className="text-sm text-muted-foreground">Noch keine Prüfungsordnung hinterlegt.</p>
            ) : (
              <div className="flex flex-col gap-2">
                {regulations.map((regulation) => (
                  <RegulationBlock
                    key={regulation.id}
                    regulation={regulation}
                    detail={regulationDetails[regulation.id]}
                    isOpen={expandedRegulation === regulation.id}
                    canEdit={canEdit}
                    sportExercises={exercises ?? []}
                    onToggle={() => onToggleRegulation(regulation.id)}
                    onChanged={() => onRegulationChanged(regulation.id)}
                  />
                ))}
              </div>
            )}
          </div>
        </CardContent>
      )}
    </Card>
  );
}

function RegulationBlock({
  regulation,
  detail,
  isOpen,
  canEdit,
  sportExercises,
  onToggle,
  onChanged,
}: {
  regulation: Regulation;
  detail: RegulationDetail | undefined;
  isOpen: boolean;
  canEdit: boolean;
  sportExercises: Exercise[];
  onToggle: () => void;
  onChanged: () => void;
}) {
  const [editingMeta, setEditingMeta] = useState(false);
  const [name, setName] = useState(regulation.name);
  const [description, setDescription] = useState(regulation.description ?? "");
  const [sourceUrl, setSourceUrl] = useState(regulation.sourceUrl ?? "");
  const [savingMeta, setSavingMeta] = useState(false);
  const [adding, setAdding] = useState(false);

  async function saveMeta() {
    if (!name.trim()) {
      toast.error("Name ist erforderlich.");
      return;
    }
    setSavingMeta(true);
    try {
      await api.put(`/api/sports/regulations/${regulation.id}`, {
        name: name.trim(),
        description: description.trim() || null,
        sourceUrl: sourceUrl.trim() || null,
      });
      toast.success("Prüfungsordnung aktualisiert.");
      setEditingMeta(false);
      onChanged();
    } catch (err) {
      toast.error(apiError(err, "Prüfungsordnung konnte nicht gespeichert werden."));
    } finally {
      setSavingMeta(false);
    }
  }

  // Übungen der Sportart, die noch nicht Teil dieser PO sind - für "Hinzufügen".
  const linkedIds = new Set((detail?.exercises ?? []).map((re) => re.exerciseId));
  const addableExercises = sportExercises.filter((e) => !linkedIds.has(e.id));

  return (
    <div className="rounded-md border">
      <button
        type="button"
        className="flex w-full items-center justify-between gap-2 px-3 py-2 text-sm"
        onClick={onToggle}
      >
        <span className="flex min-w-0 items-center gap-2 font-medium">
          <ScrollText className="size-4 shrink-0" />
          <span className="[overflow-wrap:anywhere]">{regulation.name}</span>
        </span>
        {isOpen ? <ChevronDown className="size-4 shrink-0" /> : <ChevronRight className="size-4 shrink-0" />}
      </button>
      {isOpen && (
        <div className="border-t px-3 py-2">
          {!detail ? (
            <p className="text-sm text-muted-foreground">Lädt…</p>
          ) : (
            <>
              <div className="mb-2 flex items-center justify-between gap-2">
                <p className="text-xs text-muted-foreground">
                  Version {detail.currentVersion.versionLabel} · gültig ab{" "}
                  {new Date(detail.currentVersion.validFrom).toLocaleDateString("de-DE")}
                </p>
                {canEdit && !editingMeta && (
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setName(regulation.name);
                      setDescription(regulation.description ?? "");
                      setSourceUrl(regulation.sourceUrl ?? "");
                      setEditingMeta(true);
                    }}
                  >
                    <Pencil className="size-3.5" />
                    Bearbeiten
                  </Button>
                )}
              </div>

              {canEdit && editingMeta ? (
                <div className="mb-3 flex flex-col gap-3 rounded-md border p-3">
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor={`reg-name-${regulation.id}`}>Name</Label>
                    <Input id={`reg-name-${regulation.id}`} value={name} onChange={(e) => setName(e.target.value)} maxLength={120} />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor={`reg-desc-${regulation.id}`}>Rahmenbedingungen (mehrzeilig)</Label>
                    <textarea id={`reg-desc-${regulation.id}`} className={textareaClass} rows={4} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={4000} />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor={`reg-url-${regulation.id}`}>Quelle (URL, optional)</Label>
                    <Input id={`reg-url-${regulation.id}`} value={sourceUrl} onChange={(e) => setSourceUrl(e.target.value)} maxLength={500} />
                  </div>
                  <div className="flex gap-2">
                    <Button type="button" size="sm" disabled={savingMeta} onClick={saveMeta}>
                      {savingMeta ? "Speichert…" : "Speichern"}
                    </Button>
                    <Button type="button" size="sm" variant="ghost" onClick={() => setEditingMeta(false)}>
                      Abbrechen
                    </Button>
                  </div>
                </div>
              ) : (
                detail.regulation.description && (
                  <div className="mb-3 rounded-md bg-primary/5 px-3 py-2.5">
                    <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-primary">Rahmenbedingungen</p>
                    <p className="whitespace-pre-line text-sm leading-relaxed">{detail.regulation.description}</p>
                  </div>
                )
              )}

              <ul className="flex flex-col gap-2">
                {detail.exercises.map((re) => (
                  <RegExerciseRow
                    key={re.exerciseId}
                    regulationId={regulation.id}
                    re={re}
                    canEdit={canEdit}
                    onChanged={onChanged}
                  />
                ))}
              </ul>

              {canEdit && (
                <div className="mt-3">
                  {adding ? (
                    <AddRegExerciseForm
                      regulationId={regulation.id}
                      addableExercises={addableExercises}
                      onDone={() => {
                        setAdding(false);
                        onChanged();
                      }}
                      onCancel={() => setAdding(false)}
                    />
                  ) : (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={addableExercises.length === 0}
                      title={addableExercises.length === 0 ? "Alle Übungen der Sportart sind bereits enthalten" : undefined}
                      onClick={() => setAdding(true)}
                    >
                      <Plus className="size-3.5" />
                      Übung hinzufügen
                    </Button>
                  )}
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

function RegExerciseRow({
  regulationId,
  re,
  canEdit,
  onChanged,
}: {
  regulationId: string;
  re: RegulationExerciseInfo;
  canEdit: boolean;
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [mandatory, setMandatory] = useState(re.isMandatory);
  const [points, setPoints] = useState(String(re.maxPoints));
  const [notes, setNotes] = useState(re.scoringNotes ?? "");
  const [saving, setSaving] = useState(false);

  async function save() {
    setSaving(true);
    try {
      await api.put(`/api/sports/regulations/${regulationId}/exercises/${re.exerciseId}`, {
        isMandatory: mandatory,
        maxPoints: Number(points) || 0,
        scoringNotes: notes.trim() || null,
      });
      toast.success("Gespeichert.");
      setEditing(false);
      onChanged();
    } catch (err) {
      toast.error(apiError(err, "Konnte nicht gespeichert werden."));
    } finally {
      setSaving(false);
    }
  }

  async function remove() {
    if (!window.confirm(`„${re.exerciseName}" aus dieser Prüfungsordnung entfernen?`)) return;
    try {
      await api.delete(`/api/sports/regulations/${regulationId}/exercises/${re.exerciseId}`);
      toast.success("Übung entfernt.");
      onChanged();
    } catch (err) {
      toast.error(apiError(err, "Konnte nicht entfernt werden."));
    }
  }

  if (canEdit && editing) {
    return (
      <li className="flex flex-col gap-3 rounded-md border bg-muted/30 p-3 text-sm">
        <span className="font-medium [overflow-wrap:anywhere]">{re.exerciseName}</span>
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex items-center gap-1.5">
            <input type="checkbox" className="size-4 accent-primary" checked={mandatory} onChange={(e) => setMandatory(e.target.checked)} />
            Pflicht
          </label>
          <div className="flex flex-col gap-1">
            <Label className="text-xs">Punkte</Label>
            <Input type="number" min={0} max={1000} className="w-24" value={points} onChange={(e) => setPoints(e.target.value)} />
          </div>
        </div>
        <div className="flex flex-col gap-1">
          <Label className="text-xs">Bewertungshinweise (optional)</Label>
          <textarea className={textareaClass} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={1000} />
        </div>
        <div className="flex gap-2">
          <Button type="button" size="sm" disabled={saving} onClick={save}>
            {saving ? "Speichert…" : "Speichern"}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
            Abbrechen
          </Button>
        </div>
      </li>
    );
  }

  return (
    <li className="text-sm">
      <div className="flex items-start justify-between gap-2">
        <span className="min-w-0 font-medium [overflow-wrap:anywhere]">
          {re.exerciseName}
          {!re.isMandatory && <span className="ml-1.5 text-xs font-normal text-muted-foreground">(optional)</span>}
        </span>
        <div className="flex shrink-0 items-center gap-1">
          {re.maxPoints > 0 && <Badge variant="outline">{re.maxPoints} Punkte</Badge>}
          {canEdit && (
            <>
              <Button type="button" size="icon-sm" variant="ghost" aria-label="Bearbeiten" onClick={() => { setMandatory(re.isMandatory); setPoints(String(re.maxPoints)); setNotes(re.scoringNotes ?? ""); setEditing(true); }}>
                <Pencil className="size-3.5" />
              </Button>
              <Button type="button" size="icon-sm" variant="ghost" aria-label="Entfernen" onClick={remove}>
                <Trash2 className="size-3.5 text-muted-foreground" />
              </Button>
            </>
          )}
        </div>
      </div>
      {re.scoringNotes && <p className="text-muted-foreground [overflow-wrap:anywhere]">{re.scoringNotes}</p>}
    </li>
  );
}

function AddRegExerciseForm({
  regulationId,
  addableExercises,
  onDone,
  onCancel,
}: {
  regulationId: string;
  addableExercises: Exercise[];
  onDone: () => void;
  onCancel: () => void;
}) {
  const [exerciseId, setExerciseId] = useState("");
  const [mandatory, setMandatory] = useState(true);
  const [points, setPoints] = useState("0");
  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);

  async function submit() {
    if (!exerciseId) {
      toast.error("Übung wählen.");
      return;
    }
    setSaving(true);
    try {
      await api.post(`/api/sports/regulations/${regulationId}/exercises`, {
        exerciseId,
        isMandatory: mandatory,
        maxPoints: Number(points) || 0,
        scoringNotes: notes.trim() || null,
      });
      toast.success("Übung hinzugefügt.");
      onDone();
    } catch (err) {
      toast.error(apiError(err, "Übung konnte nicht hinzugefügt werden."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-md border p-3">
      <div className="flex flex-col gap-1.5">
        <Label className="text-xs">Übung</Label>
        <Select value={exerciseId} onValueChange={(v) => setExerciseId(v ?? "")}>
          <SelectTrigger>
            <SelectValue placeholder="Übung wählen…" />
          </SelectTrigger>
          <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
            {addableExercises.map((e) => (
              <SelectItem key={e.id} value={e.id}>
                {e.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex items-center gap-1.5 text-sm">
          <input type="checkbox" className="size-4 accent-primary" checked={mandatory} onChange={(e) => setMandatory(e.target.checked)} />
          Pflicht
        </label>
        <div className="flex flex-col gap-1">
          <Label className="text-xs">Punkte</Label>
          <Input type="number" min={0} max={1000} className="w-24" value={points} onChange={(e) => setPoints(e.target.value)} />
        </div>
      </div>
      <div className="flex flex-col gap-1">
        <Label className="text-xs">Bewertungshinweise (optional)</Label>
        <textarea className={textareaClass} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={1000} />
      </div>
      <div className="flex gap-2">
        <Button type="button" size="sm" disabled={saving} onClick={submit}>
          {saving ? "Fügt hinzu…" : "Hinzufügen"}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          Abbrechen
        </Button>
      </div>
    </div>
  );
}

function ExerciseRow({
  exercise,
  canEdit,
  onSaved,
}: {
  exercise: Exercise;
  canEdit: boolean;
  onSaved: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(exercise.name);
  const [description, setDescription] = useState(exercise.description ?? "");
  const [difficulty, setDifficulty] = useState<ExerciseDifficulty>(exercise.difficulty);
  const [category, setCategory] = useState(exercise.category ?? "");
  const [scoring, setScoring] = useState(exercise.scoringCriteria ?? "");
  const [saving, setSaving] = useState(false);

  async function save() {
    if (!name.trim()) {
      toast.error("Name ist erforderlich.");
      return;
    }
    setSaving(true);
    try {
      await api.put(`/api/exercises/${exercise.id}`, {
        name: name.trim(),
        description: description.trim() || null,
        difficulty,
        category: category.trim() || null,
        scoringCriteria: scoring.trim() || null,
      });
      toast.success("Übung aktualisiert.");
      setEditing(false);
      onSaved();
    } catch (err) {
      toast.error(apiError(err, "Übung konnte nicht gespeichert werden."));
    } finally {
      setSaving(false);
    }
  }

  if (canEdit && editing) {
    return (
      <li className="flex flex-col gap-3 rounded-md border bg-muted/30 p-3">
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Name</Label>
          <Input value={name} onChange={(e) => setName(e.target.value)} maxLength={200} />
        </div>
        <div className="flex flex-wrap gap-3">
          <div className="flex flex-col gap-1.5">
            <Label className="text-xs">Schwierigkeit</Label>
            <Select value={String(difficulty)} onValueChange={(v) => setDifficulty(Number(v ?? "0") as ExerciseDifficulty)}>
              <SelectTrigger className="w-40">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DIFFICULTIES.map((d) => (
                  <SelectItem key={d} value={String(d)}>
                    {difficultyLabel[d]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="flex flex-1 flex-col gap-1.5">
            <Label className="text-xs">Kategorie (optional)</Label>
            <Input value={category} onChange={(e) => setCategory(e.target.value)} maxLength={80} />
          </div>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Beschreibung (optional)</Label>
          <textarea className={textareaClass} rows={2} value={description} onChange={(e) => setDescription(e.target.value)} maxLength={2000} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label className="text-xs">Bewertungskriterien (optional)</Label>
          <textarea className={textareaClass} rows={2} value={scoring} onChange={(e) => setScoring(e.target.value)} maxLength={2000} />
        </div>
        <div className="flex gap-2">
          <Button type="button" size="sm" disabled={saving} onClick={save}>
            {saving ? "Speichert…" : "Speichern"}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
            Abbrechen
          </Button>
        </div>
      </li>
    );
  }

  return (
    <li className="rounded-md border px-3 py-2">
      <div className="flex items-center justify-between gap-2 text-sm">
        <div className="flex min-w-0 items-center gap-2">
          <span className="font-medium [overflow-wrap:anywhere]">{exercise.name}</span>
          {exercise.clubId && (
            <Badge variant="outline" className="border-amber-500/40 text-amber-700">
              Verein
            </Badge>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-1">
          <Badge variant="outline">{difficultyLabel[exercise.difficulty]}</Badge>
          {canEdit && (
            <Button
              type="button"
              size="icon-sm"
              variant="ghost"
              aria-label="Übung bearbeiten"
              onClick={() => {
                setName(exercise.name);
                setDescription(exercise.description ?? "");
                setDifficulty(exercise.difficulty);
                setCategory(exercise.category ?? "");
                setScoring(exercise.scoringCriteria ?? "");
                setEditing(true);
              }}
            >
              <Pencil className="size-3.5" />
            </Button>
          )}
        </div>
      </div>
      {exercise.category && <p className="mt-0.5 text-xs text-muted-foreground">{exercise.category}</p>}
      {exercise.scoringCriteria && <p className="mt-1 text-sm text-muted-foreground [overflow-wrap:anywhere]">{exercise.scoringCriteria}</p>}
    </li>
  );
}

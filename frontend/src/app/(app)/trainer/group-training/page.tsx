"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { Club, GroupTrainingCategory, GroupTrainingExercise, GroupTrainingLibrary } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ArrowLeft, ChevronDown, ChevronRight, ChevronUp, Clock, Copy, Download, Pencil, Plus, Sparkles, Trash2, X } from "lucide-react";
import { toast } from "sonner";

const CATS: GroupTrainingCategory[] = [0, 1, 2];
const categoryLabel: Record<GroupTrainingCategory, string> = { 0: "Welpen", 1: "Junghunde", 2: "Basis" };
const categoryVariant: Record<GroupTrainingCategory, "default" | "secondary" | "outline"> = { 0: "default", 1: "secondary", 2: "outline" };

const EXAM_FLAGS: { bit: number; label: string }[] = [
  { bit: 1, label: "BH" },
  { bit: 2, label: "IBGH1" },
  { bit: 4, label: "IBGH2" },
  { bit: 8, label: "IBGH3" },
];
const examLabels = (mask: number) => EXAM_FLAGS.filter((f) => (mask & f.bit) !== 0).map((f) => f.label);

const textareaClass =
  "w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30";

type ExerciseForm = {
  id: string | null;
  category: GroupTrainingCategory;
  title: string;
  focus: string;
  durationMinutes: string;
  description: string;
  examTargets: number;
};

const emptyExerciseForm = (): ExerciseForm => ({ id: null, category: 0, title: "", focus: "", durationMinutes: "", description: "", examTargets: 0 });

type UnitForm = {
  id: string | null;
  category: GroupTrainingCategory;
  title: string;
  description: string;
  exerciseIds: string[];
};

const emptyUnitForm = (): UnitForm => ({ id: null, category: 0, title: "", description: "", exerciseIds: [] });

export default function GroupTrainingPage() {
  const [clubs, setClubs] = useState<Club[] | null>(null);
  const [clubId, setClubId] = useState("");
  const [library, setLibrary] = useState<GroupTrainingLibrary | null>(null);

  const [exForm, setExForm] = useState<ExerciseForm | null>(null);
  const [unitForm, setUnitForm] = useState<UnitForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [importing, setImporting] = useState(false);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [addExerciseId, setAddExerciseId] = useState("");

  async function importStarter() {
    if (!window.confirm("Best-Practice-Bausteine und Einheiten in diesen Verein übernehmen? Du kannst danach alles frei anpassen oder löschen.")) return;
    setImporting(true);
    try {
      const data = await api.post<GroupTrainingLibrary>(`/api/group-training/clubs/${clubId}/import-starter`);
      setLibrary(data);
      toast.success("Starter-Katalog übernommen.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Katalog konnte nicht übernommen werden.");
    } finally {
      setImporting(false);
    }
  }

  const loadLibrary = useCallback(async (id: string) => {
    try {
      const data = await api.get<GroupTrainingLibrary>(`/api/group-training/clubs/${id}/library`);
      setLibrary(data);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Bibliothek konnte nicht geladen werden.");
    }
  }, []);

  useEffect(() => {
    // Eigene Vereine (nur die, in denen man Trainer:in ist) laden.
    api
      .get<Club[]>("/api/groups/my-clubs")
      .then((data) => {
        setClubs(data);
        if (data.length > 0) setClubId((prev) => prev || data[0].id);
      })
      .catch(() => setClubs([]));
  }, []);

  useEffect(() => {
    // Bibliothek des gewählten Vereins laden (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (clubId) loadLibrary(clubId);
  }, [clubId, loadLibrary]);

  function toggleExpand(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  // ---- Bausteine ----
  async function saveExercise() {
    if (!exForm) return;
    if (!exForm.title.trim()) {
      toast.error("Titel eingeben.");
      return;
    }
    const body = {
      category: exForm.category,
      title: exForm.title.trim(),
      focus: exForm.focus.trim() || null,
      durationMinutes: exForm.durationMinutes ? Number(exForm.durationMinutes) : null,
      description: exForm.description.trim() || null,
      examTargets: exForm.examTargets,
    };
    setSaving(true);
    try {
      if (exForm.id) await api.put(`/api/group-training/exercises/${exForm.id}`, body);
      else await api.post(`/api/group-training/clubs/${clubId}/exercises`, body);
      toast.success(exForm.id ? "Baustein aktualisiert." : "Baustein angelegt.");
      setExForm(null);
      await loadLibrary(clubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Baustein konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  async function deleteExercise(ex: GroupTrainingExercise) {
    if (!window.confirm(`Baustein „${ex.title}" löschen? Er wird auch aus allen Einheiten entfernt.`)) return;
    try {
      await api.delete(`/api/group-training/exercises/${ex.id}`);
      toast.success("Baustein gelöscht.");
      await loadLibrary(clubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Baustein konnte nicht gelöscht werden.");
    }
  }

  function editExercise(ex: GroupTrainingExercise) {
    setExForm({
      id: ex.id,
      category: ex.category,
      title: ex.title,
      focus: ex.focus ?? "",
      durationMinutes: ex.durationMinutes != null ? String(ex.durationMinutes) : "",
      description: ex.description ?? "",
      examTargets: ex.examTargets,
    });
    if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
  }

  // ---- Einheiten ----
  async function saveUnit() {
    if (!unitForm) return;
    if (!unitForm.title.trim()) {
      toast.error("Titel eingeben.");
      return;
    }
    if (unitForm.exerciseIds.length === 0) {
      toast.error("Mindestens einen Baustein wählen.");
      return;
    }
    const body = {
      category: unitForm.category,
      title: unitForm.title.trim(),
      description: unitForm.description.trim() || null,
      exerciseIds: unitForm.exerciseIds,
    };
    setSaving(true);
    try {
      if (unitForm.id) await api.put(`/api/group-training/units/${unitForm.id}`, body);
      else await api.post(`/api/group-training/clubs/${clubId}/units`, body);
      toast.success(unitForm.id ? "Einheit aktualisiert." : "Einheit angelegt.");
      setUnitForm(null);
      await loadLibrary(clubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einheit konnte nicht gespeichert werden.");
    } finally {
      setSaving(false);
    }
  }

  async function deleteUnit(id: string, title: string) {
    if (!window.confirm(`Einheit „${title}" löschen?`)) return;
    try {
      await api.delete(`/api/group-training/units/${id}`);
      toast.success("Einheit gelöscht.");
      await loadLibrary(clubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einheit konnte nicht gelöscht werden.");
    }
  }

  async function duplicateUnit(id: string) {
    try {
      await api.post(`/api/group-training/units/${id}/duplicate`);
      toast.success("Einheit als Kopie angelegt.");
      await loadLibrary(clubId);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einheit konnte nicht kopiert werden.");
    }
  }

  function moveItem(index: number, dir: -1 | 1) {
    setUnitForm((f) => {
      if (!f) return f;
      const ids = [...f.exerciseIds];
      const target = index + dir;
      if (target < 0 || target >= ids.length) return f;
      [ids[index], ids[target]] = [ids[target], ids[index]];
      return { ...f, exerciseIds: ids };
    });
  }

  const exerciseById = (id: string) => library?.exercises.find((e) => e.id === id);

  function exerciseCard(ex: GroupTrainingExercise) {
    return (
      <div key={ex.id} className="flex items-start justify-between gap-2 rounded-md border p-2.5">
        <div className="min-w-0">
          <p className="text-sm font-medium [overflow-wrap:anywhere]">{ex.title}</p>
          <div className="mt-0.5 flex flex-wrap items-center gap-1.5">
            {ex.focus && <Badge variant="secondary">{ex.focus}</Badge>}
            {ex.durationMinutes != null && <span className="text-xs text-muted-foreground">{ex.durationMinutes} Min</span>}
            {examLabels(ex.examTargets).map((l) => (
              <Badge key={l} variant="outline">{l}</Badge>
            ))}
          </div>
          {ex.description && <p className="mt-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">{ex.description}</p>}
        </div>
        <div className="flex shrink-0 gap-0.5">
          <Button type="button" size="icon" variant="ghost" className="size-8" onClick={() => editExercise(ex)} aria-label="Bearbeiten">
            <Pencil className="size-3.5" />
          </Button>
          <Button type="button" size="icon" variant="ghost" className="size-8" onClick={() => deleteExercise(ex)} aria-label="Löschen">
            <Trash2 className="size-3.5 text-muted-foreground" />
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link href="/trainer" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
          <ArrowLeft className="size-4" />
          Zur Trainer-Übersicht
        </Link>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">Gruppentraining</h1>
        <p className="text-muted-foreground">
          Die gemeinsame Trainingsbibliothek deines Vereins: wiederverwendbare Übungs-Bausteine (Welpen, Junghunde,
          Basis) und daraus zusammengestellte Einheiten – von allen Vereinstrainer:innen pflegbar.
        </p>
      </div>

      {clubs === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : clubs.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">
            Die Vereins-Trainingsbibliothek ist für Vereinstrainer:innen. Du bist aktuell für keinen Verein als
            Trainer:in eingetragen.
          </CardContent>
        </Card>
      ) : (
        <>
          {clubs.length > 1 && (
            <div className="flex flex-col gap-2">
              <Label>Verein</Label>
              <Select value={clubId} onValueChange={(v) => setClubId(v ?? "")}>
                <SelectTrigger className="w-full sm:w-72">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {clubs.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          {library === null ? (
            <p className="text-muted-foreground">Lädt…</p>
          ) : (
            <>
              {library.exercises.length === 0 && library.units.length === 0 && (
                <Card className="border-primary/40 bg-primary/5">
                  <CardContent className="flex flex-col items-start gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-start gap-3">
                      <Sparkles className="mt-0.5 size-6 shrink-0 text-primary" />
                      <div>
                        <p className="font-medium">Mit einem fertigen Katalog starten?</p>
                        <p className="text-sm text-muted-foreground">
                          Übernimm einen Best-Practice-Satz an Bausteinen und Einheiten (Welpen, Junghunde, Basis) –
                          danach alles frei anpassbar.
                        </p>
                      </div>
                    </div>
                    <Button className="shrink-0" disabled={importing} onClick={importStarter}>
                      <Download className="size-4" />
                      {importing ? "Übernehme…" : "Starter-Katalog übernehmen"}
                    </Button>
                  </CardContent>
                </Card>
              )}

              {/* ===================== Bausteine ===================== */}
              <section className="flex flex-col gap-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h2 className="text-lg font-semibold">Bausteine</h2>
                    <p className="text-sm text-muted-foreground">Wiederverwendbare Übungen – Grundlage für die Einheiten.</p>
                  </div>
                  {!exForm && (
                    <div className="flex flex-wrap gap-2">
                      {(library.exercises.length > 0 || library.units.length > 0) && (
                        <Button size="sm" variant="outline" disabled={importing} onClick={importStarter} title="Fehlende Best-Practice-Inhalte ergänzen">
                          <Download className="size-4" />
                          Starter-Katalog
                        </Button>
                      )}
                      <Button size="sm" onClick={() => setExForm(emptyExerciseForm())}>
                        <Plus className="size-4" />
                        Neuer Baustein
                      </Button>
                    </div>
                  )}
                </div>

                {exForm && (
                  <Card>
                    <CardHeader className="p-3">
                      <CardTitle className="text-base">{exForm.id ? "Baustein bearbeiten" : "Neuer Baustein"}</CardTitle>
                    </CardHeader>
                    <CardContent className="flex flex-col gap-3 p-3 pt-0">
                      <div className="flex flex-wrap gap-3">
                        <div className="flex flex-col gap-1">
                          <Label className="text-xs">Kategorie</Label>
                          <Select value={String(exForm.category)} onValueChange={(v) => setExForm({ ...exForm, category: Number(v ?? "0") as GroupTrainingCategory })}>
                            <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
                            <SelectContent>
                              {CATS.map((c) => (
                                <SelectItem key={c} value={String(c)}>{categoryLabel[c]}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="flex flex-col gap-1">
                          <Label className="text-xs">Dauer (Min)</Label>
                          <Input type="number" min={1} max={180} className="w-24" value={exForm.durationMinutes} onChange={(e) => setExForm({ ...exForm, durationMinutes: e.target.value })} />
                        </div>
                      </div>
                      <div className="flex flex-col gap-1">
                        <Label className="text-xs">Titel</Label>
                        <Input value={exForm.title} onChange={(e) => setExForm({ ...exForm, title: e.target.value })} maxLength={200} />
                      </div>
                      <div className="flex flex-col gap-1">
                        <Label className="text-xs">Fokus (z.B. Leinenführigkeit)</Label>
                        <Input value={exForm.focus} onChange={(e) => setExForm({ ...exForm, focus: e.target.value })} maxLength={80} />
                      </div>
                      <div className="flex flex-col gap-1">
                        <Label className="text-xs">Ablauf / Beschreibung</Label>
                        <textarea className={textareaClass} rows={2} value={exForm.description} onChange={(e) => setExForm({ ...exForm, description: e.target.value })} maxLength={2000} />
                      </div>
                      <div className="flex flex-col gap-1.5">
                        <Label className="text-xs">Bereitet auf Prüfung vor (optional)</Label>
                        <div className="flex flex-wrap gap-3">
                          {EXAM_FLAGS.map((f) => (
                            <label key={f.bit} className="flex items-center gap-1.5 text-sm">
                              <input
                                type="checkbox"
                                className="size-4 accent-primary"
                                checked={(exForm.examTargets & f.bit) !== 0}
                                onChange={(e) => setExForm({ ...exForm, examTargets: e.target.checked ? exForm.examTargets | f.bit : exForm.examTargets & ~f.bit })}
                              />
                              {f.label}
                            </label>
                          ))}
                        </div>
                      </div>
                      <div className="flex gap-2">
                        <Button type="button" disabled={saving} onClick={saveExercise}>
                          {saving ? "Speichert…" : exForm.id ? "Speichern" : "Anlegen"}
                        </Button>
                        <Button type="button" variant="ghost" onClick={() => setExForm(null)}>Abbrechen</Button>
                      </div>
                    </CardContent>
                  </Card>
                )}

                {library.exercises.length === 0 ? (
                  <Card>
                    <CardContent className="py-8 text-center text-sm text-muted-foreground">Noch keine Bausteine. Lege den ersten an.</CardContent>
                  </Card>
                ) : (
                  CATS.map((cat) => {
                    const list = library.exercises.filter((e) => e.category === cat);
                    if (list.length === 0) return null;
                    return (
                      <div key={cat} className="flex flex-col gap-2">
                        <h3 className="text-sm font-medium text-muted-foreground">{categoryLabel[cat]}</h3>
                        {list.map(exerciseCard)}
                      </div>
                    );
                  })
                )}
              </section>

              {/* ===================== Einheiten ===================== */}
              <section className="flex flex-col gap-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h2 className="text-lg font-semibold">Einheiten</h2>
                    <p className="text-sm text-muted-foreground">Geordnete Zusammenstellungen aus Bausteinen – Mischung &amp; Reihenfolge.</p>
                  </div>
                  {!unitForm && (
                    <Button
                      size="sm"
                      disabled={library.exercises.length === 0}
                      title={library.exercises.length === 0 ? "Zuerst Bausteine anlegen" : undefined}
                      onClick={() => setUnitForm(emptyUnitForm())}
                    >
                      <Plus className="size-4" />
                      Neue Einheit
                    </Button>
                  )}
                </div>

                {unitForm && (
                  <Card>
                    <CardHeader className="p-3">
                      <CardTitle className="text-base">{unitForm.id ? "Einheit bearbeiten" : "Neue Einheit"}</CardTitle>
                    </CardHeader>
                    <CardContent className="flex flex-col gap-3 p-3 pt-0">
                      <div className="flex flex-wrap gap-3">
                        <div className="flex flex-col gap-1">
                          <Label className="text-xs">Kategorie</Label>
                          <Select value={String(unitForm.category)} onValueChange={(v) => setUnitForm({ ...unitForm, category: Number(v ?? "0") as GroupTrainingCategory })}>
                            <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
                            <SelectContent>
                              {CATS.map((c) => (
                                <SelectItem key={c} value={String(c)}>{categoryLabel[c]}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="flex flex-1 flex-col gap-1">
                          <Label className="text-xs">Titel</Label>
                          <Input value={unitForm.title} onChange={(e) => setUnitForm({ ...unitForm, title: e.target.value })} maxLength={200} />
                        </div>
                      </div>
                      <div className="flex flex-col gap-1">
                        <Label className="text-xs">Beschreibung (optional)</Label>
                        <textarea className={textareaClass} rows={2} value={unitForm.description} onChange={(e) => setUnitForm({ ...unitForm, description: e.target.value })} maxLength={2000} />
                      </div>

                      <div className="flex flex-col gap-1.5">
                        <Label className="text-xs">Übungen (Reihenfolge)</Label>
                        {unitForm.exerciseIds.length === 0 ? (
                          <p className="text-xs text-muted-foreground">Noch keine Bausteine gewählt.</p>
                        ) : (
                          <ol className="flex flex-col gap-1.5">
                            {unitForm.exerciseIds.map((id, index) => {
                              const ex = exerciseById(id);
                              return (
                                <li key={`${id}-${index}`} className="flex items-center gap-2 rounded-md border bg-muted/30 p-2">
                                  <span className="text-xs text-muted-foreground">{index + 1}.</span>
                                  <span className="min-w-0 flex-1 text-sm [overflow-wrap:anywhere]">
                                    {ex ? ex.title : "(unbekannt)"}
                                    {ex?.focus && <span className="text-muted-foreground"> · {ex.focus}</span>}
                                  </span>
                                  <div className="flex shrink-0 gap-0.5">
                                    <Button type="button" size="icon" variant="ghost" className="size-7" disabled={index === 0} onClick={() => moveItem(index, -1)} aria-label="Nach oben">
                                      <ChevronUp className="size-4" />
                                    </Button>
                                    <Button type="button" size="icon" variant="ghost" className="size-7" disabled={index === unitForm.exerciseIds.length - 1} onClick={() => moveItem(index, 1)} aria-label="Nach unten">
                                      <ChevronDown className="size-4" />
                                    </Button>
                                    <Button
                                      type="button"
                                      size="icon"
                                      variant="ghost"
                                      className="size-7"
                                      onClick={() => setUnitForm({ ...unitForm, exerciseIds: unitForm.exerciseIds.filter((_, i) => i !== index) })}
                                      aria-label="Entfernen"
                                    >
                                      <X className="size-4 text-muted-foreground" />
                                    </Button>
                                  </div>
                                </li>
                              );
                            })}
                          </ol>
                        )}
                        <div className="flex gap-2">
                          <Select
                            value={addExerciseId}
                            onValueChange={(v) => {
                              if (v) setUnitForm({ ...unitForm, exerciseIds: [...unitForm.exerciseIds, v] });
                              setAddExerciseId("");
                            }}
                          >
                            <SelectTrigger className="flex-1">
                              <SelectValue placeholder="Baustein hinzufügen…" />
                            </SelectTrigger>
                            <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
                              {library.exercises.map((ex) => (
                                <SelectItem key={ex.id} value={ex.id}>
                                  {categoryLabel[ex.category]} · {ex.title}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>

                      <div className="flex gap-2">
                        <Button type="button" disabled={saving} onClick={saveUnit}>
                          {saving ? "Speichert…" : unitForm.id ? "Speichern" : "Anlegen"}
                        </Button>
                        <Button type="button" variant="ghost" onClick={() => setUnitForm(null)}>Abbrechen</Button>
                      </div>
                    </CardContent>
                  </Card>
                )}

                {library.units.length === 0 ? (
                  <Card>
                    <CardContent className="py-8 text-center text-sm text-muted-foreground">Noch keine Einheiten zusammengestellt.</CardContent>
                  </Card>
                ) : (
                  CATS.map((cat) => {
                    const list = library.units.filter((u) => u.category === cat);
                    if (list.length === 0) return null;
                    return (
                      <div key={cat} className="flex flex-col gap-2">
                        <h3 className="text-sm font-medium text-muted-foreground">{categoryLabel[cat]}</h3>
                        {list.map((unit) => {
                          const isOpen = expanded.has(unit.id);
                          return (
                            <Card key={unit.id}>
                              <CardHeader className="space-y-0 p-3">
                                <button type="button" onClick={() => toggleExpand(unit.id)} aria-expanded={isOpen} className="flex w-full items-start justify-between gap-2 text-left">
                                  <span className="flex min-w-0 items-start gap-1.5">
                                    {isOpen ? <ChevronDown className="mt-0.5 size-4 shrink-0" /> : <ChevronRight className="mt-0.5 size-4 shrink-0" />}
                                    <CardTitle className="text-sm font-medium break-words [overflow-wrap:anywhere]">{unit.title}</CardTitle>
                                  </span>
                                  <Badge variant={categoryVariant[unit.category]} className="shrink-0">{categoryLabel[unit.category]}</Badge>
                                </button>
                                <p className="mt-1 flex items-center gap-1 pl-5.5 text-xs text-muted-foreground">
                                  <Clock className="size-3" />
                                  {unit.items.length} Übungen · {unit.totalMinutes} Min
                                </p>
                              </CardHeader>
                              {isOpen && (
                                <CardContent className="flex flex-col gap-3 border-t p-3">
                                  {unit.description && <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{unit.description}</p>}
                                  <ol className="flex flex-col gap-1.5">
                                    {unit.items.map((item, idx) => (
                                      <li key={item.id} className="rounded-md border bg-muted/30 p-2.5">
                                        <div className="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-1">
                                          <span className="text-sm font-medium [overflow-wrap:anywhere]">{idx + 1}. {item.exercise.title}</span>
                                          <span className="flex items-center gap-1.5">
                                            {item.exercise.focus && <Badge variant="secondary">{item.exercise.focus}</Badge>}
                                            {item.exercise.durationMinutes != null && <span className="text-xs text-muted-foreground">{item.exercise.durationMinutes} Min</span>}
                                          </span>
                                        </div>
                                        {item.exercise.description && <p className="mt-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">{item.exercise.description}</p>}
                                      </li>
                                    ))}
                                  </ol>
                                  <div className="flex flex-wrap gap-2">
                                    <Button type="button" size="sm" variant="outline" onClick={() => { setUnitForm({ id: unit.id, category: unit.category, title: unit.title, description: unit.description ?? "", exerciseIds: unit.items.map((i) => i.exerciseId) }); if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" }); }}>
                                      <Pencil className="size-3.5" />
                                      Bearbeiten
                                    </Button>
                                    <Button type="button" size="sm" variant="outline" onClick={() => duplicateUnit(unit.id)}>
                                      <Copy className="size-3.5" />
                                      Duplizieren
                                    </Button>
                                    <Button type="button" size="sm" variant="ghost" onClick={() => deleteUnit(unit.id, unit.title)}>
                                      <Trash2 className="size-3.5 text-muted-foreground" />
                                      Löschen
                                    </Button>
                                  </div>
                                </CardContent>
                              )}
                            </Card>
                          );
                        })}
                      </div>
                    );
                  })
                )}
              </section>
            </>
          )}
        </>
      )}
    </div>
  );
}

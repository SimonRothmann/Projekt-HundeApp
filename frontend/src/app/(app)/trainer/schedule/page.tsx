"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import type {
  Club,
  Group,
  GroupTrainingCategory,
  GroupTrainingExercise,
  GroupTrainingLibrary,
  GroupTrainingSession,
} from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ArrowLeft, ChevronDown, ChevronUp, Clock, MapPin, Pencil, Plus, Sparkles, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const CATS: GroupTrainingCategory[] = [0, 1, 2];
const categoryLabel: Record<GroupTrainingCategory, string> = { 0: "Welpen", 1: "Junghunde", 2: "Basis" };
const categoryVariant: Record<GroupTrainingCategory, "default" | "secondary" | "outline"> = { 0: "default", 1: "secondary", 2: "outline" };
const WEEKDAYS = ["Sonntag", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag"];

const textareaClass =
  "w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30";

const todayIso = () => new Date().toISOString().slice(0, 10);
const fmtDate = (iso: string) => new Date(iso).toLocaleDateString("de-DE", { weekday: "short", day: "2-digit", month: "2-digit", year: "numeric" });
const fmtTime = (iso: string) => new Date(iso).toLocaleTimeString("de-DE", { hour: "2-digit", minute: "2-digit" });

type ContentDraft = { key: string; exerciseId: string | null; freeText: string | null };
let draftSeq = 0;
const exDraft = (id: string): ContentDraft => ({ key: `k${draftSeq++}`, exerciseId: id, freeText: null });
const textDraft = (text: string): ContentDraft => ({ key: `k${draftSeq++}`, exerciseId: null, freeText: text });

type Mode = "single" | "series";
type Form = {
  editingId: string | null;
  mode: Mode;
  groupId: string;
  category: GroupTrainingCategory;
  date: string; // single
  time: string;
  weekday: number; // series
  fromDate: string;
  toDate: string;
  durationMinutes: string;
  location: string;
  notes: string;
  content: ContentDraft[];
};

const emptyForm = (): Form => ({
  editingId: null,
  mode: "single",
  groupId: "",
  category: 0,
  date: todayIso(),
  time: "18:00",
  weekday: 2,
  fromDate: todayIso(),
  toDate: todayIso(),
  durationMinutes: "60",
  location: "",
  notes: "",
  content: [],
});

export default function SchedulePage() {
  const { user } = useAuth();
  const [clubs, setClubs] = useState<Club[] | null>(null);
  const [clubId, setClubId] = useState("");
  const [groups, setGroups] = useState<Group[]>([]);
  const [library, setLibrary] = useState<GroupTrainingLibrary | null>(null);
  const [sessions, setSessions] = useState<GroupTrainingSession[] | null>(null);

  const [filterGroup, setFilterGroup] = useState("");
  const [filterCategory, setFilterCategory] = useState("");
  const [mineOnly, setMineOnly] = useState(false);

  const [form, setForm] = useState<Form | null>(null);
  const [addBausteinId, setAddBausteinId] = useState("");
  const [addFreeText, setAddFreeText] = useState("");
  const [saving, setSaving] = useState(false);
  const [generating, setGenerating] = useState(false);

  const exercisesById = useMemo(() => {
    const m = new Map<string, GroupTrainingExercise>();
    library?.exercises.forEach((e) => m.set(e.id, e));
    return m;
  }, [library]);

  const loadSchedule = useCallback(async () => {
    if (!clubId) return;
    const params = new URLSearchParams({ from: todayIso() });
    if (filterGroup) params.set("groupId", filterGroup);
    if (filterCategory) params.set("category", filterCategory);
    if (mineOnly) params.set("mineOnly", "true");
    try {
      setSessions(await api.get<GroupTrainingSession[]>(`/api/group-training/schedule/clubs/${clubId}?${params}`));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Termine konnten nicht geladen werden.");
    }
  }, [clubId, filterGroup, filterCategory, mineOnly]);

  useEffect(() => {
    api.get<Club[]>("/api/groups/my-clubs").then((data) => {
      setClubs(data);
      if (data.length > 0) setClubId((prev) => prev || data[0].id);
    }).catch(() => setClubs([]));
  }, []);

  useEffect(() => {
    if (!clubId) return;
    api.get<Group[]>(`/api/clubs/${clubId}/groups`).then(setGroups).catch(() => setGroups([]));
    api.get<GroupTrainingLibrary>(`/api/group-training/clubs/${clubId}/library`).then(setLibrary).catch(() => setLibrary(null));
  }, [clubId]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadSchedule();
  }, [loadSchedule]);

  function openCreate() {
    const f = emptyForm();
    if (groups.length > 0) f.groupId = groups[0].id;
    setForm(f);
    if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function openEdit(s: GroupTrainingSession) {
    const d = new Date(s.startsAt);
    setForm({
      editingId: s.id,
      mode: "single",
      groupId: s.groupId,
      category: s.category,
      date: d.toISOString().slice(0, 10),
      time: d.toTimeString().slice(0, 5),
      weekday: d.getDay(),
      fromDate: todayIso(),
      toDate: todayIso(),
      durationMinutes: String(s.durationMinutes),
      location: s.location ?? "",
      notes: s.notes ?? "",
      content: s.items.map((i) => (i.exerciseId ? exDraft(i.exerciseId) : textDraft(i.freeText ?? ""))),
    });
    if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function patch(p: Partial<Form>) {
    setForm((f) => (f ? { ...f, ...p } : f));
  }
  function moveContent(index: number, dir: -1 | 1) {
    setForm((f) => {
      if (!f) return f;
      const c = [...f.content];
      const t = index + dir;
      if (t < 0 || t >= c.length) return f;
      [c[index], c[t]] = [c[t], c[index]];
      return { ...f, content: c };
    });
  }

  async function generateContent() {
    if (!form) return;
    setGenerating(true);
    try {
      const picked = await api.get<GroupTrainingExercise[]>(`/api/group-training/schedule/clubs/${clubId}/generate-content?category=${form.category}`);
      if (picked.length === 0) {
        toast.info("Keine passenden Bausteine in der Bibliothek – lege welche an oder übernimm den Starter-Katalog.");
      }
      patch({ content: picked.map((e) => exDraft(e.id)) });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Vorschlag konnte nicht erzeugt werden.");
    } finally {
      setGenerating(false);
    }
  }

  function applyUnit(unitId: string) {
    const unit = library?.units.find((u) => u.id === unitId);
    if (!unit) return;
    patch({ content: unit.items.map((i) => exDraft(i.exerciseId)) });
  }

  function contentPayload(content: ContentDraft[]) {
    return content
      .filter((c) => c.exerciseId || c.freeText?.trim())
      .map((c) => ({ exerciseId: c.exerciseId, freeText: c.exerciseId ? null : c.freeText?.trim() || null }));
  }

  async function submit() {
    if (!form) return;
    if (!form.groupId) return toast.error("Gruppe wählen.");
    const items = contentPayload(form.content);
    const duration = Number(form.durationMinutes) || 60;
    const trainerUserIds = user ? [user.userId] : [];
    setSaving(true);
    try {
      if (form.mode === "single") {
        const startsAt = new Date(`${form.date}T${form.time}`).toISOString();
        const body = { groupId: form.groupId, category: form.category, startsAt, durationMinutes: duration, location: form.location.trim() || null, notes: form.notes.trim() || null, trainerUserIds, items };
        if (form.editingId) await api.put(`/api/group-training/schedule/sessions/${form.editingId}`, body);
        else await api.post(`/api/group-training/schedule/clubs/${clubId}/sessions`, body);
        toast.success(form.editingId ? "Termin aktualisiert." : "Termin angelegt.");
      } else {
        const starts = seriesDates(form);
        if (starts.length === 0) return toast.error("Kein Termin im Zeitraum am gewählten Wochentag.");
        const body = { groupId: form.groupId, category: form.category, starts, durationMinutes: duration, location: form.location.trim() || null, trainerUserIds, items };
        const created = await api.post<GroupTrainingSession[]>(`/api/group-training/schedule/clubs/${clubId}/series`, body);
        toast.success(`${created.length} Termine angelegt.`);
      }
      setForm(null);
      await loadSchedule();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Speichern fehlgeschlagen.");
    } finally {
      setSaving(false);
    }
  }

  async function cancelSession(s: GroupTrainingSession) {
    if (!window.confirm(`Termin am ${fmtDate(s.startsAt)} absagen? Mitglieder sehen die Absage.`)) return;
    try {
      await api.post(`/api/group-training/schedule/sessions/${s.id}/cancel`);
      toast.success("Termin abgesagt.");
      await loadSchedule();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Absagen fehlgeschlagen.");
    }
  }
  async function deleteSession(s: GroupTrainingSession) {
    if (!window.confirm(`Termin am ${fmtDate(s.startsAt)} endgültig löschen?`)) return;
    try {
      await api.delete(`/api/group-training/schedule/sessions/${s.id}`);
      toast.success("Termin gelöscht.");
      await loadSchedule();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Löschen fehlgeschlagen.");
    }
  }

  const contentLabel = (c: ContentDraft) => (c.exerciseId ? exercisesById.get(c.exerciseId)?.title ?? "(Baustein)" : c.freeText ?? "");

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link href="/trainer" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
          <ArrowLeft className="size-4" />
          Zur Trainer-Übersicht
        </Link>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">Terminplanung</h1>
        <p className="text-muted-foreground">Plane Gruppentrainings: wann, welche Gruppe, was gemacht wird. Mitglieder sehen die Termine ihrer Gruppe.</p>
      </div>

      {clubs === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : clubs.length === 0 ? (
        <Card><CardContent className="py-10 text-center text-muted-foreground">Die Terminplanung ist für Vereinstrainer:innen. Du bist für keinen Verein als Trainer:in eingetragen.</CardContent></Card>
      ) : (
        <>
          <div className="flex flex-wrap items-end gap-3">
            {clubs.length > 1 && (
              <div className="flex flex-col gap-1">
                <Label>Verein</Label>
                <Select value={clubId} onValueChange={(v) => setClubId(v ?? "")}>
                  <SelectTrigger className="w-56"><SelectValue /></SelectTrigger>
                  <SelectContent>{clubs.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}</SelectContent>
                </Select>
              </div>
            )}
            {!form && <Button className="ml-auto" onClick={openCreate}><Plus className="size-4" />Neuer Termin</Button>}
          </div>

          {form && (
            <Card>
              <CardHeader className="p-3">
                <CardTitle className="text-base">{form.editingId ? "Termin bearbeiten" : "Neuer Termin"}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-3 p-3 pt-0">
                {!form.editingId && (
                  <div className="flex gap-2">
                    <Button type="button" size="sm" variant={form.mode === "single" ? "default" : "outline"} onClick={() => patch({ mode: "single" })}>Einzeltermin</Button>
                    <Button type="button" size="sm" variant={form.mode === "series" ? "default" : "outline"} onClick={() => patch({ mode: "series" })}>Wöchentliche Serie</Button>
                  </div>
                )}

                <div className="flex flex-wrap gap-3">
                  <div className="flex flex-col gap-1">
                    <Label className="text-xs">Gruppe</Label>
                    <Select value={form.groupId} onValueChange={(v) => patch({ groupId: v ?? "" })}>
                      <SelectTrigger className="w-52"><SelectValue placeholder="Gruppe…" /></SelectTrigger>
                      <SelectContent>{groups.map((g) => <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>)}</SelectContent>
                    </Select>
                  </div>
                  <div className="flex flex-col gap-1">
                    <Label className="text-xs">Stufe</Label>
                    <Select value={String(form.category)} onValueChange={(v) => patch({ category: Number(v ?? "0") as GroupTrainingCategory })}>
                      <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
                      <SelectContent>{CATS.map((c) => <SelectItem key={c} value={String(c)}>{categoryLabel[c]}</SelectItem>)}</SelectContent>
                    </Select>
                  </div>
                </div>

                {form.mode === "single" ? (
                  <div className="flex flex-wrap gap-3">
                    <div className="flex flex-col gap-1"><Label className="text-xs">Datum</Label><Input type="date" className="w-40" value={form.date} onChange={(e) => patch({ date: e.target.value })} /></div>
                    <div className="flex flex-col gap-1"><Label className="text-xs">Uhrzeit</Label><Input type="time" className="w-28" value={form.time} onChange={(e) => patch({ time: e.target.value })} /></div>
                    <div className="flex flex-col gap-1"><Label className="text-xs">Dauer (Min)</Label><Input type="number" min={15} max={240} className="w-24" value={form.durationMinutes} onChange={(e) => patch({ durationMinutes: e.target.value })} /></div>
                  </div>
                ) : (
                  <div className="flex flex-wrap gap-3">
                    <div className="flex flex-col gap-1">
                      <Label className="text-xs">Wochentag</Label>
                      <Select value={String(form.weekday)} onValueChange={(v) => patch({ weekday: Number(v ?? "2") })}>
                        <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
                        <SelectContent>{[1, 2, 3, 4, 5, 6, 0].map((d) => <SelectItem key={d} value={String(d)}>{WEEKDAYS[d]}</SelectItem>)}</SelectContent>
                      </Select>
                    </div>
                    <div className="flex flex-col gap-1"><Label className="text-xs">Uhrzeit</Label><Input type="time" className="w-28" value={form.time} onChange={(e) => patch({ time: e.target.value })} /></div>
                    <div className="flex flex-col gap-1"><Label className="text-xs">Von</Label><Input type="date" className="w-40" value={form.fromDate} onChange={(e) => patch({ fromDate: e.target.value })} /></div>
                    <div className="flex flex-col gap-1"><Label className="text-xs">Bis</Label><Input type="date" className="w-40" value={form.toDate} onChange={(e) => patch({ toDate: e.target.value })} /></div>
                    <div className="flex flex-col gap-1"><Label className="text-xs">Dauer (Min)</Label><Input type="number" min={15} max={240} className="w-24" value={form.durationMinutes} onChange={(e) => patch({ durationMinutes: e.target.value })} /></div>
                  </div>
                )}
                {form.mode === "series" && <p className="text-xs text-muted-foreground">Erzeugt {seriesDates(form).length} Einzeltermine – danach frei einzeln anpass-/absagbar.</p>}

                <div className="flex flex-col gap-1">
                  <Label className="text-xs">Ort (optional, z.B. Wald / Parkplatz)</Label>
                  <Input value={form.location} onChange={(e) => patch({ location: e.target.value })} maxLength={200} placeholder="Üblicher Platz, wenn leer" />
                </div>
                {form.mode === "single" && (
                  <div className="flex flex-col gap-1">
                    <Label className="text-xs">Notiz (optional)</Label>
                    <textarea className={textareaClass} rows={2} value={form.notes} onChange={(e) => patch({ notes: e.target.value })} maxLength={2000} />
                  </div>
                )}

                {/* Inhalt */}
                <div className="flex flex-col gap-2 rounded-md border bg-muted/20 p-2.5">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <Label className="text-xs">Inhalt (Übungen in Reihenfolge)</Label>
                    <div className="flex flex-wrap gap-2">
                      <Button type="button" size="sm" variant="outline" disabled={generating} onClick={generateContent}>
                        <Sparkles className="size-3.5" />
                        {generating ? "Generiere…" : "Vorschlag generieren"}
                      </Button>
                      {library && library.units.filter((u) => u.category === form.category).length > 0 && (
                        <Select value="" onValueChange={(v) => v && applyUnit(v)}>
                          <SelectTrigger className="h-8 w-44"><SelectValue placeholder="Aus Bibliothek…" /></SelectTrigger>
                          <SelectContent>{library.units.filter((u) => u.category === form.category).map((u) => <SelectItem key={u.id} value={u.id}>{u.title}</SelectItem>)}</SelectContent>
                        </Select>
                      )}
                    </div>
                  </div>

                  {form.content.length === 0 ? (
                    <p className="text-xs text-muted-foreground">Noch kein Inhalt – generieren, aus der Bibliothek übernehmen oder unten hinzufügen.</p>
                  ) : (
                    <ol className="flex flex-col gap-1.5">
                      {form.content.map((c, index) => (
                        <li key={c.key} className="flex items-center gap-2 rounded-md border bg-background p-2">
                          <span className="text-xs text-muted-foreground">{index + 1}.</span>
                          <span className="min-w-0 flex-1 text-sm [overflow-wrap:anywhere]">
                            {contentLabel(c)}
                            {c.freeText && <Badge variant="outline" className="ml-1">Freitext</Badge>}
                          </span>
                          <div className="flex shrink-0 gap-0.5">
                            <Button type="button" size="icon" variant="ghost" className="size-7" disabled={index === 0} onClick={() => moveContent(index, -1)} aria-label="Hoch"><ChevronUp className="size-4" /></Button>
                            <Button type="button" size="icon" variant="ghost" className="size-7" disabled={index === form.content.length - 1} onClick={() => moveContent(index, 1)} aria-label="Runter"><ChevronDown className="size-4" /></Button>
                            <Button type="button" size="icon" variant="ghost" className="size-7" onClick={() => patch({ content: form.content.filter((_, i) => i !== index) })} aria-label="Entfernen"><X className="size-4 text-muted-foreground" /></Button>
                          </div>
                        </li>
                      ))}
                    </ol>
                  )}

                  <div className="flex flex-wrap gap-2">
                    <Select value={addBausteinId} onValueChange={(v) => { if (v) { patch({ content: [...form.content, exDraft(v)] }); } setAddBausteinId(""); }}>
                      <SelectTrigger className="h-8 flex-1 min-w-40"><SelectValue placeholder="Baustein hinzufügen…" /></SelectTrigger>
                      <SelectContent className="max-h-[60vh] touch-pan-y overscroll-contain">
                        {(library?.exercises ?? []).map((e) => <SelectItem key={e.id} value={e.id}>{categoryLabel[e.category]} · {e.title}</SelectItem>)}
                      </SelectContent>
                    </Select>
                    <div className="flex flex-1 gap-2">
                      <Input className="flex-1" placeholder="Freitext-Übung" value={addFreeText} onChange={(e) => setAddFreeText(e.target.value)} maxLength={500} />
                      <Button type="button" size="sm" variant="outline" disabled={!addFreeText.trim()} onClick={() => { patch({ content: [...form.content, textDraft(addFreeText.trim())] }); setAddFreeText(""); }}>+</Button>
                    </div>
                  </div>
                </div>

                <div className="flex gap-2">
                  <Button type="button" disabled={saving} onClick={submit}>{saving ? "Speichert…" : form.editingId ? "Speichern" : form.mode === "series" ? "Serie anlegen" : "Anlegen"}</Button>
                  <Button type="button" variant="ghost" onClick={() => setForm(null)}>Abbrechen</Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Filter */}
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex flex-col gap-1">
              <Label className="text-xs">Gruppe</Label>
              <Select value={filterGroup} onValueChange={(v) => setFilterGroup(v ?? "")}>
                <SelectTrigger className="h-8 w-44"><SelectValue placeholder="Alle Gruppen" /></SelectTrigger>
                <SelectContent><SelectItem value="">Alle Gruppen</SelectItem>{groups.map((g) => <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1">
              <Label className="text-xs">Stufe</Label>
              <Select value={filterCategory} onValueChange={(v) => setFilterCategory(v ?? "")}>
                <SelectTrigger className="h-8 w-36"><SelectValue placeholder="Alle" /></SelectTrigger>
                <SelectContent><SelectItem value="">Alle</SelectItem>{CATS.map((c) => <SelectItem key={c} value={String(c)}>{categoryLabel[c]}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <label className="flex items-center gap-1.5 pb-1.5 text-sm"><input type="checkbox" className="size-4 accent-primary" checked={mineOnly} onChange={(e) => setMineOnly(e.target.checked)} />Nur meine</label>
          </div>

          {/* Agenda */}
          {sessions === null ? (
            <p className="text-muted-foreground">Lädt…</p>
          ) : sessions.length === 0 ? (
            <Card><CardContent className="py-8 text-center text-sm text-muted-foreground">Keine kommenden Termine.</CardContent></Card>
          ) : (
            <div className="flex flex-col gap-2">
              {sessions.map((s) => (
                <Card key={s.id} className={cn(s.status === 1 && "opacity-60")}>
                  <CardContent className="flex flex-col gap-2 p-3">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium">{fmtDate(s.startsAt)} · {fmtTime(s.startsAt)} <span className="text-muted-foreground">({s.durationMinutes} Min)</span></p>
                        <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{s.groupName}</p>
                      </div>
                      <div className="flex shrink-0 flex-wrap items-center justify-end gap-1">
                        <Badge variant={categoryVariant[s.category]}>{categoryLabel[s.category]}</Badge>
                        {s.status === 1 && <Badge variant="outline">Abgesagt</Badge>}
                      </div>
                    </div>
                    {s.location && <p className="flex items-center gap-1 text-xs text-muted-foreground"><MapPin className="size-3" />{s.location}</p>}
                    {s.items.length > 0 && (
                      <p className="flex items-center gap-1 text-xs text-muted-foreground"><Clock className="size-3" />{s.items.length} Übungen · {s.plannedMinutes} Min</p>
                    )}
                    {s.items.length > 0 && (
                      <ol className="flex flex-col gap-0.5 pl-1 text-sm">
                        {s.items.map((i, idx) => (
                          <li key={i.id} className="[overflow-wrap:anywhere]">{idx + 1}. {i.exercise ? i.exercise.title : i.freeText}{i.exercise?.focus && <span className="text-muted-foreground"> · {i.exercise.focus}</span>}</li>
                        ))}
                      </ol>
                    )}
                    <div className="flex flex-wrap gap-2">
                      <Button type="button" size="sm" variant="outline" onClick={() => openEdit(s)}><Pencil className="size-3.5" />Bearbeiten</Button>
                      {s.status === 0 && <Button type="button" size="sm" variant="ghost" onClick={() => cancelSession(s)}>Absagen</Button>}
                      <Button type="button" size="sm" variant="ghost" onClick={() => deleteSession(s)}><Trash2 className="size-3.5 text-muted-foreground" />Löschen</Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

// Konkrete Termin-Zeitpunkte einer wöchentlichen Serie (zeitzonen-korrekt im Browser).
function seriesDates(form: Form): string[] {
  const out: string[] = [];
  const from = new Date(`${form.fromDate}T${form.time}`);
  const to = new Date(`${form.toDate}T23:59:59`);
  if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime()) || from > to) return out;
  const d = new Date(from);
  // auf den ersten passenden Wochentag ab "from" vorrücken
  while (d.getDay() !== form.weekday && d <= to) d.setDate(d.getDate() + 1);
  while (d <= to) {
    out.push(d.toISOString());
    d.setDate(d.getDate() + 7);
  }
  return out;
}

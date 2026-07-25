"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import type { Group, GroupTrainingCategory, GroupTrainingLibrary, GroupTrainingUnit } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ArrowLeft, ChevronDown, ChevronRight, Clock, Copy, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const categoryLabel: Record<GroupTrainingCategory, string> = { 0: "Welpen", 1: "Junghunde", 2: "Allgemein" };
const categoryVariant: Record<GroupTrainingCategory, "default" | "secondary" | "outline"> = {
  0: "default",
  1: "secondary",
  2: "outline",
};
const categoryOrder: GroupTrainingCategory[] = [0, 1, 2];

// Mehrzeiliges Feld im Stil von <Input> (es gibt keine Textarea-Komponente).
const textareaClass =
  "w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-1.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30";

type ItemDraft = { title: string; focus: string; durationMinutes: string; description: string };

const emptyItem = (): ItemDraft => ({ title: "", focus: "", durationMinutes: "", description: "" });

export default function GroupTrainingPage() {
  const [library, setLibrary] = useState<GroupTrainingLibrary | null>(null);
  const [groups, setGroups] = useState<Group[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Formular (Erstellen/Bearbeiten)
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [fTitle, setFTitle] = useState("");
  const [fDescription, setFDescription] = useState("");
  const [fCategory, setFCategory] = useState<GroupTrainingCategory>(0);
  const [fGroupId, setFGroupId] = useState("");
  const [fItems, setFItems] = useState<ItemDraft[]>([emptyItem()]);
  const [submitting, setSubmitting] = useState(false);

  // Ansicht/Interaktion pro Einheit
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [copyUnitId, setCopyUnitId] = useState<string | null>(null);
  const [copyGroupId, setCopyGroupId] = useState("");
  const [copying, setCopying] = useState(false);

  async function loadLibrary() {
    try {
      const data = await api.get<GroupTrainingLibrary>("/api/group-training/library");
      setLibrary(data);
      setLoadError(null);
    } catch (err) {
      // Das Backend liefert bei Nicht-Trainern eine strukturierte Absage.
      setLoadError(
        err instanceof ApiError ? err.message : "Gruppentraining konnte nicht geladen werden.",
      );
    }
  }

  useEffect(() => {
    // Initialer Datenabruf bei Mount (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLibrary();
    api
      .get<Group[]>("/api/groups")
      .then(setGroups)
      .catch(() => {
        /* Gruppen sind optional (nur für "In Gruppe übernehmen") */
      });
  }, []);

  function toggleExpand(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function openCreate() {
    setEditingId(null);
    setFTitle("");
    setFDescription("");
    setFCategory(0);
    setFGroupId("");
    setFItems([emptyItem()]);
    setFormOpen(true);
  }

  function openEdit(unit: GroupTrainingUnit) {
    setEditingId(unit.id);
    setFTitle(unit.title);
    setFDescription(unit.description ?? "");
    setFCategory(unit.category);
    setFGroupId(unit.groupId ?? "");
    setFItems(
      unit.items.length
        ? unit.items.map((i) => ({
            title: i.title,
            focus: i.focus ?? "",
            durationMinutes: i.durationMinutes != null ? String(i.durationMinutes) : "",
            description: i.description ?? "",
          }))
        : [emptyItem()],
    );
    setFormOpen(true);
    if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function setItem(index: number, patch: Partial<ItemDraft>) {
    setFItems((prev) => prev.map((it, i) => (i === index ? { ...it, ...patch } : it)));
  }

  async function submitForm() {
    if (!fTitle.trim()) {
      toast.error("Titel eingeben.");
      return;
    }
    const items = fItems
      .filter((i) => i.title.trim())
      .map((i) => ({
        title: i.title.trim(),
        description: i.description.trim() || null,
        focus: i.focus.trim() || null,
        durationMinutes: i.durationMinutes ? Number(i.durationMinutes) : null,
      }));
    if (items.length === 0) {
      toast.error("Mindestens eine Übung angeben.");
      return;
    }

    setSubmitting(true);
    try {
      if (editingId) {
        await api.put(`/api/group-training/units/${editingId}`, {
          title: fTitle.trim(),
          description: fDescription.trim() || null,
          category: fCategory,
          items,
        });
        toast.success("Einheit aktualisiert.");
      } else {
        await api.post("/api/group-training/units", {
          title: fTitle.trim(),
          description: fDescription.trim() || null,
          category: fCategory,
          groupId: fGroupId || null,
          items,
        });
        toast.success("Einheit angelegt.");
      }
      setFormOpen(false);
      await loadLibrary();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einheit konnte nicht gespeichert werden.");
    } finally {
      setSubmitting(false);
    }
  }

  async function deleteUnit(unit: GroupTrainingUnit) {
    if (!window.confirm(`Einheit „${unit.title}" wirklich löschen?`)) return;
    try {
      await api.delete(`/api/group-training/units/${unit.id}`);
      toast.success("Einheit gelöscht.");
      await loadLibrary();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einheit konnte nicht gelöscht werden.");
    }
  }

  function startCopy(unit: GroupTrainingUnit) {
    setCopyUnitId((current) => (current === unit.id ? null : unit.id));
    setCopyGroupId(groups[0]?.id ?? "");
  }

  async function doCopy(unit: GroupTrainingUnit) {
    if (!copyGroupId) {
      toast.error("Gruppe auswählen.");
      return;
    }
    setCopying(true);
    try {
      await api.post(`/api/group-training/units/${unit.id}/copy-to-group`, { groupId: copyGroupId });
      const groupName = groups.find((g) => g.id === copyGroupId)?.name ?? "Gruppe";
      toast.success(`In „${groupName}" übernommen – jetzt unter „Meine Einheiten" anpassbar.`);
      setCopyUnitId(null);
      await loadLibrary();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Einheit konnte nicht übernommen werden.");
    } finally {
      setCopying(false);
    }
  }

  function groupName(groupId: string | null) {
    if (!groupId) return null;
    return groups.find((g) => g.id === groupId)?.name ?? "Gruppe";
  }

  function unitCard(unit: GroupTrainingUnit) {
    const isOpen = expanded.has(unit.id);
    const gName = groupName(unit.groupId);
    return (
      <Card key={unit.id}>
        <CardHeader className="space-y-0 p-3">
          <button
            type="button"
            onClick={() => toggleExpand(unit.id)}
            aria-expanded={isOpen}
            className="flex w-full items-start justify-between gap-2 text-left"
          >
            <div className="flex min-w-0 items-start gap-1.5">
              {isOpen ? <ChevronDown className="mt-0.5 size-4 shrink-0" /> : <ChevronRight className="mt-0.5 size-4 shrink-0" />}
              <CardTitle className="text-sm font-medium break-words [overflow-wrap:anywhere]">{unit.title}</CardTitle>
            </div>
            <div className="flex shrink-0 flex-wrap items-center justify-end gap-1">
              <Badge variant={categoryVariant[unit.category]}>{categoryLabel[unit.category]}</Badge>
              {gName && <Badge variant="outline">{gName}</Badge>}
            </div>
          </button>
          <p className="mt-1 flex items-center gap-1 pl-5.5 text-xs text-muted-foreground">
            <Clock className="size-3" />
            {unit.items.length} Übungen · {unit.totalMinutes} Min
          </p>
        </CardHeader>

        {isOpen && (
          <CardContent className="flex flex-col gap-3 border-t p-3">
            {unit.description && (
              <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{unit.description}</p>
            )}
            <ol className="flex flex-col gap-2">
              {unit.items.map((item, idx) => (
                <li key={item.id} className="rounded-md border bg-muted/30 p-2.5">
                  <div className="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-1">
                    <span className="text-sm font-medium [overflow-wrap:anywhere]">
                      {idx + 1}. {item.title}
                    </span>
                    <span className="flex items-center gap-1.5">
                      {item.focus && <Badge variant="secondary">{item.focus}</Badge>}
                      {item.durationMinutes != null && (
                        <span className="text-xs text-muted-foreground">{item.durationMinutes} Min</span>
                      )}
                    </span>
                  </div>
                  {item.description && (
                    <p className="mt-1 text-xs text-muted-foreground [overflow-wrap:anywhere]">{item.description}</p>
                  )}
                </li>
              ))}
            </ol>

            <div className="flex flex-wrap items-center gap-2">
              {unit.isMine ? (
                <>
                  <Button type="button" size="sm" variant="outline" onClick={() => openEdit(unit)}>
                    <Pencil className="size-3.5" />
                    Bearbeiten
                  </Button>
                  <Button type="button" size="sm" variant="ghost" onClick={() => deleteUnit(unit)}>
                    <Trash2 className="size-3.5 text-muted-foreground" />
                    Löschen
                  </Button>
                </>
              ) : (
                groups.length > 0 &&
                (copyUnitId === unit.id ? (
                  <div className="flex flex-wrap items-center gap-2">
                    <Select value={copyGroupId} onValueChange={(v) => setCopyGroupId(v ?? "")}>
                      <SelectTrigger className="h-8 w-44">
                        <SelectValue placeholder="Gruppe wählen…" />
                      </SelectTrigger>
                      <SelectContent>
                        {groups.map((g) => (
                          <SelectItem key={g.id} value={g.id}>
                            {g.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button type="button" size="sm" disabled={copying} onClick={() => doCopy(unit)}>
                      {copying ? "Übernehme…" : "Übernehmen"}
                    </Button>
                    <Button type="button" size="sm" variant="ghost" onClick={() => setCopyUnitId(null)}>
                      Abbrechen
                    </Button>
                  </div>
                ) : (
                  <Button type="button" size="sm" variant="outline" onClick={() => startCopy(unit)}>
                    <Copy className="size-3.5" />
                    In Gruppe übernehmen
                  </Button>
                ))
              )}
            </div>
          </CardContent>
        )}
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link
          href="/trainer"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-4" />
          Zur Trainer-Übersicht
        </Link>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">Gruppentraining</h1>
        <p className="text-muted-foreground">
          Vorgefertigte Trainingseinheiten für Welpen und Junghunde – für deine Gruppe übernehmen und anpassen –
          plus eigene Einheiten, die du selbst zusammenstellst.
        </p>
      </div>

      {loadError ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground">{loadError}</CardContent>
        </Card>
      ) : library === null ? (
        <p className="text-muted-foreground">Lädt…</p>
      ) : (
        <>
          {formOpen ? (
            <Card>
              <CardHeader className="p-3">
                <CardTitle className="text-base">{editingId ? "Einheit bearbeiten" : "Neue Einheit"}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-3 p-3 pt-0">
                <div className="flex flex-col gap-2">
                  <Label htmlFor="gt-title">Titel</Label>
                  <Input id="gt-title" value={fTitle} onChange={(e) => setFTitle(e.target.value)} maxLength={200} />
                </div>
                <div className="flex flex-col gap-2">
                  <Label htmlFor="gt-desc">Beschreibung (optional)</Label>
                  <textarea
                    id="gt-desc"
                    className={textareaClass}
                    rows={2}
                    value={fDescription}
                    onChange={(e) => setFDescription(e.target.value)}
                    maxLength={2000}
                  />
                </div>
                <div className="flex flex-wrap gap-3">
                  <div className="flex flex-col gap-2">
                    <Label>Kategorie</Label>
                    <Select
                      value={String(fCategory)}
                      onValueChange={(v) => setFCategory(Number(v ?? "0") as GroupTrainingCategory)}
                    >
                      <SelectTrigger className="w-40">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {categoryOrder.map((c) => (
                          <SelectItem key={c} value={String(c)}>
                            {categoryLabel[c]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  {!editingId && groups.length > 0 && (
                    <div className="flex flex-col gap-2">
                      <Label>Für Gruppe (optional)</Label>
                      <Select value={fGroupId} onValueChange={(v) => setFGroupId(v ?? "")}>
                        <SelectTrigger className="w-52">
                          <SelectValue placeholder="Keine (Bibliothek)" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="">Keine (Bibliothek)</SelectItem>
                          {groups.map((g) => (
                            <SelectItem key={g.id} value={g.id}>
                              {g.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  )}
                </div>

                <div className="flex flex-col gap-2">
                  <Label>Übungen</Label>
                  <div className="flex flex-col gap-2">
                    {fItems.map((item, index) => (
                      <div key={index} className="flex flex-col gap-2 rounded-md border bg-muted/30 p-2.5">
                        <div className="flex items-center gap-2">
                          <span className="text-xs text-muted-foreground">{index + 1}.</span>
                          <Input
                            className="flex-1"
                            placeholder="Titel der Übung"
                            value={item.title}
                            onChange={(e) => setItem(index, { title: e.target.value })}
                            maxLength={200}
                          />
                          {fItems.length > 1 && (
                            <Button
                              type="button"
                              size="icon"
                              variant="ghost"
                              className="size-8 shrink-0"
                              onClick={() => setFItems((prev) => prev.filter((_, i) => i !== index))}
                              aria-label="Übung entfernen"
                            >
                              <Trash2 className="size-4 text-muted-foreground" />
                            </Button>
                          )}
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Input
                            className="flex-1"
                            placeholder="Fokus (z.B. Leinenführigkeit)"
                            value={item.focus}
                            onChange={(e) => setItem(index, { focus: e.target.value })}
                            maxLength={80}
                          />
                          <Input
                            type="number"
                            min={1}
                            max={180}
                            className="w-24"
                            placeholder="Min"
                            value={item.durationMinutes}
                            onChange={(e) => setItem(index, { durationMinutes: e.target.value })}
                          />
                        </div>
                        <textarea
                          className={textareaClass}
                          rows={2}
                          placeholder="Beschreibung / Ablauf (optional)"
                          value={item.description}
                          onChange={(e) => setItem(index, { description: e.target.value })}
                          maxLength={2000}
                        />
                      </div>
                    ))}
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    className="self-start"
                    onClick={() => setFItems((prev) => [...prev, emptyItem()])}
                  >
                    <Plus className="size-3.5" />
                    Übung
                  </Button>
                </div>

                <div className="flex gap-2">
                  <Button type="button" disabled={submitting} onClick={submitForm}>
                    {submitting ? "Speichert…" : editingId ? "Speichern" : "Anlegen"}
                  </Button>
                  <Button type="button" variant="ghost" onClick={() => setFormOpen(false)}>
                    Abbrechen
                  </Button>
                </div>
              </CardContent>
            </Card>
          ) : (
            <Button className="self-start" onClick={openCreate}>
              <Plus className="size-4" />
              Neue Einheit
            </Button>
          )}

          <section className="flex flex-col gap-3">
            <div>
              <h2 className="text-lg font-semibold">Meine Einheiten</h2>
              <p className="text-sm text-muted-foreground">
                Selbst erstellte und aus Vorlagen übernommene Einheiten – frei anpassbar.
              </p>
            </div>
            {library.mine.length === 0 ? (
              <Card>
                <CardContent className="py-8 text-center text-sm text-muted-foreground">
                  Noch keine eigenen Einheiten. Lege eine an oder übernimm eine Vorlage.
                </CardContent>
              </Card>
            ) : (
              library.mine.map(unitCard)
            )}
          </section>

          <section className="flex flex-col gap-3">
            <div>
              <h2 className="text-lg font-semibold">Vorlagen</h2>
              <p className="text-sm text-muted-foreground">
                Fachlich vorbereitete, komplette Einheiten – für jede Trainer:in sichtbar.
              </p>
            </div>
            {categoryOrder.map((cat) => {
              const units = library.templates.filter((u) => u.category === cat);
              if (units.length === 0) return null;
              return (
                <div key={cat} className="flex flex-col gap-2">
                  <h3 className={cn("text-sm font-medium text-muted-foreground")}>{categoryLabel[cat]}</h3>
                  {units.map(unitCard)}
                </div>
              );
            })}
          </section>
        </>
      )}
    </div>
  );
}

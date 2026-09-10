"use client";

import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { DogCondition, Exercise, Goal, Sport, TrainingSession } from "@/lib/types";
import {
  emptyRow,
  letzteSportart,
  zeilenAusEinheit,
  type ExerciseRow,
} from "@/lib/trainingsvorlage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Clock, History, ListChecks, MapPin, MessageSquarePlus, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { enqueueRequest } from "@/lib/offline-queue";
import { difficultyLabel } from "@/lib/constants";
import { LocationTimeFields, type LocationValue } from "@/components/dogs/location-time-fields";
import { ConditionPicker } from "@/components/dogs/condition-picker";

import { useT } from "@/lib/i18n";
/**
 * Sentinel in den Auswahllisten: führt zur Freitext-Eingabe statt zu einer
 * Katalog-Übung. Bewusst dort, wo man sucht, wenn die eigene Übung fehlt -
 * vorher lag der Umschalter auf einem Stift-Symbol ohne Beschriftung, das am
 * Telefon kaum jemand fand und das mit "Bearbeiten" verwechselt wurde.
 */
const FREE_TEXT_OPTION = "__freitext__";

/**
 * Übliche Trainingsdauern zum Antippen. Getippt wurde die Zahl bisher auf der
 * Ziffern-Tastatur - für einen Wert, der fast immer einer von diesen vieren
 * ist. Das Zahlenfeld bleibt daneben stehen, für alles andere.
 */
const DAUER_VORSCHLAEGE = [30, 45, 60, 90];

/**
 * Formular "Neues Training" für das Trainingstagebuch. Hält seinen gesamten
 * Formular-State selbst (Zeilen, Übungs-Lookup pro Sportart, Submit-Status) -
 * die Hundeseite orchestriert nur noch (siehe TODO.md Roadmap 5b, gleiches
 * Muster wie GoalCreateForm nach dem goals-section-Refactor).
 *
 * onSaved(offline): offline=true, wenn der Eintrag nur in die
 * IndexedDB-Warteschlange geschrieben wurde - der Aufrufer soll dann NICHT
 * neu vom Server laden (der Eintrag ist dort noch nicht vorhanden).
 */
export function TrainingForm({
  dogId,
  sports,
  goals,
  letzteEinheit,
  onSaved,
}: {
  dogId: string;
  sports: Sport[];
  goals: Goal[] | null;
  /** Zuletzt gespeicherte Einheit dieses Hundes - Vorlage für die nächste. */
  letzteEinheit: TrainingSession | null;
  onSaved: (offline: boolean) => Promise<void>;
}) {
  const t = useT();
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [duration, setDuration] = useState(30);
  const [condition, setCondition] = useState<DogCondition | null>(null);
  const [notes, setNotes] = useState("");
  // Ort und Uhrzeit gleich beim Erfassen - der Server nimmt beides seit jeher
  // entgegen und ermittelt daraus das Wetter; nur das Formular bot es bisher
  // nicht an, man musste den Eintrag erst speichern und dann im Tagebuch
  // nachtragen.
  const [showContext, setShowContext] = useState(false);
  const [startTime, setStartTime] = useState("");
  const [location, setLocation] = useState<LocationValue>({
    latitude: null,
    longitude: null,
    locationName: "",
  });
  // Führt der Hund nur eine Sportart, ist sie nicht der Rede wert - dann
  // steht sie gleich drin und die Übungsliste ist sofort bedienbar.
  const [rows, setRows] = useState<ExerciseRow[]>(() => [
    emptyRow(sports.length === 1 ? sports[0].id : ""),
  ]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Selten Gebrauchtes öffnet sich erst auf Wunsch: Kommentar und
  // Bewertungskriterien je Übungszeile (Zeilenindex) und die Notiz zum ganzen
  // Training. Aufgeklappt machten sie das Formular so lang, dass "Training
  // speichern" 1,3 Bildschirme unter dem Formularanfang lag.
  const [offeneDetails, setOffeneDetails] = useState<Set<number>>(new Set());
  const [zeigeNotiz, setZeigeNotiz] = useState(false);
  const [exercisesBySport, setExercisesBySport] = useState<Record<string, Exercise[]>>({});

  // Übungen ALLER Sportarten des Hundes einmal laden. Das sind ein bis drei
  // Abrufe und erspart zweierlei: die Übungsliste steht sofort bereit statt
  // erst nach dem Wählen der Sportart, und "Wie beim letzten Mal" kann jede
  // Übung ihrer Sportart zuordnen - die Historie führt nur die Übung, nicht
  // die Sportart (siehe TrainingExercise).
  const sportSchluessel = sports.map((s) => s.id).join(",");
  useEffect(() => {
    let abgebrochen = false;
    // Nebeneinander und jede Liste für sich übernehmen, statt nacheinander zu
    // laden und erst am Ende alles gemeinsam: bei drei Sportarten stand die
    // Zuordnung sonst erst nach drei aufeinanderfolgenden Abrufen bereit -
    // und genau in dieser Zeitspanne war "Wie beim letzten Mal" wirkungslos.
    Promise.all(
      sportSchluessel
        .split(",")
        .filter(Boolean)
        .map(async (sportId) => {
          try {
            const uebungen = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
            if (abgebrochen) return;
            // Bereits Geladenes gewinnt - der Abruf soll nichts überschreiben,
            // was inzwischen durch eine Auswahl hereinkam.
            setExercisesBySport((prev) => (prev[sportId] ? prev : { ...prev, [sportId]: uebungen }));
          } catch {
            // Offline oder Serverfehler: die Liste lädt dann wie bisher beim
            // Antippen der Sportart nach. Kein Grund, das Formular zu blockieren.
          }
        }),
    );
    return () => {
      abgebrochen = true;
    };
    // Am Schlüssel statt am Array: sports wird auf der Hundeseite bei jedem
    // Rendern neu gebildet, ein Abhängen daran liefe endlos. Der Schlüssel
    // ändert sich genau dann, wenn sich die Sportarten wirklich ändern.
  }, [sportSchluessel]);

  /** Übung -> Sportart, aus allem, was geladen ist. */
  const sportVonUebung = useMemo(() => {
    const karte: Record<string, string> = {};
    for (const [sportId, uebungen] of Object.entries(exercisesBySport)) {
      for (const uebung of uebungen) karte[uebung.id] = sportId;
    }
    return karte;
  }, [exercisesBySport]);

  /**
   * Die Zeilen, wie sie angezeigt werden - mit nachgetragener Sportart.
   *
   * "Wie beim letzten Mal" lässt sich antippen, bevor die Übungslisten da
   * sind; auf dem Hundeplatz können das einige Sekunden sein. Die Zeilen
   * bekamen dann eine leere Sportart, ihre Übungsliste blieb leer - und weil
   * in der Zeile trotzdem eine Übungs-Id steht, zeigte das Übungs-Feld diese
   * Id an statt des Namens. Genau das war im Tagebuch als "Nummer statt
   * Übungsname" zu sehen.
   *
   * Abgeleitet und nicht in den Zustand geschrieben: die Zuordnung ist keine
   * Eingabe des Nutzers, sondern ergibt sich aus den geladenen Listen. Sobald
   * sie da sind, stimmt die Anzeige von selbst.
   *
   * Nur Zeilen ohne eigene Sportart: eine von Hand gewählte darf das nicht
   * überschreiben.
   */
  const zeilen = useMemo(
    () =>
      rows.map((row) =>
        row.sportId || !row.exerciseId ? row : { ...row, sportId: sportVonUebung[row.exerciseId] ?? "" },
      ),
    [rows, sportVonUebung],
  );

  const vorlageDatum = letzteEinheit
    ? new Date(letzteEinheit.date).toLocaleDateString("de-DE", { day: "numeric", month: "long" })
    : "";

  function uebernimmLetzteEinheit() {
    if (!letzteEinheit) return;
    setRows(zeilenAusEinheit(letzteEinheit, sportVonUebung));
    setOffeneDetails(new Set());
    setDuration(letzteEinheit.durationMinutes);
  }

  async function ensureExercisesLoaded(sportId: string) {
    if (exercisesBySport[sportId] || !sportId) return;
    const exercises = await api.get<Exercise[]>(`/api/sports/${sportId}/exercises`);
    setExercisesBySport((prev) => ({ ...prev, [sportId]: exercises }));
  }

  function updateRow(index: number, patch: Partial<ExerciseRow>) {
    setRows((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function switchToFreeText(index: number) {
    updateRow(index, { isFreeText: true, sportId: "", exerciseId: "", trainingPlanItemId: "", freeText: "" });
  }

  async function handleSportChange(index: number, sportId: string) {
    if (sportId === FREE_TEXT_OPTION) {
      switchToFreeText(index);
      return;
    }
    updateRow(index, { sportId, exerciseId: "", trainingPlanItemId: "" });
    await ensureExercisesLoaded(sportId);
  }

  function handleExerciseChange(index: number, exerciseId: string) {
    if (exerciseId === FREE_TEXT_OPTION) {
      switchToFreeText(index);
      return;
    }
    updateRow(index, { exerciseId, trainingPlanItemId: "" });
  }

  // Plan-Ziele (siehe GoalsSection), die zur gewählten Übung passen - nur
  // aus aktiven Zielen (Status 0) und ohne Pausenwochen, damit man einen
  // Tagebucheintrag optional einem Wochenziel zuordnen kann (siehe
  // TrainingExercise.TrainingPlanItemId). Bereits erfüllte Ziele bleiben
  // wählbar, falls man dieselbe Übung öfter als das Ziel trainieren möchte.
  function planItemOptionsFor(exerciseId: string) {
    if (!exerciseId) return [];
    return (goals ?? [])
      .filter((g) => g.status === 0)
      .flatMap((g) => g.trainingPlan?.items ?? [])
      .filter((item) => !item.isRestWeek && item.exerciseId === exerciseId);
  }

  function addRow() {
    setRows((prev) => [...prev, emptyRow(letzteSportart(zeilen))]);
  }

  function removeRow(index: number) {
    setRows((prev) => prev.filter((_, i) => i !== index));
    // Offene Details wandern mit ihren Zeilen: Indizes hinter der gelöschten
    // rücken eins auf.
    setOffeneDetails((prev) => new Set([...prev].filter((i) => i !== index).map((i) => (i > index ? i - 1 : i))));
  }

  // Ort und Zeit gehören zum einzelnen Training, nicht zum Formular - nach dem
  // Speichern also zurücksetzen, sonst trägt der nächste Eintrag stillschweigend
  // Ort und Uhrzeit des vorigen.
  function resetContext() {
    setStartTime("");
    setLocation({ latitude: null, longitude: null, locationName: "" });
    setShowContext(false);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const validRows = zeilen.filter((r) => (r.isFreeText ? r.freeText.trim() : r.exerciseId));
    if (validRows.length === 0) {
      toast.error(t("Mindestens eine Übung auswählen oder eintragen."));
      return;
    }

    const payload = {
      dogId,
      date,
      durationMinutes: duration,
      notes: notes || null,
      // Backend erwartet TimeOnly; leer = nicht gesetzt.
      startTime: startTime ? `${startTime}:00` : null,
      latitude: location.latitude,
      longitude: location.longitude,
      locationName: location.locationName.trim() || null,
      condition,
      exercises: validRows.map((r) => ({
        exerciseId: r.isFreeText ? null : r.exerciseId,
        freeTextLabel: r.isFreeText ? r.freeText.trim() : null,
        rating: r.rating,
        difficulty: 0,
        success: r.success,
        notes: r.notes || null,
        trainingPlanItemId: r.isFreeText ? null : r.trainingPlanItemId || null,
      })),
    };

    setIsSubmitting(true);
    try {
      await api.post<TrainingSession>("/api/trainings", payload);
      toast.success(t("Training gespeichert."));
      setRows([emptyRow(letzteSportart(zeilen))]);
      setOffeneDetails(new Set());
      setZeigeNotiz(false);
      setCondition(null);
      setNotes("");
      resetContext();
      await onSaved(false);
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
      } else {
        // Kein HTTP-Fehler vom Server, sondern ein Netzwerkfehler (offline) -
        // siehe PRODUCT_REQUIREMENTS.md "Offline": Training ohne Internet erfassen.
        await enqueueRequest({ path: "/api/trainings", method: "POST", body: payload, label: t("Training") });
        toast.success(t("Offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist."));
        setRows([emptyRow(letzteSportart(zeilen))]);
        setOffeneDetails(new Set());
        setZeigeNotiz(false);
        setNotes("");
        resetContext();
        await onSaved(true);
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  // Für die Speichern-Leiste: was beim Tippen tatsächlich gespeichert würde.
  const gueltigeZeilen = zeilen.filter((r) => (r.isFreeText ? r.freeText.trim() : r.exerciseId)).length;

  return (
    // overflow-visible: Card schneidet sonst ab (overflow-hidden) - und ein
    // Vorfahr mit overflow-hidden macht sich selbst zum Bezugsrahmen von
    // "sticky". Die Speichern-Leiste bliebe dann nie am Bildschirmrand stehen.
    <Card className="overflow-visible">
      <CardHeader>
        <CardTitle className="text-base">{t("Neues Training")}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          {/* Hundesport ist das Wiederholungsgeschäft: dieselben Übungen, Woche
              für Woche. Der Übungssatz der letzten Einheit ist deshalb die
              bessere Vorbelegung als ein leeres Formular - übrig bleibt das,
              was sich wirklich unterscheidet. Steht ganz oben, weil es der
              erste Handgriff sein soll und nicht der letzte. */}
          {letzteEinheit && (
            <div className="flex min-w-0 flex-col items-start gap-1.5 rounded-md border border-dashed p-3">
              <Button type="button" variant="outline" size="sm" onClick={uebernimmLetzteEinheit}>
                <History className="size-4" />
                {t("Wie beim letzten Mal")}
              </Button>
              <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
                {/* Ganzer Satz je Anzahl statt eingesetzter Zahl: "die 1 Übungen"
                    stand sonst da, sobald die letzte Einheit nur eine hatte. */}
                {letzteEinheit.exercises.length === 1
                  ? t("Übernimmt die Übung vom {datum}. Bewertungen und Notizen bleiben offen.", {
                      datum: vorlageDatum,
                    })
                  : t("Übernimmt die {anzahl} Übungen vom {datum}. Bewertungen und Notizen bleiben offen.", {
                      anzahl: letzteEinheit.exercises.length,
                      datum: vorlageDatum,
                    })}
              </p>
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="date">{t("Datum")}</Label>
              <Input id="date" type="date" required value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
            <div className="flex min-w-0 flex-col gap-2">
              <Label htmlFor="duration">{t("Dauer (Minuten)")}</Label>
              <div className="flex flex-wrap items-center gap-1.5">
                {DAUER_VORSCHLAEGE.map((minuten) => (
                  <button
                    key={minuten}
                    type="button"
                    onClick={() => setDuration(minuten)}
                    aria-pressed={duration === minuten}
                    className={`h-9 rounded-md border px-2.5 text-sm coarse:h-11 ${
                      duration === minuten
                        ? "border-accent bg-accent text-accent-foreground"
                        : "border-input text-muted-foreground"
                    }`}
                  >
                    {minuten}
                  </button>
                ))}
                <Input
                  id="duration"
                  type="number"
                  min={1}
                  required
                  aria-label={t("Dauer in Minuten")}
                  value={duration}
                  onChange={(e) => setDuration(Number(e.target.value))}
                  className="h-9 w-20 coarse:h-11"
                />
              </div>
            </div>
          </div>

          <div className="flex flex-col gap-3">
            <Label>{t("Übungen")}</Label>
            {zeilen.map((row, index) => {
              const exercises = exercisesBySport[row.sportId] ?? [];
              const selectedExercise = exercises.find((ex) => ex.id === row.exerciseId);
              const planItemOptions = planItemOptionsFor(row.exerciseId);
              return (
                <div key={index} className="flex flex-col gap-3 rounded-md border p-3">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                  {row.isFreeText ? (
                    <div className="flex flex-col gap-2 sm:w-72">
                      <Label htmlFor={`freetext-${index}`}>{t("Eigene Übung")}</Label>
                      <Input
                        id={`freetext-${index}`}
                        placeholder={t("z.B. Spaziergang mit Bällchenspiel")}
                        value={row.freeText}
                        onChange={(e) => updateRow(index, { freeText: e.target.value })}
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-7 self-start px-2 text-xs text-muted-foreground"
                        onClick={() => updateRow(index, { isFreeText: false, freeText: "" })}
                      >
                        <ListChecks className="size-3.5" />
{t("Doch eine Übung aus dem Katalog")}
                      </Button>
                    </div>
                  ) : (
                    <>
                  <div className="flex flex-col gap-2 sm:w-48">
                    <Label>{t("Sportart")}</Label>
                    <Select
                      value={row.sportId}
                      onValueChange={(value) => handleSportChange(index, value ?? "")}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder={t("Auswählen…")} />
                      </SelectTrigger>
                      <SelectContent>
                        {sports.map((s) => (
                          <SelectItem key={s.id} value={s.id}>
                            {s.name}
                          </SelectItem>
                        ))}
                        {/* Auch hier, nicht nur unter Übung: wer einen
                            Spaziergang einträgt, findet schon bei der
                            Sportart nichts Passendes. */}
                        <SelectItem value={FREE_TEXT_OPTION}>{t("Eigene Übung eintragen…")}</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex flex-col gap-2 sm:w-56">
                    <Label>{t("Übung")}</Label>
                    <Select
                      value={row.exerciseId}
                      disabled={!row.sportId}
                      onValueChange={(value) => handleExerciseChange(index, value ?? "")}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder={t("Auswählen…")} />
                      </SelectTrigger>
                      <SelectContent>
                        {exercises.map((ex) => (
                          <SelectItem key={ex.id} value={ex.id}>
                            {ex.name} ({difficultyLabel[ex.difficulty]})
                          </SelectItem>
                        ))}
                        {/* Der Moment, in dem man merkt, dass die eigene Übung
                            im Katalog fehlt - genau hier gehört der Ausweg hin. */}
                        <SelectItem value={FREE_TEXT_OPTION}>{t("Nicht dabei? Eigene Übung eintragen…")}</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  {planItemOptions.length > 0 && (
                    <div className="flex flex-col gap-2 sm:w-48">
                      <Label>{t("Plan-Ziel (optional)")}</Label>
                      <Select
                        value={row.trainingPlanItemId}
                        onValueChange={(value) => updateRow(index, { trainingPlanItemId: value ?? "" })}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={t("Kein Plan-Ziel")} />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="">{t("Kein Plan-Ziel")}</SelectItem>
                          {planItemOptions.map((item) => (
                            <SelectItem key={item.id} value={item.id}>
                              KW {item.weekNumber} ({item.completedCount}/{item.repetitionsTarget}x)
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  )}
                    </>
                  )}
                  <div className="flex flex-col gap-2">
                    <Label>{t("Bewertung")}</Label>
                    <div className="flex gap-1" role="group" aria-label={t("Bewertung, 1 bis 5")}>
                      {[1, 2, 3, 4, 5].map((value) => (
                        <button
                          key={value}
                          type="button"
                          onClick={() => updateRow(index, { rating: value })}
                          aria-label={`${value} von 5`}
                          aria-pressed={row.rating === value}
                          className={`flex size-8 items-center justify-center rounded-md border text-sm coarse:size-11 ${
                            row.rating >= value
                              ? "border-accent bg-accent text-accent-foreground"
                              : "border-input text-muted-foreground"
                          }`}
                        >
                          {value}
                        </button>
                      ))}
                    </div>
                  </div>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={row.success}
                      onChange={(e) => updateRow(index, { success: e.target.checked })}
                    />
                    Erfolgreich
                  </label>
                </div>
                {/* Kommentar und Bewertungskriterien eine Ebene tiefer: selten
                    gebraucht, aufgeklappt aber jede Zeile doppelt so hoch.
                    Das Plan-Ziel oben bleibt bewusst sichtbar - es erscheint
                    nur, wenn die Übung im laufenden Plan steht, und zählt dort
                    den Fortschritt. */}
                {offeneDetails.has(index) && (
                  <div className="flex flex-col gap-2">
                    {/* Kommentar zur einzelnen Übung. Der Wert wurde schon
                        immer mitgeschickt, es gab nur nie ein Feld dafür. */}
                    <div className="flex flex-col gap-1.5">
                      <Label htmlFor={`row-notes-${index}`} className="text-xs text-muted-foreground">
                        {t("Kommentar zur Übung (optional)")}
                      </Label>
                      <Input
                        id={`row-notes-${index}`}
                        placeholder="z.B. Ablenkung durch Jogger, zweiter Versuch sauber"
                        value={row.notes}
                        onChange={(e) => updateRow(index, { notes: e.target.value })}
                      />
                    </div>
                    {selectedExercise?.scoringCriteria && (
                      <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
                        <strong className="text-foreground">Bewertungskriterien:</strong>{" "}
                        {selectedExercise.scoringCriteria}
                      </p>
                    )}
                  </div>
                )}
                {/* Aufklappen und Entfernen teilen sich eine Zeile - auf dem
                    Handy stand der Mülleimer sonst allein in einer eigenen. */}
                <div className="flex items-center justify-between gap-2">
                  {offeneDetails.has(index) ? (
                    <span />
                  ) : (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="h-7 px-2 text-xs text-muted-foreground"
                      onClick={() => setOffeneDetails((prev) => new Set(prev).add(index))}
                    >
                      <MessageSquarePlus className="size-3.5" />
                      {selectedExercise?.scoringCriteria ? t("Kommentar & Bewertungskriterien") : t("Kommentar")}
                    </Button>
                  )}
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t("Entfernen")}
                    onClick={() => removeRow(index)}
                    disabled={zeilen.length === 1}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                </div>
                </div>
              );
            })}
            <Button type="button" variant="outline" size="sm" className="self-start" onClick={addRow}>
              <Plus className="size-4" />
{t("Übung hinzufügen")}
            </Button>
          </div>

          <div className="flex flex-col gap-2">
            <Label>Verfassung (optional)</Label>
            <ConditionPicker value={condition} onChange={setCondition} disabled={isSubmitting} />
          </div>

          <div className="flex flex-col gap-3">
            {showContext ? (
              <div className="flex min-w-0 flex-col gap-3 rounded-md border p-3">
                <LocationTimeFields
                  idPrefix="new-training"
                  time={startTime}
                  onTimeChange={setStartTime}
                  location={location}
                  onLocationChange={setLocation}
                />
                <p className="text-xs text-muted-foreground">
{t("Mit Ort und Uhrzeit wird das Wetter automatisch ermittelt.")}
                </p>
                <Button type="button" size="sm" variant="ghost" className="self-start" onClick={resetContext}>
{t("Ort & Zeit wieder entfernen")}
                </Button>
              </div>
            ) : (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="self-start"
                onClick={() => setShowContext(true)}
              >
                <MapPin className="size-4" />
{t("Ort & Uhrzeit angeben")}
              </Button>
            )}
            {!showContext && (startTime || location.locationName) && (
              <p className="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
                {startTime && (
                  <span className="flex items-center gap-1">
                    <Clock className="size-3" />
                    {startTime}
                  </span>
                )}
                {location.locationName && (
                  <span className="flex items-center gap-1 [overflow-wrap:anywhere]">
                    <MapPin className="size-3" />
                    {location.locationName}
                  </span>
                )}
              </p>
            )}
          </div>

          {zeigeNotiz || notes ? (
            <div className="flex flex-col gap-2">
              <Label htmlFor="notes">{t("Notizen zum ganzen Training")}</Label>
              <Input id="notes" value={notes} onChange={(e) => setNotes(e.target.value)} autoFocus={zeigeNotiz && !notes} />
            </div>
          ) : (
            <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => setZeigeNotiz(true)}>
              <MessageSquarePlus className="size-4" />
              {t("Notiz zum Training")}
            </Button>
          )}

          {/* Speichern-Leiste: bleibt am unteren Bildschirmrand stehen,
              solange das Formular im Bild ist - im Daumenbereich statt am
              Ende einer langen Seite. Auf dem Telefon sitzt sie über der
              unteren Navigation (4,375rem hoch), ab md gibt es die nicht.
              Die Zeile links bestätigt, was gespeichert wird. */}
          <div className="sticky bottom-[calc(4.375rem+env(safe-area-inset-bottom))] z-30 -mx-4 -mb-4 flex items-center justify-between gap-3 rounded-b-xl border-t bg-card/95 px-4 py-3 backdrop-blur md:bottom-0">
            <span className="min-w-0 text-sm text-muted-foreground">
              {gueltigeZeilen === 1
                ? t("1 Übung · {minuten} Min.", { minuten: duration })
                : t("{anzahl} Übungen · {minuten} Min.", { anzahl: gueltigeZeilen, minuten: duration })}
            </span>
            <Button type="submit" disabled={isSubmitting} className="shrink-0 coarse:min-h-11">
              {isSubmitting ? t("Wird gespeichert…") : t("Training speichern")}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

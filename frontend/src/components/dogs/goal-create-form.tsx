"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, Regulation, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "sonner";
import { findeStartPo, leseStartPo, loescheStartPo } from "@/lib/start-po";

import { useT } from "@/lib/i18n";
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
  const t = useT();
  const [sportId, setSportId] = useState("");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [regulationId, setRegulationId] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [notes, setNotes] = useState("");
  const [isCustom, setIsCustom] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Hat die Person selbst gewählt, darf die Vorauswahl vom Start über die
  // Prüfungsordnungs-Seite nicht mehr dazwischenfunken - auch nicht, wenn ihre
  // Antwort erst danach eintrifft.
  const selbstGewaehlt = useRef(false);
  const vorausgewaehlt = useRef(false);

  // Sportart aus dem Katalog, die zur Vorauswahl gehört, aber beim Hund nicht
  // angeboten wird (etwa weil im Erststart schon eine andere gewählt wurde).
  // Sie wird nur in der Auswahl mit angezeigt - aktiviert wird dadurch nichts.
  const [zusatzSport, setZusatzSport] = useState<Sport | null>(null);

  useEffect(() => {
    // Wer über "Kostenlos starten" auf einer Prüfungsordnungs-Seite kam, hat
    // dort ein Kürzel hinterlassen (lib/start-po.ts). Passt es zu einer
    // Prüfung, sind Sportart und Prüfung schon gewählt - mehr nicht: Es wird
    // nichts angelegt oder aktiviert, die Person legt das Ziel selbst an.
    const kuerzel = leseStartPo();
    if (!kuerzel || vorausgewaehlt.current) return;

    let abgebrochen = false;
    (async () => {
      try {
        // Der Abgleich läuft gegen den ganzen Katalog, nicht gegen die
        // Sportarten des Hundes: Die Prüfung soll auch dann gefunden werden,
        // wenn ihre Sportart hier (noch) nicht angeboten wird. Nur die
        // globalen zählen - der öffentliche Katalog, der die Kürzel vergibt,
        // kennt keine vereinseigenen.
        const alle = await api.get<Sport[]>("/api/sports");
        const global = alle.filter((s) => s.clubId === null);
        // Liste leer: Es gibt nichts zu vergleichen, also auch nichts zu
        // verwerfen.
        if (global.length === 0) return;
        const katalog = await Promise.all(
          global.map(async (sport) => ({
            sport,
            regulations: await api.get<Regulation[]>(`/api/sports/${sport.id}/regulations`),
          })),
        );
        if (abgebrochen) return;
        const treffer = findeStartPo(kuerzel, katalog);
        // Ein Kürzel, das zu nichts passt (Prüfung umbenannt, Link von Hand
        // geändert), wird nicht ewig mitgeschleppt.
        if (!treffer) {
          loescheStartPo();
          return;
        }
        if (selbstGewaehlt.current) return;
        vorausgewaehlt.current = true;
        const gefunden = katalog.find((k) => k.sport.id === treffer.sportId);
        setZusatzSport(gefunden?.sport ?? null);
        setSportId(treffer.sportId);
        setRegulations(gefunden?.regulations ?? []);
        setRegulationId(treffer.regulationId);
      } catch {
        // Kein Netz: Das Kürzel bleibt, beim nächsten Öffnen klappt es. Die
        // Vorauswahl ist eine Hilfe, keine Voraussetzung.
      }
    })();
    return () => {
      abgebrochen = true;
    };
    // Bewusst nur beim Öffnen: Die Elternseite reicht `sports` bei jedem
    // Rendern neu durch, ein Neustart würde die Abrufe ständig abbrechen.
  }, []);

  const sportOptionen =
    zusatzSport && !sports.some((s) => s.id === zusatzSport.id) ? [...sports, zusatzSport] : sports;

  async function handleSportChange(value: string) {
    selbstGewaehlt.current = true;
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
          ? t("Individueller Plan angelegt - füge jetzt Wochenübungen hinzu.")
          : t("Ziel angelegt - Trainingsplan wurde generiert."),
      );
      // Das Ziel steht - die Vorauswahl hat ihren Zweck erfüllt. Gleich wie
      // auch immer gewählt wurde: Ein Ziel reicht, es soll nicht beim
      // nächsten Hund wieder auftauchen.
      loescheStartPo();
      await onCreated();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Ziel konnte nicht angelegt werden."));
    } finally {
      setIsSubmitting(false);
    }
  }

  const selectedRegulation = regulations.find((r) => r.id === regulationId);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("Neues Ziel")}</CardTitle>
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
            <span>{t("Individueller Plan (ohne Prüfungsziel) – leere Wochen, Übungen manuell festlegen")}</span>
          </label>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label>{t("Sportart")}</Label>
              <Select required value={sportId} onValueChange={(value) => handleSportChange(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder={t("Auswählen…")} />
                </SelectTrigger>
                <SelectContent>
                  {sportOptionen.map((s) => (
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
              <Label>{t("Prüfung")}</Label>
              <Select value={regulationId} onValueChange={(value) => setRegulationId(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder={t("Allgemein (alle Übungen der Sportart)")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="">{t("Allgemein (alle Übungen der Sportart)")}</SelectItem>
                  {regulations.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
{t("Legt fest, aus welcher Pflichtübungsliste der Plan generiert wird - mehrere Prüfungsordnungen derselben Sportart (z.B. Fährte A/B/C) haben unterschiedliche Anforderungen.")}
              </p>
              {selectedRegulation?.description && (
                <div className="rounded-md bg-primary/5 px-3 py-2.5">
                  <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-primary-text">Rahmenbedingungen</p>
                  <p className="whitespace-pre-line text-sm leading-relaxed">{selectedRegulation.description}</p>
                </div>
              )}
            </div>
          )}
          <div className="flex flex-col gap-2">
            <Label htmlFor="goalNotes">{t("Notizen")}</Label>
            <Input id="goalNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <Button type="submit" className="self-start" disabled={isSubmitting}>
            {isSubmitting
              ? isCustom
                ? t("Wird angelegt…")
                : t("Wird generiert…")
              : isCustom
                ? t("Individuellen Plan anlegen")
                : t("Ziel anlegen & Plan generieren")}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

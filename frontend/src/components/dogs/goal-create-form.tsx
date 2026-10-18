"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError } from "@/lib/api";
import type { Goal, NextStage, Regulation, Sport } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "sonner";
import { findeStartPo, leseStartPo, loescheStartPo } from "@/lib/start-po";
import { ordnungenImLand, ordnungenPfad, SPORTARTEN_ALLE } from "@/lib/katalog-land";
import { usePreferences } from "@/lib/preferences-context";
import { VORGABE_LAND } from "@/lib/i18n/laender";

import { useSprache, useT } from "@/lib/i18n";
import { katalogName, katalogText } from "@/lib/katalog-name";
/**
 * Formular zum Anlegen eines neuen Ziels (mit oder ohne Prüfungsbezug).
 * Eigenständig mit eigenem State - die Ziel-Anlage ist unabhängig von der
 * Ziel-/Plan-Darstellung in GoalsSection. onCreated wird nach erfolgreichem
 * Anlegen gerufen (schließt in der Regel das Formular und lädt die Ziele neu).
 *
 * `vorauswahl`: Sportart und Prüfung stehen schon fest (Folgeziel nach einer
 * bestandenen Prüfung). Gilt nur beim Öffnen - wer das Formular mit anderer
 * Vorauswahl öffnen will, gibt ihm einen neuen key.
 */
export function GoalCreateForm({
  dogId,
  sports,
  vorauswahl = null,
  onCreated,
}: {
  dogId: string;
  sports: Sport[];
  vorauswahl?: NextStage | null;
  onCreated: () => Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();
  const { preferences } = usePreferences();
  const land = preferences.country ?? VORGABE_LAND;
  const [sportId, setSportId] = useState(vorauswahl?.sportId ?? "");
  const [regulations, setRegulations] = useState<Regulation[]>([]);
  const [regulationId, setRegulationId] = useState(vorauswahl?.regulationId ?? "");
  const [targetDate, setTargetDate] = useState("");
  const [notes, setNotes] = useState("");
  const [isCustom, setIsCustom] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Hat die Person selbst gewählt, darf die Vorauswahl vom Start über die
  // Prüfungsordnungs-Seite nicht mehr dazwischenfunken - auch nicht, wenn ihre
  // Antwort erst danach eintrifft.
  const selbstGewaehlt = useRef(false);
  // Mit Vorauswahl über Props hat der Merkzettel der Prüfungsordnungs-Seite
  // nichts mehr zu sagen.
  const vorausgewaehlt = useRef(vorauswahl !== null);

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
        // Sportarten des Hundes und nicht gegen die des Landes: Die Prüfung
        // soll auch dann gefunden werden, wenn ihre Sportart hier (noch) nicht
        // angeboten wird - etwa weil jemand aus Österreich noch das
        // Standardland trägt. Nur die globalen zählen - der öffentliche
        // Katalog, der die Kürzel vergibt, kennt keine vereinseigenen.
        const alle = await api.get<Sport[]>(SPORTARTEN_ALLE);
        const global = alle.filter((s) => s.clubId === null);
        // Liste leer: Es gibt nichts zu vergleichen, also auch nichts zu
        // verwerfen.
        if (global.length === 0) return;
        const katalog = await Promise.all(
          global.map(async (sport) => ({
            sport,
            // Ohne Land: Die Kürzel entstehen aus allen Ordnungen, auch denen
            // anderer Länder (siehe vergebeKuerzel).
            regulations: await api.get<Regulation[]>(ordnungenPfad(sport.id, null)),
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
        // Angezeigt werden die Ordnungen des Landes - die angesteuerte bleibt.
        setRegulations(ordnungenImLand(gefunden?.regulations ?? [], land, treffer.regulationId));
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
    // Gilt ebenso für das Land.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    // Die Prüfungen der vorgewählten Sportart nachladen, damit die Auswahl
    // die vorgewählte Prüfung auch zeigt - und die Sportart selbst, falls sie
    // beim Hund nicht angeboten wird (wie bei der Vorauswahl vom Start).
    if (!vorauswahl) return;
    let abgebrochen = false;
    (async () => {
      try {
        const [pruefungen, alle] = await Promise.all([
          api.get<Regulation[]>(ordnungenPfad(vorauswahl.sportId, null)),
          sports.some((s) => s.id === vorauswahl.sportId) ? Promise.resolve(null) : api.get<Sport[]>(SPORTARTEN_ALLE),
        ]);
        if (abgebrochen) return;
        // Die vorgewählte Prüfung bleibt auch dann in der Liste, wenn sie nicht
        // zum Land gehört.
        setRegulations(ordnungenImLand(pruefungen, land, vorauswahl.regulationId));
        if (alle) setZusatzSport(alle.find((s) => s.id === vorauswahl.sportId) ?? null);
      } catch {
        // Ohne Netz bleibt die Vorauswahl stehen, nur die Auswahlliste fehlt -
        // das Ziel lässt sich trotzdem anlegen.
      }
    })();
    return () => {
      abgebrochen = true;
    };
    // Nur beim Öffnen, wie die Vorauswahl selbst.
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
      const data = await api.get<Regulation[]>(ordnungenPfad(value, land));
      setRegulations(data);
      // Nur eine Prüfung: Es gibt nichts zu wählen.
      if (data.length === 1) setRegulationId(data[0].id);
    } catch {
      // Ohne Liste keine Auswahl - das Backend verlangt die Prüfung trotzdem,
      // wenn die Sportart welche hat, und meldet es beim Anlegen.
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
  // Hat die Sportart Prüfungen, gehört eine zum Ziel. Die frühere Wahl
  // "Allgemein (alle Übungen der Sportart)" ist weg: Bei der UPr zog sie die
  // Übungen aller drei Stufen heran, also auch "Steh", das die UPr 1 gar
  // nicht prüft. Wer ohne Prüfung trainieren will, nimmt den individuellen Plan.
  const pruefungFehlt = !isCustom && regulations.length > 0 && !regulationId;

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
                      {katalogName(s, sprache)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="targetDate">{t("Zieldatum")}</Label>
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
              <Select required value={regulationId} onValueChange={(value) => setRegulationId(value ?? "")}>
                <SelectTrigger>
                  <SelectValue placeholder={t("Prüfung wählen…")} />
                </SelectTrigger>
                <SelectContent>
                  {regulations.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {katalogName(r, sprache)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
{t("Der Plan enthält genau die Übungen dieser Prüfung. Ohne Prüfung trainieren: oben „Individueller Plan“ wählen.")}
              </p>
              {selectedRegulation && katalogText(selectedRegulation.description, selectedRegulation.descriptionEn, sprache) && (
                <div className="rounded-md bg-primary/5 px-3 py-2.5">
                  <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-primary-text">{t("Rahmenbedingungen")}</p>
                  <p className="whitespace-pre-line text-sm leading-relaxed [overflow-wrap:anywhere]">{katalogText(selectedRegulation.description, selectedRegulation.descriptionEn, sprache)}</p>
                </div>
              )}
            </div>
          )}
          <div className="flex flex-col gap-2">
            <Label htmlFor="goalNotes">{t("Notizen")}</Label>
            <Input id="goalNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <Button type="submit" className="self-start" disabled={isSubmitting || pruefungFehlt}>
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

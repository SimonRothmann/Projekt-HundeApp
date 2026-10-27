"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { CalendarClock, CircleCheck, Clock, ChevronDown, Pencil, Plus, Trash2, TriangleAlert } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import {
  GESUNDHEIT_ART,
  GESUNDHEIT_ZUSTAND,
  type Dog,
  type DogHealthRecord,
  type GesundheitsArt,
  type GesundheitsZustand,
} from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  ARTEN,
  ART_TEXT,
  ART_UEBERSCHRIFT,
  TITEL_VORSCHLAEGE,
  faelligNach,
  fristText,
  gliedere,
  naechstes,
  pruefeEintrag,
  schnellwahlen,
  standardTitel,
  terminText,
  uhrzeitFuerServer,
  uhrzeitKurz,
  zustand,
  type GesundheitsEingabe,
  type NaechstesZeile,
  type Ton,
} from "@/lib/gesundheit";
import { datumKurz, heuteIso } from "@/lib/pruefung";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { useSprache, useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/**
 * Der Bereich "Gesundheit" der Hundeseite: Impfungen, Entwurmung, Parasitenschutz und
 * Tierarzttermine des Hundes. Oben "Als Nächstes" (überfällig rot, bald fällig gelb, der nächste
 * Tierarzttermin), darunter die aktuellen Einträge je Art und - aufklappbar - der Verlauf.
 *
 * Nur für Besitzer:innen und Mitbesitzer:innen: Die Hundeseite zeigt den Reiter gar nicht erst,
 * und der Server liefert die Einträge niemandem sonst. Es ist ein Merkzettel und kein Nachweis -
 * maßgeblich ist, was der Tierarzt im Impfpass einträgt; Dogity rechnet keine Fristen aus.
 * Der Zustand steht immer in drei Formen: Farbe, Symbol und Wort.
 *
 * Geladen wird erst, wenn der Reiter zum ersten Mal offen ist - die Hundeseite selbst braucht
 * die Liste nicht.
 */
export function GesundheitTab({ dog, aktiv, onChanged }: { dog: Dog; aktiv: boolean; onChanged: () => Promise<void> }) {
  const t = useT();
  const sprache = useSprache();
  const [eintraege, setEintraege] = useState<DogHealthRecord[] | null>(null);
  const [ladefehler, setLadefehler] = useState(false);
  // null = kein Formular; "neu" = neuer Eintrag; sonst der Eintrag, der bearbeitet wird.
  const [formular, setFormular] = useState<"neu" | DogHealthRecord | null>(null);
  const [verlaufOffen, setVerlaufOffen] = useState(false);

  const laden = useCallback(async () => {
    try {
      setEintraege(await api.get<DogHealthRecord[]>(`/api/dogs/${dog.id}/gesundheit`));
      setLadefehler(false);
    } catch {
      setLadefehler(true);
    }
  }, [dog.id]);

  useEffect(() => {
    if (!aktiv) return;
    // Datenabruf beim Öffnen des Reiters (externe Quelle: REST API).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void laden();
  }, [aktiv, laden]);

  // Nach jeder Änderung: Liste und Hundeseite (Stand am Hund) neu laden.
  async function nachAenderung() {
    setFormular(null);
    await laden();
    await onChanged();
  }

  async function loeschen(eintrag: DogHealthRecord) {
    if (!window.confirm(t("Diesen Eintrag wirklich löschen?"))) return;
    try {
      await api.delete(`/api/dogs/${dog.id}/gesundheit/${eintrag.id}`);
      toast.success(t("Eintrag gelöscht."));
      await nachAenderung();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Das hat nicht geklappt."));
    }
  }

  const heute = heuteIso();
  const { aktuell, verlauf } = gliedere(eintraege ?? []);
  const zeilen = naechstes(t, eintraege ?? [], heute, sprache);

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex min-w-0 flex-col gap-1">
        <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
          {t("Maßgeblich ist, was der Tierarzt im Impfpass einträgt. Dogity rechnet keine Fristen aus: „Gültig bis“ bzw. „nächste fällig“ trägst du selbst ein.")}
        </p>
        <p className="text-xs text-muted-foreground [overflow-wrap:anywhere]">
          {t("Das sehen nur du und die Mitbesitzer:innen von {name} – deine Trainer:in nicht.", { name: dog.name })}
        </p>
      </div>

      {formular === null ? (
        <Button type="button" variant="outline" className="min-h-11 w-full" onClick={() => setFormular("neu")}>
          <Plus className="size-4" aria-hidden />
          {t("Eintrag hinzufügen")}
        </Button>
      ) : (
        // key: Beim Wechsel zwischen "neu" und einem Eintrag beginnt das Formular von vorn.
        <EintragFormular
          key={formular === "neu" ? "neu" : formular.id}
          dog={dog}
          eintrag={formular === "neu" ? null : formular}
          onGespeichert={nachAenderung}
          onAbbrechen={() => setFormular(null)}
        />
      )}

      {ladefehler && eintraege === null ? (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-muted-foreground">{t("Einträge konnten nicht geladen werden.")}</p>
          <Button type="button" size="sm" variant="outline" className="coarse:min-h-11" onClick={() => void laden()}>
            {t("Erneut versuchen")}
          </Button>
        </div>
      ) : eintraege === null ? (
        <p className="text-sm text-muted-foreground">{t("Lädt…")}</p>
      ) : eintraege.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("Noch keine Einträge. Trag zum Beispiel die Tollwut-Impfung ein.")}</p>
      ) : (
        <>
          <section className="flex min-w-0 flex-col gap-2" aria-label={t("Als Nächstes")}>
            <h3 className="text-sm font-medium">{t("Als Nächstes")}</h3>
            {zeilen.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("Nichts Dringendes in den nächsten 30 Tagen.")}</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {zeilen.map((zeile) => (
                  <NaechstesKarte key={zeile.id} zeile={zeile} />
                ))}
              </ul>
            )}
          </section>

          {aktuell.map((gruppe) => (
            <section key={gruppe.art} className="flex min-w-0 flex-col gap-2" aria-label={t(ART_UEBERSCHRIFT[gruppe.art])}>
              <h3 className="text-sm font-medium">{t(ART_UEBERSCHRIFT[gruppe.art])}</h3>
              <ul className="flex flex-col gap-2">
                {gruppe.eintraege.map((e) => (
                  <EintragKarte key={e.id} eintrag={e} heute={heute} onBearbeiten={() => setFormular(e)} onLoeschen={() => loeschen(e)} />
                ))}
              </ul>
            </section>
          ))}

          {verlauf.length > 0 && (
            <section className="flex min-w-0 flex-col gap-2">
              <button
                type="button"
                aria-expanded={verlaufOffen}
                onClick={() => setVerlaufOffen((offen) => !offen)}
                className="flex min-h-11 w-full items-center justify-between gap-2 rounded-lg border border-surface-border bg-surface px-3 text-left text-sm font-medium"
              >
                <span className="min-w-0 [overflow-wrap:anywhere]">{t("Verlauf ({n})", { n: verlauf.length })}</span>
                <ChevronDown className={cn("size-4 shrink-0 transition-transform", verlaufOffen && "rotate-180")} aria-hidden />
              </button>
              {verlaufOffen && (
                <ul className="flex flex-col gap-2">
                  {verlauf.map((e) => (
                    <EintragKarte key={e.id} eintrag={e} heute={heute} verlauf onBearbeiten={() => setFormular(e)} onLoeschen={() => loeschen(e)} />
                  ))}
                </ul>
              )}
            </section>
          )}
        </>
      )}
    </div>
  );
}

/** Farben je Ton: dazu immer Symbol und Wort, die Farbe allein sagt nichts. */
const TON_KLASSEN: Record<Ton, string> = {
  rot: "border-destructive/40 bg-destructive/10",
  gelb: "border-amber-500/40 bg-amber-500/10",
  neutral: "border-surface-border bg-surface",
};

function NaechstesKarte({ zeile }: { zeile: NaechstesZeile }) {
  const t = useT();
  const Zeichen = zeile.ton === "rot" ? TriangleAlert : zeile.ton === "gelb" ? Clock : CalendarClock;
  const wort =
    zeile.ton === "rot" ? t("Überfällig") : zeile.ton === "gelb" ? t("Bald fällig") : t("Tierarzttermin");
  return (
    <li className={cn("flex min-w-0 items-start gap-2 rounded-lg border px-3 py-2 text-sm", TON_KLASSEN[zeile.ton])}>
      <Zeichen
        className={cn(
          "mt-0.5 size-4 shrink-0",
          zeile.ton === "rot" && "text-destructive",
          zeile.ton === "gelb" && "text-amber-700 dark:text-amber-300",
        )}
        aria-hidden
      />
      <div className="flex min-w-0 flex-col">
        <p className="font-medium [overflow-wrap:anywhere]">
          {zeile.titel}
          <span className="font-normal text-muted-foreground"> · {wort}</span>
        </p>
        <p className="text-muted-foreground [overflow-wrap:anywhere]">{zeile.text}</p>
      </div>
    </li>
  );
}

function ZustandBadge({ wert }: { wert: GesundheitsZustand }) {
  const t = useT();
  if (wert === GESUNDHEIT_ZUSTAND.Ueberfaellig) {
    return (
      <Badge variant="destructive">
        <TriangleAlert aria-hidden />
        {t("Überfällig")}
      </Badge>
    );
  }
  if (wert === GESUNDHEIT_ZUSTAND.Bald) {
    return (
      <Badge variant="outline" className="border-amber-500/50 bg-amber-500/10 text-amber-800 dark:text-amber-300">
        <Clock aria-hidden />
        {t("Bald fällig")}
      </Badge>
    );
  }
  return (
    <Badge variant="secondary">
      <CircleCheck aria-hidden />
      {t("In Ordnung")}
    </Badge>
  );
}

function EintragKarte({
  eintrag,
  heute,
  verlauf = false,
  onBearbeiten,
  onLoeschen,
}: {
  eintrag: DogHealthRecord;
  heute: string;
  /** Im Verlauf steht kein Zustand: Die Frist eines ersetzten Eintrags gilt nicht mehr. */
  verlauf?: boolean;
  onBearbeiten: () => void;
  onLoeschen: () => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const istTermin = eintrag.kind === GESUNDHEIT_ART.Tierarzttermin;
  const mitFrist = !verlauf && !istTermin && eintrag.dueDate !== null;

  return (
    <li className="flex min-w-0 flex-col gap-1.5 rounded-lg border border-surface-border bg-surface p-3">
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        <span className="min-w-0 font-medium [overflow-wrap:anywhere]">{eintrag.title}</span>
        {mitFrist && <ZustandBadge wert={zustand(eintrag.dueDate, heute)} />}
        {verlauf && <Badge variant="outline">{t(ART_TEXT[eintrag.kind])}</Badge>}
      </div>
      {istTermin ? (
        <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{terminText(t, eintrag.date, eintrag.time, sprache)}</p>
      ) : (
        <>
          {eintrag.dueDate !== null && !verlauf && (
            <p className="text-sm [overflow-wrap:anywhere]">{fristText(t, eintrag.kind, eintrag.dueDate, heute, sprache)}</p>
          )}
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">
            {verlauf && eintrag.dueDate !== null
              ? t("Gegeben am {datum}, gültig bis {bis}", { datum: datumKurz(eintrag.date, sprache), bis: datumKurz(eintrag.dueDate, sprache) })
              : t("Gegeben am {datum}", { datum: datumKurz(eintrag.date, sprache) })}
          </p>
        </>
      )}
      {eintrag.note && <p className="text-sm [overflow-wrap:anywhere]">{eintrag.note}</p>}
      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" variant="ghost" className="coarse:min-h-11" onClick={onBearbeiten}>
          <Pencil className="size-3.5" aria-hidden />
          {t("Bearbeiten")}
        </Button>
        <Button type="button" size="sm" variant="ghost" className="text-destructive hover:text-destructive coarse:min-h-11" onClick={onLoeschen}>
          <Trash2 className="size-3.5" aria-hidden />
          {t("Löschen")}
        </Button>
      </div>
    </li>
  );
}

/**
 * Das Formular für einen neuen oder zu ändernden Eintrag: Art, Titel (mit Vorschlägen), Datum, bei einem
 * Tierarzttermin die Uhrzeit, sonst "gültig bis" bzw. "nächste fällig" mit Schnellwahl, und eine Notiz.
 * Die Schnellwahl füllt das Datum nur vor und lässt sich überschreiben.
 */
function EintragFormular({
  dog,
  eintrag,
  onGespeichert,
  onAbbrechen,
}: {
  dog: Dog;
  eintrag: DogHealthRecord | null;
  onGespeichert: () => Promise<void>;
  onAbbrechen: () => void;
}) {
  const t = useT();
  const kennung = eintrag?.id ?? "neu";
  const [eingabe, setEingabe] = useState<GesundheitsEingabe>(() => ({
    art: eintrag?.kind ?? GESUNDHEIT_ART.Impfung,
    titel: eintrag?.title ?? "",
    datum: eintrag?.date ?? heuteIso(),
    uhrzeit: uhrzeitKurz(eintrag?.time) ?? "",
    faellig: eintrag?.dueDate ?? "",
    notiz: eintrag?.note ?? "",
  }));
  const [versucht, setVersucht] = useState(false);
  const [speichert, setSpeichert] = useState(false);

  const fehler = pruefeEintrag(t, eingabe);
  const istTermin = eingabe.art === GESUNDHEIT_ART.Tierarzttermin;
  const wahlen = schnellwahlen(eingabe.art);

  function aendere(neu: Partial<GesundheitsEingabe>) {
    setEingabe((vorher) => ({ ...vorher, ...neu }));
  }

  function waehleArt(art: GesundheitsArt) {
    setEingabe((vorher) => {
      // Ein Vorschlag der alten Art (oder noch nichts) wird durch den der neuen ersetzt; Eigenes bleibt.
      const eigener = vorher.titel.trim() !== "" && !ARTEN.some((a) => TITEL_VORSCHLAEGE[a].some((v) => t(v) === vorher.titel));
      return {
        ...vorher,
        art,
        titel: eigener ? vorher.titel : t(standardTitel(art)),
        // Was die neue Art nicht kennt, fällt weg.
        uhrzeit: art === GESUNDHEIT_ART.Tierarzttermin ? vorher.uhrzeit : "",
        faellig: art === GESUNDHEIT_ART.Tierarzttermin ? "" : vorher.faellig,
      };
    });
  }

  async function speichern() {
    setVersucht(true);
    if (fehler !== null) return;
    setSpeichert(true);
    try {
      const nutzlast = {
        kind: eingabe.art,
        title: eingabe.titel.trim(),
        date: eingabe.datum,
        time: istTermin ? uhrzeitFuerServer(eingabe.uhrzeit) : null,
        dueDate: istTermin || eingabe.faellig === "" ? null : eingabe.faellig,
        note: eingabe.notiz.trim() === "" ? null : eingabe.notiz.trim(),
      };
      if (eintrag) await api.put(`/api/dogs/${dog.id}/gesundheit/${eintrag.id}`, nutzlast);
      else await api.post(`/api/dogs/${dog.id}/gesundheit`, nutzlast);
      toast.success(t("Eintrag gespeichert."));
      await onGespeichert();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Eintrag konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  const vorschlaege = TITEL_VORSCHLAEGE[eingabe.art];

  return (
    <div className="flex min-w-0 flex-col gap-4 rounded-md border border-primary/30 p-3">
      <p className="font-medium">{eintrag ? t("Eintrag bearbeiten") : t("Eintrag hinzufügen")}</p>

      <div className="flex min-w-0 flex-col gap-2">
        <Label>{t("Art")}</Label>
        <div className="grid grid-cols-2 gap-2" role="group" aria-label={t("Art")}>
          {ARTEN.map((art) => (
            <button
              key={art}
              type="button"
              aria-pressed={eingabe.art === art}
              onClick={() => waehleArt(art)}
              className={cn(
                "flex min-h-11 min-w-0 items-center justify-center rounded-md border px-2 text-sm font-medium transition-colors",
                eingabe.art === art ? "border-primary bg-primary/10 text-primary-text" : "border-input text-muted-foreground hover:bg-muted",
              )}
            >
              <span className="min-w-0 py-1 text-center leading-tight [overflow-wrap:anywhere]">{t(ART_TEXT[art])}</span>
            </button>
          ))}
        </div>
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`gesundheit-titel-${kennung}`}>{t("Titel")}</Label>
        <Input
          id={`gesundheit-titel-${kennung}`}
          value={eingabe.titel}
          maxLength={TEXTLAENGE.gesundheitTitel}
          onChange={(e) => aendere({ titel: e.target.value })}
          className="h-11 min-w-0"
        />
        {vorschlaege.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {vorschlaege.map((v) => (
              <button
                key={v}
                type="button"
                aria-pressed={eingabe.titel === t(v)}
                onClick={() => aendere({ titel: t(v) })}
                className={cn(
                  "min-h-11 min-w-0 rounded-full border px-3 text-sm transition-colors [overflow-wrap:anywhere]",
                  eingabe.titel === t(v) ? "border-primary bg-primary/10 text-primary-text" : "border-input text-muted-foreground hover:bg-muted",
                )}
              >
                {t(v)}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`gesundheit-datum-${kennung}`}>{istTermin ? t("Termin am") : t("Gegeben am")}</Label>
        <Input
          id={`gesundheit-datum-${kennung}`}
          type="date"
          required
          value={eingabe.datum}
          onChange={(e) => aendere({ datum: e.target.value })}
          className="h-11 min-w-0"
        />
      </div>

      {istTermin ? (
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor={`gesundheit-uhrzeit-${kennung}`}>{t("Uhrzeit (optional)")}</Label>
          <Input
            id={`gesundheit-uhrzeit-${kennung}`}
            type="time"
            value={eingabe.uhrzeit}
            onChange={(e) => aendere({ uhrzeit: e.target.value })}
            className="h-11 min-w-0"
          />
        </div>
      ) : (
        <div className="flex min-w-0 flex-col gap-2">
          <Label htmlFor={`gesundheit-faellig-${kennung}`}>
            {eingabe.art === GESUNDHEIT_ART.Impfung ? t("Gültig bis (optional)") : t("Nächste fällig (optional)")}
          </Label>
          <Input
            id={`gesundheit-faellig-${kennung}`}
            type="date"
            value={eingabe.faellig}
            min={eingabe.datum || undefined}
            onChange={(e) => aendere({ faellig: e.target.value })}
            className="h-11 min-w-0"
          />
          {wahlen.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {wahlen.map((wahl) => (
                <button
                  key={wahl.label}
                  type="button"
                  disabled={!/^\d{4}-\d{2}-\d{2}$/.test(eingabe.datum)}
                  onClick={() => aendere({ faellig: faelligNach(eingabe.datum, wahl) })}
                  className="min-h-11 min-w-0 rounded-full border border-input px-3 text-sm text-muted-foreground transition-colors hover:bg-muted disabled:opacity-50"
                >
                  {t(wahl.label)}
                </button>
              ))}
            </div>
          )}
          <p className="text-xs text-muted-foreground">
            {t("Trag das Datum ein, das der Tierarzt im Impfpass vermerkt hat. Die Schnellwahl füllt es nur vor.")}
          </p>
        </div>
      )}

      <div className="flex min-w-0 flex-col gap-2">
        <Label htmlFor={`gesundheit-notiz-${kennung}`}>{t("Notiz (optional)")}</Label>
        <Input
          id={`gesundheit-notiz-${kennung}`}
          value={eingabe.notiz}
          maxLength={TEXTLAENGE.gesundheitNotiz}
          onChange={(e) => aendere({ notiz: e.target.value })}
          className="h-11 min-w-0"
        />
        <p className="text-xs text-muted-foreground">{t("Die Notiz sehen nur du und die Mitbesitzer:innen.")}</p>
      </div>

      {/* Ein noch leeres Formular bekommt keinen Vorwurf: erst nach dem Versuch zu speichern. */}
      {versucht && fehler && <p className="text-sm text-destructive">{fehler}</p>}

      <div className="flex flex-wrap gap-2">
        <Button type="button" className="min-h-11 flex-1" disabled={speichert} onClick={speichern}>
          {speichert ? t("Speichert…") : t("Speichern")}
        </Button>
        <Button type="button" variant="ghost" className="min-h-11" onClick={onAbbrechen}>
          {t("Abbrechen")}
        </Button>
      </div>
    </div>
  );
}

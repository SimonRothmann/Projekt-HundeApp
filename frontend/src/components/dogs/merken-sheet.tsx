"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Plus, X } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError, istVoruebergehendUnerreichbar } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { Abschnittsname } from "@/components/dogs/abschnittsname";
import { EintragenSuche } from "@/components/dogs/eintragen-suche";
import { WOCHENTAGE, STUNDEN, stundeText, tagGewaehlt } from "@/lib/erinnerungen";
import { inhaltsSchluessel } from "@/lib/eintragen";
import { heuteIso } from "@/lib/kalendertag";
import { useSprache, useT } from "@/lib/i18n";
import { katalogName } from "@/lib/katalog-name";
import {
  baueMerkenNutzlast,
  istGueltigerText,
  istGueltigesZielDatum,
  naechstesTrainingText,
  rhythmusNutzlast,
  schalteTag,
  uebungsChips,
  vorgabeZielDatum,
} from "@/lib/merkzettel";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { useEintragenDaten, useUebungsKatalog, type EintragenVorab } from "@/lib/use-eintragen-daten";
import { useTastatur } from "@/lib/use-tastatur";
import { MERKZETTEL_ZIEL, TRAININGS_QUELLE } from "@/lib/types";
import type { Dog, Exercise, GroupTrainingSession, MerkzettelEintrag, MerkzettelZiel, NaechstesTraining } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * Das Merken-Fenster: "das machen wir nächstes Training" in Sekunden festhalten. Ein Textfeld (die Tastatur
 * diktiert - kein eigenes Mikrofon), optional eine Übung, ein Ziel, ein Knopf. Das Ziel "Nächstes Training"
 * steht vorgewählt und trägt den erkannten Termin in der Beschriftung.
 *
 * Gespeichert wird wie beim Training: ohne Netz geht der Eintrag mit einer Id vom Gerät in die
 * Offline-Warteschlange und entsteht beim Nachsenden genau einmal. Ändern und Löschen gehen nur online.
 *
 * Zwei Hüllen für denselben Inhalt: ein eigenes Fenster (Hundeseite, Startseite) und eine Ansicht im
 * Eintragen-Fenster ("Fürs nächste Mal merken").
 */
export function MerkenSheet({
  open,
  onOpenChange,
  dogId,
  hunde,
  vorab,
  eintrag,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Der Hund, für den das Fenster aufgeht. */
  dogId: string;
  /** Die Hunde, zwischen denen das Fenster wechseln lässt; mit nur dem einen gibt es keine Auswahl. */
  hunde: Dog[];
  /** Was die aufrufende Seite zu dem Hund schon weiß (Hundeseite). */
  vorab?: EintragenVorab | null;
  /** Ein vorhandener Eintrag, der geändert wird; null = neuer. */
  eintrag?: MerkzettelEintrag | null;
  /** offline = true: nur in der Warteschlange - die Seite soll nicht vom Server neu laden. */
  onSaved: (offline: boolean) => void | Promise<void>;
}) {
  const t = useT();
  // Ob schon etwas im Fenster steht, das beim Schließen verloren ginge (siehe EintragenSheet).
  const entwurfRef = useRef(false);
  const meldeEntwurf = useCallback((vorhanden: boolean) => {
    entwurfRef.current = vorhanden;
  }, []);
  const tastatur = useTastatur(open);

  return (
    <Sheet
      open={open}
      onOpenChange={(offen, details) => {
        if (!offen && entwurfRef.current && (details.reason === "outside-press" || details.reason === "escape-key")) return;
        onOpenChange(offen);
      }}
    >
      <SheetContent
        side="bottom"
        showCloseButton={false}
        className={cn("max-h-[92dvh] gap-0 overflow-hidden rounded-t-xl p-0", tastatur.hoehe !== null && "transition-none")}
        // Bei offener Tastatur (iPhone): über die Tastatur rücken und auf den sichtbaren Bereich schrumpfen.
        style={tastatur.hoehe !== null ? { bottom: tastatur.unten, maxHeight: tastatur.hoehe - 8 } : undefined}
      >
        <MerkenInhalt
          startHund={dogId}
          hunde={hunde}
          vorab={vorab ?? null}
          eintrag={eintrag ?? null}
          onEntwurf={meldeEntwurf}
          onClose={() => onOpenChange(false)}
          onSaved={onSaved}
        />
        <SheetDescription className="sr-only">{t("Etwas für das nächste Training merken.")}</SheetDescription>
      </SheetContent>
    </Sheet>
  );
}

/** Stabil, damit der Hook für die Gruppentermine nicht nachlädt: Das Fenster braucht sie nicht. */
const OHNE_TERMINE: GroupTrainingSession[] = [];

/** Merkt sich pro Hund, dass "Wann trainierst du meistens?" übersprungen wurde - nur ein Komfort, nie nötig. */
function uebersprungenSchluessel(dogId: string): string {
  return `dogity-rhythmus-uebersprungen-${dogId}`;
}

function leseUebersprungen(dogId: string): boolean {
  try {
    return window.localStorage.getItem(uebersprungenSchluessel(dogId)) === "1";
  } catch {
    return false;
  }
}

function merkeUebersprungen(dogId: string) {
  try {
    window.localStorage.setItem(uebersprungenSchluessel(dogId), "1");
  } catch {
    // Ohne Speicher fragt das Fenster beim nächsten Mal noch einmal - kein Schaden.
  }
}

type Ansicht = "haupt" | "suche";

/** Der Inhalt des Fensters; der Hülle gehört das Schließen (Fenster) bzw. das Zurück (Ansicht im Eintragen-Fenster). */
export function MerkenInhalt({
  startHund,
  hunde,
  vorab,
  eintrag,
  onEntwurf,
  onClose,
  onSaved,
}: {
  startHund: string;
  hunde: Dog[];
  vorab: EintragenVorab | null;
  eintrag: MerkzettelEintrag | null;
  /** Meldet der Hülle, ob schon etwas eingetippt ist (dann verwirft ein Tipp daneben das Fenster nicht). */
  onEntwurf?: (vorhanden: boolean) => void;
  onClose: () => void;
  onSaved: (offline: boolean) => void | Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();

  const [hundId, setHundId] = useState(startHund);
  const [text, setText] = useState(eintrag?.text ?? "");
  const [uebung, setUebung] = useState<{ id: string; name: string; nameEn: string | null } | null>(
    eintrag?.exerciseId ? { id: eintrag.exerciseId, name: eintrag.exerciseName ?? "", nameEn: eintrag.exerciseNameEn } : null,
  );
  // Der Name der gewählten Übung, wenn er den Text vorbelegt hat: Nur dann darf ein Wechsel ihn ersetzen.
  const [vorbelegt, setVorbelegt] = useState<string | null>(null);
  const [ziel, setZiel] = useState<MerkzettelZiel>(eintrag?.goal ?? MERKZETTEL_ZIEL.NaechstesTraining);
  const [datum, setDatum] = useState(() => eintrag?.goal === MERKZETTEL_ZIEL.Datum && eintrag.targetDate ? eintrag.targetDate : vorgabeZielDatum());
  const [ansicht, setAnsicht] = useState<Ansicht>("haupt");
  const [speichert, setSpeichert] = useState(false);
  const feld = useRef<HTMLTextAreaElement>(null);

  const daten = useEintragenDaten(hundId, vorab, OHNE_TERMINE);
  const chips = uebungsChips(daten.goals, daten.sessions);

  // Das nächste Training für die Beschriftung des Ziels. Ohne Netz bleibt es bei "Nächstes Training".
  const [naechstes, setNaechstes] = useState<{ dogId: string; wert: NaechstesTraining } | null>(null);
  useEffect(() => {
    let abgebrochen = false;
    api.get<NaechstesTraining>(`/api/dogs/${hundId}/naechstes-training`).then(
      (wert) => {
        if (!abgebrochen) setNaechstes({ dogId: hundId, wert });
      },
      () => {},
    );
    return () => {
      abgebrochen = true;
    };
  }, [hundId]);
  const aktuellesNaechstes = naechstes?.dogId === hundId ? naechstes.wert : null;

  // Einmalig fragen, wann der Hund meistens trainiert - nur, wenn nichts erkannt wurde.
  const [neuUebersprungen, setNeuUebersprungen] = useState<ReadonlySet<string>>(new Set());
  const uebersprungen = neuUebersprungen.has(hundId) || leseUebersprungen(hundId);
  const fragtRhythmus = aktuellesNaechstes?.source === TRAININGS_QUELLE.Unbekannt && !uebersprungen && !eintrag;

  useEffect(() => {
    onEntwurf?.(text.trim() !== "" && eintrag === null);
  }, [onEntwurf, text, eintrag]);

  // iOS-Safari schiebt das fokussierte Feld erst beim ersten Tastendruck über die
  // Tastatur; deshalb nach dem Öffnen selbst in den Blick holen (wie beim Datumsfeld im Tagebuch).
  // Die Tastatur braucht einen Moment, bis das Fenster auf den sichtbaren Bereich geschrumpft ist.
  useEffect(() => {
    if (ansicht !== "haupt") return;
    feld.current?.scrollIntoView({ block: "center" });
    const spaeter = window.setTimeout(() => feld.current?.scrollIntoView({ block: "center" }), 400);
    return () => window.clearTimeout(spaeter);
  }, [ansicht]);

  function waehleUebung(neu: { id: string; name: string; nameEn: string | null } | null) {
    setUebung(neu);
    // Die Übung gibt dem Eintrag einen Anfang: Wer nur die Übung antippt, kann sofort merken.
    // Ersetzt wird nur, was leer ist oder von einer früheren Wahl stammt - nie, was jemand getippt hat.
    if (neu && (text.trim() === "" || text === vorbelegt)) {
      setText(neu.name);
      setVorbelegt(neu.name);
    } else if (!neu && text === vorbelegt) {
      setText("");
      setVorbelegt(null);
    }
  }

  function waehleHund(neu: string) {
    setHundId(neu);
    // Die Übung gehört zum Plan und den Einheiten des vorigen Hundes.
    setUebung(null);
    setVorbelegt(null);
  }

  async function speichern() {
    if (!istGueltigerText(text)) {
      toast.error(t("Bitte etwas zum Merken eintragen."));
      return;
    }
    if (ziel === MERKZETTEL_ZIEL.Datum && !istGueltigesZielDatum(datum)) {
      toast.error(t("Bitte ein Datum wählen, das nicht in der Vergangenheit liegt."));
      return;
    }

    const nutzlast = baueMerkenNutzlast({
      id: eintrag?.id ?? crypto.randomUUID(),
      text,
      exerciseId: uebung?.id ?? null,
      ziel,
      datum,
    });

    setSpeichert(true);
    try {
      if (eintrag) {
        await api.put(`/api/dogs/${hundId}/merkzettel/${eintrag.id}`, nutzlast);
        toast.success(t("Änderung gespeichert."));
      } else {
        await api.post(`/api/dogs/${hundId}/merkzettel`, nutzlast);
        toast.success(t("Gemerkt."));
      }
      onClose();
      await onSaved(false);
    } catch (err) {
      if (err instanceof ApiError && !istVoruebergehendUnerreichbar(err)) {
        toast.error(err.message);
        return;
      }
      if (eintrag) {
        // Ändern geht nur mit Netz: Eine Änderung in der Warteschlange könnte einen Eintrag überschreiben,
        // der sich inzwischen geändert hat.
        toast.error(t("Änderung konnte nicht gespeichert werden."));
        return;
      }
      // Kein HTTP-Fehler vom Server, sondern ein Netzwerkfehler (offline) oder ein Server, der gerade neu
      // startet: wie beim Training in die Warteschlange. Dieselbe Id - kam die erste Anfrage doch an,
      // erkennt der Server den Eintrag wieder und legt ihn nicht noch einmal an.
      try {
        await enqueueRequest({ path: `/api/dogs/${hundId}/merkzettel`, method: "POST", body: nutzlast, label: t("Merkzettel") });
      } catch {
        // Auch die Warteschlange ist nicht erreichbar: Das Fenster bleibt offen, damit der Text nicht verloren geht.
        toast.error(t("Der Eintrag konnte nicht gemerkt werden."));
        return;
      }
      toast.success(
        err instanceof ApiError
          ? t("Server gerade nicht erreichbar – gemerkt, wird gleich automatisch nachgeschickt.")
          : t("Offline gemerkt. Wird synchronisiert, sobald wieder Internet verfügbar ist."),
      );
      onClose();
      await onSaved(true);
    } finally {
      setSpeichert(false);
    }
  }

  const titel = (
    <SheetHeader className="shrink-0 flex-row items-center justify-between gap-2 p-4 pb-2">
      <SheetTitle className="text-lg">
        {ansicht === "suche" ? t("Übung wählen") : eintrag ? t("Eintrag ändern") : t("Fürs nächste Training merken")}
      </SheetTitle>
      <Button type="button" variant="ghost" size="icon" className="size-11 shrink-0" aria-label={t("Schließen")} onClick={onClose}>
        <X className="size-5" />
      </Button>
    </SheetHeader>
  );

  if (ansicht === "suche") {
    return (
      <>
        {titel}
        <UebungsSuche
          sports={daten.sports}
          offline={daten.offline}
          gewaehlt={uebung ? new Set([inhaltsSchluessel(uebung.id, uebung.name)]) : new Set()}
          onWaehle={(gewaehlt) => {
            waehleUebung({ id: gewaehlt.id, name: gewaehlt.name, nameEn: gewaehlt.nameEn ?? null });
            setAnsicht("haupt");
          }}
          // Eine eigene Bezeichnung ist keine Katalog-Übung: Sie gehört in den Text.
          onFreitext={(frei) => {
            setText((vorher) => (vorher.trim() === "" ? frei : `${vorher.trim()} ${frei}`));
            setAnsicht("haupt");
          }}
          onZurueck={() => setAnsicht("haupt")}
        />
      </>
    );
  }

  const hund = hunde.find((h) => h.id === hundId);
  const mitHundWahl = hunde.length > 1 && eintrag === null;
  // Die gewählte Übung steht auch dann als Chip da, wenn sie nicht zu den vorgeschlagenen gehört ("Andere…").
  const andereGewaehlt = uebung !== null && !chips.some((chip) => chip.exerciseId === uebung.id);

  return (
    <>
      {titel}
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto overscroll-contain px-4 pb-4">
        {mitHundWahl && (
          <div className="flex flex-wrap gap-2" role="group" aria-label={t("Hund wählen")}>
            {hunde.map((h) => (
              <Chip key={h.id} gewaehlt={h.id === hundId} className="pl-1.5" onClick={() => waehleHund(h.id)}>
                <DogAvatar dogId={h.id} hasImage={h.hasImage} name={h.name} className="size-7" iconClassName="size-4" />
                <span className="min-w-0 truncate">{h.name}</span>
              </Chip>
            ))}
          </div>
        )}
        {!mitHundWahl && hund && hunde.length > 1 && (
          <p className="text-sm text-muted-foreground [overflow-wrap:anywhere]">{hund.name}</p>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="merken-text" className="sr-only">
            {t("Was soll beim nächsten Training drankommen?")}
          </Label>
          <textarea
            id="merken-text"
            ref={feld}
            autoFocus
            rows={3}
            value={text}
            maxLength={TEXTLAENGE.merkzettelText}
            onChange={(e) => setText(e.target.value)}
            placeholder={t("Was soll beim nächsten Training drankommen?")}
            className="min-h-24 w-full min-w-0 resize-none rounded-md border border-input bg-transparent px-3 py-2 text-base focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/85 focus-visible:outline-none md:text-sm"
          />
          <p className="text-xs text-muted-foreground">{t("Tipp: Mikrofon der Tastatur – einfach diktieren")}</p>
        </div>

        {(chips.length > 0 || andereGewaehlt || daten.sports.length > 0) && (
          <section className="flex flex-col gap-2" aria-labelledby="merken-uebung">
            <Abschnittsname id="merken-uebung">{t("Übung (optional)")}</Abschnittsname>
            <div className="flex flex-wrap gap-2">
              {chips.map((chip) => (
                <Chip
                  key={chip.exerciseId}
                  gewaehlt={uebung?.id === chip.exerciseId}
                  onClick={() => waehleUebung(uebung?.id === chip.exerciseId ? null : { id: chip.exerciseId, name: chip.name, nameEn: chip.nameEn })}
                >
                  {katalogName({ name: chip.name, nameEn: chip.nameEn }, sprache)}
                </Chip>
              ))}
              {andereGewaehlt && uebung && (
                <Chip gewaehlt onClick={() => waehleUebung(null)}>
                  {katalogName({ name: uebung.name, nameEn: uebung.nameEn }, sprache)}
                </Chip>
              )}
              <Chip className="border-dashed" onClick={() => setAnsicht("suche")}>
                <Plus className="size-4 shrink-0" aria-hidden />
                {t("Andere…")}
              </Chip>
            </div>
          </section>
        )}

        <section className="flex flex-col gap-2" aria-labelledby="merken-ziel">
          <Abschnittsname id="merken-ziel">{t("Wann?")}</Abschnittsname>
          <div role="group" aria-label={t("Wann?")} className="flex flex-wrap gap-2">
            <Chip gewaehlt={ziel === MERKZETTEL_ZIEL.NaechstesTraining} onClick={() => setZiel(MERKZETTEL_ZIEL.NaechstesTraining)}>
              {naechstesTrainingText(t, sprache, aktuellesNaechstes)}
            </Chip>
            <Chip gewaehlt={ziel === MERKZETTEL_ZIEL.NaechsteWoche} onClick={() => setZiel(MERKZETTEL_ZIEL.NaechsteWoche)}>
              {t("Nächste Woche")}
            </Chip>
            <Chip gewaehlt={ziel === MERKZETTEL_ZIEL.Datum} onClick={() => setZiel(MERKZETTEL_ZIEL.Datum)}>
              {t("Datum…")}
            </Chip>
          </div>
          {ziel === MERKZETTEL_ZIEL.Datum && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="merken-datum" className="text-xs text-muted-foreground">
                {t("Datum")}
              </Label>
              <Input
                id="merken-datum"
                type="date"
                min={heuteIso()}
                value={datum}
                onChange={(e) => setDatum(e.target.value)}
                className="h-11 min-w-0"
              />
            </div>
          )}
        </section>

        {fragtRhythmus && (
          <RhythmusFrage
            dogId={hundId}
            onGespeichert={(wert) => setNaechstes({ dogId: hundId, wert })}
            onUeberspringen={() => {
              merkeUebersprungen(hundId);
              setNeuUebersprungen((vorher) => new Set(vorher).add(hundId));
            }}
          />
        )}
      </div>

      <div className="shrink-0 border-t border-surface-border bg-popover px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
        <Button type="button" className="h-12 w-full text-base font-semibold" disabled={speichert || text.trim() === ""} onClick={speichern}>
          {eintrag ? t("Speichern") : t("Merken")}
        </Button>
      </div>
    </>
  );
}

/** Die Suche aus dem Eintragen-Fenster; der Katalog lädt erst, wenn sie aufgeht. */
function UebungsSuche({
  sports,
  offline,
  gewaehlt,
  onWaehle,
  onFreitext,
  onZurueck,
}: {
  sports: Parameters<typeof useUebungsKatalog>[0];
  offline: boolean;
  gewaehlt: ReadonlySet<string>;
  onWaehle: (uebung: Exercise) => void;
  onFreitext: (text: string) => void;
  onZurueck: () => void;
}) {
  const katalog = useUebungsKatalog(sports);
  return (
    <EintragenSuche
      sports={sports}
      katalog={katalog}
      gewaehlt={gewaehlt}
      offline={offline}
      onWaehle={onWaehle}
      onFreitext={onFreitext}
      onZurueck={onZurueck}
    />
  );
}

/**
 * "Wann trainierst du meistens?" - einmalig, überspringbar. Die Antwort gilt für den Hund (alle Besitzer:innen)
 * und wird vor dem Muster aus dem Tagebuch benutzt.
 */
function RhythmusFrage({
  dogId,
  onGespeichert,
  onUeberspringen,
}: {
  dogId: string;
  onGespeichert: (naechstes: NaechstesTraining) => void;
  onUeberspringen: () => void;
}) {
  const t = useT();
  const [tage, setTage] = useState(0);
  const [stunde, setStunde] = useState<number | null>(null);
  const [speichert, setSpeichert] = useState(false);
  const nutzlast = rhythmusNutzlast(tage, stunde);

  async function speichern() {
    if (!nutzlast) return;
    setSpeichert(true);
    try {
      onGespeichert(await api.put<NaechstesTraining>(`/api/dogs/${dogId}/trainingsrhythmus`, nutzlast));
      toast.success(t("Gespeichert. Danach richtet sich die Erinnerung."));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Der Rhythmus konnte nicht gespeichert werden."));
    } finally {
      setSpeichert(false);
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-surface-border p-3" aria-labelledby="merken-rhythmus">
      <div className="flex flex-col gap-1">
        <h3 id="merken-rhythmus" className="text-sm font-medium">
          {t("Wann trainierst du meistens?")}
        </h3>
        <p className="text-xs text-muted-foreground">{t("Dann weiß Dogity, wann es dich erinnern soll. Du kannst es überspringen.")}</p>
      </div>
      <div role="group" aria-label={t("Wochentage")} className="flex flex-wrap gap-2">
        {WOCHENTAGE.map((tag) => (
          <Chip
            key={tag.bit}
            gewaehlt={tagGewaehlt(tage, tag.bit)}
            aria-label={t(tag.lang)}
            onClick={() => setTage((vorher) => schalteTag(vorher, tag.bit))}
            className="size-11 shrink-0 justify-center px-0 text-center"
          >
            {t(tag.kurz)}
          </Chip>
        ))}
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="merken-stunde" className="text-xs text-muted-foreground">
          {t("Uhrzeit")}
        </Label>
        <select
          id="merken-stunde"
          value={stunde === null ? "" : String(stunde)}
          onChange={(e) => setStunde(e.target.value === "" ? null : Number(e.target.value))}
          className="h-11 w-40 max-w-full rounded-md border border-input bg-transparent px-3 text-base md:text-sm"
        >
          <option value="">{t("wechselt")}</option>
          {STUNDEN.map((h) => (
            <option key={h} value={h}>
              {stundeText(h)}
            </option>
          ))}
        </select>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" className="min-h-11" disabled={!nutzlast || speichert} onClick={speichern}>
          {t("Speichern")}
        </Button>
        <Button type="button" variant="ghost" className="min-h-11" disabled={speichert} onClick={onUeberspringen}>
          {t("Überspringen")}
        </Button>
      </div>
    </section>
  );
}

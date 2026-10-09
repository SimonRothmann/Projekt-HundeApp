"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ChevronDown, History, Plus, X } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { enqueueRequest } from "@/lib/offline-queue";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetClose, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { DogAvatar } from "@/components/dogs/dog-avatar";
import { ConditionPicker } from "@/components/dogs/condition-picker";
import { LocationTimeFields, type LocationValue } from "@/components/dogs/location-time-fields";
import { Abschnittsname, Chip } from "@/components/dogs/eintragen-chip";
import { EintragenFuss } from "@/components/dogs/eintragen-fuss";
import { EintragenSuche } from "@/components/dogs/eintragen-suche";
import { EintragenZeilen } from "@/components/dogs/eintragen-zeilen";
import {
  baueNutzlast,
  datumFuerTag,
  freitextZeile,
  inhaltsSchluessel,
  istGueltig,
  istGueltigeDauer,
  istGueltigesDatum,
  katalogZeile,
  KEIN_ORT,
  letzteEinheit,
  loeseZeilenAuf,
  tagVorher,
  uhrzeitText,
  VORGABE_BEWERTUNG,
  vorbelegteDauer,
  vorbelegterOrt,
  wochenChips,
  zeileAusPlanItem,
  zeilenAusEinheit,
  zeilenSchluessel,
  zuletztChips,
  type Bewertung,
  type EintragZeile,
  type Tag,
  type Werte,
} from "@/lib/eintragen";
import { istPlanZielFehler, ohnePlanVerknuepfung } from "@/lib/trainingsplan";
import { REITER_PARAMETER } from "@/lib/hundeseite";
import { heuteIso } from "@/lib/pruefung";
import { useSprache, useT } from "@/lib/i18n";
import { ortsformat } from "@/lib/ortsformat";
import { TEXTLAENGE } from "@/lib/textlaengen";
import { useAktuellerStandort } from "@/lib/use-aktueller-standort";
import {
  useEintragenDaten,
  useUebungsKatalog,
  type EintragenVorab,
} from "@/lib/use-eintragen-daten";
import { useTastatur } from "@/lib/use-tastatur";
import type { DogCondition, Dog, Exercise, GroupTrainingSession, TrainingPlanItem, TrainingSession } from "@/lib/types";
import { cn } from "@/lib/utils";

/**
 * EIN Fenster zum Eintragen jedes Trainings - von der Startseite, von der
 * Hundeseite und aus jeder Planzeile. Ersetzt das lange Formular
 * (Datum, Dauer, Sportart, Übung, Bewertung, ... je Übung) durch das, was auf
 * dem Hundeplatz mit einer Hand gebraucht wird: Übungen antippen, speichern.
 *
 * Gleicher Eintrag wie zuvor (POST /api/trainings samt Zusammenführen am
 * selben Tag), gleiche Fehlerbehandlung, gleiche Offline-Warteschlange.
 *
 * Der Inhalt entsteht erst beim Öffnen und endet mit dem Schließen: Jedes
 * Öffnen beginnt sauber. Solange das Fenster offen ist, hält es seinen Stand
 * allein - die aufrufende Seite darf im Hintergrund neu laden (Props ändern
 * sich), ohne dass etwas Eingetipptes verloren geht.
 */
export function EintragenSheet({
  open,
  onOpenChange,
  dogId,
  hunde,
  vorab,
  termine,
  sportIdsJeHund,
  vorgabePlan,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Der Hund, für den das Fenster aufgeht. */
  dogId: string;
  /** Die Hunde, zwischen denen das Fenster wechseln lässt (eigene, aktive); darf nur den einen enthalten. */
  hunde: Dog[];
  /** Was die aufrufende Seite zu einem Hund schon weiß (Hundeseite); fehlt auf der Startseite. */
  vorab?: EintragenVorab | null;
  /** Die Gruppentermine des Nutzers ab heute, wo die Seite sie schon hat. */
  termine?: GroupTrainingSession[];
  /** Die Sportarten je Hund, wo die Seite sie schon hat (Startseite); damit filtert das Fenster auch offline. */
  sportIdsJeHund?: Record<string, string[]>;
  /** Eine Planübung, die schon gewählt sein soll (Antippen einer Planzeile). */
  vorgabePlan?: TrainingPlanItem | null;
  /** offline = true: nur in die Warteschlange geschrieben - die Seite soll NICHT neu vom Server laden. */
  onSaved: (offline: boolean) => Promise<void>;
}) {
  const t = useT();
  // Ob im Fenster schon etwas steht, das beim Schließen verloren ginge. Ein
  // Ref statt Zustand: Der Inhalt meldet es, ohne dass die Hülle neu rendert.
  const entwurfRef = useRef(false);
  const meldeEntwurf = useCallback((vorhanden: boolean) => {
    entwurfRef.current = vorhanden;
  }, []);
  const tastatur = useTastatur(open);

  return (
    <Sheet
      open={open}
      onOpenChange={(offen, details) => {
        // Ein Tipp daneben oder Escape verwirft nichts, was schon eingetragen
        // ist: Mit dem Daumen am Rand ist der Tipp daneben schnell passiert, und
        // die Übungen dieser Einheit stehen nicht noch einmal irgendwo. Schließen
        // geht dann über das X.
        if (!offen && entwurfRef.current && (details.reason === "outside-press" || details.reason === "escape-key")) return;
        onOpenChange(offen);
      }}
    >
      <SheetContent
        side="bottom"
        showCloseButton={false}
        className={cn("max-h-[92dvh] gap-0 overflow-hidden rounded-t-xl p-0", tastatur.hoehe !== null && "transition-none")}
        // Bei offener Tastatur (iPhone): über die Tastatur rücken und auf den
        // sichtbaren Bereich schrumpfen - sonst läge der Speichern-Knopf dahinter.
        style={tastatur.hoehe !== null ? { bottom: tastatur.unten, maxHeight: tastatur.hoehe - 8 } : undefined}
      >
        <EintragenInhalt
          startHund={dogId}
          hunde={hunde}
          vorab={vorab ?? null}
          termine={termine}
          sportIdsJeHund={sportIdsJeHund}
          vorgabePlan={vorgabePlan ?? null}
          onEntwurf={meldeEntwurf}
          onClose={() => onOpenChange(false)}
          onSaved={onSaved}
        />
        <SheetDescription className="sr-only">{t("Übungen antippen und speichern.")}</SheetDescription>
      </SheetContent>
    </Sheet>
  );
}

type Ansicht = "haupt" | "suche" | "ort";

function EintragenInhalt({
  startHund,
  hunde,
  vorab,
  termine,
  sportIdsJeHund,
  vorgabePlan,
  onEntwurf,
  onClose,
  onSaved,
}: {
  startHund: string;
  hunde: Dog[];
  vorab: EintragenVorab | null;
  termine: GroupTrainingSession[] | undefined;
  sportIdsJeHund: Record<string, string[]> | undefined;
  vorgabePlan: TrainingPlanItem | null;
  /** Meldet der Hülle, ob schon etwas eingetragen ist (dann verwirft ein Tipp daneben das Fenster nicht). */
  onEntwurf: (vorhanden: boolean) => void;
  onClose: () => void;
  onSaved: (offline: boolean) => Promise<void>;
}) {
  const t = useT();
  const sprache = useSprache();
  const router = useRouter();

  const [hundId, setHundId] = useState(startHund);
  const [hundWahl, setHundWahl] = useState(false);
  const [tag, setTag] = useState<Tag>("heute");
  const [anderesDatum, setAnderesDatum] = useState(() => tagVorher(heuteIso(), 2));
  const [zeilen, setZeilen] = useState<EintragZeile[]>(() => (vorgabePlan ? [zeileAusPlanItem(vorgabePlan)] : []));
  const [bewertung, setBewertung] = useState<Bewertung>(VORGABE_BEWERTUNG);
  const [offeneDetails, setOffeneDetails] = useState<ReadonlySet<string>>(new Set());
  const [ansicht, setAnsicht] = useState<Ansicht>("haupt");
  const [mehrOffen, setMehrOffen] = useState(false);
  const [verfassung, setVerfassung] = useState<DogCondition | null>(null);
  const [notiz, setNotiz] = useState("");
  const [speichert, setSpeichert] = useState(false);
  // Einmal beim Öffnen: "Heute 16:40" soll nicht unter dem Finger weiterzählen.
  const [uhrBeimOeffnen] = useState(() => uhrzeitText());

  // Dauer, Uhrzeit und Ort stehen NICHT als Zustand, solange niemand sie
  // angefasst hat: Sie ergeben sich aus den Daten des Hundes (letzte Einheit,
  // Gruppentermin), die erst Sekunden nach dem Öffnen da sein können. Eine
  // Eingabe (…Wahl) überstimmt sie dann - und nur eine Eingabe.
  const [dauerWahl, setDauerWahl] = useState<number | null>(null);
  const [uhrzeitWahl, setUhrzeitWahl] = useState<string | null>(null);
  const [ortWahl, setOrtWahl] = useState<LocationValue | null>(null);

  const daten = useEintragenDaten(hundId, vorab, termine, sportIdsJeHund?.[hundId]);
  const katalog = useUebungsKatalog(daten.sports);

  const datum = datumFuerTag(tag, anderesDatum);
  const dauer = dauerWahl ?? vorbelegteDauer(daten.sessions);
  const uhrzeit = uhrzeitWahl ?? (tag === "heute" ? uhrBeimOeffnen : "");
  const vorOrt = useMemo(
    () => vorbelegterOrt(daten.sessions, daten.termine, datum),
    [daten.sessions, daten.termine, datum],
  );
  const ort = ortWahl ?? vorOrt ?? KEIN_ORT;
  const hatOrt = ort.locationName.trim() !== "" || (ort.latitude !== null && ort.longitude !== null);
  const standort = useAktuellerStandort(ort, setOrtWahl);

  const aufgeloest = useMemo(
    () => loeseZeilenAuf(zeilen, bewertung, daten.goals, datum),
    [zeilen, bewertung, daten.goals, datum],
  );
  const anzahl = aufgeloest.filter((a) => istGueltig(a.zeile)).length;

  // "Mehr" liegt im Fuß, was es aufklappt im Inhalt darüber: ins Bild holen,
  // sonst tippt man und sieht nichts geschehen.
  const mehrRef = useRef<HTMLElement>(null);
  useEffect(() => {
    if (mehrOffen) mehrRef.current?.scrollIntoView({ block: "nearest" });
  }, [mehrOffen]);

  useEffect(() => {
    onEntwurf(zeilen.length > 0 || notiz !== "");
  }, [onEntwurf, zeilen.length, notiz]);

  const gewaehlt = useMemo(() => new Set(zeilen.map(zeilenSchluessel)), [zeilen]);
  const wochen = useMemo(() => wochenChips(daten.goals), [daten.goals]);
  const letzte = useMemo(() => letzteEinheit(daten.sessions), [daten.sessions]);
  const zuletzt = useMemo(
    () => zuletztChips(daten.sessions, new Set(wochen.map((c) => inhaltsSchluessel(c.item.exerciseId, c.name)))),
    [daten.sessions, wochen],
  );
  const uebungen = useMemo(() => {
    const karte = new Map<string, Exercise>();
    for (const liste of Object.values(katalog)) for (const uebung of liste) karte.set(uebung.id, uebung);
    return karte;
  }, [katalog]);

  // ---- Zeilen ----

  /** Wählt eine Übung ab, wenn sie schon gewählt ist, sonst hinzu: Chips sind Schalter. */
  function schalte(schluessel: string, neu: () => EintragZeile) {
    setZeilen((vorher) =>
      vorher.some((zeile) => zeilenSchluessel(zeile) === schluessel)
        ? vorher.filter((zeile) => zeilenSchluessel(zeile) !== schluessel)
        : [...vorher, neu()],
    );
  }

  function wieBeimLetztenMal() {
    if (!letzte) return;
    setZeilen((vorher) => {
      const daraus = zeilenAusEinheit(letzte);
      const da = new Set(vorher.map(zeilenSchluessel));
      // Wie die anderen Chips ein Schalter: Ist schon alles gewählt, nimmt der
      // zweite Tipp die Übungen der Einheit wieder heraus.
      if (daraus.every((zeile) => da.has(zeilenSchluessel(zeile)))) {
        const raus = new Set(daraus.map(zeilenSchluessel));
        return vorher.filter((zeile) => !raus.has(zeilenSchluessel(zeile)));
      }
      // Nur, was noch fehlt - auch innerhalb der Einheit nichts doppelt.
      const neu = daraus.filter((zeile) => {
        const schluessel = zeilenSchluessel(zeile);
        if (da.has(schluessel)) return false;
        da.add(schluessel);
        return true;
      });
      return [...vorher, ...neu];
    });
  }

  function aendereZeile(schluessel: string, aenderung: Partial<EintragZeile>) {
    setZeilen((vorher) => vorher.map((zeile) => (zeile.schluessel === schluessel ? { ...zeile, ...aenderung } : zeile)));
  }

  function aendereWerte(schluessel: string, aenderung: Partial<Werte>) {
    const aktuell = aufgeloest.find((a) => a.zeile.schluessel === schluessel);
    if (!aktuell) return;
    aendereZeile(schluessel, { eigeneWerte: { rating: aktuell.rating, success: aktuell.success, ...aenderung } });
  }

  function entferneZeile(schluessel: string) {
    setZeilen((vorher) => vorher.filter((zeile) => zeile.schluessel !== schluessel));
    setOffeneDetails((vorher) => new Set([...vorher].filter((offen) => offen !== schluessel)));
  }

  function waehleHund(neu: string) {
    setHundId(neu);
    setHundWahl(false);
    // Das ausdrücklich gewählte Plan-Ziel gehört zum Plan des vorigen Hundes.
    setZeilen((vorher) => vorher.map((zeile) => ({ ...zeile, vorgabePlan: null, planGeloest: false })));
  }

  // ---- Speichern ----

  async function speichern() {
    if (aufgeloest.filter((a) => istGueltig(a.zeile)).length === 0) {
      toast.error(t("Mindestens eine Übung auswählen oder eintragen."));
      return;
    }
    if (!istGueltigesDatum(datum)) {
      toast.error(t("Bitte einen Tag wählen, der nicht in der Zukunft liegt."));
      return;
    }
    if (!istGueltigeDauer(dauer)) {
      toast.error(t("Die Dauer muss in ganzen Minuten angegeben sein."));
      return;
    }

    const nutzlast = baueNutzlast({
      dogId: hundId,
      datum,
      dauer,
      notiz,
      uhrzeit,
      ort,
      verfassung,
      zeilen: aufgeloest,
    });

    setSpeichert(true);
    try {
      let gespeichert: TrainingSession;
      try {
        gespeichert = await api.post<TrainingSession>("/api/trainings", nutzlast);
      } catch (err) {
        // Das automatisch gesetzte Plan-Ziel gibt es nicht mehr (Plan woanders
        // neu erzeugt): ohne Verknüpfung speichern statt das Training zu verlieren.
        const ohne = istPlanZielFehler(err) ? ohnePlanVerknuepfung(nutzlast) : null;
        if (ohne === null) throw err;
        gespeichert = await api.post<TrainingSession>("/api/trainings", ohne);
      }
      toast.success(t("Training gespeichert."), {
        action: {
          label: t("Ansehen"),
          onClick: () => router.push(`/dogs/${hundId}?${REITER_PARAMETER}=tagebuch&eintrag=${gespeichert.id}`),
        },
      });
      onClose();
      await onSaved(false);
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(err.message);
        return;
      }
      // Kein HTTP-Fehler vom Server, sondern ein Netzwerkfehler (offline) -
      // siehe PRODUCT_REQUIREMENTS.md "Offline": Training ohne Internet erfassen.
      try {
        await enqueueRequest({ path: "/api/trainings", method: "POST", body: nutzlast, label: t("Training") });
      } catch {
        // Auch die Warteschlange ist nicht erreichbar: das Fenster bleibt offen,
        // damit die Eingabe nicht verloren geht.
        toast.error(t("Training konnte nicht gespeichert werden."));
        return;
      }
      toast.success(t("Offline gespeichert. Wird synchronisiert, sobald wieder Internet verfügbar ist."));
      onClose();
      await onSaved(true);
    } finally {
      setSpeichert(false);
    }
  }

  // ---- Anzeige ----

  const ortSprache = ortsformat(sprache);
  const tagText =
    tag === "heute"
      ? t("Heute")
      : tag === "gestern"
        ? t("Gestern")
        : /^\d{4}-\d{2}-\d{2}$/.test(anderesDatum)
          ? new Date(anderesDatum + "T12:00:00").toLocaleDateString(ortSprache, { day: "numeric", month: "numeric", year: "numeric" })
          : t("Anderer Tag");
  const kontext = [[tagText, uhrzeit].filter(Boolean).join(" "), ort.locationName.trim()].filter(Boolean).join(" · ");
  const hund = hunde.find((h) => h.id === hundId);
  const kannWechseln = hunde.length > 1 && hunde.some((h) => h.id === hundId);

  const titel = (
    <SheetHeader className="shrink-0 flex-row items-center justify-between gap-2 p-4 pb-2">
      <SheetTitle className="text-lg">
        {ansicht === "suche" ? t("Übung wählen") : ansicht === "ort" ? t("Ort & Uhrzeit") : t("Training eintragen")}
      </SheetTitle>
      <SheetClose render={<Button variant="ghost" size="icon" className="size-11 shrink-0" aria-label={t("Schließen")} />}>
        <X className="size-5" />
      </SheetClose>
    </SheetHeader>
  );

  if (ansicht === "suche") {
    return (
      <>
        {titel}
        <EintragenSuche
          sports={daten.sports}
          katalog={katalog}
          gewaehlt={gewaehlt}
          offline={daten.offline}
          onWaehle={(uebung) => {
            setZeilen((vorher) =>
              vorher.some((zeile) => zeilenSchluessel(zeile) === inhaltsSchluessel(uebung.id, uebung.name))
                ? vorher
                : [...vorher, katalogZeile(uebung)],
            );
            setAnsicht("haupt");
          }}
          onFreitext={(text) => {
            setZeilen((vorher) => [...vorher, freitextZeile(text)]);
            setAnsicht("haupt");
          }}
          onZurueck={() => setAnsicht("haupt")}
        />
      </>
    );
  }

  if (ansicht === "ort") {
    return (
      <>
        {titel}
        <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto overscroll-contain px-4 pb-4 [&_input]:h-11">
          <LocationTimeFields
            idPrefix="eintragen"
            time={uhrzeit}
            onTimeChange={setUhrzeitWahl}
            location={ort}
            onLocationChange={setOrtWahl}
          />
          <p className="text-xs text-muted-foreground">{t("Mit Ort und Uhrzeit wird das Wetter automatisch ermittelt.")}</p>
          {(uhrzeit || hatOrt) && (
            <Button
              type="button"
              variant="ghost"
              className="min-h-11 self-start"
              onClick={() => {
                setUhrzeitWahl("");
                setOrtWahl(KEIN_ORT);
              }}
            >
              {t("Ort & Zeit wieder entfernen")}
            </Button>
          )}
        </div>
        <div className="shrink-0 border-t border-surface-border bg-popover px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
          <Button type="button" className="h-12 w-full text-base font-semibold" onClick={() => setAnsicht("haupt")}>
            {t("Fertig")}
          </Button>
        </div>
      </>
    );
  }

  return (
    <>
      {titel}
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto overscroll-contain px-4 pb-4">
        {/* Hund und Tag */}
        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            {hund &&
              (kannWechseln ? (
                <Chip aria-expanded={hundWahl} onClick={() => setHundWahl((offen) => !offen)} className="pl-1.5">
                  <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-7" iconClassName="size-4" />
                  <span className="min-w-0 truncate font-medium">{hund.name}</span>
                  <ChevronDown className={cn("size-4 shrink-0 transition-transform", hundWahl && "rotate-180")} aria-hidden />
                </Chip>
              ) : (
                <span className="inline-flex min-h-11 max-w-full min-w-0 items-center gap-1.5 rounded-full border border-input py-1.5 pr-3.5 pl-1.5 text-sm">
                  <DogAvatar dogId={hund.id} hasImage={hund.hasImage} name={hund.name} className="size-7" iconClassName="size-4" />
                  <span className="min-w-0 truncate font-medium">{hund.name}</span>
                </span>
              ))}
            <div role="group" aria-label={t("Tag")} className="flex flex-wrap gap-2">
              <Chip gewaehlt={tag === "heute"} onClick={() => setTag("heute")}>
                {t("Heute")}
              </Chip>
              <Chip gewaehlt={tag === "gestern"} onClick={() => setTag("gestern")}>
                {t("Gestern")}
              </Chip>
              <Chip gewaehlt={tag === "anderer"} onClick={() => setTag("anderer")}>
                {t("Anderer Tag")}
              </Chip>
            </div>
          </div>
          {hundWahl && (
            <div className="flex flex-wrap gap-2" role="group" aria-label={t("Hund wählen")}>
              {hunde.map((h) => (
                <Chip key={h.id} gewaehlt={h.id === hundId} className="pl-1.5" onClick={() => waehleHund(h.id)}>
                  <DogAvatar dogId={h.id} hasImage={h.hasImage} name={h.name} className="size-7" iconClassName="size-4" />
                  <span className="min-w-0 truncate">{h.name}</span>
                </Chip>
              ))}
            </div>
          )}
          {tag === "anderer" && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="eintragen-datum" className="text-xs text-muted-foreground">
                {t("Datum")}
              </Label>
              <Input
                id="eintragen-datum"
                type="date"
                max={heuteIso()}
                value={anderesDatum}
                onChange={(e) => setAnderesDatum(e.target.value)}
                className="h-11 min-w-0"
              />
            </div>
          )}
        </div>

        {daten.offline && <p className="text-sm text-muted-foreground">{t("Offline - nur freie Eingabe")}</p>}

        {wochen.length > 0 && (
          <section className="flex flex-col gap-2" aria-labelledby="eintragen-woche">
            <Abschnittsname id="eintragen-woche">{t("Diese Woche")}</Abschnittsname>
            <div className="flex flex-wrap gap-2">
              {wochen.map(({ item, name, erledigt, ziel }) => {
                const schluessel = inhaltsSchluessel(item.exerciseId, name);
                return (
                  <Chip key={item.id} gewaehlt={gewaehlt.has(schluessel)} onClick={() => schalte(schluessel, () => zeileAusPlanItem(item))}>
                    <span className="min-w-0">{name}</span>
                    <span className="shrink-0 tabular-nums text-muted-foreground">
                      {erledigt}/{ziel}
                    </span>
                  </Chip>
                );
              })}
            </div>
          </section>
        )}

        {(letzte || zuletzt.length > 0) && (
          <section className="flex flex-col gap-2" aria-labelledby="eintragen-zuletzt">
            <Abschnittsname id="eintragen-zuletzt">{t("Zuletzt geübt")}</Abschnittsname>
            <div className="flex flex-wrap gap-2">
              {letzte && (
                <Chip
                  gewaehlt={zeilenAusEinheit(letzte).every((zeile) => gewaehlt.has(zeilenSchluessel(zeile)))}
                  onClick={wieBeimLetztenMal}
                >
                  <History className="size-4 shrink-0" aria-hidden />
                  {t("Wie beim letzten Mal ({anzahl})", { anzahl: letzte.exercises.length })}
                </Chip>
              )}
              {zuletzt.map(({ exerciseId, name }) => {
                const schluessel = inhaltsSchluessel(exerciseId, name);
                return (
                  <Chip
                    key={schluessel}
                    gewaehlt={gewaehlt.has(schluessel)}
                    onClick={() => schalte(schluessel, () => (exerciseId ? katalogZeile({ id: exerciseId, name }) : freitextZeile(name)))}
                  >
                    {name}
                  </Chip>
                );
              })}
            </div>
          </section>
        )}

        <Chip className="self-start border-dashed" onClick={() => setAnsicht("suche")}>
          <Plus className="size-4 shrink-0" aria-hidden />
          {t("Andere Übung")}
        </Chip>

        <EintragenZeilen
          zeilen={aufgeloest}
          bewertung={bewertung}
          onBewertung={setBewertung}
          offen={offeneDetails}
          onUmschalten={(schluessel) =>
            setOffeneDetails((vorher) => {
              const neu = new Set(vorher);
              if (!neu.delete(schluessel)) neu.add(schluessel);
              return neu;
            })
          }
          onAendere={aendereZeile}
          onWerte={aendereWerte}
          onNeutral={(schluessel) => aendereZeile(schluessel, { eigeneWerte: null })}
          onEntferne={entferneZeile}
          uebungen={uebungen}
          ohneVorschlaege={wochen.length === 0 && !letzte && zuletzt.length === 0}
        />

        {mehrOffen && (
          <section ref={mehrRef} className="flex flex-col gap-3 rounded-lg border border-surface-border p-3">
            <div className="flex flex-col gap-2">
              <Label>{t("Verfassung (optional)")}</Label>
              <ConditionPicker value={verfassung} onChange={setVerfassung} disabled={speichert} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="eintragen-notiz">{t("Notizen zum ganzen Training")}</Label>
              <Input
                id="eintragen-notiz"
                value={notiz}
                maxLength={TEXTLAENGE.trainingsNotiz}
                className="h-11"
                onChange={(e) => setNotiz(e.target.value)}
              />
            </div>
          </section>
        )}
      </div>

      <EintragenFuss
        dauer={dauer}
        onDauer={setDauerWahl}
        kontext={kontext}
        hatOrt={hatOrt}
        standortLaeuft={standort.laeuft}
        onOrtBearbeiten={() => setAnsicht("ort")}
        onStandort={standort.ermitteln}
        mehrOffen={mehrOffen}
        onMehr={() => setMehrOffen((offen) => !offen)}
        anzahl={anzahl}
        speichert={speichert}
        onSpeichern={speichern}
      />
    </>
  );
}

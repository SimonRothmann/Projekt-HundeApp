"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { Check } from "lucide-react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { tagKurz } from "@/lib/anmeldung";
import { useSprache, useT } from "@/lib/i18n";
import { datumMitWochentag, heuteIso } from "@/lib/pruefung";
import type { AttendanceDay, GroupRegistration } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

/**
 * Anwesenheit je Termin: oben der Tag, darunter alle Angemeldeten als große
 * Abhak-Zeilen. Jeder Tipp speichert sofort - sofort sichtbar (optimistisch),
 * bei einem Fehler springt der Haken mit einer Meldung zurück.
 *
 * Gedacht für den Platz: Handschuhe, Leine in der Hand, Sonne auf dem Display.
 * Deshalb sind die Zeilen groß, und es gibt keinen "Speichern"-Knopf.
 *
 * Zur Wahl stehen "Heute", die zurückliegenden Termine der Gruppe und ein
 * Datumsfeld für jeden anderen Tag. Zukünftige Termine fehlen bei den Chips:
 * Sie lassen sich nicht abhaken (der Server lehnt sie ab), also wären sie
 * Chips ohne Zweck.
 */
export function RegistrationAttendance({
  groupId,
  registrierungen,
  onAnzahlGeaendert,
}: {
  groupId: string;
  registrierungen: GroupRegistration[];
  /** Eine Anmeldung hat einen Haken mehr (+1) oder weniger (-1) - die Zahl "7× da" in der Liste folgt. */
  onAnzahlGeaendert: (id: string, delta: 1 | -1) => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const heute = heuteIso();
  const [tage, setTage] = useState<AttendanceDay[]>([]);
  const [datum, setDatum] = useState(heute);
  // null = wird geladen. Die IDs der Anmeldungen, die am gewählten Tag da waren.
  const [anwesend, setAnwesend] = useState<Set<string> | null>(null);
  const [beschaeftigt, setBeschaeftigt] = useState<Set<string>>(new Set());
  // Der Tag, für den gerade gilt, was angezeigt wird: Eine späte Antwort für
  // einen früher gewählten Tag darf die Anzeige nicht überschreiben.
  const aktuellerTag = useRef(datum);

  useEffect(() => {
    let abgebrochen = false;
    api
      .get<AttendanceDay[]>(`/api/groups/${groupId}/registrations/days`)
      .then((antwort) => {
        if (!abgebrochen) setTage(antwort);
      })
      .catch(() => {
        // Die Tageschips sind eine Abkürzung - "Heute" und das Datumsfeld gehen auch ohne.
      });
    return () => {
      abgebrochen = true;
    };
  }, [groupId]);

  useEffect(() => {
    aktuellerTag.current = datum;
    let abgebrochen = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAnwesend(null);
    api
      .get<string[]>(`/api/groups/${groupId}/registrations/attendance?date=${datum}`)
      .then((ids) => {
        if (!abgebrochen) setAnwesend(new Set(ids));
      })
      .catch((err) => {
        if (abgebrochen) return;
        toast.error(err instanceof ApiError ? err.message : t("Anwesenheit konnte nicht geladen werden."));
        setAnwesend(new Set());
      });
    return () => {
      abgebrochen = true;
    };
    // t bewusst nicht in der Liste: nur für die Fehlermeldung.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId, datum]);

  // Alphabetisch nach Rufname: So findet man den Hund beim Aufrufen schnell.
  const zeilen = useMemo(
    () => [...registrierungen].sort((a, b) => a.dogName.localeCompare(b.dogName, "de", { sensitivity: "base" })),
    [registrierungen],
  );

  // Die Chips: Heute, dazu die zurückliegenden Termine der Gruppe (ohne den heutigen doppelt zu zeigen).
  const chips = useMemo(() => {
    const termine = tage.filter((tag) => tag.date < heute).map((tag) => tag.date);
    return [heute, ...termine.sort().reverse()];
  }, [tage, heute]);

  const inZukunft = datum > heute;

  async function umschalten(r: GroupRegistration) {
    if (!anwesend || inZukunft || beschaeftigt.has(r.id)) return;
    const tag = datum;
    const wirdAnwesend = !anwesend.has(r.id);

    const setze = (da: boolean) =>
      setAnwesend((alt) => {
        if (!alt || aktuellerTag.current !== tag) return alt;
        const neu = new Set(alt);
        if (da) neu.add(r.id);
        else neu.delete(r.id);
        return neu;
      });

    setze(wirdAnwesend);
    setBeschaeftigt((b) => new Set(b).add(r.id));
    try {
      await api.put(`/api/groups/${groupId}/registrations/${r.id}/attendance`, { date: tag, present: wirdAnwesend });
      onAnzahlGeaendert(r.id, wirdAnwesend ? 1 : -1);
    } catch (err) {
      setze(!wirdAnwesend);
      toast.error(err instanceof ApiError ? err.message : t("Der Haken konnte nicht gespeichert werden."));
    } finally {
      setBeschaeftigt((b) => {
        const neu = new Set(b);
        neu.delete(r.id);
        return neu;
      });
    }
  }

  const da = anwesend ? zeilen.filter((r) => anwesend.has(r.id)).length : 0;

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="text-base">{t("Anwesenheit")}</CardTitle>
      </CardHeader>
      <CardContent className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t("Tag wählen")}>
          {chips.map((tag) => (
            <button
              key={tag}
              type="button"
              aria-pressed={datum === tag}
              onClick={() => setDatum(tag)}
              className={cn(
                "min-h-9 rounded-full border px-3 text-sm font-medium transition-colors coarse:min-h-11",
                datum === tag ? "border-primary bg-primary/10 text-primary-text" : "border-border text-muted-foreground hover:bg-muted",
              )}
            >
              {tag === heute ? t("Heute") : tagKurz(tag, sprache)}
            </button>
          ))}
          {/* Hinter den Chips, ohne sichtbare Beschriftung (das Feld erklärt sich selbst). Keine feste
              Mindestbreite: Safari auf dem iPhone ließe sonst das Feld über den Kartenrand ragen
              (siehe globals.css). */}
          <Input
            id="anwesenheit-datum"
            type="date"
            aria-label={t("Anderer Tag")}
            value={datum}
            max={heute}
            onChange={(e) => {
              // Ein geleertes Feld lässt den Tag stehen, statt ins Leere zu fragen.
              if (e.target.value) setDatum(e.target.value);
            }}
            className="min-w-0 flex-1 basis-36 sm:max-w-48"
          />
        </div>

        <div className="flex items-baseline justify-between gap-2 border-t pt-3">
          <h3 className="min-w-0 text-sm font-semibold [overflow-wrap:anywhere]">
            {datum === heute ? t("Heute") : datumMitWochentag(datum, sprache)}
          </h3>
          <p className="shrink-0 text-sm font-medium" aria-live="polite">
            {anwesend ? t("{x} von {n} da", { x: da, n: zeilen.length }) : ""}
          </p>
        </div>

        {inZukunft && (
          <p className="text-sm text-muted-foreground">{t("Dieser Tag liegt noch in der Zukunft. Abhaken geht erst am Tag selbst.")}</p>
        )}

        {zeilen.length === 0 ? (
          <p className="py-2 text-center text-sm text-muted-foreground">{t("Noch keine Anmeldungen.")}</p>
        ) : anwesend === null ? (
          <p className="py-2 text-center text-sm text-muted-foreground">{t("Lädt…")}</p>
        ) : (
          <ul className="flex min-w-0 flex-col gap-2">
            {zeilen.map((r) => {
              const istDa = anwesend.has(r.id);
              return (
                <li key={r.id} className="min-w-0">
                  <button
                    type="button"
                    role="checkbox"
                    aria-checked={istDa}
                    disabled={inZukunft}
                    onClick={() => void umschalten(r)}
                    className={cn(
                      "flex min-h-14 w-full min-w-0 items-center gap-3 rounded-lg border px-3 py-2 text-left transition-colors disabled:opacity-60",
                      istDa ? "border-primary bg-primary/10" : "border-border hover:bg-muted",
                    )}
                  >
                    <span
                      aria-hidden="true"
                      className={cn(
                        "flex size-8 shrink-0 items-center justify-center rounded-md border-2",
                        istDa ? "border-primary bg-primary text-primary-foreground" : "border-muted-foreground/40",
                      )}
                    >
                      {istDa && <Check className="size-5" strokeWidth={3} />}
                    </span>
                    <span className="min-w-0">
                      <span className="block font-medium [overflow-wrap:anywhere]">{r.dogName}</span>
                      <span className="block text-sm text-muted-foreground [overflow-wrap:anywhere]">{`${r.firstName} ${r.lastName}`}</span>
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

"use client";

import { useMemo, useRef, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { GpsPoint, GpsTrack, GpsWalkRun } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { TrackMap } from "@/components/tracking/track-map";
import { WalkRunEvaluation } from "@/components/tracking/walk-run-evaluation";
import { ablaufKommentarSpeichern } from "@/components/tracking/walk-run-comment";
import { TrailErgebnisFormular } from "@/components/tracking/trail-angaben";
import { istWischNachUnten, kommentarAenderung } from "@/lib/ablauf-ergebnis";
import { liegezeitMinuten } from "@/lib/faehrten-bild";
import { istMantrailing, stundenText, trailAenderungen } from "@/lib/mantrailing";
import { toast } from "sonner";
import { useSprache, useT } from "@/lib/i18n";
import { TEXTLAENGE } from "@/lib/textlaengen";

/**
 * Das Ergebnis eines Ablaufs, sofort nach dem Speichern.
 *
 * Bisher gab es dafür einen Hinweis, der nach ein paar Sekunden verschwand;
 * die Auswertung steckte zwei bis drei Bildschirme tief im Tagebuch. Dabei ist
 * der Moment direkt nach dem Ablaufen der, in dem man sie wissen will - der
 * Hund steht noch neben einem, und man weiß noch, wo er die Spur verloren hat.
 *
 * Der Aufrufer erhält erst mit dem Schließen die Meldung, dass gespeichert
 * wurde (onClose): Auf der Startseite verschwindet die Fährte mit dem
 * Neuladen aus "Heute gelegt", und mit ihr der Recorder samt diesem Fenster.
 *
 * Der Kommentar wird beim Schließen mitgespeichert, nicht mit einem eigenen
 * Knopf: Wer ihn tippt und dann "Fertig" drückt, erwartet, dass er gespeichert
 * ist. Ebenso beim Mantrailing das Ergebnis (Person gefunden, Anzeigeverhalten)
 * und das Trailalter, das hier aus der Zeit zwischen Legen und Suchen
 * vorgeschlagen wird - die Angaben stehen fest, solange der Hund noch neben
 * einem steht.
 */
export function AblaufErgebnisSheet({
  trackId,
  run,
  laidTrackPoints,
  track,
  onClose,
}: {
  trackId: string;
  run: GpsWalkRun;
  /** Die Spur des Laufs; beim Mantrailing (kind) kommen Ergebnis und Trailalter dazu. */
  track?: GpsTrack;
  /** Die gelegte Fährte für die Karte; ohne sie gibt es keine Karte. */
  laidTrackPoints?: GpsPoint[];
  onClose: () => void;
}) {
  const t = useT();
  const sprache = useSprache();
  const [kommentar, setKommentar] = useState(run.comment ?? "");
  const [speichert, setSpeichert] = useState(false);
  const trail = istMantrailing(track);
  const [gefunden, setGefunden] = useState<boolean | null>(run.personFound ?? null);
  const [anzeige, setAnzeige] = useState<number | null>(run.indication ?? null);
  // Das Alter der Spur, sonst die Zeit vom Ende des Legens bis zum Start dieses Laufs.
  const [alterText, setAlterText] = useState(() => (track ? stundenText(liegezeitMinuten(track, run), sprache) : ""));
  // Scheiterte das Speichern des Kommentars einmal, schließt der nächste
  // Versuch trotzdem - sonst säße man ohne Netz in einem Fenster fest.
  const aufgegeben = useRef(false);
  // Anfangsfokus auf die Kopfzeile statt auf "Fertig": Der Knopf liegt am Ende
  // des scrollbaren Fensters, und das Fokussieren scrollte beim Öffnen dorthin
  // - die Auswertung wäre aus dem Bild. Die Kopfzeile ist oben, und es geht
  // auf iOS auch keine Tastatur auf wie bei einem Eingabefeld.
  const kopfRef = useRef<HTMLDivElement>(null);
  const wischStart = useRef<{ x: number; y: number } | null>(null);

  async function schliessen() {
    if (speichert) return;
    const aenderung = aufgegeben.current ? null : kommentarAenderung(run.comment, kommentar);
    const trailAenderung = aufgegeben.current || !trail || !track ? null : trailAenderungen(track, run, { gefunden, anzeige, alterText });
    if (trailAenderung?.fehler) {
      toast.error(t("Das Trailalter muss eine Zahl in Stunden sein, höchstens 168."));
      return;
    }
    if (aenderung || trailAenderung?.ergebnis || trailAenderung?.trail) {
      setSpeichert(true);
      try {
        if (aenderung) await ablaufKommentarSpeichern(trackId, run.id, kommentar);
        if (trailAenderung?.ergebnis) await api.put(`/api/gps-tracks/${trackId}/walk-runs/${run.id}/trail`, trailAenderung.ergebnis);
        if (trailAenderung?.trail) {
          try {
            await api.put(`/api/gps-tracks/${trackId}/trail`, trailAenderung.trail);
          } catch (err) {
            // Das Alter am Trail ändert nur, wer das Training bearbeiten darf; eine betreuende Trainer:in
            // trägt am Eintrag der Familie das Ergebnis ein (oben gespeichert), das Alter bleibt stehen.
            if (!(err instanceof ApiError && err.status === 404)) throw err;
          }
        }
      } catch (err) {
        aufgegeben.current = true;
        setSpeichert(false);
        toast.error(
          err instanceof ApiError
            ? err.message
            : t("Der Kommentar konnte nicht gespeichert werden. Tippe noch einmal auf „Fertig“, um ohne ihn zu schließen."),
        );
        return;
      }
      setSpeichert(false);
    }
    onClose();
  }

  const ausgewertet = run.evaluatedAt != null && run.avgDeviationMeters != null;
  // Stabile Liste: Ein neues Array je Rendern (jeder Tastendruck im
  // Kommentarfeld) ließe die Karte ihre Daten neu setzen und den Ausschnitt
  // zurückspringen, den man mit dem Finger gewählt hat.
  const laufListe = useMemo(() => [run], [run]);

  return (
    <Sheet open onOpenChange={(offen) => !offen && void schliessen()}>
      <SheetContent
        side="bottom"
        initialFocus={kopfRef}
        className="max-h-[90vh] overflow-y-auto pb-[max(1rem,env(safe-area-inset-bottom))]"
      >
        {/* Wischen nach unten schließt, wie bei einem Fenster dieser Art zu
            erwarten. Nur auf der Kopfzeile: auf der Karte und im Inhalt würde
            dieselbe Bewegung scrollen oder die Karte verschieben. */}
        <SheetHeader
          ref={kopfRef}
          tabIndex={-1}
          className="outline-none"
          onTouchStart={(e) => {
            const berührung = e.touches[0];
            wischStart.current = berührung ? { x: berührung.clientX, y: berührung.clientY } : null;
          }}
          onTouchEnd={(e) => {
            const start = wischStart.current;
            const ende = e.changedTouches[0];
            wischStart.current = null;
            if (start && ende && istWischNachUnten(ende.clientX - start.x, ende.clientY - start.y)) void schliessen();
          }}
        >
          <span aria-hidden className="mx-auto mb-2 h-1 w-10 rounded-full bg-muted-foreground/30" />
          <SheetTitle>{trail ? t("Trail gesucht") : t("Fährte abgelaufen")}</SheetTitle>
        </SheetHeader>
        <div className="flex min-w-0 flex-col gap-4 px-4">
          {ausgewertet ? (
            <WalkRunEvaluation run={run} gross art={track} />
          ) : (
            <p className="text-sm text-muted-foreground">{t("Dieser Ablauf konnte nicht ausgewertet werden.")}</p>
          )}

          {laidTrackPoints && laidTrackPoints.length > 0 && (
            <TrackMap points={laidTrackPoints} walkRuns={laufListe} art={track} />
          )}

          {trail && (
            <TrailErgebnisFormular
              negativ={track?.negativeTrail === true}
              gefunden={gefunden}
              anzeige={anzeige}
              alterText={alterText}
              alterVorgeschlagen={track?.ageMinutes == null}
              onGefunden={setGefunden}
              onAnzeige={setAnzeige}
              onAlter={setAlterText}
            />
          )}

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ablauf-ergebnis-kommentar" className="text-sm text-muted-foreground">
              {t("Kommentar zum Ablauf (optional)")}
            </Label>
            <Input
              id="ablauf-ergebnis-kommentar"
              value={kommentar}
              maxLength={TEXTLAENGE.ablaufKommentar}
              onChange={(e) => setKommentar(e.target.value)}
              className="h-11"
            />
          </div>

          <Button type="button" size="lg" className="h-12 w-full text-base" disabled={speichert} onClick={() => void schliessen()}>
            {t("Fertig")}
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}

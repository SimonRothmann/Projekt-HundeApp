"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import type { AblaufSpur, GpsPoint, GpsWalkPoint, GpsWalkRun } from "@/lib/types";
import { spurArt } from "@/lib/spurart";
import { Button } from "@/components/ui/button";
import { Footprints } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/lib/auth-context";
import {
  ablaufSchluessel,
  sicherungAnzeigen,
  sicherungLesen,
  sicherungLoeschen,
  sicherungSchreiben,
  sicherungSpeichern,
  type AblaufSicherung,
} from "@/lib/aufzeichnung-sicherung";
import { useGpsRecorder } from "@/lib/use-gps-recorder";
import { useMedienMarker } from "@/lib/use-medien-marker";
import { istLuecke, lueckeDauerText, lueckeSekunden } from "@/lib/ablauf-luecken";
import { GENAUIGKEIT_KLASSE } from "@/components/tracking/genauigkeit-klasse";
import { genauigkeitsStufe, GPS_PROFIL_ABLAUF } from "@/lib/gps-profil";
import { TrackMap } from "@/components/tracking/track-map";
import { AblaufErgebnisSheet } from "@/components/tracking/ablauf-ergebnis-sheet";
import { AnsatzHinweis } from "@/components/tracking/ansatz-hinweis";
import { ansatzHinweis, ansatzPunkt } from "@/lib/ansatz";
import { rohePeilung } from "@/lib/karte/peilung";
import { AufzeichnungVollbild } from "@/components/tracking/aufzeichnung-vollbild";
import { speicherErgebnisMelden, UnterbrocheneAufzeichnungKarte } from "@/components/tracking/unterbrochene-aufzeichnung";
import { anschussSignal, primeHapticsAudio, rueckrufSignal, useWalkRunHaptics } from "@/lib/use-walk-run-haptics";
import { anschussFlaeche } from "@/lib/anschussflaeche";
import { MAX_RUECKRUFE, NEUER_RUECKRUF_ZUSTAND, rueckrufFortsetzen, rueckrufLinie, type RueckrufZustand } from "@/lib/rueckruf";

import { useRueckfrage } from "@/components/ui/rueckfrage";
import { useT } from "@/lib/i18n";
// Stabile Leerreferenz für den Idle-Fall (keine Aufzeichnung läuft) - siehe
// onLivePointsChange-Effect. Ein Inline-[] wäre bei jedem Render neu.
const EMPTY_WALK_POINTS: GpsWalkPoint[] = [];
// Ebenso für die Karte: Ein neues [] je Rendern ließe sie ihre Daten neu setzen.
const KEINE_PUNKTE: GpsPoint[] = [];

function toWalkPoint(position: GeolocationPosition): GpsWalkPoint {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    timestamp: new Date(position.timestamp).toISOString(),
    accuracy: position.coords.accuracy,
    // Wird erst serverseitig nach dem Speichern berechnet (GpsTrackEvaluator).
    deviationMeters: null,
  };
}

/**
 * Zeichnet den Ablauf einer bereits gelegten Fährte auf (siehe
 * TODO.md "Fährte erneut ablaufen können"; seit 2026-11-15 nur einmal je Fährte): separate Aufzeichnung, die als
 * eigene Linie zum Vergleich mit der gelegten Fährte auf der Karte erscheint
 * (siehe TrackMap), statt die ursprünglichen Punkte zu überschreiben.
 *
 * Die Live-Punkte werden per `onLivePointsChange` an den Aufrufer gemeldet -
 * so kann er die eine, gemeinsame Karte der Fährte um die frisch entstehende
 * Ablauf-Linie ergänzen, statt eine zweite Karte anzuzeigen.
 */
export function WalkRunRecorder({
  trackId,
  onSaved,
  onLivePointsChange,
  onAufzeichnung,
  laidTrackPoints,
  label,
  hervorgehoben = false,
  track,
}: {
  trackId: string;
  onSaved: () => Promise<void>;
  /** Meldet, ob gerade aufgezeichnet wird - für Aufrufer, die währenddessen nicht verschwinden dürfen. */
  onAufzeichnung?: (laeuft: boolean) => void;
  // (trackId, points): der Aufrufer kann so EINEN stabilen Callback für alle
  // Tracks nutzen, statt pro Track eine neue Funktion zu erzeugen - Letzteres
  // führte zu einer Endlos-Render-Schleife (Effect unten hängt von
  // onLivePointsChange ab).
  onLivePointsChange?: (trackId: string, points: GpsWalkPoint[]) => void;
  // Punkte der gelegten Fährte - Grundlage für die Vibration vor
  // Gegenständen und Abbiegungen. Optional, damit Aufrufer, die keine
  // Legung haben, den Recorder ohne Haptics-Nebeneffekt nutzen können.
  laidTrackPoints?: GpsPoint[];
  /** Beschriftung des Startknopfs - auf der Startseite "Jetzt ablaufen". */
  label?: string;
  /** Startknopf als gefüllter Hauptknopf, auf dem Handy in voller Breite - auf der Startseite für die wartende Fährte. */
  hervorgehoben?: boolean;
  /**
   * Die gelegte Spur selbst. Beim Mantrailing und der Schweißfährte (kind) ändern sich Begriffe, Grenzen und das Ergebnis-Fenster;
   * bei einem doppelblinden Trail sieht das Team die gelegte Linie während der Suche nicht und bekommt keine
   * Vibration an Abbiegungen - beides verriete den Verlauf. Dasselbe gilt für eine Fährte, die verdeckt
   * abgesucht wird (doubleBlind): Linie, Marker und Haptik sind aus; zu sehen ist der Startpunkt, und
   * bis der Ansatz gefunden ist, Entfernung und Richtung dorthin.
   */
  track?: AblaufSpur;
}) {
  const t = useT();
  const rueckfrage = useRueckfrage();
  const art = spurArt(track);
  const trail = art === "trail";
  const schweiss = art === "schweiss";
  const verdeckt = track?.doubleBlind === true;
  const sichtbareLegung = verdeckt ? undefined : laidTrackPoints;
  // Nur die Fährte sucht ihren Ansatz; der Trail beginnt dort, wo die Suche anfängt (unverändert).
  const ansatz = verdeckt && art === "faehrte" ? ansatzPunkt(laidTrackPoints ?? []) : null;
  // Auf der Karte erlaubt ist der Startpunkt der Legung - der Rest nicht.
  const verdeckteKarte = useMemo(() => (ansatz ? [ansatz] : KEINE_PUNKTE), [ansatz]);
  // Die Schweißfährte zeigt verdeckt statt des Startpunkts nur eine Fläche von 30 x 30 m um den Anschuss
  // (leicht versetzt, Paket SF2); keine Entfernung oder Richtung dorthin.
  const anschuss = verdeckt && schweiss ? ansatzPunkt(laidTrackPoints ?? []) : null;
  const flaeche = useMemo(() => (anschuss ? anschussFlaeche(anschuss, trackId) : null), [anschuss, trackId]);
  // Die Linie der Legung für die Rückrufregel: sie hat das Gerät, auch wenn die Karte sie verbirgt.
  const rueckrufLinieDerLegung = useMemo(() => (schweiss ? rueckrufLinie(laidTrackPoints ?? []) : []), [schweiss, laidTrackPoints]);
  const [rueckrufe, setRueckrufe] = useState(0);
  const rueckrufRef = useRef<RueckrufZustand>(NEUER_RUECKRUF_ZUSTAND);
  // Bis zu dieser Punktzahl (beim Fortsetzen einer Sicherung) wird nichts mehr gemeldet.
  const ruhigBisRef = useRef(0);
  const { isRecording, points, setPoints, currentAccuracy, start: startRecording, stop } = useGpsRecorder(toWalkPoint, GPS_PROFIL_ABLAUF);
  // Kommentar zu diesem Ablauf-Versuch. Getippt wird er nicht mehr während der
  // Aufzeichnung (die Tastatur über der Karte war ein Taschen-Ziel), sondern
  // im Ergebnis-Fenster danach (AblaufErgebnisSheet) bzw. später im Tagebuch.
  // Hier bleibt nur der Wert einer alten, unterbrochenen Sicherung, damit er
  // beim Fortsetzen/Speichern nicht verloren geht.
  const [comment, setComment] = useState("");
  const { user } = useAuth();
  const schluessel = ablaufSchluessel(trackId);
  // Ein nicht beendeter Ablauf dieser Fährte - siehe FahrteRecorder, dort
  // dasselbe für das Legen.
  const [unterbrochen, setUnterbrochen] = useState<AblaufSicherung | null>(null);
  const [speichert, setSpeichert] = useState(false);
  const laufendRef = useRef<{ begonnen: number; seite: string; ablaufId: string } | null>(null);
  // Der gerade gespeicherte, ausgewertete Ablauf: solange er gesetzt ist, steht
  // das Ergebnis-Fenster. onSaved kommt erst mit dem Schließen (siehe
  // AblaufErgebnisSheet).
  const [ergebnis, setErgebnis] = useState<GpsWalkRun | null>(null);

  useEffect(() => sicherungAnzeigen(schluessel), [schluessel]);

  useEffect(() => {
    if (!user || laufendRef.current) return;
    const gesichert = sicherungLesen(schluessel, user.userId);
    // Gesicherter Stand aus dem localStorage (externe Quelle), nur beim Mount.
    setUnterbrochen(gesichert?.art === "ablauf" ? gesichert : null);
  }, [schluessel, user]);

  useEffect(() => {
    const laufend = laufendRef.current;
    if (!isRecording || !laufend || !user) return;
    sicherungSchreiben({ art: "ablauf", userId: user.userId, trackId, kommentar: comment, points, ...laufend });
  }, [isRecording, points, comment, user, trackId]);

  function aufzeichnen(fortsetzen?: AblaufSicherung) {
    // Muss aus dem Klick-Handler synchron passieren, sonst bleibt der
    // AudioContext auf iOS suspended und die späteren Alarmtöne bleiben stumm.
    primeHapticsAudio();
    medien.starten();
    laufendRef.current = fortsetzen
      ? { begonnen: fortsetzen.begonnen, seite: fortsetzen.seite, ablaufId: fortsetzen.ablaufId ?? crypto.randomUUID() }
      : // Die Kennung des künftigen Ablaufs: Jedes Speichern derselben Aufzeichnung ist dieselbe Anfrage.
        { begonnen: Date.now(), seite: window.location.pathname, ablaufId: crypto.randomUUID() };
    if (fortsetzen) {
      setComment(fortsetzen.kommentar);
      setUnterbrochen(null);
    }
    rueckrufRef.current = NEUER_RUECKRUF_ZUSTAND;
    ruhigBisRef.current = fortsetzen?.points.length ?? 0;
    startRecording(fortsetzen?.points);
  }

  useEffect(() => {
    onAufzeichnung?.(isRecording);
  }, [isRecording, onAufzeichnung]);

  useEffect(() => {
    // EMPTY_WALK_POINTS ist eine stabile Referenz (Modul-Konstante), damit der
    // Idle-Fall NICHT bei jedem Render ein neues [] meldet und so einen
    // Update-Loop im Aufrufer auslöst.
    onLivePointsChange?.(trackId, isRecording ? points : EMPTY_WALK_POINTS);
  }, [points, isRecording, onLivePointsChange, trackId]);

  // Titel und Zeile stehen fest (nicht bei jedem Punkt neu): Die Tonspur hält nur die Seite wach.
  const medien = useMedienMarker(isRecording, {
    titel: trail ? t("Trail suchen") : schweiss ? t("Schweißfährte arbeiten") : t("Fährte ablaufen"),
    zeile: t("Aufzeichnung läuft"),
  });

  // Einmal je Lücke ein Hinweis, sobald nach ihr wieder ein Punkt kommt. Beim Fortsetzen einer Sicherung
  // zählt die Pause bis zum ersten neuen Punkt ebenso als Lücke (gleiche Regel wie beim Zeichnen).
  const lueckenGemeldet = useRef(-1);
  useEffect(() => {
    if (!isRecording) {
      lueckenGemeldet.current = -1;
      return;
    }
    const n = points.length;
    // Beim Start (auch beim Fortsetzen) gelten die vorhandenen Punkte als gesehen.
    if (lueckenGemeldet.current < 0) lueckenGemeldet.current = n;
    if (n < 2 || n <= lueckenGemeldet.current) return;
    lueckenGemeldet.current = n;
    const [davor, jetzt] = [points[n - 2], points[n - 1]];
    if (istLuecke(davor, jetzt))
      toast.info(
        t("{dauer} ohne GPS (Bildschirm aus?) – dieses Stück ist gestrichelt.", {
          dauer: lueckeDauerText(lueckeSekunden(davor, jetzt), (min) => t("{n} Min.", { n: min })),
        }),
      );
  }, [isRecording, points, t]);

  // Rückrufe live (nur Schweißfährte, auch verdeckt): dieselbe Regel wie in der Auswertung (lib/rueckruf.ts).
  useEffect(() => {
    if (!schweiss) return;
    if (!isRecording) {
      rueckrufRef.current = NEUER_RUECKRUF_ZUSTAND;
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setRueckrufe(0);
      return;
    }
    const vorher = rueckrufRef.current;
    const neu = rueckrufFortsetzen(vorher, rueckrufLinieDerLegung, points);
    rueckrufRef.current = neu;
    const hoerbar = neu.verarbeitet > ruhigBisRef.current;
    if (hoerbar && verdeckt && neu.anschussGefunden && !vorher.anschussGefunden) {
      anschussSignal();
      toast.success(t("Anschuss gefunden"));
    }
    if (neu.stellen.length !== vorher.stellen.length) {
      setRueckrufe(neu.stellen.length);
      if (hoerbar && neu.stellen.length > vorher.stellen.length) {
        rueckrufSignal();
        const nr = neu.stellen.length;
        const text = t("Rückruf {n}: Ihr seid von der Fährte abgekommen.", { n: nr });
        toast.warning(nr > MAX_RUECKRUFE ? `${text} ${t("– damit wäre die Prüfung nicht bestanden")}` : text);
      }
    }
  }, [schweiss, verdeckt, isRecording, points, rueckrufLinieDerLegung, t]);

  const hinweis = ansatz ? ansatzHinweis(points, ansatz) : null;

  // Vibriere 10 Schritte (~8 m) vor jedem markierten Gegenstand und vor
  // jeder erkannten Abbiegung der Legung. sessionKey = trackId+isRecording:
  // beim Neustart der Aufnahme wird die "schon-vibriert"-Menge zurückgesetzt.
  // Nicht bei der Schweißfährte: Die Vibration vor Wundbett und Verweiserpunkt verriete, wo der Hund verweisen soll.
  useWalkRunHaptics(
    schweiss ? undefined : sichtbareLegung,
    isRecording && points.length > 0 ? points[points.length - 1] : null,
    `${trackId}:${isRecording ? "on" : "off"}`,
  );

  /**
   * Ablauf verwerfen - nur nach Rückfrage, wie beim Legen. Ein
   * versehentlicher Abbruch mitten im Suchen kostet den ganzen Versuch.
   */
  async function abbrechen() {
    if (!(await rueckfrage({ titel: t("Ablauf verwerfen?"), text: t("Die bisher aufgezeichnete Strecke geht verloren."), bestaetigen: t("Verwerfen"), gefaehrlich: true }))) return;
    stop();
    setPoints([]);
    laufendRef.current = null;
    sicherungLoeschen(schluessel);
  }

  async function stopRecording() {
    stop();
    const laufend = laufendRef.current;
    laufendRef.current = null;

    if (points.length === 0) {
      sicherungLoeschen(schluessel);
      toast.error(t("Keine GPS-Punkte aufgezeichnet."));
      return;
    }

    const sicherung: AblaufSicherung = {
      art: "ablauf",
      userId: user?.userId ?? "",
      trackId,
      kommentar: comment,
      points,
      ablaufId: laufend?.ablaufId ?? crypto.randomUUID(),
      begonnen: laufend?.begonnen ?? Date.now(),
      seite: laufend?.seite ?? window.location.pathname,
    };
    if (user) sicherungSchreiben(sicherung);
    await speichern(sicherung);
  }

  // Ohne Netz landet der Ablauf in der Warteschlange - siehe
  // PRODUCT_REQUIREMENTS.md "Offline": GPS speichern ohne Internet. Scheitert
  // das Speichern, bleibt er als unterbrochener Ablauf stehen.
  async function speichern(sicherung: AblaufSicherung) {
    setSpeichert(true);
    const gespeichert = await sicherungSpeichern(sicherung, t("Ablauf-Versuch"));
    setSpeichert(false);
    // Mit Ergebnis-Fenster braucht es den Hinweis nicht: Das Fenster ist die
    // Bestätigung. Offline gibt es nichts auszuwerten - dort bleibt der Hinweis.
    const mitErgebnis = gespeichert.ausgang === "gespeichert" && gespeichert.ablauf != null;
    if (!mitErgebnis) speicherErgebnisMelden(gespeichert, t, "ablauf");
    setPoints([]);
    setComment("");

    if (gespeichert.ausgang === "fehler") {
      setUnterbrochen(user ? sicherung : null);
      return;
    }
    setUnterbrochen(null);
    if (gespeichert.ausgang === "gespeichert") {
      if (gespeichert.ablauf) setErgebnis(gespeichert.ablauf);
      else await onSaved();
    }
  }

  async function ergebnisSchliessen() {
    setErgebnis(null);
    await onSaved();
  }

  // Auch im Ruhezustand des Recorders gerendert (Knopf bzw. Hinweis auf eine
  // unterbrochene Aufzeichnung): Das Fenster erscheint, wenn die Aufnahme
  // gerade beendet wurde.
  const ergebnisFenster = ergebnis && (
    <AblaufErgebnisSheet
      trackId={trackId}
      run={ergebnis}
      laidTrackPoints={laidTrackPoints}
      track={track}
      onClose={() => void ergebnisSchliessen()}
    />
  );

  if (!isRecording && unterbrochen) {
    return (
      <>
        <UnterbrocheneAufzeichnungKarte
          sicherung={unterbrochen}
          beschaeftigt={speichert}
          onFortsetzen={() => aufzeichnen(unterbrochen)}
          onSpeichern={() => speichern(unterbrochen)}
          onVerwerfen={() => {
            sicherungLoeschen(schluessel);
            setUnterbrochen(null);
          }}
        />
        {ergebnisFenster}
      </>
    );
  }

  if (!isRecording) {
    return (
      <>
        <Button
          size={hervorgehoben ? "lg" : "sm"}
          variant={hervorgehoben ? "default" : "outline"}
          className={hervorgehoben ? "w-full coarse:min-h-11 sm:w-auto" : undefined}
          disabled={speichert}
          onClick={() => aufzeichnen()}
        >
          <Footprints className="size-4" />
          {label ?? (trail ? t("Trail suchen") : schweiss ? t("Fährte arbeiten") : t("Fährte ablaufen"))}
        </Button>
        {ergebnisFenster}
      </>
    );
  }

  // Wie beim Legen im Vollbild: Der Ablauf findet draußen statt, nicht am
  // Schreibtisch. Die Karte trägt der Recorder hier selbst bei - die gelegte
  // Fährte hat er als Prop, die entstehende Linie sind seine eigenen Punkte.
  return (
    <AufzeichnungVollbild
      titel={trail ? t("Trail suchen") : schweiss ? t("Schweißfährte arbeiten") : t("Fährte ablaufen")}
      status={
        <>
          {t("{punkte} Punkte", { punkte: points.length })}
          {currentAccuracy !== null && (
            <>
              {" · "}
              <span
                className={GENAUIGKEIT_KLASSE[genauigkeitsStufe(currentAccuracy, GPS_PROFIL_ABLAUF)]}
                title={t("GPS-Genauigkeit (Radius des Fehlerkreises).")}
              >
                ±{Math.round(currentAccuracy)} m
              </span>
            </>
          )}
          {schweiss && rueckrufe > 0 && (
            <>
              {" · "}
              <span className="font-medium text-destructive">{t("Rückrufe: {n}", { n: rueckrufe })}</span>
            </>
          )}
        </>
      }
      abschlussLabel={t("Ablauf beenden")}
      abschlussFrage={t("Ablauf beenden und speichern?")}
      onAbschluss={stopRecording}
      onAbbrechen={abbrechen}
    >
      <TrackMap points={sichtbareLegung ?? verdeckteKarte} liveWalkRunPoints={points} art={track} flaeche={flaeche} fill />
      {hinweis && <AnsatzHinweis hinweis={hinweis} laufrichtung={rohePeilung(points)} />}
    </AufzeichnungVollbild>
  );
}

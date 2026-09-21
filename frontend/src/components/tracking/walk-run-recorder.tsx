"use client";

import { useEffect, useRef, useState } from "react";
import type { GpsPoint, GpsWalkPoint } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
import { TrackMap } from "@/components/tracking/track-map";
import { AufzeichnungVollbild } from "@/components/tracking/aufzeichnung-vollbild";
import { speicherErgebnisMelden, UnterbrocheneAufzeichnungKarte } from "@/components/tracking/unterbrochene-aufzeichnung";
import { primeHapticsAudio, useWalkRunHaptics } from "@/lib/use-walk-run-haptics";

import { useT } from "@/lib/i18n";
// Stabile Leerreferenz für den Idle-Fall (keine Aufzeichnung läuft) - siehe
// onLivePointsChange-Effect. Ein Inline-[] wäre bei jedem Render neu.
const EMPTY_WALK_POINTS: GpsWalkPoint[] = [];

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
 * Zeichnet einen Ablauf-Versuch für eine bereits gelegte Fährte auf (siehe
 * TODO.md "Fährte erneut ablaufen können"): separate Aufzeichnung, die als
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
  laidTrackPoints,
  label,
}: {
  trackId: string;
  onSaved: () => Promise<void>;
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
}) {
  const t = useT();
  const { isRecording, points, setPoints, currentAccuracy, start: startRecording, stop } = useGpsRecorder(toWalkPoint);
  // Optionaler Kommentar zu diesem Ablauf-Versuch (z.B. "bei Regen", "Hund
  // hat an Winkel 2 verloren") - wird beim Stoppen mitgespeichert.
  const [comment, setComment] = useState("");
  const { user } = useAuth();
  const schluessel = ablaufSchluessel(trackId);
  // Ein nicht beendeter Ablauf dieser Fährte - siehe FahrteRecorder, dort
  // dasselbe für das Legen.
  const [unterbrochen, setUnterbrochen] = useState<AblaufSicherung | null>(null);
  const [speichert, setSpeichert] = useState(false);
  const laufendRef = useRef<{ begonnen: number; seite: string } | null>(null);

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
    laufendRef.current = fortsetzen
      ? { begonnen: fortsetzen.begonnen, seite: fortsetzen.seite }
      : { begonnen: Date.now(), seite: window.location.pathname };
    if (fortsetzen) {
      setComment(fortsetzen.kommentar);
      setUnterbrochen(null);
    }
    startRecording(fortsetzen?.points);
  }

  useEffect(() => {
    // EMPTY_WALK_POINTS ist eine stabile Referenz (Modul-Konstante), damit der
    // Idle-Fall NICHT bei jedem Render ein neues [] meldet und so einen
    // Update-Loop im Aufrufer auslöst.
    onLivePointsChange?.(trackId, isRecording ? points : EMPTY_WALK_POINTS);
  }, [points, isRecording, onLivePointsChange, trackId]);

  // Vibriere 10 Schritte (~8 m) vor jedem markierten Gegenstand und vor
  // jeder erkannten Abbiegung der Legung. sessionKey = trackId+isRecording:
  // beim Neustart der Aufnahme wird die "schon-vibriert"-Menge zurückgesetzt.
  useWalkRunHaptics(
    laidTrackPoints,
    isRecording && points.length > 0 ? points[points.length - 1] : null,
    `${trackId}:${isRecording ? "on" : "off"}`,
  );

  /**
   * Ablauf verwerfen - nur nach Rückfrage, wie beim Legen. Ein
   * versehentlicher Abbruch mitten im Suchen kostet den ganzen Versuch.
   */
  function abbrechen() {
    if (!confirm(t("Ablauf verwerfen? Die bisher aufgezeichnete Strecke geht verloren."))) return;
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
    const ergebnis = await sicherungSpeichern(sicherung, t("Ablauf-Versuch"));
    setSpeichert(false);
    speicherErgebnisMelden(ergebnis, t, "ablauf");
    setPoints([]);
    setComment("");

    if (ergebnis.ausgang === "fehler") {
      setUnterbrochen(user ? sicherung : null);
      return;
    }
    setUnterbrochen(null);
    if (ergebnis.ausgang === "gespeichert") await onSaved();
  }

  if (!isRecording && unterbrochen) {
    return (
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
    );
  }

  if (!isRecording) {
    return (
      <Button size="sm" variant="outline" disabled={speichert} onClick={() => aufzeichnen()}>
        <Footprints className="size-4" />
        {label ?? t("Fährte erneut ablaufen")}
      </Button>
    );
  }

  // Wie beim Legen im Vollbild: Der Ablauf findet draußen statt, nicht am
  // Schreibtisch. Die Karte trägt der Recorder hier selbst bei - die gelegte
  // Fährte hat er als Prop, die entstehende Linie sind seine eigenen Punkte.
  return (
    <AufzeichnungVollbild
      titel={t("Fährte ablaufen")}
      status={
        <>
          {points.length} Punkte
          {currentAccuracy !== null && (
            <>
              {" · "}
              <span
                className={
                  currentAccuracy <= 10 ? "text-green-600" : currentAccuracy <= 25 ? "text-yellow-600" : "text-red-600"
                }
                title="GPS-Genauigkeit (Radius des Fehlerkreises)."
              >
                ±{Math.round(currentAccuracy)} m
              </span>
            </>
          )}
        </>
      }
      aktionen={
        <Input
          placeholder={t("Kommentar zum Ablauf (optional)")}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
        />
      }
      abschlussLabel={t("Ablauf beenden")}
      abschlussFrage={t("Ablauf beenden und speichern?")}
      onAbschluss={stopRecording}
      onAbbrechen={abbrechen}
    >
      <TrackMap points={laidTrackPoints ?? []} liveWalkRunPoints={points} fill />
    </AufzeichnungVollbild>
  );
}

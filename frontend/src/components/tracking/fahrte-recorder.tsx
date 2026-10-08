"use client";

import { useEffect, useRef, useState } from "react";
import type { GpsMarkerType, GpsPoint, GpsTrack } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Cookie, MapPin, MapPinPlus, Package, Waypoints } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/lib/auth-context";
import {
  faehrtenSchluessel,
  sicherungAnzeigen,
  sicherungLesen,
  sicherungLoeschen,
  sicherungSchreiben,
  sicherungSpeichern,
  type FaehrtenSicherung,
} from "@/lib/aufzeichnung-sicherung";
import { UNTERGRUENDE, untergrundUmschalten } from "@/lib/untergrund";
import { cn } from "@/lib/utils";
import { useGpsRecorder } from "@/lib/use-gps-recorder";
import { TrackMap } from "@/components/tracking/track-map";
import { AufzeichnungVollbild } from "@/components/tracking/aufzeichnung-vollbild";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";
import { speicherErgebnisMelden, UnterbrocheneAufzeichnungKarte } from "@/components/tracking/unterbrochene-aufzeichnung";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
function toAutomaticPoint(position: GeolocationPosition): GpsPoint {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    timestamp: new Date(position.timestamp).toISOString(),
    accuracy: position.coords.accuracy,
    pointType: 0,
    label: null,
    markerType: 0,
  };
}

// Auswahl beim Markieren: entscheidet, wie ein Halt an dieser Stelle später
// gewertet wird (Gegenstand = Verweisen erwünscht, Leckerlipot/Verleitung =
// erklärt und neutral) - siehe GpsTrackEvaluator im Backend.
const MARKER_TYPES = [
  { value: 0, label: uebersetzbar("Gegenstand"), icon: Package },
  { value: 1, label: uebersetzbar("Leckerlipot"), icon: Cookie },
  { value: 2, label: uebersetzbar("Verleitung"), icon: Waypoints },
  { value: 3, label: uebersetzbar("Sonstiges"), icon: MapPinPlus },
] as const;

/**
 * Direkter Einstiegspunkt für die GPS-Fährtenaufzeichnung, ohne vorher ein
 * vollständiges Trainingstagebuch-Training mit bewerteten Übungen anlegen
 * zu müssen. Gespeichert wird mit EINER Anfrage (Hund, Datum, Fährte); der
 * Server hängt die Fährte an die Einheit des Tages - siehe stopRecording.
 */
export function FahrteRecorder({
  dogId,
  onSaved,
  onAufzeichnung,
}: {
  dogId: string;
  onSaved: () => Promise<void>;
  /**
   * Meldet, ob gerade aufgezeichnet wird. Der Recorder steht in einem Fenster
   * der Hundeseite, das sich währenddessen nicht schließen darf: Mit dem Fenster
   * ginge die laufende Aufzeichnung verloren.
   */
  onAufzeichnung?: (laeuft: boolean) => void;
}) {
  const t = useT();
  const { isRecording, points, setPoints, currentAccuracy, start, stop, markPoint } = useGpsRecorder(
    toAutomaticPoint,
    // Fährte braucht höhere Präzision als ein normaler Spaziergang: Erstaufnahme
    // und Wiederholung sollen deckungsgleich sein. Schwellwert 8 m verwirft
    // Kaltstart-Schlechtpunkte früh. Ohne Netz (kein aGPS) konvergiert der
    // Chip aber oft nur bis 10-20 m - dann lockert sich der Filter nach 15 s
    // schrittweise bis 20 m, statt die Aufzeichnung dauerhaft leer zu lassen
    // (der Kalman-Filter gewichtet schlechtere Messungen ohnehin schwächer).
    { maxAccuracyMeters: 8, relaxedMaxAccuracyMeters: 20, kalman: true },
  );
  // Auch der Ablauf der eben gelegten Fährte (Karte "Fährte gespeichert") ist
  // eine Aufzeichnung, die das Fenster nicht überleben würde.
  const [ablaufLaeuft, setAblaufLaeuft] = useState(false);
  useEffect(() => {
    onAufzeichnung?.(isRecording || ablaufLaeuft);
  }, [isRecording, ablaufLaeuft, onAufzeichnung]);
  const [untergrund, setUntergrund] = useState<string[]>([]);
  const [isMarking, setIsMarking] = useState(false);
  // Die eben gelegte Fährte: bietet direkt danach das Ablaufen an, statt dass
  // man sie im Tagebuch wieder heraussuchen muss. Offline gespeichert gibt es
  // noch keine Id - dann erinnert später die Startseite ("Heute gelegt").
  const [gelegt, setGelegt] = useState<GpsTrack | null>(null);
  const { user } = useAuth();
  const schluessel = faehrtenSchluessel(dogId);
  // Eine nicht beendete Aufzeichnung dieses Hundes (siehe
  // lib/aufzeichnung-sicherung.ts) - statt des Startknopfs angezeigt.
  const [unterbrochen, setUnterbrochen] = useState<FaehrtenSicherung | null>(null);
  const [speichert, setSpeichert] = useState(false);
  // Was die Sicherung über die Punkte hinaus braucht: die schon beim Start
  // vergebene Id der Fährte, Beginn und Seite. Bleibt beim Fortsetzen gleich.
  const laufendRef = useRef<{ trackId: string; begonnen: number; seite: string } | null>(null);

  // Angemeldet beim Hinweis der App-Hülle: Was dieser Recorder zeigt, muss
  // der nicht noch einmal zeigen.
  useEffect(() => sicherungAnzeigen(schluessel), [schluessel]);

  useEffect(() => {
    // Während einer Aufzeichnung ist die Sicherung die laufende, keine
    // unterbrochene.
    if (!user || laufendRef.current) return;
    const gesichert = sicherungLesen(schluessel, user.userId);
    // Gesicherter Stand aus dem localStorage (externe Quelle), nur beim Mount.
    setUnterbrochen(gesichert?.art === "faehrte" ? gesichert : null);
  }, [schluessel, user]);

  // Jeder neue Punkt, jeder Marker und jede Änderung am Untergrund geht
  // sofort in die Sicherung.
  useEffect(() => {
    const laufend = laufendRef.current;
    if (!isRecording || !laufend || !user) return;
    sicherungSchreiben({ art: "faehrte", userId: user.userId, dogId, untergrund, points, ...laufend });
  }, [isRecording, points, untergrund, user, dogId]);

  function startRecording() {
    laufendRef.current = { trackId: crypto.randomUUID(), begonnen: Date.now(), seite: window.location.pathname };
    start();
  }

  function fortsetzen(gesichert: FaehrtenSicherung) {
    laufendRef.current = { trackId: gesichert.trackId, begonnen: gesichert.begonnen, seite: gesichert.seite };
    setUntergrund(gesichert.untergrund);
    setUnterbrochen(null);
    start(gesichert.points);
  }

  /**
   * Setzt einen Marker der gewählten Art an der aktuellen Position.
   *
   * Der Typ kommt als Argument, nicht mehr aus einem Auswahlfeld: Vorher
   * waren es zwei Bedienschritte - Art im Aufklappmenü wählen, dann "Punkt
   * setzen" drücken. Draußen, im Stehen, mit Hund an der Leine ist das eine
   * Hand zu viel. Jetzt ist jeder Typ ein eigener Knopf, ein Tipp genügt.
   */
  function markObject(typ: GpsMarkerType) {
    setIsMarking(true);
    markPoint(
      (point) => {
        setPoints((prev) => [...prev, { ...point, pointType: 1, label: null, markerType: typ }]);
        setIsMarking(false);
        toast.success(t("{art} markiert.", { art: t(MARKER_TYPES.find((m) => m.value === typ)?.label ?? uebersetzbar("Marker")) }));
      },
      () => setIsMarking(false),
    );
  }

  /**
   * Aufzeichnung verwerfen - nur nach Rückfrage.
   *
   * Im Vollbild gibt es bewusst keinen beiläufigen Ausweg: Eine gelegte
   * Fährte lässt sich nicht wiederholen, ein versehentlicher Abbruch wäre
   * der Verlust des ganzen Trainings.
   */
  function abbrechen() {
    if (!confirm(t("Aufzeichnung verwerfen? Die bisher aufgezeichnete Fährte geht verloren."))) return;
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

    // Ohne Anmeldung (sollte hier nicht vorkommen) gibt es keine Sicherung;
    // gespeichert wird trotzdem.
    const sicherung: FaehrtenSicherung = {
      art: "faehrte",
      userId: user?.userId ?? "",
      dogId,
      untergrund,
      points,
      trackId: laufend?.trackId ?? crypto.randomUUID(),
      begonnen: laufend?.begonnen ?? Date.now(),
      seite: laufend?.seite ?? window.location.pathname,
    };
    // Den letzten Stand festhalten, bevor die Anfrage losgeht: Scheitert sie,
    // ist genau das hier der Stand, den "So speichern" später schickt.
    if (user) sicherungSchreiben(sicherung);
    await speichern(sicherung);
  }

  /**
   * Speichert eine Fährte - frisch beendet oder aus einer Sicherung.
   *
   * Klappt es nicht, bleibt die Sicherung stehen und erscheint als
   * unterbrochene Fährte zum erneuten Versuch. Vorher gingen die Punkte in
   * diesem Fall nach der Fehlermeldung verloren.
   */
  async function speichern(sicherung: FaehrtenSicherung) {
    setSpeichert(true);
    const ergebnis = await sicherungSpeichern(sicherung, t("Fährte"));
    setSpeichert(false);
    speicherErgebnisMelden(ergebnis, t, "faehrte");
    setPoints([]);
    setUntergrund([]);

    if (ergebnis.ausgang === "fehler") {
      setUnterbrochen(user ? sicherung : null);
      return;
    }
    setUnterbrochen(null);
    if (ergebnis.ausgang === "gespeichert") setGelegt(ergebnis.faehrte);
    await onSaved();
  }

  const autoPunkte = points.filter((p) => p.pointType !== 1).length;
  const markerPunkte = points.filter((p) => p.pointType === 1).length;

  // Nicht am Aufzeichnen: Untergrund wählen und starten. Die Hundeseite zeigt
  // das im Fenster "Fährte legen" (Titel und Rahmen kommen von dort).
  if (!isRecording) {
    return (
      <div className="flex flex-col gap-3">
        {unterbrochen && (
          <UnterbrocheneAufzeichnungKarte
            sicherung={unterbrochen}
            beschaeftigt={speichert}
            onFortsetzen={() => fortsetzen(unterbrochen)}
            onSpeichern={() => speichern(unterbrochen)}
            onVerwerfen={() => {
              sicherungLoeschen(schluessel);
              setUnterbrochen(null);
            }}
          />
        )}
        {gelegt && (
          <div className="flex flex-col gap-2 rounded-lg border border-surface-border bg-surface p-3">
            <p className="text-sm">
              <span className="font-medium">{t("Fährte gespeichert.")}</span>{" "}
              {t("Ablaufen, sobald sie alt genug ist - die Startseite erinnert dich daran.")}
            </p>
            <div className="flex flex-wrap items-center gap-2">
              <WalkRunRecorder
                trackId={gelegt.id}
                laidTrackPoints={gelegt.points}
                label={t("Jetzt ablaufen")}
                onAufzeichnung={setAblaufLaeuft}
                onSaved={async () => {
                  setGelegt(null);
                  await onSaved();
                }}
              />
              <Button type="button" size="sm" variant="ghost" onClick={() => setGelegt(null)}>
                {t("Später")}
              </Button>
            </div>
          </div>
        )}
        {!unterbrochen && (
          <>
            {/* Antippen statt Tippen: Getippter Text vor dem Start löste auf
                dem iPhone beim Legen "Eingabe widerrufen" aus (siehe
                lib/untergrund.ts). Aussehen wie die Verfassung
                (ConditionPicker), Trefferfläche aber 44 px - beim Fährtelegen
                oft mit Handschuh. */}
            <div className="flex flex-col gap-2">
              <span id="fahrte-untergrund" className="text-sm font-medium">
                {t("Untergrund (optional)")}{" "}
                <span className="font-normal text-muted-foreground">· {t("mehrere möglich")}</span>
              </span>
              <div role="group" aria-labelledby="fahrte-untergrund" className="flex flex-wrap gap-1.5">
                {UNTERGRUENDE.map((u) => {
                  const aktiv = untergrund.includes(u);
                  return (
                    <button
                      key={u}
                      type="button"
                      aria-pressed={aktiv}
                      onClick={() => setUntergrund((vorher) => untergrundUmschalten(vorher, u))}
                      className={cn(
                        "rounded-full border px-3 py-1.5 text-sm transition-colors coarse:min-h-11",
                        aktiv
                          ? "border-primary bg-primary/15 text-primary-text"
                          : "border-input text-muted-foreground hover:border-primary/50 hover:bg-accent/30",
                      )}
                    >
                      {t(u)}
                    </button>
                  );
                })}
              </div>
            </div>
            <Button onClick={startRecording} disabled={speichert} className="w-full coarse:min-h-11">
              <MapPin className="size-4" />
              {t("Legen starten")}
            </Button>
          </>
        )}
      </div>
    );
  }

  return (
    <AufzeichnungVollbild
      titel={t("Fährte legen")}
      status={
        <>
          {t("{punkte} Punkte · {marker} Marker", { punkte: autoPunkte, marker: markerPunkte })}
          {currentAccuracy !== null && (
            <>
              {" · "}
              <span
                // Draußen im Sonnenlicht gelesen: 700er-Töne im Hellen (>= 5:1 auf
                // Weiß), halbfett, damit "±6 m" nicht in der Leiste untergeht.
                className={
                  currentAccuracy <= 8
                    ? "font-semibold text-green-700 dark:text-green-400"
                    : currentAccuracy <= 15
                      ? "font-semibold text-amber-700 dark:text-amber-400"
                      : "font-semibold text-destructive"
                }
                title={t("GPS-Genauigkeit (Fehlerkreis-Radius). Punkte ungenauer als 8 m werden verworfen; findet das GPS länger keinen genauen Fix (z.B. ohne Netz), lockert sich der Filter schrittweise bis 20 m.")}
              >
                ±{Math.round(currentAccuracy)} m
              </span>
            </>
          )}
        </>
      }
      aktionen={
        // Ein Knopf je Markerart, nebeneinander im Daumenbereich. h-16 statt
        // der üblichen Knopfhöhe: Das Ziel muss mit einer Hand, im Stehen,
        // ohne Hinsehen zu treffen sein. Die Beschriftung bricht um statt
        // abgeschnitten zu werden ("Leckerlipot" muss man draußen ganz lesen
        // können); min-h-16 hält die Tippfläche bei jedem Umbruch >= 44 px.
        <div className="grid grid-cols-4 gap-2">
          {MARKER_TYPES.map((markerArt) => (
            <Button
              key={markerArt.value}
              type="button"
              variant="outline"
              disabled={isMarking}
              onClick={() => markObject(markerArt.value)}
              className="h-auto min-h-16 flex-col gap-1 px-1 py-1.5 text-xs leading-tight whitespace-normal"
            >
              <markerArt.icon className="size-5 shrink-0" />
              <span className="w-full min-w-0 text-center break-words">{t(markerArt.label)}</span>
            </Button>
          ))}
        </div>
      }
      abschlussLabel={t("Legen beenden")}
      abschlussFrage={t("Legen beenden und die Fährte speichern?")}
      onAbschluss={stopRecording}
      onAbbrechen={abbrechen}
    >
      <TrackMap points={points} live fill />
    </AufzeichnungVollbild>
  );
}

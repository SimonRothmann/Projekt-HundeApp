"use client";

import { useEffect, useRef, useState } from "react";
import type { GpsMarkerType, GpsPoint, GpsTrack } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Chip } from "@/components/ui/chip";
import { Cookie, MapPin, MapPinPlus, Package, Undo2, Waypoints } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/lib/auth-context";
import {
  faehrtenSchluessel,
  sicherungAnzeigen,
  sicherungLesen,
  sicherungLoeschen,
  sicherungSchreiben,
  sicherungSpeichern,
  markerEntfernen,
  type FaehrtenSicherung,
} from "@/lib/aufzeichnung-sicherung";
import { BODENZUSTAENDE, TRAIL_UNTERGRUENDE, UNTERGRUENDE, untergrundUmschalten } from "@/lib/untergrund";
import { automatischePunkte, markerPunkte, PUNKT_AUTOMATISCH, PUNKT_MARKER } from "@/lib/geo";
import { ABLAUF_FRIST_STUNDEN_TRAIL } from "@/lib/faehrte";
import { LEERE_TRAIL_ANGABEN, type TrailAngabenVorLegen } from "@/lib/mantrailing";
import { TrailLegenFormular } from "@/components/tracking/trail-angaben";
import { cn } from "@/lib/utils";
import { useGpsRecorder } from "@/lib/use-gps-recorder";
import { GENAUIGKEIT_KLASSE } from "@/components/tracking/genauigkeit-klasse";
import { genauigkeitsStufe, GPS_PROFIL_LEGEN } from "@/lib/gps-profil";
import { TrackMap } from "@/components/tracking/track-map";
import { AufzeichnungVollbild } from "@/components/tracking/aufzeichnung-vollbild";
import { WalkRunRecorder } from "@/components/tracking/walk-run-recorder";
import { GegenstandsMaterial } from "@/components/tracking/gegenstaende";
import { speicherErgebnisMelden, UnterbrocheneAufzeichnungKarte } from "@/components/tracking/unterbrochene-aufzeichnung";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
function toAutomaticPoint(position: GeolocationPosition): GpsPoint {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    timestamp: new Date(position.timestamp).toISOString(),
    accuracy: position.coords.accuracy,
    pointType: PUNKT_AUTOMATISCH,
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

// Beim Mantrailing gibt es keine Gegenstände, Leckerlipots und Verleitungen: Es bleibt ein
// allgemeiner Marker (z. B. für einen Richtungswechsel). Er zählt wie "Sonstiges" und wirkt
// weder auf Gegenstände noch auf erklärte Halte.
const TRAIL_MARKER = [{ value: 3, label: uebersetzbar("Marker"), icon: MapPinPlus }] as const;

// So lange nach dem Setzen eines Markers bietet die Aufzeichnung "Zurücknehmen" an. Länger wäre der
// Knopf im Weg, kürzer erwischte er den Fehlgriff nicht: Zwischen Tippen und Merken liegen ein paar Sekunden.
const ZURUECKNEHMEN_MS = 10_000;

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
  mantrailing = false,
}: {
  dogId: string;
  onSaved: () => Promise<void>;
  /**
   * Ein Trail statt einer Fährte (Mantrailing): andere Begriffe, die Angaben zum Trail vor dem
   * Legen, keine Gegenstände. Gilt für die Lebensdauer des Recorders - wer wechselt, hängt ihn
   * mit einem anderen key neu ein (siehe DogFaehrteSheet).
   */
  mantrailing?: boolean;
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
    // Die Fährte braucht höhere Präzision als ein Spaziergang (Begründung im Profil).
    GPS_PROFIL_LEGEN,
  );
  // Auch der Ablauf der eben gelegten Fährte (Karte "Fährte gespeichert") ist
  // eine Aufzeichnung, die das Fenster nicht überleben würde.
  const [ablaufLaeuft, setAblaufLaeuft] = useState(false);
  useEffect(() => {
    onAufzeichnung?.(isRecording || ablaufLaeuft);
  }, [isRecording, ablaufLaeuft, onAufzeichnung]);
  const [untergrund, setUntergrund] = useState<string[]>([]);
  const [bodenzustand, setBodenzustand] = useState<string[]>([]);
  // Der zuletzt gesetzte Marker, solange er sich zurücknehmen lässt (siehe ZURUECKNEHMEN_MS) - als Objekt,
  // damit genau er entfernt wird und nicht ein anderer Marker an derselben Stelle.
  const [zuruecknehmbar, setZuruecknehmbar] = useState<GpsPoint | null>(null);
  // Ob dies ein Trail ist: zunächst die Wahl des Aufrufers, beim Fortsetzen einer unterbrochenen
  // Aufzeichnung aber das, was sie war.
  const [trailModus, setTrailModus] = useState(mantrailing);
  const [trailAngaben, setTrailAngaben] = useState<TrailAngabenVorLegen>(LEERE_TRAIL_ANGABEN);
  const markerArten = trailModus ? TRAIL_MARKER : MARKER_TYPES;
  const untergruende = trailModus ? TRAIL_UNTERGRUENDE : UNTERGRUENDE;
  const [isMarking, setIsMarking] = useState(false);
  // Die eben gelegte Fährte: bietet direkt danach das Ablaufen an, statt dass
  // man sie im Tagebuch wieder heraussuchen muss. Offline gespeichert gibt es
  // noch keine Id - dann erinnert später die Startseite ("Zuletzt gelegt").
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

  useEffect(() => {
    if (!zuruecknehmbar) return;
    const timer = setTimeout(() => setZuruecknehmbar(null), ZURUECKNEHMEN_MS);
    return () => clearTimeout(timer);
  }, [zuruecknehmbar]);

  // Jeder neue Punkt, jeder Marker (auch ein zurückgenommener, der dann fehlt) und jede Änderung am
  // Untergrund geht sofort in die Sicherung.
  useEffect(() => {
    const laufend = laufendRef.current;
    if (!isRecording || !laufend || !user) return;
    sicherungSchreiben({
      art: "faehrte",
      userId: user.userId,
      dogId,
      untergrund,
      // Der Bodenzustand gehört zur Fährte; ein Trail kennt ihn nicht.
      ...(trailModus ? {} : { bodenzustand }),
      points,
      ...(trailModus ? { trail: trailAngaben } : {}),
      ...laufend,
    });
  }, [isRecording, points, untergrund, bodenzustand, user, dogId, trailModus, trailAngaben]);

  function startRecording() {
    laufendRef.current = { trackId: crypto.randomUUID(), begonnen: Date.now(), seite: window.location.pathname };
    setZuruecknehmbar(null);
    start();
  }

  function fortsetzen(gesichert: FaehrtenSicherung) {
    laufendRef.current = { trackId: gesichert.trackId, begonnen: gesichert.begonnen, seite: gesichert.seite };
    setUntergrund(gesichert.untergrund);
    setBodenzustand(gesichert.bodenzustand ?? []);
    setZuruecknehmbar(null);
    setTrailModus(gesichert.trail != null);
    setTrailAngaben(gesichert.trail ?? LEERE_TRAIL_ANGABEN);
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
        const marker: GpsPoint = { ...point, pointType: PUNKT_MARKER, label: null, markerType: typ };
        setPoints((prev) => [...prev, marker]);
        setZuruecknehmbar(marker);
        setIsMarking(false);
        toast.success(
          trailModus
            ? t("Marker gesetzt.")
            : t("{art} markiert.", { art: t(MARKER_TYPES.find((m) => m.value === typ)?.label ?? uebersetzbar("Marker")) }),
        );
      },
      () => setIsMarking(false),
    );
  }

  /**
   * Nimmt den zuletzt gesetzten Marker zurück, solange der Knopf da ist. Ältere Marker bleiben bewusst:
   * Der Knopf gilt dem Fehlgriff eben, kein Verlauf zum Zurückblättern. Aus der Sicherung verschwindet er
   * mit dem nächsten Schreiben der Punkte.
   */
  function markerZuruecknehmen() {
    if (!zuruecknehmbar) return;
    setPoints((prev) => markerEntfernen(prev, zuruecknehmbar));
    setZuruecknehmbar(null);
  }

  /**
   * Aufzeichnung verwerfen - nur nach Rückfrage.
   *
   * Im Vollbild gibt es bewusst keinen beiläufigen Ausweg: Eine gelegte
   * Fährte lässt sich nicht wiederholen, ein versehentlicher Abbruch wäre
   * der Verlust des ganzen Trainings.
   */
  function abbrechen() {
    if (
      !confirm(
        trailModus
          ? t("Aufzeichnung verwerfen? Der bisher aufgezeichnete Trail geht verloren.")
          : t("Aufzeichnung verwerfen? Die bisher aufgezeichnete Fährte geht verloren."),
      )
    )
      return;
    stop();
    setPoints([]);
    setZuruecknehmbar(null);
    laufendRef.current = null;
    sicherungLoeschen(schluessel);
  }

  async function stopRecording() {
    stop();
    setZuruecknehmbar(null);
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
      ...(trailModus ? {} : { bodenzustand }),
      points,
      ...(trailModus ? { trail: trailAngaben } : {}),
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
    const ergebnis = await sicherungSpeichern(sicherung, sicherung.trail ? t("Trail") : t("Fährte"));
    setSpeichert(false);
    speicherErgebnisMelden(ergebnis, t, "faehrte");
    setPoints([]);
    setUntergrund([]);
    setBodenzustand([]);
    setTrailAngaben(LEERE_TRAIL_ANGABEN);

    if (ergebnis.ausgang === "fehler") {
      setUnterbrochen(user ? sicherung : null);
      return;
    }
    setUnterbrochen(null);
    if (ergebnis.ausgang === "gespeichert") setGelegt(ergebnis.faehrte);
    await onSaved();
  }

  const autoPunkte = automatischePunkte(points).length;
  const markerAnzahl = markerPunkte(points).length;

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
              <span className="font-medium">{trailModus ? t("Trail gespeichert.") : t("Fährte gespeichert.")}</span>{" "}
              {trailModus
                ? t("Suchen, sobald die Liegezeit um ist - die Startseite zeigt ihn, solange er sich noch suchen lässt ({stunden} Stunden nach dem Legen).", { stunden: ABLAUF_FRIST_STUNDEN_TRAIL })
                : t("Ablaufen, sobald sie alt genug ist - die Startseite erinnert dich daran.")}
            </p>
            <div className="flex flex-wrap items-center gap-2">
              <WalkRunRecorder
                trackId={gelegt.id}
                laidTrackPoints={gelegt.points}
                track={gelegt}
                label={trailModus ? t("Jetzt suchen") : t("Jetzt ablaufen")}
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
            {/* Das Material trägt man hier nach, nicht beim Legen: Dort bleibt es bei einem Tipp je Marker.
                Zugeklappt, damit "Jetzt ablaufen" oben bleibt; die Fährtendetails bieten dasselbe später. */}
            {!trailModus && (
              <GegenstandsMaterial
                trackId={gelegt.id}
                punkte={gelegt.points}
                onGeaendert={(punkt) =>
                  setGelegt((vorher) =>
                    vorher && { ...vorher, points: vorher.points.map((p) => (p.id === punkt.id ? { ...p, material: punkt.material } : p)) },
                  )
                }
              />
            )}
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
                {untergruende.map((u) => {
                  const aktiv = untergrund.includes(u);
                  return (
                    <Chip
                      key={u}
                      kompakt
                      gewaehlt={aktiv}
                      onClick={() => setUntergrund((vorher) => untergrundUmschalten(vorher, u, untergruende))}
                    >
                      {t(u)}
                    </Chip>
                  );
                })}
              </div>
            </div>
            {/* Nur bei der Fährte: Frost und Schnee ändern, wie sich der Geruch hält. */}
            {!trailModus && (
              <div className="flex flex-col gap-2">
                <span id="fahrte-bodenzustand" className="text-sm font-medium">
                  {t("Bodenzustand (optional)")}{" "}
                  <span className="font-normal text-muted-foreground">· {t("mehrere möglich")}</span>
                </span>
                <div role="group" aria-labelledby="fahrte-bodenzustand" className="flex flex-wrap gap-1.5">
                  {BODENZUSTAENDE.map((zustand) => (
                    <Chip
                      key={zustand}
                      kompakt
                      gewaehlt={bodenzustand.includes(zustand)}
                      onClick={() => setBodenzustand((vorher) => untergrundUmschalten(vorher, zustand, BODENZUSTAENDE))}
                    >
                      {t(zustand)}
                    </Chip>
                  ))}
                </div>
              </div>
            )}
            {trailModus && <TrailLegenFormular werte={trailAngaben} onChange={setTrailAngaben} />}
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
      titel={trailModus ? t("Trail legen") : t("Fährte legen")}
      status={
        <>
          {t("{punkte} Punkte · {marker} Marker", { punkte: autoPunkte, marker: markerAnzahl })}
          {currentAccuracy !== null && (
            <>
              {" · "}
              <span
                className={GENAUIGKEIT_KLASSE[genauigkeitsStufe(currentAccuracy, GPS_PROFIL_LEGEN)]}
                title={t("GPS-Genauigkeit (Fehlerkreis-Radius). Punkte ungenauer als {max} m werden verworfen; findet das GPS länger keinen genauen Fix (z.B. ohne Netz), lockert sich der Filter schrittweise bis {gelockert} m.", { max: GPS_PROFIL_LEGEN.maxAccuracyMeters, gelockert: GPS_PROFIL_LEGEN.relaxedMaxAccuracyMeters })}
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
        // hyphens-auto: Bei 375 px brach "Gegenstand" sonst ohne Strich mitten im Wort um ("Gegenstan|d").
        <div className={cn("grid gap-2", trailModus ? "grid-cols-1" : "grid-cols-4")}>
          {markerArten.map((markerArt) => (
            <Button
              key={markerArt.value}
              type="button"
              variant="outline"
              disabled={isMarking}
              onClick={() => markObject(markerArt.value)}
              className="h-auto min-h-16 flex-col gap-1 px-1 py-1.5 text-xs leading-tight whitespace-normal"
            >
              <markerArt.icon className="size-5 shrink-0" />
              <span className="w-full min-w-0 text-center break-words hyphens-auto">{t(markerArt.label)}</span>
            </Button>
          ))}
        </div>
      }
      abschlussLabel={t("Legen beenden")}
      abschlussFrage={trailModus ? t("Legen beenden und den Trail speichern?") : t("Legen beenden und die Fährte speichern?")}
      onAbschluss={stopRecording}
      onAbbrechen={abbrechen}
    >
      <TrackMap points={points} live fill />
      {/* Über der Karte statt in der Bedienleiste: Erschiene der Knopf dort, rutschten die Marker-Knöpfe
          im ungünstigsten Moment unter dem Daumen weg. bottom-12 lässt Maßstab und Quellenangabe der Karte frei. */}
      {zuruecknehmbar && (
        <div className="pointer-events-none absolute inset-x-4 bottom-12 z-10 flex justify-center">
          <Button
            type="button"
            variant="secondary"
            onClick={markerZuruecknehmen}
            className="pointer-events-auto min-h-12 gap-2 px-6 text-base shadow-md"
          >
            <Undo2 className="size-5" />
            {t("Marker zurücknehmen")}
          </Button>
        </div>
      )}
    </AufzeichnungVollbild>
  );
}

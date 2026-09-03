"use client";

import { useEffect, useId, useRef, useState } from "react";
import { useTheme } from "next-themes";
import { EBENE_GEAENDERT, KARTEN_EBENEN, ebeneMerken, gespeicherteEbene, type KartenEbene } from "@/lib/karten-ebenen";
import type { GpsWalkRun } from "@/lib/types";
import { bearingDegrees } from "@/lib/geo";
import { Map, Satellite } from "lucide-react";

import { useT } from "@/lib/i18n";
import { uebersetzbar } from "@/lib/i18n/sprachen";
/// <reference types="leaflet" />

// Kompatibel zu sowohl GpsPoint (pointType/label gesetzt) als auch
// GpsWalkPoint (kennt beide Felder nicht - zählt dann automatisch als
// "automatischer Punkt", siehe pointType !== 1 unten).
type MapPoint = { latitude: number; longitude: number; pointType?: number; label?: string | null; markerType?: number };

// Eigene Farbe pro Ablauf-Versuch, damit mehrere Wiederholungen auf der
// Karte unterscheidbar bleiben (zyklisch wiederverwendet, falls mehr
// Versuche als Farben vorhanden sind).
const WALK_RUN_COLORS = ["#2563eb", "#9333ea", "#0d9488", "#dc2626"];

// Fester Farbwert statt einer Theme-CSS-Variable: Leaflet setzt "color" als
// reines SVG-Attribut (stroke="..."), nicht als CSS-Eigenschaft - var(...)
// wird in einem XML-Attribut nicht aufgelöst, die Linie blieb dadurch
// unsichtbar (nur Kacheln/Marker waren zu sehen).
const TRACK_LINE_COLOR = "#16a34a";

// Ampelfarben für die Abweichung der Ablauf-Linie (Schwellen siehe
// GpsTrackEvaluator im Backend - bewusst großzügig, weil der GPS-Fehler
// selbst in derselben Größenordnung liegt).
const DEVIATION_GREEN_MAX_M = 3;
const DEVIATION_AMBER_MAX_M = 6;
const DEVIATION_COLORS = { green: "#16a34a", amber: "#d97706", red: "#dc2626" } as const;

function deviationColor(meters: number | null | undefined): string | null {
  if (meters == null) return null;
  if (meters <= DEVIATION_GREEN_MAX_M) return DEVIATION_COLORS.green;
  if (meters <= DEVIATION_AMBER_MAX_M) return DEVIATION_COLORS.amber;
  return DEVIATION_COLORS.red;
}

// Beschriftung manueller Marker nach fachlicher Bedeutung (GpsMarkerType).
const MARKER_LABELS = ["Gegenstand", "Leckerlipot", "Verleitung", "Marker"] as const;
const MARKER_COLORS = ["orange", "#a855f7", "#0ea5e9", "#94a3b8"] as const;

// Stockungen: unerklärt = Warnsignal (rot), Verweisen am Gegenstand = gut
// (grün), erklärt/neutral (grau).
const STOP_COLORS = ["#dc2626", "#16a34a", "#94a3b8"] as const;
const STOP_LABELS = [uebersetzbar("Unerklärte Stockung"), uebersetzbar("Verweisen"), uebersetzbar("Halt (erklärt)")] as const;

// Schrittgeschwindigkeit, kurze Distanzen - ein nahes Zoom-Level zeigt
// einzelne Abbiegungen deutlich, statt die ganze (noch kurze) Strecke winzig
// in der Bildmitte darzustellen.
const LIVE_INITIAL_ZOOM = 18;

// Für die Peilungs-Berechnung (Karte in Laufrichtung drehen) werden nicht
// nur die letzten zwei Punkte genommen: bei geringer Schrittgeschwindigkeit
// liegen aufeinanderfolgende Punkte oft nur wenige Meter auseinander und die
// gemessene Richtung springt durch GPS-Rauschen wild um. Peilung über ein
// größeres Fenster mittelt das aus. Mindestabstand verhindert zusätzlich
// Sprünge im Stillstand.
const BEARING_WINDOW_POINTS = 5;
const BEARING_MIN_DISTANCE_M = 3;
// EMA-Glättung für die Rotation selbst: harte Sprünge in der Kartenanzeige
// wirken schwindelerregend. α klein = langsam nachdrehen, aber ruhig.
const HEADING_SMOOTH_ALPHA = 0.2;

/**
 * Rendert eine Fährte auf einer OpenStreetMap-Karte (kostenlos, kein API-Key
 * nötig - siehe COST STRATEGY.md "Ziel: 0-10€ monatlich"). Leaflet wird
 * dynamisch importiert, da es auf `window`/`document` zugreift und daher
 * nicht serverseitig gerendert werden darf. Optional werden Ablauf-Versuche
 * (siehe FahrteRecorder "Fährte erneut ablaufen") als zusätzliche Linien
 * zum Vergleich mit der gelegten Fährte eingezeichnet.
 *
 * Mit `live` Karte einmalig erzeugen und bei neuen Punkten nur die Layer
 * aktualisieren (statt komplett neu aufzubauen) - damit ruckelt die Karte
 * nicht bei jedem GPS-Tick während einer laufenden Aufnahme. Zusätzlich
 * folgt die Kartenmitte per `panTo` dem aktuellen Standort, ohne den vom
 * Nutzer gewählten Zoom zu verändern.
 */
export function TrackMap({
  points,
  walkRuns = [],
  live = false,
  liveWalkRunPoints,
  fill = false,
}: {
  points: MapPoint[];
  walkRuns?: GpsWalkRun[];
  live?: boolean;
  // Punkte eines aktuell laufenden Ablauf-Versuchs. Wenn gesetzt und nicht
  // leer, verhält sich die Karte wie im Live-Modus (folgt der Position,
  // Rotation, Kompass) UND zeigt weiter die Legung + historische Abläufe -
  // der neue Versuch entsteht dabei live in derselben Karte, statt in einer
  // zweiten daneben.
  liveWalkRunPoints?: MapPoint[];
  // Füllt die Fläche des Elternelements statt der festen Höhe von 256 px.
  // Gebraucht für die Vollbildaufzeichnung, wo die Karte den ganzen
  // verbleibenden Platz bekommt.
  fill?: boolean;
}) {
  const t = useT();
  // Live entweder weil eine neue Fährte gelegt wird (bisheriges Verhalten)
  // oder weil ein Ablauf-Versuch mitten in der Aufzeichnung ist.
  const hasLiveWalkRun = !!(liveWalkRunPoints && liveWalkRunPoints.length > 0);
  const isLive = live || hasLiveWalkRun;
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<import("leaflet").Map | null>(null);
  const leafletRef = useRef<typeof import("leaflet") | null>(null);
  const layerGroupRef = useRef<import("leaflet").LayerGroup | null>(null);
  const hasSetInitialViewRef = useRef(false);
  const [mapReady, setMapReady] = useState(false);

  // Kantenlänge der Drehfläche (siehe unten): die Diagonale des Rahmens.
  // Kartenhintergrund. Der Anfangswert wird erst nach dem Einhängen gelesen -
  // beim Serverrendern gibt es keinen localStorage, und ein abweichender
  // erster Wert würde die Hydration stören.
  const [ebene, setEbene] = useState<KartenEbene>("strasse");
  const kachelEbeneRef = useRef<import("leaflet").TileLayer | null>(null);
  // Eindeutige Kennung dieser Karte. Gebraucht, um die Stilregeln unten auf
  // GENAU DIESE Karte zu begrenzen: Auf einer Seite können mehrere Karten
  // liegen - das Vollbild der Aufzeichnung und die Karten der bisherigen
  // Fährten in der Historie. Eine seitenweite Regel wie
  // ".leaflet-tile-pane { filter: … }" greift auf alle durch; das Luftbild im
  // Vollbild wurde dadurch von der dunklen Straßenkarte im Hintergrund
  // invertiert und sah aus wie ein Negativ.
  const kartenId = `karte-${useId().replace(/:/g, "")}`;
  const { resolvedTheme } = useTheme();
  // Nur der Straßenstil wird abgedunkelt; ein invertiertes Luftbild sähe aus
  // wie ein Negativ.
  const dunkel = resolvedTheme === "dark" && KARTEN_EBENEN[ebene].abdunkelbar;

  function ebeneWechseln() {
    const naechste: KartenEbene = ebene === "strasse" ? "luftbild" : "strasse";
    setEbene(naechste);
    ebeneMerken(naechste);
  }

  const rahmenRef = useRef<HTMLDivElement | null>(null);
  const [kantenlaenge, setKantenlaenge] = useState(0);
  // Ob schon grob auf den Standort zentriert wurde (siehe Effect unten).
  const hatGrobeZentrierungRef = useRef(false);

  // Gespeicherte Ebene erst hier lesen, siehe oben - und auf Wechsel in
  // anderen Karten derselben Seite hören.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEbene(gespeicherteEbene());

    const beiWechsel = (e: Event) => setEbene((e as CustomEvent<KartenEbene>).detail);
    window.addEventListener(EBENE_GEAENDERT, beiWechsel);
    return () => window.removeEventListener(EBENE_GEAENDERT, beiWechsel);
  }, []);

  // Ebene wechseln: alte Kachelschicht entfernen, neue setzen.
  useEffect(() => {
    const map = mapRef.current;
    const L = leafletRef.current;
    if (!map || !L) return;

    const gewaehlt = KARTEN_EBENEN[ebene];
    kachelEbeneRef.current?.remove();
    kachelEbeneRef.current = L.tileLayer(gewaehlt.url, {
      attribution: gewaehlt.attribution,
      maxZoom: gewaehlt.maxZoom,
    }).addTo(map);
    // Unter die Spur legen, nicht darüber.
    kachelEbeneRef.current.bringToBack();
  }, [ebene, mapReady]);

  // Rahmen messen: Die Drehfläche muss so groß sein wie die Diagonale, sonst
  // deckt sie bei schrägem Winkel ihren eigenen Ausschnitt nicht mehr ab.
  // Leaflet muss danach seine Größe neu bestimmen, sonst rechnet es weiter
  // mit den alten Maßen und die Karte sitzt versetzt.
  useEffect(() => {
    const rahmen = rahmenRef.current;
    if (!rahmen) return;

    const messen = () => {
      const { width, height } = rahmen.getBoundingClientRect();
      const diagonale = Math.ceil(Math.hypot(width, height));
      setKantenlaenge((vorher) => (vorher === diagonale ? vorher : diagonale));
    };

    messen();
    const beobachter = new ResizeObserver(messen);
    beobachter.observe(rahmen);
    return () => beobachter.disconnect();
  }, []);

  // Kartenausrichtung: nur im Live-Modus umschaltbar; Nutzer kann per
  // Kompass-Button zwischen den drei Modi zyklen.
  // - "north-arrow": Nord oben, Richtungspfeil an der eigenen Position
  //   (klassisch, wie Google Maps 'North Up' - keine dreh-bedingten Glitches)
  // - "heading": Karte selbst in Fahrtrichtung gedreht (klassischer Navi-Modus)
  // - "north": Nord oben ohne Zusatzpfeil (statische Ansicht)
  type OrientationMode = "north-arrow" | "heading" | "north";
  // Während der Aufzeichnung dreht die Karte standardmäßig mit der
  // Laufrichtung. Vorher stand hier "north-arrow" - der Modus war also
  // vorhanden, aber hinter dem Kompass-Knopf versteckt, und wer ihn nicht
  // fand, lief mit fest nach Norden ausgerichteter Karte über den Acker.
  // Die drei Modi bleiben, nur die Voreinstellung wechselt.
  const [orientation, setOrientation] = useState<OrientationMode>(isLive ? "heading" : "north");
  const [headingDeg, setHeadingDeg] = useState(0);
  const smoothedHeadingRef = useRef<number | null>(null);

  const rotateWithHeading = orientation === "heading";
  const showPositionArrow = isLive && orientation === "north-arrow";
  // Sofort grob auf den eigenen Standort zentrieren, ohne auf den ersten
  // aufgezeichneten Punkt zu warten.
  //
  // Vorher sprang die Karte erst, wenn der erste Punkt die Genauigkeitsprüfung
  // bestand - bei kaltem GPS werden aber genau die ersten Messungen verworfen
  // (gefordert sind 8 m, gelockert wird erst nach 15 s). So lange sah man
  // ganz Deutschland. Der Filter soll schlechte Punkte aus der AUFZEICHNUNG
  // halten; wohin die Karte schaut, ist eine andere Frage - dafür genügt eine
  // grobe Position.
  //
  // Die genaue Ausrichtung übernimmt weiterhin der erste echte Punkt: Diese
  // Zentrierung setzt hasSetInitialViewRef bewusst NICHT, damit dort noch
  // einmal exakt eingerastet wird statt nur sanft zu schwenken.
  useEffect(() => {
    if (!mapReady || !isLive) return;
    if (hatGrobeZentrierungRef.current || hasSetInitialViewRef.current) return;
    if (typeof navigator === "undefined" || !navigator.geolocation) return;

    let abgebrochen = false;
    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (abgebrochen || hasSetInitialViewRef.current) return;
        hatGrobeZentrierungRef.current = true;
        mapRef.current?.setView(
          [position.coords.latitude, position.coords.longitude],
          LIVE_INITIAL_ZOOM,
        );
      },
      () => {
        // Kein Standort (verweigert, kein Empfang): Es bleibt bei der
        // Übersichtskarte, und der erste aufgezeichnete Punkt richtet sie aus.
      },
      // Bewusst genügsam: Es geht nur um den Bildausschnitt. enableHighAccuracy
      // würde denselben langsamen Kaltstart abwarten, den wir gerade umgehen
      // wollen; ein bis zu einer Minute alter Wert aus dem Cache reicht dafür.
      { enableHighAccuracy: false, timeout: 5000, maximumAge: 60000 },
    );

    return () => {
      abgebrochen = true;
    };
  }, [mapReady, isLive]);

  // Nach jedem Größen- oder Moduswechsel muss Leaflet seine Maße neu
  // bestimmen - sonst rechnet es mit der alten Fläche weiter und die Karte
  // sitzt versetzt oder zeigt graue Kacheln.
  useEffect(() => {
    mapRef.current?.invalidateSize({ animate: false });
  }, [kantenlaenge, rotateWithHeading]);

  // Ziehen abschalten, solange die Karte mitdreht.
  //
  // Leaflet weiß nichts von der Drehung - sie liegt als CSS-Transformation
  // darüber. Eine Zugbewegung nach oben verschiebt die Karte deshalb nicht
  // nach oben, sondern um den Drehwinkel versetzt. Beim Aufzeichnen fällt
  // das kaum auf, weil die Karte ohnehin jede Sekunde der eigenen Position
  // nachgeführt wird und jedes Verschieben sofort überschreibt - aber eine
  // Bedienung, die in die falsche Richtung zieht, ist schlechter als eine,
  // die es gar nicht anbietet. Wer verschieben will, schaltet per Kompass
  // auf "Nord oben".
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    if (rotateWithHeading) map.dragging.disable();
    else map.dragging.enable();
  }, [rotateWithHeading, mapReady]);

  function cycleOrientation() {
    setOrientation((prev) =>
      prev === "north-arrow" ? "heading" : prev === "heading" ? "north" : "north-arrow",
    );
  }

  // Effect 1: Karte + Tile-Layer genau einmal erzeugen (nur beim Mount).
  useEffect(() => {
    if (!containerRef.current) return;

    // imporleaflet ist asynchron - läuft der Effect erneut, bevor das
    // Promise aufgelöst ist (React Strict Mode ruft Effects im Dev-Modus
    // doppelt auf), würde sonst ein zweites L.map() auf demselben Container
    // aufgerufen werden, während das erste noch nicht aufgeräumt ist -
    // Leaflet wirft dann "Map container is already initialized".
    let cancelled = false;

    import("leaflet").then((L) => {
      if (cancelled || !containerRef.current) return;

      leafletRef.current = L;
      // Vorläufige Sicht (Deutschland-Mitte), bis der erste GPS-Punkt
      // eintrifft und Effect 2 per setView auf den tatsächlichen Standort
      // springt - ohne das bliebe die Karte ohne jede Kachel leer/grau.
      const map = L.map(containerRef.current).setView([51.1657, 10.4515], 6);
      mapRef.current = map;

      const start = KARTEN_EBENEN[gespeicherteEbene()];
      kachelEbeneRef.current = L.tileLayer(start.url, {
        attribution: start.attribution,
        maxZoom: start.maxZoom,
      }).addTo(map);

      layerGroupRef.current = L.layerGroup().addTo(map);
      setMapReady(true);
    });

    return () => {
      cancelled = true;
      mapRef.current?.remove();
      mapRef.current = null;
      leafletRef.current = null;
      layerGroupRef.current = null;
      hasSetInitialViewRef.current = false;
    };
  }, []);

  // Effect 2: bei jeder Änderung von points/walkRuns nur die Layer neu
  // zeichnen (Karte selbst bleibt bestehen) - läuft auch einmal direkt
  // nachdem die Karte fertig initialisiert ist (mapReady wechselt auf true).
  useEffect(() => {
    const L = leafletRef.current;
    const map = mapRef.current;
    const layerGroup = layerGroupRef.current;
    if (!L || !map || !layerGroup) return;

    layerGroup.clearLayers();

    // Linie/Start/Ende beziehen sich nur auf die automatischen GPS-Punkte -
    // manuell gesetzte Marker für gelegte Gegenstände (Schussstelle,
    // Apportel etc.) gehören nicht zur eigentlichen Laufstrecke und würden
    // die Linienführung verzerren.
    const automaticPoints = points.filter((p) => p.pointType !== 1);
    const manualPoints = points.filter((p) => p.pointType === 1);
    const latLngs = automaticPoints.map((p) => [p.latitude, p.longitude] as [number, number]);

    if (latLngs.length > 0) {
      L.polyline(latLngs, { color: TRACK_LINE_COLOR }).addTo(layerGroup);
      L.circleMarker(latLngs[0], { radius: 6, color: "green" }).addTo(layerGroup).bindTooltip("Start (gelegt)");
      if (!live) {
        L.circleMarker(latLngs[latLngs.length - 1], { radius: 6, color: "red" })
          .addTo(layerGroup)
          .bindTooltip("Ende (gelegt)");
      }
    }

    manualPoints.forEach((p) => {
      const kind = p.markerType ?? 0;
      const color = MARKER_COLORS[kind] ?? MARKER_COLORS[3];
      L.circleMarker([p.latitude, p.longitude], {
        radius: 7,
        color,
        fillColor: color,
        fillOpacity: 0.9,
      })
        .addTo(layerGroup)
        .bindTooltip(p.label || MARKER_LABELS[kind] || "Marker");
    });

    walkRuns.forEach((run, index) => {
      if (run.points.length === 0) return;
      const fallbackColor = WALK_RUN_COLORS[index % WALK_RUN_COLORS.length];
      const runLatLngs = run.points.map((p) => [p.latitude, p.longitude] as [number, number]);
      const isEvaluated = run.points.some((p) => p.deviationMeters != null);

      if (isEvaluated) {
        // Je Segment die Farbe des schlechteren der beiden Endpunkte - so ist
        // auf einen Blick sichtbar, WO das Team abgekommen ist.
        for (let i = 0; i < runLatLngs.length - 1; i++) {
          const worse = Math.max(run.points[i].deviationMeters ?? 0, run.points[i + 1].deviationMeters ?? 0);
          L.polyline([runLatLngs[i], runLatLngs[i + 1]], {
            color: deviationColor(worse) ?? fallbackColor,
            weight: 4,
          })
            .addTo(layerGroup)
            .bindTooltip(`Ablauf ${index + 1}: ${worse.toFixed(1)} m Abweichung`);
        }
      } else {
        L.polyline(runLatLngs, { color: fallbackColor, dashArray: "6 6" })
          .addTo(layerGroup)
          .bindTooltip(`Ablauf-Versuch ${index + 1}`);
      }

      // Erkannte Halte als Ringe: zeigen die Stellen, an denen der Hund
      // gesucht/verwiesen hat - auch dort, wo die Position gar nicht abweicht.
      (run.stops ?? []).forEach((stop) => {
        L.circleMarker([stop.latitude, stop.longitude], {
          radius: 9,
          color: STOP_COLORS[stop.kind] ?? STOP_COLORS[2],
          fill: false,
          weight: 3,
        })
          .addTo(layerGroup)
          .bindTooltip(
            `${t(STOP_LABELS[stop.kind] ?? uebersetzbar("Halt"))}: ${stop.durationSeconds}s${stop.markerLabel ? ` (${stop.markerLabel})` : ""}`,
          );
      });
    });

    // Aktuell laufender Ablauf-Versuch (falls einer aktiv ist): nächste Farbe
    // im Zyklus, breiter und dichter gestrichelt, damit sich die frische
    // Linie visuell klar von den gespeicherten Versuchen abhebt.
    let liveWalkLatLngs: [number, number][] = [];
    if (hasLiveWalkRun && liveWalkRunPoints) {
      const liveColor = WALK_RUN_COLORS[walkRuns.length % WALK_RUN_COLORS.length];
      liveWalkLatLngs = liveWalkRunPoints.map((p) => [p.latitude, p.longitude] as [number, number]);
      if (liveWalkLatLngs.length > 0) {
        L.polyline(liveWalkLatLngs, { color: liveColor, dashArray: "3 4", weight: 4 })
          .addTo(layerGroup)
          .bindTooltip(`Ablauf-Versuch ${walkRuns.length + 1} (läuft)`);
        const last = liveWalkLatLngs[liveWalkLatLngs.length - 1];
        // Modus "Nord oben + Richtungspfeil": statt einfachem Punkt ein
        // rotierendes SVG-Icon, dessen Spitze in die aktuelle Bewegungs-
        // richtung zeigt - so sieht der Nutzer die Ausrichtung, ohne dass
        // sich die ganze Karte drehen muss.
        if (showPositionArrow) {
          const arrowIcon = L.divIcon({
            className: "",
            html: `<svg viewBox="0 0 24 24" width="28" height="28" style="transform: rotate(${smoothedHeadingRef.current ?? 0}deg); transform-origin: 50% 50%;"><circle cx="12" cy="12" r="10" fill="${liveColor}" fill-opacity="0.25"/><polygon points="12,3 18,20 12,16 6,20" fill="${liveColor}" stroke="white" stroke-width="1"/></svg>`,
            iconSize: [28, 28],
            iconAnchor: [14, 14],
          });
          L.marker(last, { icon: arrowIcon }).addTo(layerGroup);
        } else {
          L.circleMarker(last, {
            radius: 6,
            color: liveColor,
            fillColor: liveColor,
            fillOpacity: 1,
          }).addTo(layerGroup);
        }
      }
    }

    const allLatLngs = [...latLngs, ...manualPoints.map((p) => [p.latitude, p.longitude] as [number, number])];
    if (isLive) {
      // Während der Aufnahme: Kartenmitte folgt dem aktuellen Standort, der
      // Zoom bleibt unverändert, damit der Nutzer nicht laufend neu
      // hineinzoomen muss, sobald er die Karte einmal passend eingestellt hat.
      // Bei einem laufenden Ablauf-Versuch folgt die Karte dessen Position,
      // sonst dem letzten Punkt der aktuellen Legung.
      const followSource = hasLiveWalkRun && liveWalkRunPoints ? liveWalkRunPoints : automaticPoints;
      const followLatLngs = hasLiveWalkRun && liveWalkRunPoints ? liveWalkLatLngs : latLngs;
      if (followLatLngs.length > 0) {
        const latest = followLatLngs[followLatLngs.length - 1];
        if (!hasSetInitialViewRef.current) {
          map.setView(latest, LIVE_INITIAL_ZOOM);
          hasSetInitialViewRef.current = true;
        } else {
          map.panTo(latest);
        }

        // Peilung aus einem Fenster der letzten Punkte berechnen (Rauschen
        // dämpfen). Das älteste Ende des Fensters wird so weit zurück
        // verschoben, bis der Abstand zum aktuellen Punkt ausreicht -
        // beugt Sprüngen bei Stillstand vor.
        if (followSource.length >= 2) {
          const latestPoint = followSource[followSource.length - 1];
          let anchor = followSource[Math.max(0, followSource.length - BEARING_WINDOW_POINTS)];
          for (let i = followSource.length - 2; i >= 0; i--) {
            anchor = followSource[i];
            const dx = (latestPoint.longitude - anchor.longitude) * 111111 * Math.cos((latestPoint.latitude * Math.PI) / 180);
            const dy = (latestPoint.latitude - anchor.latitude) * 111111;
            if (Math.sqrt(dx * dx + dy * dy) >= BEARING_MIN_DISTANCE_M) break;
          }
          const raw = bearingDegrees(anchor, latestPoint);
          // Kürzeste Winkeldifferenz zum bisherigen (geglätteten) Wert -
          // ohne die Behandlung würde ein Sprung von 350° auf 10° als
          // -340°-Bewegung geglättet, obwohl es nur +20° sind.
          const prev = smoothedHeadingRef.current;
          if (prev === null) {
            smoothedHeadingRef.current = raw;
          } else {
            let diff = raw - prev;
            if (diff > 180) diff -= 360;
            if (diff < -180) diff += 360;
            smoothedHeadingRef.current = (prev + HEADING_SMOOTH_ALPHA * diff + 360) % 360;
          }
          setHeadingDeg(smoothedHeadingRef.current);
        }
      }
    } else if (allLatLngs.length > 0) {
      // Abgeschlossene/historische Fährte: ganze Strecke ins Bild einpassen.
      const allWalkRunLatLngs = walkRuns.flatMap((r) => r.points.map((p) => [p.latitude, p.longitude] as [number, number]));
      map.fitBounds([...allLatLngs, ...allWalkRunLatLngs], { padding: [16, 16] });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mapReady, points, walkRuns, live, liveWalkRunPoints, showPositionArrow, headingDeg]);

  // Im Live-Modus die Karte schon vor dem ersten Punkt anzeigen (wartet auf
  // die erste Positionsmessung), damit sie nicht erst nach einigen Sekunden
  // "einschnappt".
  if (!isLive && points.length === 0 && walkRuns.length === 0) {
    return <p className="text-sm text-muted-foreground">{t("Keine GPS-Punkte aufgezeichnet.")}</p>;
  }

  // Rotation nur im "heading"-Modus. Die Rotation wird per CSS-transform auf
  // den Karten-Container gelegt, weil Leaflet selbst keine Rotation kennt.
  // Kompass-Button und OSM-Attribution werden per Counter-Rotation aufrecht
  // gehalten, damit Text/Steuerelemente immer lesbar bleiben.
  const rotationDeg = rotateWithHeading ? -headingDeg : 0;
  const orientationTitle =
    orientation === "north-arrow"
      ? t("Nord oben + Richtungspfeil (klicken für 'in Laufrichtung')")
      : orientation === "heading"
        ? t("Karte in Laufrichtung (klicken für 'Nord oben ohne Pfeil')")
        : t("Nord oben, statisch (klicken für 'Nord + Pfeil')");

  return (
    <div
      id={kartenId}
      ref={rahmenRef}
      className={`relative w-full overflow-hidden ${fill ? "h-full" : "h-64 rounded-md"}`}
    >
      {/* Drehebene und Leaflet-Element sind bewusst GETRENNT.
          
          Leaflet setzt eigene Klassen direkt auf sein Element
          (leaflet-container, leaflet-touch, …). Stand am selben Element auch
          ein von React verwaltetes className, das sich ändert - hier beim
          Wechsel der Ausrichtung -, schrieb React das Attribut komplett neu
          und löschte Leaflets Klassen mit. Ohne leaflet-container verliert
          die Karte ihr gesamtes Styling: Sie wurde schwarz.

          Deshalb gehört die Drehung auf eine eigene Ebene, und das
          Leaflet-Element bekommt ein KONSTANTES className, das React nach
          dem Einhängen nie wieder anfasst. */}
      <div
        className="transition-transform duration-500 ease-out [&_.leaflet-control-attribution]:origin-bottom-right"
        style={
          rotateWithHeading
            ? {
                // Quadrat mit der Diagonale des Rahmens als Kantenlänge, um
                // den Mittelpunkt gedreht. Ohne das schauen bei gedrehter
                // Karte die Ecken des Rahmens ins Leere - bei 90° blieben
                // oben und unten schwarze Keile stehen, weil ein hochkantes
                // Rechteck gedreht seinen eigenen Ausschnitt nicht mehr
                // deckt. Ein Quadrat dieser Kantenlänge deckt jeden Winkel.
                position: "absolute",
                left: "50%",
                top: "50%",
                width: kantenlaenge,
                height: kantenlaenge,
                transform: `translate(-50%, -50%) rotate(${rotationDeg}deg)`,
              }
            : { width: "100%", height: "100%" }
        }
      >
        <div ref={containerRef} className="size-full" />
      </div>
      {/* OSM-Attribution wieder aufrichten wenn Karte rotiert: gegenrotieren
          um denselben Winkel. Selector greift die Leaflet-Attribution direkt
          im rotierten Container. */}
      {/* Dunkler Kartenstil per Filter statt per eigener Kachelquelle.
          
          Ein fertiger dunkler Straßenstil käme nur von Anbietern mit
          API-Schlüssel (CARTO, Stadia) - der freie dunkle Stil von Esri hat
          in den Zoomstufen, die das Fährtelegen braucht, gar keine Daten
          ("Map data not yet available"). Invertieren plus Farbtondrehung
          liefert aus derselben, schon geladenen Kachel ein dunkles Bild:
          keine zweite Quelle, kein Schlüssel, keine zusätzlichen Abrufe.
          
          Nur auf die Kacheln, nicht auf die ganze Karte - sonst würde die
          Spur gleich mit invertiert und wäre nicht mehr grün.
          
          saturate(0.45) ist nicht Geschmack, sondern nötig: Der OSM-Stil ist
          voller bunter Ladensymbole, und invert + hue-rotate macht daraus
          grelles Magenta. Entsättigt fügen sie sich ein, ohne zu
          verschwinden. */}
      {dunkel && (
        <style>
          {`#${kartenId} .leaflet-tile-pane { filter: invert(1) hue-rotate(180deg) brightness(0.9) contrast(0.9) saturate(0.45); }`}
        </style>
      )}
      {rotateWithHeading && (
        <style>
          {`#${kartenId} .leaflet-control-attribution { transform: rotate(${headingDeg}deg); transform-origin: 100% 100%; }`}
        </style>
      )}
      {/* Ebenenwechsel: immer erreichbar, auch bei fertigen Fährten - beim
          Auswerten will man denselben Wechsel wie beim Legen. */}
      <button
        type="button"
        onClick={ebeneWechseln}
        title={t("Kartenhintergrund: {jetzt} (klicken für {andere})", {
          jetzt: t(KARTEN_EBENEN[ebene].label),
          andere: t(KARTEN_EBENEN[ebene === "strasse" ? "luftbild" : "strasse"].label),
        })}
        className="absolute left-2 top-2 z-[400] flex items-center gap-1.5 rounded-full border bg-background/90 px-3 py-2 text-xs font-medium shadow-md backdrop-blur coarse:min-h-10"
      >
        {ebene === "strasse" ? <Map className="size-4" /> : <Satellite className="size-4" />}
        {t(KARTEN_EBENEN[ebene].label)}
      </button>
      {isLive && (
        <button
          type="button"
          onClick={cycleOrientation}
          title={orientationTitle}
          className="absolute right-2 top-2 z-[400] flex size-10 items-center justify-center rounded-full border bg-background/90 shadow-md backdrop-blur"
        >
          {/* Kompassnadel: zeigt immer nach geografisch Nord. Bei rotierter
              Karte kompensiert der Zeiger die Rotation, damit "N" sichtbar bleibt. */}
          <svg
            viewBox="0 0 24 24"
            className="size-6 transition-transform duration-500 ease-out"
            style={{ transform: `rotate(${headingDeg}deg)` }}
            aria-hidden
          >
            <polygon points="12,3 15,13 12,11 9,13" fill="#dc2626" />
            <polygon points="12,21 9,11 12,13 15,11" fill="#64748b" />
            <text x="12" y="8.5" textAnchor="middle" fontSize="4" fill="white" fontWeight="bold">N</text>
          </svg>
        </button>
      )}
    </div>
  );
}

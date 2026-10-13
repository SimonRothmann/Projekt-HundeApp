"use client";

import { useEffect, useRef, useState } from "react";
import { useTheme } from "next-themes";
import type { Map as KartenObjekt, Marker, Popup } from "maplibre-gl";
import { Map as KartenSymbol, Satellite } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { EBENE_GEAENDERT, KARTEN_EBENEN, ebeneMerken, gespeicherteEbene, type KartenEbene } from "@/lib/karten-ebenen";
import type { GpsWalkRun } from "@/lib/types";
import { useT } from "@/lib/i18n";
import { AMBER_MAX_M, GREEN_MAX_M } from "@/components/tracking/walk-run-evaluation";
import { kartenTexte } from "@/lib/karte/kartentexte";
import { glaettePeilung, naechsteAusrichtung, rohePeilung, zielPeilung, type Ausrichtung } from "@/lib/karte/peilung";
import {
  DEVIATION_COLORS,
  TRACK_CASING_COLOR,
  TRACK_LINE_COLOR,
  WALK_RUN_COLORS,
  ablaufDaten,
  kartenGrenzen,
  legungDaten,
  leer,
  liveAblaufDaten,
  type KartenPunkt,
} from "@/lib/karte/spur-daten";
import {
  TIPPBARE_EBENEN,
  datenAnwenden,
  ebenenAnlegen,
  leererDatensatz,
  type SpurDatensatz,
} from "@/lib/karte/spur-ebenen";
import { stilLaden, stilWaehlen } from "@/lib/karte/stil";
import { webgl2Verfuegbar } from "@/lib/karte/webgl";

// Schrittgeschwindigkeit, kurze Distanzen - ein nahes Zoom-Level zeigt
// einzelne Abbiegungen deutlich, statt die ganze (noch kurze) Strecke winzig
// in der Bildmitte darzustellen. MapLibre-Zoom ist der von Leaflet minus 1:
// 17 hier entspricht dem früheren 18.
const LIVE_INITIAL_ZOOM = 17;

// Wie weit neben einer Linie oder einem Punkt ein Tipp noch trifft. Die Linien
// sind 4 px schmal, ein Finger ist es nicht.
const TREFFER_RADIUS_PX = 12;

// Das Nachführen der Karte zur eigenen Position: lang genug, dass es nicht
// ruckelt, kürzer als der GPS-Takt, damit es nicht hinterherhinkt.
const NACHFUEHR_DAUER_MS = 600;

// Eigene Konstante statt eines Standardwerts []: Der wäre bei jedem Rendern ein
// neues Array, und jeder Effekt, der von walkRuns abhängt, liefe jedes Mal.
const KEINE_ABLAEUFE: GpsWalkRun[] = [];

// MapLibre startet seinen Web Worker nicht von selbst: Es leitet dessen
// Adresse aus import.meta.url ab, und die ist unter dem Bundler keine
// http-Adresse - der Worker startet nie, die Karte bleibt leer, ohne dass ein
// Fehler fällt. Deshalb zeigen wir auf die mitgelieferten Dateien
// (public/karte/maplibre-<Version>/, kopiert von scripts/maplibre-worker.mjs).
// Absolut und vom eigenen Ursprung, dann startet MapLibre ihn als Modul-Worker
// ohne Blob. Einmal je Seite genügt.
let workerEingerichtet = false;
function workerEinrichten(maplibre: typeof import("maplibre-gl")): void {
  if (workerEingerichtet) return;
  maplibre.setWorkerUrl(`${location.origin}/karte/maplibre-${maplibre.getVersion()}/maplibre-gl-worker.mjs`);
  workerEingerichtet = true;
}

/**
 * Der oberste antippbare Text an dieser Stelle, mit großzügiger Trefferfläche.
 * queryRenderedFeatures liefert das oberste Feature zuerst.
 */
function textAn(map: KartenObjekt, x: number, y: number): string | null {
  const ebenen = TIPPBARE_EBENEN.filter((id) => map.getLayer(id));
  if (ebenen.length === 0) return null;
  const treffer = map.queryRenderedFeatures(
    [
      [x - TREFFER_RADIUS_PX, y - TREFFER_RADIUS_PX],
      [x + TREFFER_RADIUS_PX, y + TREFFER_RADIUS_PX],
    ],
    { layers: ebenen },
  );
  const text = treffer[0]?.properties?.text;
  return typeof text === "string" ? text : null;
}

/**
 * Der Richtungspfeil an der eigenen Position, als SVG-Element gebaut (kein
 * innerHTML - siehe html-senken.test.ts). Die Spitze zeigt nach oben; gedreht
 * wird über Marker.setRotation, nicht hier.
 */
function pfeilElement(farbe: string): HTMLElement {
  const ns = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("width", "28");
  svg.setAttribute("height", "28");

  const kreis = document.createElementNS(ns, "circle");
  kreis.setAttribute("cx", "12");
  kreis.setAttribute("cy", "12");
  kreis.setAttribute("r", "10");
  kreis.setAttribute("fill", farbe);
  kreis.setAttribute("fill-opacity", "0.25");

  const spitze = document.createElementNS(ns, "polygon");
  spitze.setAttribute("points", "12,3 18,20 12,16 6,20");
  spitze.setAttribute("fill", farbe);
  spitze.setAttribute("stroke", "white");
  spitze.setAttribute("stroke-width", "1");

  svg.append(kreis, spitze);
  const huelle = document.createElement("div");
  // Der Pfeil soll Tipps und Ziehen nicht abfangen.
  huelle.style.pointerEvents = "none";
  huelle.append(svg);
  return huelle;
}

/**
 * Rendert eine Fährte auf einer Karte aus OpenStreetMap-Vektorkacheln
 * (kostenlos, kein API-Key nötig - siehe COST STRATEGY.md "Ziel: 0-10€
 * monatlich"). MapLibre GL wird dynamisch importiert, da es auf
 * `window`/`document` zugreift und daher nicht serverseitig gerendert werden
 * darf. Optional werden Ablauf-Versuche (siehe FahrteRecorder "Fährte erneut
 * ablaufen") als zusätzliche Linien zum Vergleich mit der gelegten Fährte
 * eingezeichnet.
 *
 * Die Karte wird genau einmal erzeugt. Neue Punkte (live) schreiben nur in die
 * Datenquellen - damit ruckelt nichts bei jedem GPS-Tick einer laufenden
 * Aufnahme. Zusätzlich folgt die Kartenmitte dem aktuellen Standort, ohne den
 * vom Nutzer gewählten Zoom zu verändern.
 *
 * Die Aufzeichnung hängt nicht an dieser Karte: Sie liest den GPS-Strom selbst
 * und reicht nur die Punkte herein. Kann die Karte nicht gezeichnet werden,
 * läuft die Aufzeichnung unverändert weiter.
 */
export function TrackMap({
  points,
  walkRuns = KEINE_ABLAEUFE,
  live = false,
  liveWalkRunPoints,
  fill = false,
}: {
  points: KartenPunkt[];
  walkRuns?: GpsWalkRun[];
  live?: boolean;
  // Punkte eines aktuell laufenden Ablauf-Versuchs. Wenn gesetzt und nicht
  // leer, verhält sich die Karte wie im Live-Modus (folgt der Position,
  // Rotation, Kompass) UND zeigt weiter die Legung + historische Abläufe -
  // der neue Versuch entsteht dabei live in derselben Karte, statt in einer
  // zweiten daneben.
  liveWalkRunPoints?: KartenPunkt[];
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

  // Das Element, das MapLibre übernimmt. Sein className ist KONSTANT: MapLibre
  // setzt eigene Klassen darauf, und schriebe React das Attribut nach einer
  // Änderung neu, gingen sie verloren.
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<KartenObjekt | null>(null);
  const maplibreRef = useRef<typeof import("maplibre-gl") | null>(null);
  const [mapReady, setMapReady] = useState(false);
  // "geraet": kein WebGL2, oder die Karte ließ sich nicht erzeugen - ein
  // neuer Versuch ändert daran nichts. "laden": Kartenbibliothek oder Stil
  // kamen nicht an, auf dem Platz bei schlechtem Empfang der wahrscheinliche
  // Fall; dafür gibt es "Erneut versuchen" (zählt versuch hoch und erzeugt
  // die Karte neu).
  const [kartenFehler, setKartenFehler] = useState<"geraet" | "laden" | null>(null);
  const [versuch, setVersuch] = useState(0);

  // Werte, die nur beim Erzeugen der Karte gelten.
  const anfangRef = useRef({ fill, t });

  // Aktueller Stand der Quellen. Neue Daten gehen hierher UND in die Karte;
  // nach einem Stilwechsel füllt ebenenAnlegen die frischen Quellen aus dem
  // Stand statt aus dem, womit die Karte einmal erzeugt wurde.
  const datenRef = useRef<SpurDatensatz>(leererDatensatz());

  const hasSetInitialViewRef = useRef(false);
  // Ob schon grob auf den Standort zentriert wurde (siehe Effect unten).
  const hatGrobeZentrierungRef = useRef(false);
  const smoothedHeadingRef = useRef<number | null>(null);
  const letzterPunktRef = useRef<string | null>(null);
  const pfeilRef = useRef<Marker | null>(null);
  const pfeilFarbeRef = useRef<string | null>(null);
  const popupRef = useRef<Popup | null>(null);
  const nadelRef = useRef<SVGSVGElement>(null);

  // Kartenhintergrund. Der Anfangswert wird erst nach dem Einhängen gelesen -
  // beim Serverrendern gibt es keinen localStorage, und ein abweichender
  // erster Wert würde die Hydration stören.
  const [ebene, setEbene] = useState<KartenEbene>("strasse");
  const { resolvedTheme } = useTheme();
  // Der tatsächlich aktive Modus. Solange next-themes ihn noch nicht kennt,
  // gilt die Klasse am <html>: Sonst startete die dunkle App mit dem hellen
  // Stil und wechselte erst einen Moment später.
  const dunkel =
    resolvedTheme !== undefined
      ? resolvedTheme === "dark"
      : typeof document !== "undefined" && document.documentElement.classList.contains("dark");
  const stilName = stilWaehlen(ebene, dunkel);
  const dunkelRef = useRef(dunkel);
  // Welcher Stil gerade auf der Karte liegt, und welche Anfrage die neueste ist.
  const angewandterStilRef = useRef<string | null>(null);
  const stilAnfrageRef = useRef(0);

  useEffect(() => {
    dunkelRef.current = dunkel;
  }, [dunkel]);

  function ebeneWechseln() {
    const naechste: KartenEbene = ebene === "strasse" ? "luftbild" : "strasse";
    setEbene(naechste);
    ebeneMerken(naechste);
  }

  // Gespeicherte Ebene erst hier lesen, siehe oben - und auf Wechsel in
  // anderen Karten derselben Seite hören.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEbene(gespeicherteEbene());

    const beiWechsel = (e: Event) => setEbene((e as CustomEvent<KartenEbene>).detail);
    window.addEventListener(EBENE_GEAENDERT, beiWechsel);
    return () => window.removeEventListener(EBENE_GEAENDERT, beiWechsel);
  }, []);

  // Kartenausrichtung: nur im Live-Modus, per Knopf oben rechts - Laufrichtung
  // oder Norden (warum nur zwei Modi: lib/karte/peilung.ts). Gezogen und
  // gezoomt werden kann in beiden, gedreht wird allein hierüber.
  // Während der Aufzeichnung dreht die Karte standardmäßig mit der
  // Laufrichtung: Wer den Knopf nicht fand, lief sonst mit fest nach Norden
  // ausgerichteter Karte über den Acker.
  const [orientation, setOrientation] = useState<Ausrichtung>(isLive ? "heading" : "north");
  const showPositionArrow = isLive && orientation === "north";

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
        mapRef.current?.jumpTo({
          center: [position.coords.longitude, position.coords.latitude],
          zoom: LIVE_INITIAL_ZOOM,
        });
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

  function cycleOrientation() {
    const naechste = naechsteAusrichtung(orientation);
    setOrientation(naechste);
    // Wer steht, hat keine Laufrichtung - die Karte bliebe nach Norden
    // ausgerichtet, und der Tipp sähe aus, als hätte er nichts bewirkt.
    if (naechste === "heading" && smoothedHeadingRef.current === null) {
      toast(t("Die Karte dreht sich, sobald du ein paar Schritte gehst."));
    }
  }

  // Karte genau einmal erzeugen (nur beim Mount).
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    // import ist asynchron - läuft der Effect erneut, bevor das Promise
    // aufgelöst ist (React Strict Mode ruft Effects im Dev-Modus doppelt
    // auf), würde sonst eine zweite Karte auf demselben Container entstehen,
    // während die erste noch nicht aufgeräumt ist.
    let abgebrochen = false;
    let map: KartenObjekt | null = null;

    (async () => {
      // Die Ebene direkt aus dem Speicher, nicht aus dem State: Der hat beim
      // Start noch den Vorgabewert, und wer das Luftbild gewählt hat, sähe
      // sonst erst kurz die Straßenkarte.
      const geladenerStil = stilWaehlen(gespeicherteEbene(), dunkelRef.current);
      let maplibre: typeof import("maplibre-gl");
      let stil: Awaited<ReturnType<typeof stilLaden>>;
      try {
        [maplibre, stil] = await Promise.all([import("maplibre-gl"), stilLaden(geladenerStil, location.origin)]);
      } catch {
        if (!abgebrochen) setKartenFehler("laden");
        return;
      }
      if (abgebrochen) return;

      try {
        // Erst nach dem Laden prüfen: Die Probe legt einen Kontext an, und der
        // Fehlerfall braucht keinen eigenen Zweig im Effect selbst.
        if (!webgl2Verfuegbar()) throw new Error("WebGL2 nicht verfügbar");
        workerEinrichten(maplibre);

        map = new maplibre.Map({
          container,
          style: stil,
          // Vorläufige Sicht (Deutschland-Mitte), bis der erste GPS-Punkt
          // eintrifft - ohne das bliebe die Karte leer.
          center: [10.4515, 51.1657],
          zoom: 5,
          maxZoom: 20,
          // Die Namensnennung der Kartendaten ist Lizenzpflicht und muss
          // sichtbar bleiben, nicht hinter einem Knopf.
          attributionControl: { compact: false },
          // Gedreht wird allein über die Ausrichtung des Kompass-Knopfs, nicht
          // per Geste: Zwei-Finger-Drehen und Neigen würden gegen das
          // Nachführen kämpfen.
          dragRotate: false,
          pitchWithRotate: false,
          touchPitch: false,
          maxPitch: 0,
          // Eingebettet (Hundeseite) scrollt ein Finger die Seite statt die
          // Karte zu verschieben; im Vollbild gehört die ganze Fläche der Karte.
          cooperativeGestures: !anfangRef.current.fill,
          locale: kartenTexte(anfangRef.current.t),
        });
        map.touchZoomRotate.disableRotation();
        map.keyboard.disableRotation();

        // Meter sind beim Fährtelegen die Einheit. Unten gestapelt: Was zuerst
        // hinzukommt, liegt zuunterst.
        map.addControl(new maplibre.ScaleControl({ unit: "metric" }), "bottom-left");
        // Zoomknöpfe nur mit Maus: Am Handy zoomt man mit zwei Fingern, und auf
        // der 256 px hohen Karte der Hundeseite verdeckten die beiden Knöpfe
        // ein Viertel der Fläche - oft genau den Start der Fährte.
        if (window.matchMedia("(pointer: fine)").matches) {
          map.addControl(new maplibre.NavigationControl({ showCompass: false }), "bottom-left");
        }

        const karte = map;
        karte.on("style.load", () => ebenenAnlegen(karte, datenRef.current));

        // Kompassnadel: zeigt immer nach geografisch Nord, also gegen die
        // Drehung der Karte.
        karte.on("rotate", () => {
          nadelRef.current?.style.setProperty("transform", `rotate(${-karte.getBearing()}deg)`);
        });

        // Antippen: Text der Linie oder des Punkts als Popup. setText, nie
        // HTML - Labels stammen von Nutzer:innen.
        karte.on("click", (e) => {
          const text = textAn(karte, e.point.x, e.point.y);
          popupRef.current?.remove();
          popupRef.current = null;
          if (text === null) return;
          popupRef.current = new maplibre.Popup({ closeButton: false, maxWidth: "240px" })
            .setLngLat(e.lngLat)
            .setText(text)
            .addTo(karte);
        });
        // Mit der Maus zeigt der Zeiger, was antippbar ist (Touch hat keinen).
        karte.on("mousemove", (e) => {
          karte.getCanvas().style.cursor = textAn(karte, e.point.x, e.point.y) !== null ? "pointer" : "";
        });

        mapRef.current = karte;
        maplibreRef.current = maplibre;
        angewandterStilRef.current = geladenerStil;
        setMapReady(true);
      } catch {
        // Kein WebGL2 oder die Karte ließ sich nicht erzeugen: ruhiger Hinweis
        // statt leerer Fläche. Die Aufzeichnung selbst bleibt davon unberührt.
        if (!abgebrochen) setKartenFehler("geraet");
      }
    })();

    return () => {
      abgebrochen = true;
      popupRef.current?.remove();
      pfeilRef.current?.remove();
      map?.remove();
      mapRef.current = null;
      maplibreRef.current = null;
      popupRef.current = null;
      pfeilRef.current = null;
      pfeilFarbeRef.current = null;
      angewandterStilRef.current = null;
      hasSetInitialViewRef.current = false;
      hatGrobeZentrierungRef.current = false;
      letzterPunktRef.current = null;
    };
  }, [versuch]);

  // Stil wechseln, wenn Ebene oder Theme sich ändern. Nur die neueste Anfrage
  // darf anwenden: Wer zweimal schnell umschaltet, soll nicht den Stil sehen,
  // dessen Datei zuletzt eintraf.
  useEffect(() => {
    const map = mapRef.current;
    // Zähler vor dem frühen Abbruch erhöhen: Wer von hell auf Luftbild und
    // gleich zurück auf hell tippt, entwertet damit die noch laufende Anfrage
    // - sie käme sonst später an und legte das Luftbild über die Straße.
    const nr = ++stilAnfrageRef.current;
    if (!mapReady || !map || angewandterStilRef.current === stilName) return;

    stilLaden(stilName, location.origin)
      .then((stil) => {
        if (nr !== stilAnfrageRef.current || mapRef.current !== map) return;
        // Ohne diff: Der Stil wird ganz ersetzt, "style.load" feuert und legt
        // die eigenen Ebenen neu an.
        map.setStyle(stil, { diff: false });
        angewandterStilRef.current = stilName;
      })
      .catch(() => {
        // Stil nicht erreichbar: Die Karte bleibt im bisherigen.
      });
  }, [mapReady, stilName]);

  // Daten: je Gruppe nur die betroffenen Quellen schreiben. Läuft auch vor der
  // Karte - dann landen die Daten im Stand und kommen mit "style.load" hinein.
  useEffect(() => {
    const d = legungDaten(points, live, t);
    const teil = { "legung-linie": d.linie, "legung-enden": d.enden, marker: d.marker };
    Object.assign(datenRef.current, teil);
    datenAnwenden(mapRef.current, teil);
  }, [mapReady, points, live, t]);

  useEffect(() => {
    const d = ablaufDaten(walkRuns, t);
    const teil = { "ablauf-bewertet": d.bewertet, "ablauf-unbewertet": d.unbewertet, halte: d.halte };
    Object.assign(datenRef.current, teil);
    datenAnwenden(mapRef.current, teil);
  }, [mapReady, walkRuns, t]);

  useEffect(() => {
    const d = liveAblaufDaten(liveWalkRunPoints, walkRuns.length, t);
    // Mit Richtungspfeil zeichnet ein Marker die Position, kein Punkt. Den
    // Pfeil gibt es erst mit bekannter Laufrichtung (siehe unten).
    const mitPfeil = showPositionArrow && smoothedHeadingRef.current !== null;
    const teil = { "ablauf-live": d.linie, position: mitPfeil ? leer<never>() : d.position };
    Object.assign(datenRef.current, teil);
    datenAnwenden(mapRef.current, teil);
  }, [mapReady, liveWalkRunPoints, walkRuns.length, showPositionArrow, t]);

  // Live: Kartenmitte folgt dem aktuellen Standort, der Zoom bleibt
  // unverändert, damit der Nutzer nicht laufend neu hineinzoomen muss, sobald
  // er die Karte einmal passend eingestellt hat. Bei einem laufenden
  // Ablauf-Versuch folgt die Karte dessen Position, sonst dem letzten Punkt
  // der aktuellen Legung. Läuft nur bei einem NEUEN Punkt: Die Peilung ist ein
  // gleitender Mittelwert und darf nicht mehrfach mit demselben Punkt gefüttert
  // werden.
  useEffect(() => {
    const map = mapRef.current;
    if (!mapReady || !map || !isLive) return;

    const quelle = hasLiveWalkRun && liveWalkRunPoints ? liveWalkRunPoints : points.filter((p) => p.pointType !== 1);
    const letzter = quelle[quelle.length - 1];
    if (!letzter) return;

    const schluessel = `${quelle.length}:${letzter.latitude}:${letzter.longitude}`;
    if (schluessel === letzterPunktRef.current) return;
    letzterPunktRef.current = schluessel;

    const roh = rohePeilung(quelle);
    if (roh !== null) smoothedHeadingRef.current = glaettePeilung(smoothedHeadingRef.current, roh);

    const center: [number, number] = [letzter.longitude, letzter.latitude];
    const bearing = zielPeilung(orientation, smoothedHeadingRef.current) ?? map.getBearing();
    if (!hasSetInitialViewRef.current) {
      map.jumpTo({ center, zoom: LIVE_INITIAL_ZOOM, bearing });
      hasSetInitialViewRef.current = true;
    } else {
      // Ohne `essential`: Wer reduzierte Bewegung eingestellt hat, bei dem
      // springt die Karte, statt zu gleiten.
      map.easeTo({ center, bearing, duration: NACHFUEHR_DAUER_MS });
    }
    // orientation bewusst nicht in den Abhängigkeiten: Ein Moduswechsel ist
    // kein neuer Punkt, ihn behandelt der Effect darunter.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mapReady, isLive, hasLiveWalkRun, points, liveWalkRunPoints]);

  // Ausrichtung gewechselt: Karte jetzt drehen, nicht erst beim nächsten Punkt.
  useEffect(() => {
    const map = mapRef.current;
    if (!mapReady || !map || !isLive) return;
    const bearing = zielPeilung(orientation, smoothedHeadingRef.current);
    if (bearing === null || Math.abs(map.getBearing() - bearing) < 0.5) return;
    map.easeTo({ bearing, duration: NACHFUEHR_DAUER_MS });
  }, [mapReady, isLive, orientation]);

  // Modus "Norden": ein drehbares Pfeil-Symbol an der eigenen Position, dessen
  // Spitze in die Laufrichtung zeigt - so sieht man die Richtung, ohne dass
  // sich die ganze Karte drehen muss. Beim Ablaufen am Ende des laufenden
  // Versuchs, beim Legen am Ende der Legung (bis 2026-10-13 gab es ihn nur beim
  // Ablaufen). rotationAlignment "map": Die Drehung ist eine Himmelsrichtung
  // und stimmt damit in jedem Modus. Steht NACH dem Nachführen, weil es dessen
  // frische Peilung liest.
  useEffect(() => {
    const map = mapRef.current;
    const maplibre = maplibreRef.current;
    if (!mapReady || !map || !maplibre) return;

    const d = liveAblaufDaten(liveWalkRunPoints, walkRuns.length, t);
    const legung = live ? points.filter((p) => p.pointType !== 1) : [];
    const legungsEnde = legung.length > 0 ? legung[legung.length - 1] : null;
    const ziel: [number, number] | null =
      d.letzter ?? (legungsEnde ? [legungsEnde.longitude, legungsEnde.latitude] : null);
    const farbe = d.letzter ? d.farbe : TRACK_LINE_COLOR;
    // Ohne bekannte Laufrichtung kein Pfeil: einer, der stur nach Norden
    // zeigt, führte in die Irre.
    if (!showPositionArrow || !ziel || smoothedHeadingRef.current === null) {
      pfeilRef.current?.remove();
      pfeilRef.current = null;
      pfeilFarbeRef.current = null;
      return;
    }

    // Die Farbe hängt am Versuch (Zyklus) - bei einem neuen Versuch neu bauen.
    if (pfeilRef.current && pfeilFarbeRef.current !== farbe) {
      pfeilRef.current.remove();
      pfeilRef.current = null;
    }
    if (!pfeilRef.current) {
      pfeilRef.current = new maplibre.Marker({
        element: pfeilElement(farbe),
        rotationAlignment: "map",
        pitchAlignment: "map",
      })
        .setLngLat(ziel)
        .addTo(map);
      pfeilFarbeRef.current = farbe;
    } else {
      pfeilRef.current.setLngLat(ziel);
    }
    pfeilRef.current.setRotation(smoothedHeadingRef.current);
  }, [mapReady, showPositionArrow, liveWalkRunPoints, walkRuns.length, live, points, t]);

  // Abgeschlossene/historische Fährte: ganze Strecke ins Bild einpassen, ohne
  // Animation - die Karte soll gleich so dastehen. Bei geänderten Daten erneut.
  useEffect(() => {
    const map = mapRef.current;
    if (!mapReady || !map || isLive) return;
    const grenzen = kartenGrenzen(points, walkRuns);
    // Oben Platz für den Ebenen-Umschalter, unten für Maßstab und
    // Namensnennung - sonst lagen Start oder Ende der Fährte genau darunter.
    if (grenzen) map.fitBounds(grenzen, { padding: { top: 52, bottom: 32, left: 16, right: 16 }, maxZoom: 19, animate: false });
  }, [mapReady, isLive, points, walkRuns]);

  // Im Live-Modus die Karte schon vor dem ersten Punkt anzeigen (wartet auf
  // die erste Positionsmessung), damit sie nicht erst nach einigen Sekunden
  // "einschnappt".
  if (!isLive && points.length === 0 && walkRuns.length === 0) {
    return <p className="text-sm text-muted-foreground">{t("Keine GPS-Punkte aufgezeichnet.")}</p>;
  }

  const orientationTitle =
    orientation === "heading"
      ? t("Die Karte dreht mit der Laufrichtung - tippen für Norden oben")
      : t("Norden oben, der Pfeil zeigt deine Laufrichtung - tippen, damit die Karte mitdreht");

  return (
    <div className={`relative isolate w-full overflow-hidden ${fill ? "h-full" : "h-64 rounded-md"}`}>
      <div ref={containerRef} className="size-full" />
      {kartenFehler && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-muted p-4 text-center text-sm text-muted-foreground">
          <p>
            {kartenFehler === "laden"
              ? t("Die Karte konnte nicht geladen werden.")
              : t("Die Karte lässt sich auf diesem Gerät nicht anzeigen.")}
          </p>
          {kartenFehler === "laden" && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="coarse:min-h-11"
              onClick={() => {
                setKartenFehler(null);
                setVersuch((v) => v + 1);
              }}
            >
              {t("Erneut versuchen")}
            </Button>
          )}
        </div>
      )}
      {/* Ebenenwechsel: immer erreichbar, auch bei fertigen Fährten - beim
          Auswerten will man denselben Wechsel wie beim Legen. */}
      {!kartenFehler && (
        <button
          type="button"
          onClick={ebeneWechseln}
          title={t("Kartenhintergrund: {jetzt} (klicken für {andere})", {
            jetzt: t(KARTEN_EBENEN[ebene].label),
            andere: t(KARTEN_EBENEN[ebene === "strasse" ? "luftbild" : "strasse"].label),
          })}
          className="absolute left-2 top-2 z-10 flex items-center gap-1.5 rounded-full border bg-background/90 px-3 py-2 text-xs font-medium shadow-md backdrop-blur coarse:min-h-10"
        >
          {ebene === "strasse" ? <KartenSymbol className="size-4" /> : <Satellite className="size-4" />}
          {t(KARTEN_EBENEN[ebene].label)}
        </button>
      )}
      {isLive && !kartenFehler && (
        <button
          type="button"
          onClick={cycleOrientation}
          title={orientationTitle}
          aria-label={orientationTitle}
          // Mit Beschriftung wie der Ebenen-Knopf: Ein Kompass allein verriet
          // nicht, welcher Modus gilt - wer stand, sah nach dem Tipp keine
          // Änderung und hielt den Knopf für tot.
          className="absolute right-2 top-2 z-10 flex items-center gap-1.5 rounded-full border bg-background/90 px-3 py-2 text-xs font-medium shadow-md backdrop-blur coarse:min-h-10"
        >
          {/* Kompassnadel: zeigt immer nach geografisch Nord. Ihre Drehung setzt
              das rotate-Ereignis der Karte (gegen deren bearing), ohne dass
              jedes Bild einer Drehung die Komponente neu rendert. */}
          <svg ref={nadelRef} viewBox="0 0 24 24" className="size-5 shrink-0" aria-hidden>
            <polygon points="12,3 15,13 12,11 9,13" fill="#dc2626" />
            <polygon points="12,21 9,11 12,13 15,11" fill="#64748b" />
          </svg>
          {orientation === "heading" ? t("Laufrichtung") : t("Norden")}
        </button>
      )}
    </div>
  );
}

/**
 * Kurzer Linienabschnitt in genau der Darstellung, die auch auf der Karte
 * gezeichnet wird - inklusive Fassung und Strichelung. Ein farbiges Kästchen
 * täte es nicht: dass die Legung eine Fassung trägt und ein noch nicht
 * ausgewerteter Ablauf gestrichelt ist, gehört mit zur Unterscheidung.
 */
function Linienprobe({
  farbe,
  fassung,
  gestrichelt = false,
}: {
  farbe: string;
  fassung?: string;
  gestrichelt?: boolean;
}) {
  return (
    <svg viewBox="0 0 24 12" width="24" height="12" className="shrink-0" aria-hidden>
      {fassung && <line x1="3" y1="6" x2="21" y2="6" stroke={fassung} strokeWidth="9" strokeOpacity="0.5" strokeLinecap="round" />}
      <line
        x1="3"
        y1="6"
        x2="21"
        y2="6"
        stroke={farbe}
        strokeWidth={fassung ? 6 : 3.5}
        strokeLinecap="round"
        strokeDasharray={gestrichelt ? "4 3" : undefined}
      />
    </svg>
  );
}

/**
 * Legende zur Karte.
 *
 * Auf der Karte liegen bis zu drei verschiedene Dinge übereinander: die
 * gelegte Fährte, die abgelaufenen Versuche und - sobald ausgewertet - deren
 * Abweichung als Ampel. Ohne Beschriftung war schlicht nicht zu erkennen, was
 * das Legen war und was das Ablaufen; beides war bis hierher sogar im selben
 * Grün gezeichnet.
 *
 * Zeigt nur, was auch wirklich auf der Karte liegt: ohne Ablauf keine Ampel.
 */
export function TrackLegend({ walkRuns }: { walkRuns: GpsWalkRun[] }) {
  const t = useT();
  const istBewertet = (run: GpsWalkRun) => run.points.some((p) => p.deviationMeters != null);
  const bewertete = walkRuns.filter(istBewertet);
  // Index aus der vollständigen Liste behalten: die Farbe eines noch nicht
  // ausgewerteten Ablaufs hängt an seiner Position, genau wie beim Zeichnen.
  const unbewertete = walkRuns
    .map((run, index) => ({ run, index }))
    .filter(({ run }) => run.points.length > 0 && !istBewertet(run));

  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 text-xs text-muted-foreground">
      <span className="flex items-center gap-1.5">
        <Linienprobe farbe={TRACK_LINE_COLOR} fassung={TRACK_CASING_COLOR} />
        <span className="font-medium text-foreground">{t("Gelegte Fährte")}</span>
      </span>
      {unbewertete.map(({ index }) => (
        <span key={index} className="flex items-center gap-1.5">
          <Linienprobe farbe={WALK_RUN_COLORS[index % WALK_RUN_COLORS.length]} gestrichelt />
          {t("Ablauf {nummer}", { nummer: index + 1 })}
        </span>
      ))}
      {bewertete.length > 0 && (
        <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="font-medium text-foreground">{t("Ablauf, Abweichung:")}</span>
          <span className="flex items-center gap-1.5">
            <Linienprobe farbe={DEVIATION_COLORS.green} />
            {t("bis {meter} m", { meter: GREEN_MAX_M })}
          </span>
          <span className="flex items-center gap-1.5">
            <Linienprobe farbe={DEVIATION_COLORS.amber} />
            {t("bis {meter} m", { meter: AMBER_MAX_M })}
          </span>
          <span className="flex items-center gap-1.5">
            <Linienprobe farbe={DEVIATION_COLORS.red} />
            {t("darüber")}
          </span>
        </span>
      )}
    </div>
  );
}

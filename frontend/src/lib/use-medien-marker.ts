import { useEffect, useRef } from "react";

/** Gerätebezogener Schalter: Uhr oder Kopfhörer gehören zu diesem Handy, nicht zum Konto. */
export const MEDIEN_MARKER_KEY = "dogity-medien-marker";

export function medienMarkerAn(): boolean {
  try {
    return window.localStorage.getItem(MEDIEN_MARKER_KEY) === "1";
  } catch {
    return false;
  }
}

export function setzeMedienMarker(an: boolean) {
  try {
    window.localStorage.setItem(MEDIEN_MARKER_KEY, an ? "1" : "0");
  } catch {
    // Ohne Speicher gilt der Schalter nur bis zum Neuladen.
  }
}

/**
 * Marker über die Mediensteuerung von Uhr oder Kopfhörer (Apple Watch "Jetzt läuft", Wear OS, Bluetooth-Tasten):
 * Eine eigene Uhr-App ist zu aufwendig, aber "Weiter"/"Zurück" erreicht jede Web-App über die Media Session.
 * Damit Handy und Uhr Dogity als laufende Wiedergabe führen, spielt eine stille Tonspur in Schleife - das
 * unterbricht Musik oder Podcast, solange aufgezeichnet wird.
 *
 * `starten()` muss direkt im Tipp auf "Legen starten" laufen: iOS spielt Ton nur nach einer Berührung.
 * shortcut: nur "Weiter" und "Zurück" (zwei Markerarten) - mehr Tasten hat die Mediensteuerung nicht.
 */
export function useMedienMarker(
  aktiv: boolean,
  { weiter, zurueck, titel, zeile }: { weiter: () => void; zurueck?: () => void; titel: string; zeile: string },
) {
  const tonRef = useRef<HTMLAudioElement | null>(null);
  const aktionen = useRef({ weiter, zurueck });
  useEffect(() => {
    aktionen.current = { weiter, zurueck };
  });

  function starten() {
    if (!medienMarkerAn() || !("mediaSession" in navigator)) return;
    tonRef.current ??= Object.assign(new Audio("/stille.wav"), { loop: true });
    void tonRef.current.play().catch(() => {
      // Ohne Ton keine Mediensteuerung - die Knöpfe am Handy gehen weiter.
    });
  }

  useEffect(() => {
    if (!aktiv || !tonRef.current || !("mediaSession" in navigator)) return;
    const sitzung = navigator.mediaSession;
    sitzung.setActionHandler("nexttrack", () => aktionen.current.weiter());
    sitzung.setActionHandler("previoustrack", aktionen.current.zurueck ? () => aktionen.current.zurueck?.() : null);
    // Pause auf der Uhr hielte die Tonspur an und mit ihr die Steuerung: ignorieren.
    sitzung.setActionHandler("pause", () => {});
    sitzung.setActionHandler("play", () => void tonRef.current?.play());
    return () => {
      for (const aktion of ["nexttrack", "previoustrack", "pause", "play"] as const) sitzung.setActionHandler(aktion, null);
      tonRef.current?.pause();
      sitzung.metadata = null;
    };
  }, [aktiv]);

  // Die Uhr zeigt Titel und Zeile wie bei einem Musikstück - dort sieht man, dass der Marker ankam.
  useEffect(() => {
    if (aktiv && tonRef.current && "mediaSession" in navigator)
      navigator.mediaSession.metadata = new MediaMetadata({ title: titel, artist: zeile, album: "Dogity" });
  }, [aktiv, titel, zeile]);

  return { starten };
}

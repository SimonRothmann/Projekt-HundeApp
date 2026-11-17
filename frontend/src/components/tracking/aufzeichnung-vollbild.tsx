"use client";

import { useCallback, useEffect, useState } from "react";
import { Lock } from "lucide-react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { HalteKnopf } from "@/components/tracking/halte-knopf";
import { EntsperrSchieber } from "@/components/tracking/entsperr-schieber";
import { useWakeLock } from "@/lib/use-wake-lock";
import { SPERRE_NACH_MS } from "@/lib/aufnahme-sperre";
import { kurzeHaptik } from "@/lib/haptik";

import { useT } from "@/lib/i18n";
/**
 * Vollbildhülle für jede laufende GPS-Aufzeichnung - Fährte legen wie
 * Ablauf suchen.
 *
 * Warum überhaupt Vollbild: Die Karte steckte vorher mit fester Höhe von
 * 256 px zwischen Kopfzeile, Zielen und Trainingsformular. Auf einem
 * 375-px-Gerät blieb davon wenig, und die Bedienelemente drängelten sich
 * darunter - während man draußen steht, den Hund an der Leine hält und mit
 * einer Hand tippt.
 *
 * Die Aufteilung folgt daraus: Karte über die volle Fläche, Bedienung im
 * unteren Drittel, wo der Daumen beim einhändigen Halten ohnehin liegt.
 *
 * Eine Hülle für beide Aufzeichnungsarten, nicht zwei: Was hier gilt -
 * Bildschirm wachhalten, versehentliches Verlassen verhindern, Sicherheits-
 * abstände am Rand - gilt für das Suchen genauso wie für das Legen, und
 * zwei Kopien liefen unweigerlich auseinander.
 */
export function AufzeichnungVollbild({
  titel,
  status,
  aktionen,
  abschlussLabel,
  abschlussFrage,
  onAbschluss,
  onAbbrechen,
  children,
}: {
  titel: string;
  /** Kurze Statuszeile, z.B. GPS-Genauigkeit und Punktzahl. */
  status?: React.ReactNode;
  /** Zusätzliche Knöpfe über dem Abschluss, z.B. Marker setzen. */
  aktionen?: React.ReactNode;
  abschlussLabel: string;
  /** Rückfrage beim Beenden per Tastatur - mit Finger oder Maus wird gehalten. */
  abschlussFrage: string;
  onAbschluss: () => void;
  /** Abbrechen verwirft die Aufzeichnung - nur nach Rückfrage. */
  onAbbrechen?: () => void;
  /** Die Karte. */
  children: React.ReactNode;
}) {
  const t = useT();
  useWakeLock(true);

  // Auto-Sperre: Das Handy steckt zwischendurch in der Tasche, und der
  // Bildschirm bleibt an. Nach SPERRE_NACH_MS ohne Berührung - und sofort,
  // wenn die Seite verborgen wird - reagiert die Bedienung nicht mehr, bis
  // jemand bewusst wischt. Nur die Hülle kennt den Zustand: Der Recorder
  // (GPS, Sicherung) läuft unberührt weiter, die Kinder bleiben im Baum.
  const [gesperrt, setGesperrt] = useState(false);
  const [ansage, setAnsage] = useState("");
  const sperren = useCallback(() => {
    setGesperrt((vorher) => {
      if (!vorher) kurzeHaptik(30);
      return true;
    });
    setAnsage(t("Bedienung gesperrt"));
  }, [t]);
  function entsperren() {
    setGesperrt(false);
    setAnsage(t("Bedienung entsperrt"));
    kurzeHaptik(30);
  }

  useEffect(() => {
    if (gesperrt) return;
    let zeitgeber = window.setTimeout(sperren, SPERRE_NACH_MS);
    const zuruecksetzen = () => {
      window.clearTimeout(zeitgeber);
      zeitgeber = window.setTimeout(sperren, SPERRE_NACH_MS);
    };
    const sichtbarkeit = () => {
      if (document.visibilityState === "hidden") sperren();
    };
    document.addEventListener("pointerdown", zuruecksetzen, true);
    document.addEventListener("keydown", zuruecksetzen, true);
    document.addEventListener("visibilitychange", sichtbarkeit);
    return () => {
      window.clearTimeout(zeitgeber);
      document.removeEventListener("pointerdown", zuruecksetzen, true);
      document.removeEventListener("keydown", zuruecksetzen, true);
      document.removeEventListener("visibilitychange", sichtbarkeit);
    };
  }, [gesperrt, sperren]);

  // Hintergrund nicht mitscrollen lassen, solange das Vollbild offen ist -
  // sonst wandert die Seite darunter weg und steht nach dem Schließen an
  // einer anderen Stelle.
  //
  // overscroll-behavior dazu: Ein Wischen nach unten beim Herausziehen des
  // Handys löste sonst "Ziehen zum Aktualisieren" aus - und ein Neuladen
  // beendet die Aufzeichnung.
  useEffect(() => {
    const html = document.documentElement.style;
    const vorher = { overflow: document.body.style.overflow, html: html.overscrollBehavior };
    document.body.style.overflow = "hidden";
    html.overscrollBehavior = "none";
    return () => {
      document.body.style.overflow = vorher.overflow;
      html.overscrollBehavior = vorher.html;
    };
  }, []);

  // Beim Serverrendern gibt es kein document. Praktisch tritt der Fall nicht
  // ein - das Vollbild erscheint erst, wenn jemand die Aufzeichnung startet -
  // aber die Prüfung kostet nichts und macht die Annahme sichtbar.
  if (typeof document === "undefined") return null;

  // Als Portal direkt an den Body, NICHT an Ort und Stelle im Baum.
  //
  // Der Grund ist die Stapelreihenfolge: Der Inhaltsbereich der App-Hülle
  // trägt "relative z-10" und bildet damit einen eigenen Stapelkontext. Ein
  // z-50 INNERHALB davon bleibt trotzdem unter der Kopfzeile (z-30) und der
  // unteren Navigation (z-40), weil die Geschwister des Inhaltsbereichs sind
  // und gegen dessen z-10 verglichen werden - nicht gegen das z-50 darin.
  // Gemessen: Das Vollbild lag zwischen App-Kopfzeile und Navigationsleiste,
  // der Abschluss-Knopf war verdeckt. Am Body hängend gibt es keinen
  // umschließenden Kontext mehr.
  return createPortal(
    <div className="fixed inset-0 z-50 flex flex-col bg-background">
      <div role="status" aria-live="polite" className="sr-only">
        {ansage}
      </div>
      {/* Kopf und Karte in einer Hülle, über der die Fangschicht liegt. inert
          nimmt sie auch aus der Tastaturbedienung; sichtbar bleibt alles. */}
      <div className="relative flex min-h-0 flex-1 flex-col">
      <div inert={gesperrt} className="flex min-h-0 flex-1 flex-col">
      <header className="flex items-center justify-between gap-2 border-b border-border/60 px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <div className="flex min-w-0 flex-col">
          <span className="truncate text-sm font-semibold">{titel}</span>
          {status && <span className="truncate text-xs text-muted-foreground">{status}</span>}
        </div>
        <div className="flex shrink-0 items-center gap-1">
          <Button variant="ghost" size="sm" className="text-muted-foreground" onClick={sperren}>
            <Lock className="size-3.5" />
            {t("Sperren")}
          </Button>
          {onAbbrechen && (
            <Button variant="ghost" size="sm" className="text-muted-foreground" onClick={onAbbrechen}>
              {t("Abbrechen")}
            </Button>
          )}
        </div>
      </header>

      {/* min-h-0: Ohne das wächst das Flex-Kind mit dem Karteninhalt statt
          den verbleibenden Platz zu füllen - die Bedienleiste rutschte dann
          aus dem Bild. */}
      <div className="relative min-h-0 flex-1">{children}</div>
      </div>
      {gesperrt && (
        <div
          aria-hidden
          className="absolute inset-0 z-[1100] touch-none"
          onContextMenu={(e) => e.preventDefault()}
        />
      )}
      </div>

      {/* Das Sperrfeld liegt über der Bedienleiste und ist genau so hoch wie
          sie - die Karte springt nicht. */}
      <div className="relative">
        <div
          inert={gesperrt}
          className="flex flex-col gap-3 border-t border-border/60 px-4 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))]"
        >
          {aktionen}
          <HalteKnopf onAusgeloest={onAbschluss} bestaetigungsfrage={abschlussFrage}>
            {abschlussLabel}
          </HalteKnopf>
        </div>
        {gesperrt && (
          <div className="absolute inset-0 z-10 flex items-center border-t border-border/60 bg-background px-4 pb-[max(0rem,env(safe-area-inset-bottom))]">
            <EntsperrSchieber onEntsperrt={entsperren} />
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}

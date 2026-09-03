"use client";

import { useEffect } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";
import { useWakeLock } from "@/lib/use-wake-lock";

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
  onAbschluss: () => void;
  /** Abbrechen verwirft die Aufzeichnung - nur nach Rückfrage. */
  onAbbrechen?: () => void;
  /** Die Karte. */
  children: React.ReactNode;
}) {
  useWakeLock(true);

  // Hintergrund nicht mitscrollen lassen, solange das Vollbild offen ist -
  // sonst wandert die Seite darunter weg und steht nach dem Schließen an
  // einer anderen Stelle.
  useEffect(() => {
    const vorher = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = vorher;
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
      <header className="flex items-center justify-between gap-2 border-b border-border/60 px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <div className="flex min-w-0 flex-col">
          <span className="truncate text-sm font-semibold">{titel}</span>
          {status && <span className="truncate text-xs text-muted-foreground">{status}</span>}
        </div>
        {onAbbrechen && (
          <Button
            variant="ghost"
            size="sm"
            className="shrink-0 text-muted-foreground"
            onClick={onAbbrechen}
          >
            Abbrechen
          </Button>
        )}
      </header>

      {/* min-h-0: Ohne das wächst das Flex-Kind mit dem Karteninhalt statt
          den verbleibenden Platz zu füllen - die Bedienleiste rutschte dann
          aus dem Bild. */}
      <div className="relative min-h-0 flex-1">{children}</div>

      <div className="flex flex-col gap-3 border-t border-border/60 px-4 pt-3 pb-[max(1rem,env(safe-area-inset-bottom))]">
        {aktionen}
        <Button size="lg" variant="destructive" className="h-14 w-full text-base" onClick={onAbschluss}>
          {abschlussLabel}
        </Button>
      </div>
    </div>,
    document.body,
  );
}

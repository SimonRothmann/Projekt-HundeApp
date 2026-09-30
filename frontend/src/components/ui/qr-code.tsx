"use client";

import { useMemo } from "react";
import { encode } from "uqr";
import { qrPfad } from "@/lib/qr-pfad";
import { cn } from "@/lib/utils";

/**
 * Ruhezone um den Code in Modulen. Die QR-Norm verlangt vier: Scanner
 * brauchen den hellen Rand, um den Code vom Hintergrund zu unterscheiden.
 */
const RUHEZONE = 4;

/**
 * QR-Code als SVG - aus der Modulmatrix gezeichnet, nicht als Markup aus
 * einer Bibliothek eingesetzt.
 *
 * Immer schwarz auf weiß, auch im dunklen Erscheinungsbild: Viele Scanner
 * erkennen invertierte Codes nicht, und auf Papier gibt es keinen dunklen
 * Modus. Die weiße Fläche samt Ruhezone gehört deshalb zur Grafik selbst.
 *
 * Die Größe kommt vom Elternelement bzw. von className (width/height); die
 * Grafik skaliert verlustfrei. Zum Drucken ist die Fehlerkorrektur Q sinnvoll
 * (ein Knick im Papier, ein Fleck), für den Bildschirm genügt M.
 */
export function QrCode({
  wert,
  beschreibung,
  ecc = "M",
  className,
}: {
  wert: string;
  /** Was der Code enthält - für Screenreader, die das Bild sonst nur als "Grafik" nennen. */
  beschreibung: string;
  ecc?: "L" | "M" | "Q" | "H";
  className?: string;
}) {
  const { pfad, seite } = useMemo(() => {
    const { data, size } = encode(wert, { ecc, border: 0 });
    return { pfad: qrPfad(data, RUHEZONE), seite: size + RUHEZONE * 2 };
  }, [wert, ecc]);

  return (
    <svg
      viewBox={`0 0 ${seite} ${seite}`}
      role="img"
      aria-label={beschreibung}
      shapeRendering="crispEdges"
      className={cn("block h-auto w-full max-w-full rounded-md", className)}
    >
      <rect width={seite} height={seite} fill="#ffffff" />
      <path d={pfad} fill="#000000" />
    </svg>
  );
}

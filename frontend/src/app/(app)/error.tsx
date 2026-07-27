"use client"; // Error-Boundaries müssen Client-Komponenten sein.

import { useEffect } from "react";
import { Button } from "@/components/ui/button";
import { isChunkLoadError, reloadOnceForChunkError } from "@/lib/chunk-reload";

/**
 * Error-Boundary für den gesamten authentifizierten Bereich (umschließt u.a.
 * die Hundeseite). Fängt Laufzeit-/Renderfehler ab, statt die Seite
 * unbenutzbar zu lassen.
 *
 * Häufigster Fall in der Praxis: ein Chunk-Load-Fehler nach einem Deploy
 * (siehe lib/chunk-reload). Der wird automatisch durch ein einmaliges Neuladen
 * behoben (frisches HTML -> neue Chunk-Namen). Schlägt das Neuladen fehl oder
 * ist es ein anderer Fehler, bleiben die Buttons als manueller Ausweg.
 *
 * Next 16: Die Recovery-Funktion heißt `unstable_retry` (nicht mehr `reset`) -
 * sie lädt das Segment neu, ohne die ganze App zu verwerfen.
 */
export default function AppError({
  error,
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  const chunk = isChunkLoadError(error);

  useEffect(() => {
    // Reines Synchronisieren mit einem externen System (Navigation/Reload),
    // kein React-State - schleifensicher via lib/chunk-reload.
    if (chunk) reloadOnceForChunkError();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-col items-center gap-4 py-16 text-center">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold">Diese Seite konnte nicht geladen werden</h2>
        <p className="text-sm text-muted-foreground">
          {chunk
            ? "Die App wurde zwischenzeitlich aktualisiert – die Seite wird neu geladen…"
            : "Bitte versuche es erneut."}
        </p>
      </div>
      <div className="flex flex-wrap justify-center gap-2">
        <Button onClick={() => unstable_retry()}>Erneut versuchen</Button>
        <Button variant="outline" onClick={() => window.location.reload()}>
          Seite neu laden
        </Button>
      </div>
    </div>
  );
}

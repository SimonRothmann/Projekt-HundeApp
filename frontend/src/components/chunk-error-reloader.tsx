"use client";

import { useEffect } from "react";
import { isChunkLoadError, reloadOnceForChunkError } from "@/lib/chunk-reload";

/**
 * Fängt Chunk-Load-Fehler ab, die NICHT über die React-Error-Boundary laufen -
 * insbesondere abgelehnte dynamische Import-Promises während einer Navigation
 * (unhandledrejection). Zusammen mit (app)/error.tsx wird so ein „Seite lässt
 * sich nicht öffnen" nach einem Deploy zuverlässig durch ein einmaliges,
 * schleifensicheres Neuladen behoben (siehe lib/chunk-reload).
 */
export function ChunkErrorReloader() {
  useEffect(() => {
    const onRejection = (e: PromiseRejectionEvent) => {
      if (isChunkLoadError(e.reason)) reloadOnceForChunkError();
    };
    const onError = (e: ErrorEvent) => {
      if (isChunkLoadError(e.error) || isChunkLoadError(e.message)) reloadOnceForChunkError();
    };
    window.addEventListener("unhandledrejection", onRejection);
    window.addEventListener("error", onError);
    return () => {
      window.removeEventListener("unhandledrejection", onRejection);
      window.removeEventListener("error", onError);
    };
  }, []);

  return null;
}

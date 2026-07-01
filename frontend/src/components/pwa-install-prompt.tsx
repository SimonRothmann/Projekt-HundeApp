"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Download, X } from "lucide-react";
import { toast } from "sonner";

// Chrome/Edge/Android fire this event before showing the native install prompt.
// iOS Safari has no equivalent — we show manual instructions there instead.
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

function isIos() {
  return /iphone|ipad|ipod/i.test(navigator.userAgent);
}

function isInStandaloneMode() {
  return (
    ("standalone" in navigator && (navigator as { standalone?: boolean }).standalone === true) ||
    window.matchMedia("(display-mode: standalone)").matches
  );
}

export function PwaInstallPrompt() {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [showIosHint, setShowIosHint] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    // Bereits installiert → nichts anzeigen.
    if (isInStandaloneMode()) return;

    if (isIos()) {
      // iOS: kein beforeinstallprompt — manuellen Hinweis nach 3 s zeigen.
      const t = window.setTimeout(() => setShowIosHint(true), 3000);
      return () => window.clearTimeout(t);
    }

    function handlePrompt(e: Event) {
      e.preventDefault();
      setDeferredPrompt(e as BeforeInstallPromptEvent);
    }
    window.addEventListener("beforeinstallprompt", handlePrompt);
    return () => window.removeEventListener("beforeinstallprompt", handlePrompt);
  }, []);

  async function installAndroid() {
    if (!deferredPrompt) return;
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    if (outcome === "accepted") {
      toast.success("App wird installiert.");
    }
    setDeferredPrompt(null);
  }

  if (dismissed) return null;

  if (deferredPrompt) {
    return (
      <div className="fixed bottom-4 left-1/2 -translate-x-1/2 z-50 flex items-center gap-3 rounded-xl border bg-background px-4 py-3 shadow-lg text-sm max-w-sm w-[calc(100%-2rem)]">
        <Download className="size-5 shrink-0 text-primary" />
        <span className="flex-1">Als App installieren für den besten Offline-Erfahrung.</span>
        <Button size="sm" onClick={installAndroid}>
          Installieren
        </Button>
        <button onClick={() => setDismissed(true)} className="text-muted-foreground hover:text-foreground">
          <X className="size-4" />
        </button>
      </div>
    );
  }

  if (showIosHint) {
    return (
      <div className="fixed bottom-4 left-1/2 -translate-x-1/2 z-50 flex flex-col gap-2 rounded-xl border bg-background px-4 py-3 shadow-lg text-sm max-w-sm w-[calc(100%-2rem)]">
        <div className="flex items-center gap-2">
          <Download className="size-5 shrink-0 text-primary" />
          <span className="font-medium">Als App installieren</span>
          <button onClick={() => setDismissed(true)} className="ml-auto text-muted-foreground hover:text-foreground">
            <X className="size-4" />
          </button>
        </div>
        <p className="text-muted-foreground text-xs leading-relaxed">
          Tippe auf{" "}
          <span className="font-medium text-foreground">
            Teilen{" "}
            <svg
              className="inline size-3.5"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8" />
              <polyline points="16 6 12 2 8 6" />
              <line x1="12" y1="2" x2="12" y2="15" />
            </svg>
          </span>{" "}
          und wähle <span className="font-medium text-foreground">&bdquo;Zum Home-Bildschirm&ldquo;</span> um Dogity offline zu nutzen.
        </p>
      </div>
    );
  }

  return null;
}

"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { useT } from "@/lib/i18n";
import { bestimmePushZustand, lesePushUmgebung, schaltePushAus, schaltePushEin, type PushZustand } from "@/lib/push";
import { useSystemFeatures } from "@/lib/system-features";

/**
 * Meldet allen Stellen, die Push anbieten (Profil, Startseite, Glocke), dass
 * sich etwas geändert hat - wer Push in der Glocke einschaltet, soll den
 * Hinweis auf der Startseite nicht weiter sehen.
 */
const GEAENDERT = "dogity:push-geaendert";

/**
 * Push auf diesem Gerät: Zustand lesen, ein- und ausschalten. Gemeinsam für
 * alle Stellen, an denen Push angeboten wird, damit Texte und Verhalten
 * überall gleich sind.
 *
 * @param aktiv false: (noch) nichts lesen - die Glocke fragt erst beim Öffnen,
 *   statt auf jeder Seite auf den Service Worker zu warten.
 */
export function usePush(aktiv = true) {
  const t = useT();
  const features = useSystemFeatures();
  const publicKey = features?.pushPublicKey ?? null;
  const [zustand, setZustand] = useState<PushZustand | null>(null);
  const [arbeitet, setArbeitet] = useState(false);

  const ermitteln = useCallback(async () => {
    if (!features) return;
    setZustand(bestimmePushZustand(await lesePushUmgebung(publicKey)));
  }, [features, publicKey]);

  useEffect(() => {
    if (!aktiv || !features) return;
    let abgebrochen = false;
    void lesePushUmgebung(publicKey).then((umgebung) => {
      if (!abgebrochen) setZustand(bestimmePushZustand(umgebung));
    });
    function neuLesen() {
      void lesePushUmgebung(publicKey).then((umgebung) => {
        if (!abgebrochen) setZustand(bestimmePushZustand(umgebung));
      });
    }
    window.addEventListener(GEAENDERT, neuLesen);
    return () => {
      abgebrochen = true;
      window.removeEventListener(GEAENDERT, neuLesen);
    };
  }, [aktiv, features, publicKey]);

  async function einschalten() {
    if (!publicKey) return;
    // Kein await vor schaltePushEin: Safari verlangt die Frage nach der
    // Erlaubnis unmittelbar im Tipp, sonst lehnt es sie stillschweigend ab.
    setArbeitet(true);
    try {
      const ergebnis = await schaltePushEin(publicKey);
      if (ergebnis === "erteilt") toast.success(t("Push-Nachrichten sind auf diesem Gerät eingeschaltet."));
      else if (ergebnis === "verweigert") toast.error(t("Die Erlaubnis für Mitteilungen wurde nicht erteilt."));
      else toast.error(t("Push-Nachrichten konnten nicht eingeschaltet werden."));
      await ermitteln();
      window.dispatchEvent(new Event(GEAENDERT));
    } finally {
      setArbeitet(false);
    }
  }

  async function ausschalten() {
    setArbeitet(true);
    try {
      const serverOk = await schaltePushAus();
      if (serverOk) toast.success(t("Push-Nachrichten sind auf diesem Gerät ausgeschaltet."));
      else toast.error(t("Das Gerät konnte nicht vollständig abgemeldet werden. Auf diesem Gerät ist Push aus."));
      await ermitteln();
      window.dispatchEvent(new Event(GEAENDERT));
    } finally {
      setArbeitet(false);
    }
  }

  return { zustand, arbeitet, einschalten, ausschalten };
}

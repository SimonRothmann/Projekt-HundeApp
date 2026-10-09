"use client";

import { useState } from "react";
import { toast } from "sonner";
import { api, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { useT } from "@/lib/i18n";

/**
 * "Ausblenden" für die Karten des Erststarts: merkt sich am Konto, dass der
 * Nutzer sie nicht mehr sehen will (POST /api/onboarding/dismiss), und meldet
 * es der Startseite erst, wenn der Server es hat - sonst käme die Karte beim
 * nächsten Laden zurück.
 */
export function AusblendenKnopf({ onDismissed }: { onDismissed: () => void }) {
  const t = useT();
  const [laeuft, setLaeuft] = useState(false);

  async function wegklicken() {
    setLaeuft(true);
    try {
      await api.post("/api/onboarding/dismiss");
      onDismissed();
    } catch (err) {
      setLaeuft(false);
      toast.error(err instanceof ApiError ? err.message : t("Konnte nicht ausgeblendet werden."));
    }
  }

  return (
    <Button
      type="button"
      size="sm"
      variant="ghost"
      className="h-11 self-start px-3 text-sm text-muted-foreground"
      disabled={laeuft}
      onClick={wegklicken}
    >
      {t("Ausblenden")}
    </Button>
  );
}

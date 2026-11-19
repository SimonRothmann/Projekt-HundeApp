"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Play } from "lucide-react";
import { ApiError, useAuth } from "@/lib/auth-context";
import { useSprache, useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * "App ausprobieren - ohne Anmeldung": legt einen Gastzugang an (Konto mit Hund, Verlauf und
 * erfundenem Verein, nach wenigen Stunden gelöscht - siehe docs/GASTZUGANG.md) und öffnet die App.
 * Fehler und Drosselung kommen als Meldung; der Server nennt den Grund.
 */
export function GastZugangKnopf({ className }: { className?: string }) {
  const t = useT();
  const sprache = useSprache();
  const router = useRouter();
  const { loginAlsGast } = useAuth();
  const [laeuft, setLaeuft] = useState(false);

  async function start() {
    setLaeuft(true);
    try {
      await loginAlsGast(sprache);
      router.push("/dashboard");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : t("Der Demo-Zugang konnte nicht angelegt werden."));
      setLaeuft(false);
    }
  }

  return (
    <Button type="button" variant="outline" size="lg" className={cn("h-11 px-6 text-sm", className)} disabled={laeuft} onClick={() => void start()}>
      <Play className="size-4" aria-hidden />
      {laeuft ? t("Demo wird vorbereitet…") : t("App ausprobieren - ohne Anmeldung")}
    </Button>
  );
}
